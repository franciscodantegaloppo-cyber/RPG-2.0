using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scatters higher-poly pine trees across the open terrain, staying clear of the spawn
// village and the Stylized Dark Castle grounds (see BuildStylizedDarkCastle.cs for the
// castle's own site coordinates).
public static class ScatterPineForest
{
    private const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    private const string RootName = "PineForest_Density";

    // Matches BuildStylizedDarkCastle's site so the two scripts never need to be kept in sync
    // by hand - if the castle moves, update both constants together.
    private const float CastleX = -200f;
    private const float CastleZ = 350f;
    private const float CastleExclusionHalfExtent = 260f;
    private const float SpawnExclusionRadius = 200f;

    private const float TerrainMin = -580f;
    private const float TerrainMax = 580f;
    private const int TargetCount = 5000;
    private const int MaxAttempts = 200000;
    private const float MinSpacing = 6f;

    // Weighted toward the two higher-poly Spruce prefabs (22.4k / 11.8k triangles) and away
    // from the RPGPP_LT low-poly trees (~200-500 triangles), per the user's request.
    private static readonly (string path, float weight)[] TreePool =
    {
        ("Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Trees/Spruce 1.prefab", 0.45f),
        ("Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Trees/Spruce 2.prefab", 0.55f),
    };

    [MenuItem("RPG/World/Scatter Pine Forest Density")]
    public static void Scatter()
    {
        var scene = EditorSceneUtility.OpenSceneSafely(ScenePath);

        var existing = GameObject.Find(RootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("No active terrain found; aborting pine scatter.");
            return;
        }

        var prefabs = new List<(GameObject go, float weight)>();
        foreach (var (path, weight) in TreePool)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogError("Missing pine prefab: " + path);
                continue;
            }
            prefabs.Add((go, weight));
        }
        if (prefabs.Count == 0)
        {
            Debug.LogError("No valid pine prefabs loaded; aborting.");
            return;
        }
        float totalWeight = 0f;
        foreach (var p in prefabs) totalWeight += p.weight;

        var root = new GameObject(RootName);
        var rng = new System.Random(9001);

        // Spatial hash grid for the minimum-spacing check - at 5000 trees a plain O(n) scan per
        // attempt against every already-placed tree would get slow, so only the neighbouring
        // cells (cell size = MinSpacing) are checked instead.
        var grid = new Dictionary<(int, int), List<Vector2>> ();
        (int, int) CellOf(float x, float z) => (Mathf.FloorToInt(x / MinSpacing), Mathf.FloorToInt(z / MinSpacing));

        bool TooCloseToExisting(float x, float z)
        {
            var (cx, cz) = CellOf(x, z);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!grid.TryGetValue((cx + dx, cz + dz), out var pts)) continue;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        if ((pts[i] - new Vector2(x, z)).sqrMagnitude < MinSpacing * MinSpacing)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        int placed = 0;
        int attempts = 0;
        while (placed < TargetCount && attempts < MaxAttempts)
        {
            attempts++;
            float wx = Mathf.Lerp(TerrainMin, TerrainMax, (float)rng.NextDouble());
            float wz = Mathf.Lerp(TerrainMin, TerrainMax, (float)rng.NextDouble());

            if (IsInSpawnZone(wx, wz) || IsInCastleZone(wx, wz))
            {
                continue;
            }

            if (TooCloseToExisting(wx, wz))
            {
                continue;
            }

            float terrainY = terrain.SampleHeight(new Vector3(wx, 0, wz)) + terrain.transform.position.y;

            var prefab = PickWeighted(prefabs, totalWeight, rng);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            inst.transform.position = new Vector3(wx, terrainY, wz);
            inst.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
            // Wide size range - saplings to old-growth pines, per "todos los tamaños".
            float scale = Mathf.Lerp(0.7f, 2.2f, (float)rng.NextDouble());
            inst.transform.localScale = Vector3.one * scale;
            inst.name = $"Pine_{placed}";

            var cell = CellOf(wx, wz);
            if (!grid.TryGetValue(cell, out var list))
            {
                list = new List<Vector2>();
                grid[cell] = list;
            }
            list.Add(new Vector2(wx, wz));
            placed++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"Scattered {placed} high-poly pines across the map (attempts={attempts}), clear of spawn and the castle.");
    }

    private static bool IsInSpawnZone(float x, float z)
    {
        return (x * x + z * z) < SpawnExclusionRadius * SpawnExclusionRadius;
    }

    private static bool IsInCastleZone(float x, float z)
    {
        return Mathf.Abs(x - CastleX) < CastleExclusionHalfExtent && Mathf.Abs(z - CastleZ) < CastleExclusionHalfExtent;
    }

    private static GameObject PickWeighted(List<(GameObject go, float weight)> prefabs, float totalWeight, System.Random rng)
    {
        float roll = (float)rng.NextDouble() * totalWeight;
        float cumulative = 0f;
        foreach (var (go, weight) in prefabs)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                return go;
            }
        }
        return prefabs[prefabs.Count - 1].go;
    }
}

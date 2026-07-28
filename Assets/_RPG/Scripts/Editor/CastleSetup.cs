using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scans the terrain for the largest reasonably-flat open spot away from the village/dungeon,
// then assembles a large walled castle there using the whole modular pack: perimeter walls with
// intermediate towers (not just corners), a gated entrance flanked by columns, a bridge/ramp
// approach, the keep, and a small village of houses filling the courtyard. Follows the same
// find-scene / instantiate / save pattern as VillageLayout/ChestSetup/HouseDecorationSetup.
public static class CastleSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string Root = "Assets/Advance Studios/Medieval Castle/Prefabs/";
    const string CastleRootName = "MedievalCastle";

    const float AvoidVillageRadius = 45f;
    const float AvoidDungeonRadius = 25f;
    const float TerrainMargin = 25f;
    const float ScanStep = 14f;
    const float FlatnessTolerance = 5.5f;
    const float MinHalfSize = 26f;   // smallest acceptable courtyard half-size (52m square)
    const float MaxHalfSize = 85f;   // hard cap so it can't swallow the entire map (170m square)

    const float WallHeightScale = 1.6f;
    const float TowerScale = 1.55f;
    const float KeepScale = 1.35f;
    const int WallsPerIntermediateTower = 4;

    [MenuItem("RPG/Setup Medieval Castle")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CastleSetup] Ignorado durante Play Mode.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("[CastleSetup] No se encontro un Terrain activo.");
            return;
        }

        GameObject wallPrefab = Load("Wall.prefab");
        GameObject towerAPrefab = Load("Tower A.prefab");
        GameObject towerBPrefab = Load("Tower B.prefab");
        GameObject houseAPrefab = Load("House A.prefab");
        GameObject houseBPrefab = Load("House B.prefab");
        GameObject keepPrefab = Load("Castle.prefab");
        GameObject gatePrefab = Load("Double Door Frame.prefab");
        GameObject sideDoorPrefab = Load("Simple Door Frame.prefab");
        GameObject columnPrefab = Load("Column.prefab");
        GameObject bridgePrefab = Load("Bridge Large.prefab") ?? Load("Bridge.prefab");
        if (wallPrefab == null || towerAPrefab == null || keepPrefab == null)
        {
            Debug.LogError("[CastleSetup] Faltan prefabs base del pack.");
            return;
        }

        GameObject oldRoot = GameObject.Find(CastleRootName);
        if (oldRoot != null)
            Object.DestroyImmediate(oldRoot);

        Vector2 wallSize = MeasureFootprint(wallPrefab);
        float segLen = Mathf.Max(wallSize.x, wallSize.y);
        if (segLen < 0.5f) segLen = 4f; // guard against a degenerate/zero measurement
        bool wallLongAxisIsX = wallSize.x >= wallSize.y;

        Vector2 towerSize = MeasureFootprint(towerAPrefab);
        float towerHalf = Mathf.Max(towerSize.x, towerSize.y) * 0.5f * TowerScale;

        Vector2 keepSize = MeasureFootprint(keepPrefab);
        Vector2 columnSize = columnPrefab != null ? MeasureFootprint(columnPrefab) : Vector2.zero;

        Vector3 villageCenter = FindVillageCenter();
        Vector3 dungeonPos = FindDungeonPos();

        Vector3 site = FindOpenSiteAndSize(terrain, villageCenter, dungeonPos, out float halfSize);

        int wallsPerFrontBack = Mathf.Max(8, Mathf.RoundToInt(halfSize * 2f / segLen));
        int wallsPerSide = Mathf.Max(8, Mathf.RoundToInt(halfSize * 2f / segLen));

        float courtyardWidth = wallsPerFrontBack * segLen;
        float courtyardDepth = wallsPerSide * segLen;

        Vector3 toVillage = villageCenter - site;
        toVillage.y = 0f;
        if (toVillage.sqrMagnitude < 1f) toVillage = Vector3.forward;
        Quaternion castleRot = Quaternion.LookRotation(-toVillage.normalized);
        // The gate faces the village, so the "front" run of the wall (local -Z) points at -toVillage.

        GameObject root = new GameObject(CastleRootName);
        root.transform.position = site;
        root.transform.rotation = castleRot;

        Vector3 LocalToWorld(float lx, float lz)
        {
            Vector3 world = site + castleRot * new Vector3(lx, 0f, lz);
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
            return world;
        }

        float halfW = courtyardWidth * 0.5f;
        float halfD = courtyardDepth * 0.5f;
        int gateSegment = wallsPerFrontBack / 2;

        Quaternion runRotFrontBack = castleRot * Quaternion.Euler(0f, wallLongAxisIsX ? 0f : 90f, 0f);
        Quaternion runRotSides = castleRot * Quaternion.Euler(0f, wallLongAxisIsX ? 90f : 0f, 0f);

        int wallCount = 0, towerCount = 0;

        for (int i = 0; i < wallsPerFrontBack; i++)
        {
            float x = -halfW + segLen * (i + 0.5f);
            bool isGate = i == gateSegment - 1 || i == gateSegment;
            bool isIntermediateTower = !isGate && i % WallsPerIntermediateTower == 0 && i > 1 && i < wallsPerFrontBack - 2;

            if (isGate)
            {
                if (gatePrefab != null && i == gateSegment - 1)
                    Place(gatePrefab, root.transform, LocalToWorld(x + segLen * 0.5f, -halfD), runRotFrontBack, "Gate");
                PlaceWall(wallPrefab, root.transform, LocalToWorld(x, halfD), runRotFrontBack, "Wall_Back_" + i, ref wallCount);
                continue;
            }

            if (isIntermediateTower)
            {
                GameObject towerPrefab = (i / WallsPerIntermediateTower) % 2 == 0 ? towerBPrefab : towerAPrefab;
                Place(towerPrefab != null ? towerPrefab : towerAPrefab, root.transform, LocalToWorld(x, -halfD), runRotFrontBack, "Tower_Front_" + i, TowerScale);
                Place(towerPrefab != null ? towerPrefab : towerAPrefab, root.transform, LocalToWorld(x, halfD), runRotFrontBack, "Tower_Back_" + i, TowerScale);
                towerCount += 2;
                continue;
            }

            PlaceWall(wallPrefab, root.transform, LocalToWorld(x, -halfD), runRotFrontBack, "Wall_Front_" + i, ref wallCount);
            PlaceWall(wallPrefab, root.transform, LocalToWorld(x, halfD), runRotFrontBack, "Wall_Back_" + i, ref wallCount);
        }

        for (int i = 0; i < wallsPerSide; i++)
        {
            float z = -halfD + segLen * (i + 0.5f);
            bool isIntermediateTower = i % WallsPerIntermediateTower == 0 && i > 1 && i < wallsPerSide - 2;

            if (isIntermediateTower)
            {
                GameObject towerPrefab = (i / WallsPerIntermediateTower) % 2 == 0 ? towerAPrefab : towerBPrefab;
                Place(towerPrefab != null ? towerPrefab : towerAPrefab, root.transform, LocalToWorld(-halfW, z), runRotSides, "Tower_Left_" + i, TowerScale);
                Place(towerPrefab != null ? towerPrefab : towerAPrefab, root.transform, LocalToWorld(halfW, z), runRotSides, "Tower_Right_" + i, TowerScale);
                towerCount += 2;
                continue;
            }

            PlaceWall(wallPrefab, root.transform, LocalToWorld(-halfW, z), runRotSides, "Wall_Left_" + i, ref wallCount);
            PlaceWall(wallPrefab, root.transform, LocalToWorld(halfW, z), runRotSides, "Wall_Right_" + i, ref wallCount);
        }

        // Corner towers - the tallest/grandest, always Tower A for a consistent silhouette.
        Place(towerAPrefab, root.transform, LocalToWorld(-halfW, -halfD), castleRot, "Tower_FrontLeft", TowerScale * 1.1f);
        Place(towerAPrefab, root.transform, LocalToWorld(halfW, -halfD), castleRot * Quaternion.Euler(0f, 90f, 0f), "Tower_FrontRight", TowerScale * 1.1f);
        Place(towerAPrefab, root.transform, LocalToWorld(-halfW, halfD), castleRot * Quaternion.Euler(0f, -90f, 0f), "Tower_BackLeft", TowerScale * 1.1f);
        Place(towerAPrefab, root.transform, LocalToWorld(halfW, halfD), castleRot * Quaternion.Euler(0f, 180f, 0f), "Tower_BackRight", TowerScale * 1.1f);
        towerCount += 4;

        // Keep at the back of the courtyard, facing the gate, scaled up for a dominant silhouette.
        Place(keepPrefab, root.transform, LocalToWorld(0f, halfD - towerHalf * 2.2f), castleRot * Quaternion.Euler(0f, 180f, 0f), "Keep", KeepScale);

        // Columns flanking the processional path from the gate to the keep.
        int columnPairs = 0;
        if (columnPrefab != null)
        {
            float pathLength = courtyardDepth - towerHalf * 4.4f;
            float colSpacing = Mathf.Max(6f, columnSize.y > 0.01f ? columnSize.y * 2.2f : 6f);
            int pairs = Mathf.Max(2, Mathf.FloorToInt(pathLength / colSpacing) - 1);
            float sideOffset = Mathf.Max(3f, columnSize.x + 1.5f);
            for (int i = 0; i < pairs; i++)
            {
                float z = -halfD + towerHalf * 2.2f + colSpacing * (i + 1);
                if (z > halfD - towerHalf * 2.2f) break;
                Place(columnPrefab, root.transform, LocalToWorld(-sideOffset, z), castleRot, "Column_L_" + i);
                Place(columnPrefab, root.transform, LocalToWorld(sideOffset, z), castleRot, "Column_R_" + i);
                columnPairs++;
            }
        }

        // A bridge/ramp approach just outside the gate.
        if (bridgePrefab != null)
            Place(bridgePrefab, root.transform, LocalToWorld(0f, -halfD - 6f), castleRot, "EntranceBridge");

        // Extra doorways off to the sides of the courtyard, purely decorative wall breaks.
        if (sideDoorPrefab != null)
        {
            Place(sideDoorPrefab, root.transform, LocalToWorld(-halfW * 0.55f, halfD - towerHalf * 1.5f), castleRot * Quaternion.Euler(0f, 90f, 0f), "SideDoor_L");
            Place(sideDoorPrefab, root.transform, LocalToWorld(halfW * 0.55f, halfD - towerHalf * 1.5f), castleRot * Quaternion.Euler(0f, -90f, 0f), "SideDoor_R");
        }

        // A small village-within-the-walls filling the courtyard on both sides of the path.
        int houses = PlaceHouses(root.transform, houseAPrefab, houseBPrefab, castleRot, halfW, halfD, towerHalf, LocalToWorld);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[CastleSetup] Castillo colocado en " + site + " (patio " + courtyardWidth.ToString("0") + "x" + courtyardDepth.ToString("0") +
            "m, " + wallCount + " muros, " + towerCount + " torres, " + columnPairs + " pares de columnas, " + houses + " casas).");
    }

    static int PlaceHouses(Transform root, GameObject houseA, GameObject houseB, Quaternion castleRot,
        float halfW, float halfD, float towerHalf, System.Func<float, float, Vector3> localToWorld)
    {
        if (houseA == null && houseB == null)
            return 0;

        int placed = 0;
        float innerMargin = towerHalf * 1.6f;
        float usableW = halfW - innerMargin;
        float usableD = halfD - towerHalf * 2.5f; // leave the keep/path strip clear

        // Two files of houses running down each side of the courtyard, leaving the central
        // gate-to-keep path clear for the columns.
        int rows = Mathf.Max(2, Mathf.FloorToInt((usableD * 2f) / 9f));
        float rowStep = (usableD * 2f) / rows;

        for (int row = 0; row < rows; row++)
        {
            float z = -usableD + rowStep * (row + 0.5f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (usableW * 0.55f);
                GameObject prefab = (row + (side > 0 ? 1 : 0)) % 2 == 0 ? houseA : houseB;
                if (prefab == null) prefab = houseA != null ? houseA : houseB;

                float yaw = Random01(row * 2 + side) * 30f - 15f;
                Quaternion faceInward = Quaternion.Euler(0f, side > 0 ? 90f : -90f, 0f);
                Place(prefab, root, localToWorld(x, z), castleRot * faceInward * Quaternion.Euler(0f, yaw, 0f), "House_" + row + "_" + side);
                placed++;
            }
        }

        return placed;
    }

    static void PlaceWall(GameObject wallPrefab, Transform parent, Vector3 worldPos, Quaternion worldRot, string name, ref int counter)
    {
        GameObject go = Place(wallPrefab, parent, worldPos, worldRot, name);
        if (go == null) return;
        go.transform.localScale = new Vector3(go.transform.localScale.x, go.transform.localScale.y * WallHeightScale, go.transform.localScale.z);
        counter++;
    }

    static float Random01(int seed)
    {
        // Deterministic pseudo-random without touching UnityEngine.Random's global state/seed.
        unchecked
        {
            int h = seed * 374761393;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)int.MaxValue;
        }
    }

    static GameObject Load(string relativePath)
    {
        string path = Root + relativePath;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            Debug.LogWarning("[CastleSetup] No se encontro prefab: " + path);
        return prefab;
    }

    static GameObject Place(GameObject prefab, Transform parent, Vector3 worldPos, Quaternion worldRot, string name, float uniformScale = 1f)
    {
        if (prefab == null) return null;
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = worldPos;
        go.transform.rotation = worldRot;
        if (!Mathf.Approximately(uniformScale, 1f))
            go.transform.localScale *= uniformScale;
        return go;
    }

    static Vector2 MeasureFootprint(GameObject prefab)
    {
        GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Bounds b = ComputeBounds(probe.transform);
        Object.DestroyImmediate(probe);
        return new Vector2(b.size.x, b.size.z);
    }

    static Bounds ComputeBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(root.position, new Vector3(4f, 4f, 4f));

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    static Vector3 FindVillageCenter()
    {
        VillageLayout layout = Object.FindAnyObjectByType<VillageLayout>();
        return layout != null ? layout.transform.position : Vector3.zero;
    }

    static Vector3 FindDungeonPos()
    {
        DungeonEntrance entrance = Object.FindAnyObjectByType<DungeonEntrance>();
        return entrance != null ? entrance.transform.position : Vector3.zero;
    }

    // Finds the candidate site that supports the LARGEST square footprint that stays within
    // FlatnessTolerance, clear of the village/dungeon and the terrain edges - so the castle scales
    // up to fill however much open flat ground actually exists instead of a fixed guessed size.
    static Vector3 FindOpenSiteAndSize(Terrain terrain, Vector3 villageCenter, Vector3 dungeonPos, out float bestHalfSize)
    {
        Vector3 terrainPos = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;

        Vector3 best = terrainPos + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        bestHalfSize = MinHalfSize;
        float bestScore = float.MinValue;
        bool found = false;

        for (float x = TerrainMargin; x <= size.x - TerrainMargin; x += ScanStep)
        {
            for (float z = TerrainMargin; z <= size.z - TerrainMargin; z += ScanStep)
            {
                Vector3 candidate = terrainPos + new Vector3(x, 0f, z);

                float maxAllowedHalf = Mathf.Min(
                    MaxHalfSize,
                    x - TerrainMargin, size.x - TerrainMargin - x,
                    z - TerrainMargin, size.z - TerrainMargin - z);
                if (maxAllowedHalf < MinHalfSize)
                    continue;

                float usableHalf = 0f;
                for (float half = MinHalfSize; half <= maxAllowedHalf; half += 8f)
                {
                    if (Vector3.Distance(candidate, villageCenter) < AvoidVillageRadius + half)
                        break;
                    if (Vector3.Distance(candidate, dungeonPos) < AvoidDungeonRadius + half)
                        break;

                    if (SampleFlatness(terrain, candidate, half) > FlatnessTolerance)
                        break;

                    usableHalf = half;
                }

                if (usableHalf < MinHalfSize)
                    continue;

                // Bigger is better; break ties by preferring sites further from the village so
                // the castle reads as its own place instead of crowding the starting area.
                float score = usableHalf * 100f - Vector3.Distance(candidate, villageCenter) * 0.05f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    bestHalfSize = usableHalf;
                    found = true;
                }
            }
        }

        if (!found)
            Debug.LogWarning("[CastleSetup] No se encontro un sitio ideal, se uso una posicion/tamano por defecto.");

        best.y = terrain.SampleHeight(best) + terrainPos.y;
        return best;
    }

    static float SampleFlatness(Terrain terrain, Vector3 center, float half)
    {
        Vector3 terrainPos = terrain.transform.position;
        float min = float.MaxValue, max = float.MinValue;

        for (int ix = -3; ix <= 3; ix++)
        {
            for (int iz = -3; iz <= 3; iz++)
            {
                Vector3 p = center + new Vector3(ix * half / 3f, 0f, iz * half / 3f);
                float h = terrain.SampleHeight(p) + terrainPos.y;
                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }
        }

        return max - min;
    }
}

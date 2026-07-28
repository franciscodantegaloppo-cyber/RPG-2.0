#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DefinitiveNatureDecorationSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/DefinitiveNatureDecoration.generate";
    const string RootName = "DefinitiveNature_OutsideProtectedZones";
    const string PackRoot = "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/";
    const int Seed = 724190;
    const int WildernessTarget = 1050;
    const int BossIslandTarget = 260;
    const float SpawnExclusionRadius = 82f;
    const float BossArenaClearRadius = 9f;
    const float BossIslandRadius = 46f;

    struct NatureEntry
    {
        public GameObject prefab;
        public string label;
        public float weight;
        public float minScale;
        public float maxScale;
        public float clearance;
        public bool castsShadows;

        public NatureEntry(string path, string name, float chance, float min, float max, float space, bool shadows)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            label = name;
            weight = chance;
            minScale = min;
            maxScale = max;
            clearance = space;
            castsShadows = shadows;
        }
    }

    [MenuItem("RPG/World/Decorate Wilderness With Definitive Nature")]
    public static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[DefinitiveNature] Sali de Play Mode para decorar el mundo.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("[DefinitiveNature] No se encontro el Terrain de SpawnVillage.");
            return;
        }

        List<NatureEntry> entries = LoadEntries();
        entries.RemoveAll(entry => entry.prefab == null);
        if (entries.Count == 0)
        {
            Debug.LogError("[DefinitiveNature] No se encontraron prefabs del pack de naturaleza.");
            return;
        }

        Random.InitState(Seed);
        GameObject previous = GameObject.Find(RootName);
        if (previous != null) Object.DestroyImmediate(previous);

        GameObject root = new GameObject(RootName);
        Transform grassGroup = NewGroup(root.transform, "Grass");
        Transform flowerGroup = NewGroup(root.transform, "Flowers");
        Transform mushroomGroup = NewGroup(root.transform, "Mushrooms");
        Transform bushGroup = NewGroup(root.transform, "Bushes");
        Transform woodGroup = NewGroup(root.transform, "Logs_Branches_Stumps");

        Vector3 spawnCenter = FindSpawnCenter();
        GameObject bossObject = FindNamedObject("superior_tree_anomaly", "superiortreeanomaly");
        Vector3 bossCenter = bossObject != null ? bossObject.transform.position : spawnCenter + new Vector3(220f, 0f, 120f);
        Bounds castleBounds = FindProtectedBounds("medievalcastle", "generated_medieval_castle", "castle");
        Bounds templeBounds = FindProtectedBounds("templeedition", "generated_temple", "temple");

        Vector3 terrainOrigin = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        int wildernessPlaced = ScatterRegion(WildernessTarget, WildernessTarget * 18, false);
        int islandPlaced = ScatterRegion(BossIslandTarget, BossIslandTarget * 22, true);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[DefinitiveNature] Decoracion lista. Exterior: " + wildernessPlaced +
                  ", isla Superior Anomaly: " + islandPlaced +
                  ". Sin arboles ni agua; spawn, castillo y templo protegidos.");

        int ScatterRegion(int target, int maxAttempts, bool bossIsland)
        {
            int placed = 0;
            for (int attempt = 0; attempt < maxAttempts && placed < target; attempt++)
            {
                Vector3 candidate;
                if (bossIsland)
                {
                    Vector2 offset = Random.insideUnitCircle * BossIslandRadius;
                    if (offset.magnitude < BossArenaClearRadius) continue;
                    candidate = bossCenter + new Vector3(offset.x, 0f, offset.y);
                }
                else
                {
                    candidate = terrainOrigin + new Vector3(
                        Random.Range(8f, terrainSize.x - 8f), 0f,
                        Random.Range(8f, terrainSize.z - 8f));
                }

                candidate.y = terrain.SampleHeight(candidate) + terrainOrigin.y;
                if (!IsInsideTerrain(candidate, terrainOrigin, terrainSize)) continue;
                // The Superior Anomaly island is an explicit requested exception: the broad
                // village protection radius reaches it, even though the rivers visually separate
                // it from spawn. Wilderness placement still respects the full exclusion.
                if (!bossIsland && Vector2.Distance(Flat(candidate), Flat(spawnCenter)) < SpawnExclusionRadius) continue;
                if (ContainsExpanded(castleBounds, candidate, 18f) || ContainsExpanded(templeBounds, candidate, 16f)) continue;
                if (IsWater(candidate) || IsSteep(candidate, terrain, bossIsland ? .58f : .68f)) continue;

                NatureEntry entry = Pick(entries);
                if (IsBlocked(candidate, entry.clearance, bossObject)) continue;

                Transform parent = entry.label.Contains("Grass") ? grassGroup :
                    entry.label.Contains("Flower") ? flowerGroup :
                    entry.label.Contains("Mushroom") ? mushroomGroup :
                    entry.label.Contains("Bush") ? bushGroup : woodGroup;
                Place(entry, candidate, parent, terrain);
                placed++;
            }
            return placed;
        }
    }

    static List<NatureEntry> LoadEntries()
    {
        return new List<NatureEntry>
        {
            new NatureEntry(PackRoot + "Grass/Grass.prefab", "Grass", 42f, .68f, 1.28f, .42f, false),
            new NatureEntry(PackRoot + "Flower/Flower.prefab", "Flower", 16f, .72f, 1.18f, .48f, false),
            new NatureEntry(PackRoot + "Mushroom/Mushrooms Patch.prefab", "Mushroom", 13f, .62f, 1.12f, .62f, false),
            new NatureEntry(PackRoot + "Bush/Bush.prefab", "Bush", 11f, .68f, 1.22f, 1.25f, true),
            new NatureEntry(PackRoot + "Log/Log.prefab", "Log", 7f, .68f, 1.08f, 1.55f, true),
            new NatureEntry(PackRoot + "Stump/Stump.prefab", "Stump", 5f, .72f, 1.12f, 1.2f, true),
            new NatureEntry(PackRoot + "Branch/Branch.prefab", "Branch", 6f, .68f, 1.18f, .9f, true)
        };
    }

    static NatureEntry Pick(List<NatureEntry> entries)
    {
        float total = 0f;
        foreach (NatureEntry entry in entries) total += entry.weight;
        float roll = Random.Range(0f, total);
        foreach (NatureEntry entry in entries)
        {
            roll -= entry.weight;
            if (roll <= 0f) return entry;
        }
        return entries[entries.Count - 1];
    }

    static void Place(NatureEntry entry, Vector3 position, Transform parent, Terrain terrain)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
        instance.name = entry.label;
        instance.transform.SetParent(parent, true);
        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(
            Mathf.InverseLerp(terrain.transform.position.x, terrain.transform.position.x + terrain.terrainData.size.x, position.x),
            Mathf.InverseLerp(terrain.transform.position.z, terrain.transform.position.z + terrain.terrainData.size.z, position.z));
        Quaternion slope = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, entry.label == "Log" || entry.label == "Branch" ? .7f : .2f));
        instance.transform.SetPositionAndRotation(position + Vector3.up * .025f, slope * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        instance.transform.localScale *= Random.Range(entry.minScale, entry.maxScale);

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = entry.castsShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = entry.castsShadows;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
        }
        SetStaticRecursive(instance);
    }

    static bool IsBlocked(Vector3 position, float radius, GameObject boss)
    {
        foreach (Collider hit in Physics.OverlapSphere(position + Vector3.up * .45f, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit == null || hit is TerrainCollider) continue;
            if (boss != null && hit.transform.IsChildOf(boss.transform)) return true;
            string name = FullPath(hit.transform).ToLowerInvariant();
            if (name.Contains(RootName.ToLowerInvariant()) || name.Contains("grass") || name.Contains("flower") ||
                name.Contains("mushroom") || name.Contains("bush")) continue;
            return true;
        }
        return false;
    }

    static bool IsWater(Vector3 position)
    {
        RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 60f, Vector3.down, 120f, ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            if (hit.point.y < position.y - .1f) continue;
            string path = FullPath(hit.transform).ToLowerInvariant();
            if (path.Contains("water") || path.Contains("river") || path.Contains("rio")) return true;
        }
        return false;
    }

    static bool IsSteep(Vector3 position, Terrain terrain, float minimumUpDot)
    {
        Vector3 local = position - terrain.transform.position;
        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(local.x / terrain.terrainData.size.x, local.z / terrain.terrainData.size.z);
        return Vector3.Dot(normal, Vector3.up) < minimumUpDot;
    }

    static Vector3 FindSpawnCenter()
    {
        GameObject spawn = GameObject.FindWithTag("SpawnPoint");
        if (spawn != null) return spawn.transform.position;
        VillageLayout layout = Object.FindAnyObjectByType<VillageLayout>();
        if (layout != null) return layout.transform.position;
        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform.position : Vector3.zero;
    }

    static GameObject FindNamedObject(params string[] fragments)
    {
        foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            string normalized = transform.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            foreach (string fragment in fragments)
                if (normalized.Contains(fragment.Replace("_", "").ToLowerInvariant())) return transform.gameObject;
        }
        return null;
    }

    static Bounds FindProtectedBounds(params string[] fragments)
    {
        GameObject root = FindNamedObject(fragments);
        if (root == null) return new Bounds(new Vector3(999999f, 0f, 999999f), Vector3.zero);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        Bounds bounds = new Bounds(root.transform.position, Vector3.one * 8f);
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        foreach (Collider collider in colliders) bounds.Encapsulate(collider.bounds);
        return bounds;
    }

    static bool ContainsExpanded(Bounds bounds, Vector3 point, float expansion)
    {
        bounds.Expand(new Vector3(expansion * 2f, 10000f, expansion * 2f));
        return bounds.Contains(point);
    }

    static bool IsInsideTerrain(Vector3 point, Vector3 origin, Vector3 size) =>
        point.x >= origin.x && point.z >= origin.z && point.x <= origin.x + size.x && point.z <= origin.z + size.z;

    static Vector2 Flat(Vector3 value) => new Vector2(value.x, value.z);

    static Transform NewGroup(Transform parent, string name)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    static void SetStaticRecursive(GameObject root)
    {
        StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic |
                                  StaticEditorFlags.ReflectionProbeStatic;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
    }

    static string FullPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    [InitializeOnLoadMethod]
    static void QueueRequestedGeneration()
    {
        EditorApplication.delayCall += TryGenerateRequested;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += TryGenerateRequested;
        };
    }

    static void TryGenerateRequested()
    {
        string absolute = Path.GetFullPath(RequestPath);
        if (!File.Exists(absolute) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(absolute);
        if (File.Exists(absolute + ".meta")) File.Delete(absolute + ".meta");
        Generate();
    }
}
#endif

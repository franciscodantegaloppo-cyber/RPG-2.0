using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Places the "Stylized Fantasy Armory" pack (Assets/Daniel Mistage/Stylized Fantasy Armory/)
// into SpawnVillage as an organized armory zone outside the house ring, plus three separated
// "extra content" corners (clothing shop, fisherman's shack, magitech lab) further out so they
// don't compete thematically with the armory. Safe to re-run: deletes its previous root first.
public static class StylizedArmorySetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/StylizedArmorySetup.generate";
    const string PackRoot = "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/";
    const string RootName = "StylizedArmoryPack";

    const float HouseRingRadius = 15f; // VillageLayout.ringRadius — the zone must stay clear of this
    const float ZoneAngleDeg = 202.5f; // gap-centered between two house directions, opposite the skeleton/dungeon area
    const float ZoneRadius = 32f;
    const float FlowMaxWidth = 44f;
    const float FlowMargin = 4f;

    const int Seed = 91177;

    struct Corner
    {
        public string Name;
        public float AngleDeg;
        public float Radius;
        public string[] Folders;
        public float CellSize;
    }

    static readonly Corner[] FarCorners =
    {
        new Corner { Name = "Clothing Shop Corner", AngleDeg = 112.5f, Radius = 65f, CellSize = 2.5f,
            Folders = new[] { "Extra Content/Fantasy Clothing Shop" } },
        new Corner { Name = "Fisherman Shack Corner", AngleDeg = -67.5f, Radius = 65f, CellSize = 2.5f,
            Folders = new[] { "Extra Content/Fantasy Fisherman's Shack" } },
        new Corner { Name = "Magitech Lab Corner", AngleDeg = 337.5f, Radius = 65f, CellSize = 2.2f,
            Folders = new[] { "Extra Content/Fantasy Magitech Laboratory - Props" } },
    };

    class PlotCursor
    {
        public float X;
        public float Z;
        public float RowDepth;
    }

    [MenuItem("RPG/World/Setup Stylized Armory Pack")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[StylizedArmorySetup] Ignorado durante Play Mode. Sali de Play para ejecutar el setup.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        Random.InitState(Seed);
        RemovePreviousRoot();

        Terrain terrain = Terrain.activeTerrain;
        Vector3 villageCenter = FindVillageCenter();
        GameObject root = new GameObject(RootName);

        Vector3 zoneCenter = PolarPoint(villageCenter, ZoneAngleDeg, ZoneRadius);
        Vector3 forward = Flat(zoneCenter - villageCenter).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        GameObject armoryZone = NewGroup(root.transform, "Armory Zone", zoneCenter);
        int placed = BuildArmoryZone(armoryZone.transform, terrain, zoneCenter, forward, right);

        GameObject farRoot = NewGroup(root.transform, "Far Clusters", villageCenter);
        foreach (Corner corner in FarCorners)
        {
            Vector3 cornerCenter = PolarPoint(villageCenter, corner.AngleDeg, corner.Radius);
            Vector3 cornerForward = Flat(cornerCenter - villageCenter).normalized;
            Vector3 cornerRight = Vector3.Cross(Vector3.up, cornerForward);

            GameObject cornerGroup = NewGroup(farRoot.transform, corner.Name, cornerCenter);
            List<GameObject> items = LoadFolders(corner.Folders);
            PlotCursor cursor = new PlotCursor();
            placed += PlaceFlowGrid(items, cornerGroup.transform, terrain, cornerCenter, cornerForward, cornerRight, cursor, corner.CellSize, "Props");
        }

        MoveHerreroToArmory(zoneCenter, forward, terrain);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[StylizedArmorySetup] Placed {placed} objects from the Stylized Fantasy Armory pack (armory zone + 3 far clusters).");
    }

    [InitializeOnLoadMethod]
    static void RunRequestedSetup()
    {
        EditorApplication.delayCall += TryRunRequestedSetup;
    }

    static void TryRunRequestedSetup()
    {
        string absoluteRequestPath = Path.GetFullPath(RequestPath);
        if (!File.Exists(absoluteRequestPath) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        File.Delete(absoluteRequestPath);
        string metaPath = absoluteRequestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        Setup();
    }

    static int BuildArmoryZone(Transform zoneRoot, Terrain terrain, Vector3 zoneCenter, Vector3 forward, Vector3 right)
    {
        int placed = 0;
        PlotCursor cursor = new PlotCursor();

        // Building sits at the zone center facing back toward the village; everything else
        // flows away from it along `forward` so the village-facing edge stays an open plaza.
        GameObject structureGroup = NewGroup(zoneRoot, "Structure", zoneCenter);
        GameObject building = LoadOne("Armory Structure and Attachables/Armory Structure/Armory.prefab");
        if (building != null)
        {
            placed += PlaceOne(building, structureGroup.transform, terrain, zoneCenter, Quaternion.LookRotation(-forward));
        }

        List<GameObject> attachables = LoadFolder("Armory Structure and Attachables/Armory Attachables");
        placed += PlaceRadial(attachables, structureGroup.transform, terrain, zoneCenter, 6f, 10f);

        GameObject entranceGroup = NewGroup(zoneRoot, "Entrance", zoneCenter);
        GameObject arch = LoadOne("Decorative Props/Entrance Arch/EntranceArch.prefab");
        if (arch != null)
            placed += PlaceOne(arch, entranceGroup.transform, terrain, zoneCenter - forward * 12f, Quaternion.LookRotation(forward));
        GameObject billboard = LoadOne("Decorative Props/Entrance Arch/ArmoryBillboard.prefab");
        if (billboard != null)
            placed += PlaceOne(billboard, entranceGroup.transform, terrain, zoneCenter - forward * 12f + right * 3f, Quaternion.LookRotation(forward));

        // Flow-layout categories: each is allocated a non-overlapping plot by the shared cursor,
        // starting behind the building (away from the village) so the entrance/plaza stays clear.
        Vector3 flowOrigin = zoneCenter + forward * 10f - right * (FlowMaxWidth * 0.5f);

        GameObject combos = NewGroup(zoneRoot, "Assembled Combinations", zoneCenter);
        placed += PlaceFlowGrid(LoadFolder("Combinations"), combos.transform, terrain, flowOrigin, forward, right, cursor, 3.5f, "Combinations");

        GameObject weapons = NewGroup(zoneRoot, "Weapons and Shields", zoneCenter);
        placed += PlaceFlowGrid(LoadFolder("Weapons and Shields"), weapons.transform, terrain, flowOrigin, forward, right, cursor, 1.6f, "Weapons", columns: 12);

        List<GameObject> forgeItems = LoadFolder("Decorative Props/Anvil and Airblower");
        forgeItems.AddRange(LoadFolder("Decorative Props/Cauldron and Campfire"));
        forgeItems.AddRange(LoadFolder("Decorative Props/Oven"));
        forgeItems.AddRange(LoadFolder("Extra Content/Fantasy Workshops and Crafting Vol2"));
        GameObject forge = NewGroup(zoneRoot, "Forge and Crafting", zoneCenter);
        placed += PlaceFlowGrid(forgeItems, forge.transform, terrain, flowOrigin, forward, right, cursor, 3f, "Forge");

        List<GameObject> furnitureItems = LoadFolder("Decorative Props/Table");
        furnitureItems.AddRange(LoadFolder("Decorative Props/Chair"));
        furnitureItems.AddRange(LoadFolder("Decorative Props/Chest"));
        furnitureItems.AddRange(LoadFolder("Decorative Props/Shelve"));
        furnitureItems.AddRange(LoadFolder("Decorative Props/Barrel"));
        furnitureItems.AddRange(LoadFolder("Decorative Props/Bucket"));
        furnitureItems.AddRange(LoadFolder("Decorative Props/Box"));
        GameObject furniture = NewGroup(zoneRoot, "Furniture and Storage", zoneCenter);
        placed += PlaceFlowGrid(furnitureItems, furniture.transform, terrain, flowOrigin, forward, right, cursor, 2.5f, "Furniture");

        List<GameObject> decorItems = LoadFolder("Decorative Props/Torch");
        decorItems.AddRange(LoadFolder("Decorative Props/Lamp"));
        decorItems.AddRange(LoadFolder("Decorative Props/Vases"));
        decorItems.AddRange(LoadFolder("Decorative Props/Books"));
        decorItems.AddRange(LoadFolder("Decorative Props/Bottles"));
        decorItems.AddRange(LoadFolder("Decorative Props/Scroll"));
        decorItems.AddRange(LoadFolder("Decorative Props/Inkwell and Feather"));
        decorItems.AddRange(LoadFolder("Decorative Props/Coin and Bag"));
        decorItems.AddRange(LoadFolder("Decorative Props/Combat Dummy"));
        decorItems.AddRange(LoadFolder("Decorative Props/Target"));
        decorItems.AddRange(LoadFolder("Decorative Props/Helmet"));
        decorItems.AddRange(LoadFolder("Decorative Props/String"));
        decorItems.AddRange(LoadFolder("Extra Content/Fantasy Herbalism Pack - Props"));
        decorItems.AddRange(LoadFolder("Extra Content/Chemtech-Biopunk Scimitar"));
        GameObject decor = NewGroup(zoneRoot, "Decor", zoneCenter);
        placed += PlaceFlowGrid(decorItems, decor.transform, terrain, flowOrigin, forward, right, cursor, 1.4f, "Decor", columns: 14);

        GameObject wagonGroup = NewGroup(zoneRoot, "Wagon", zoneCenter);
        placed += PlaceFlowGrid(LoadFolder("Wagon"), wagonGroup.transform, terrain, flowOrigin, forward, right, cursor, 3.5f, "Wagon", columns: 3);

        // Perimeter fence around the whole flow-laid-out footprint, plus loose nature scatter
        // just outside it so the zone blends into the surrounding terrain.
        float halfDepth = (cursor.Z + cursor.RowDepth) * 0.5f + 10f;
        float halfWidth = FlowMaxWidth * 0.5f + 4f;
        Vector3 flowCenter = zoneCenter + forward * (10f + halfDepth - 10f);

        GameObject fences = NewGroup(zoneRoot, "Perimeter Fences", zoneCenter);
        placed += PlacePerimeter(LoadFolder("Decorative Props/Fences"), fences.transform, terrain, flowCenter, forward, right, halfWidth, halfDepth);

        List<GameObject> natureItems = LoadFolder("Nature/Grass");
        natureItems.AddRange(LoadFolder("Nature/Plants"));
        natureItems.AddRange(LoadFolder("Nature/Rocks"));
        natureItems.AddRange(LoadFolder("Nature/Wooden Logs"));
        natureItems.AddRange(LoadFolder("Extra Content/Fantasy Farm - Props"));
        GameObject nature = NewGroup(zoneRoot, "Nature Scatter", zoneCenter);
        placed += PlaceScatterRing(natureItems, nature.transform, terrain, flowCenter, halfWidth, halfDepth);

        return placed;
    }

    static void MoveHerreroToArmory(Vector3 zoneCenter, Vector3 forward, Terrain terrain)
    {
        NPCHerrero herrero = Object.FindAnyObjectByType<NPCHerrero>(FindObjectsInactive.Include);
        if (herrero == null)
        {
            Debug.LogWarning("[StylizedArmorySetup] No se encontro NPCHerrero en la escena; no se pudo mover a la zona de armeria.");
            return;
        }

        Vector3 pos = Grounded(zoneCenter - forward * 7f, terrain);
        herrero.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-forward));

        GroundSnapOnStart snapper = herrero.GetComponent<GroundSnapOnStart>();
        if (snapper == null)
            snapper = herrero.gameObject.AddComponent<GroundSnapOnStart>();
        snapper.SnapNow();

        EditorUtility.SetDirty(herrero.gameObject);
        Debug.Log("[StylizedArmorySetup] Herrero movido a la zona de armeria: " + pos);
    }

    // ── Placement helpers ──────────────────────────────────────────────

    static int PlaceOne(GameObject prefab, Transform parent, Terrain terrain, Vector3 worldPos, Quaternion rotation)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = prefab.name;
        instance.transform.SetParent(parent, true);
        instance.transform.SetPositionAndRotation(Grounded(worldPos, terrain), rotation);
        return 1;
    }

    static int PlaceRadial(List<GameObject> prefabs, Transform parent, Terrain terrain, Vector3 center, float minRadius, float maxRadius)
    {
        int count = 0;
        for (int i = 0; i < prefabs.Count; i++)
        {
            float angle = (360f / prefabs.Count) * i + Random.Range(-8f, 8f);
            float radius = Random.Range(minRadius, maxRadius);
            Vector3 pos = PolarPoint(center, angle, radius);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
            instance.name = prefabs[i].name;
            instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(Grounded(pos, terrain), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            count++;
        }
        return count;
    }

    static int PlaceFlowGrid(List<GameObject> prefabs, Transform parent, Terrain terrain, Vector3 flowOrigin,
        Vector3 forward, Vector3 right, PlotCursor cursor, float cellSize, string label, int columns = 0)
    {
        if (prefabs.Count == 0)
        {
            Debug.LogWarning($"[StylizedArmorySetup] {label}: no se encontraron prefabs, se omite.");
            return 0;
        }

        if (columns <= 0)
            columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(prefabs.Count)));
        int rows = Mathf.CeilToInt((float)prefabs.Count / columns);
        float width = columns * cellSize;
        float depth = rows * cellSize;

        if (cursor.X + width > FlowMaxWidth)
        {
            cursor.X = 0f;
            cursor.Z += cursor.RowDepth + FlowMargin;
            cursor.RowDepth = 0f;
        }

        float plotX = cursor.X;
        float plotZ = cursor.Z;
        cursor.X += width + FlowMargin;
        cursor.RowDepth = Mathf.Max(cursor.RowDepth, depth);

        int placed = 0;
        for (int i = 0; i < prefabs.Count; i++)
        {
            int col = i % columns;
            int row = i / columns;
            float localRight = -FlowMaxWidth * 0.5f + plotX + col * cellSize + cellSize * 0.5f;
            float localForward = plotZ + row * cellSize + cellSize * 0.5f;
            Vector3 worldPos = flowOrigin + right * localRight + forward * localForward;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
            instance.name = prefabs[i].name;
            instance.transform.SetParent(parent, true);
            float yaw = Random.Range(0f, 360f);
            instance.transform.SetPositionAndRotation(Grounded(worldPos, terrain), Quaternion.Euler(0f, yaw, 0f));
            placed++;
        }
        return placed;
    }

    static int PlacePerimeter(List<GameObject> fencePrefabs, Transform parent, Terrain terrain, Vector3 center,
        Vector3 forward, Vector3 right, float halfWidth, float halfDepth)
    {
        if (fencePrefabs.Count == 0)
            return 0;

        int count = 0;
        const float spacing = 3.5f;

        for (float x = -halfWidth; x <= halfWidth; x += spacing)
        {
            count += PlaceFenceSegment(fencePrefabs, parent, terrain, center + right * x + forward * -halfDepth, count);
            count += PlaceFenceSegment(fencePrefabs, parent, terrain, center + right * x + forward * halfDepth, count);
        }
        for (float z = -halfDepth; z <= halfDepth; z += spacing)
        {
            count += PlaceFenceSegment(fencePrefabs, parent, terrain, center + right * -halfWidth + forward * z, count);
            count += PlaceFenceSegment(fencePrefabs, parent, terrain, center + right * halfWidth + forward * z, count);
        }
        return count;
    }

    static int PlaceFenceSegment(List<GameObject> fencePrefabs, Transform parent, Terrain terrain, Vector3 worldPos, int index)
    {
        GameObject prefab = fencePrefabs[index % fencePrefabs.Count];
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = prefab.name;
        instance.transform.SetParent(parent, true);
        instance.transform.SetPositionAndRotation(Grounded(worldPos, terrain), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        return 1;
    }

    static int PlaceScatterRing(List<GameObject> prefabs, Transform parent, Terrain terrain, Vector3 center, float innerRadius, float outerRadius)
    {
        if (prefabs.Count == 0)
            return 0;

        int count = 0;
        foreach (GameObject prefab in prefabs)
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(innerRadius + 1f, outerRadius + 1f);
            Vector3 pos = PolarPoint(center, angle, radius);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = prefab.name;
            instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(Grounded(pos, terrain), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            count++;
        }
        return count;
    }

    // ── Asset loading ──────────────────────────────────────────────────

    static GameObject LoadOne(string relativePath)
    {
        string path = PackRoot + relativePath;
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null)
            Debug.LogWarning("[StylizedArmorySetup] No se encontro el prefab: " + path);
        return go;
    }

    static List<GameObject> LoadFolder(string relativeFolder)
    {
        string folder = PackRoot + relativeFolder;
        var list = new List<GameObject>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
                list.Add(go);
        }
        if (list.Count == 0)
            Debug.LogWarning("[StylizedArmorySetup] Carpeta vacia o inexistente: " + folder);
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }

    static List<GameObject> LoadFolders(string[] relativeFolders)
    {
        var list = new List<GameObject>();
        foreach (string folder in relativeFolders)
            list.AddRange(LoadFolder(folder));
        return list;
    }

    // ── Small utilities ────────────────────────────────────────────────

    static GameObject NewGroup(Transform parent, string name, Vector3 worldPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = worldPos;
        return go;
    }

    static Vector3 PolarPoint(Vector3 center, float angleDeg, float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return center + new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    static Vector3 Grounded(Vector3 position, Terrain terrain)
    {
        if (terrain != null)
        {
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 0.02f;
            return position;
        }

        if (Physics.Raycast(position + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 160f, ~0, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y + 0.02f;
        return position;
    }

    static Vector3 FindVillageCenter()
    {
        VillageLayout layout = Object.FindAnyObjectByType<VillageLayout>();
        if (layout != null)
            return layout.transform.position;

        GameObject spawn = GameObject.FindWithTag("SpawnPoint");
        if (spawn != null)
            return spawn.transform.position;

        return Vector3.zero;
    }

    static void RemovePreviousRoot()
    {
        GameObject oldRoot = GameObject.Find(RootName);
        if (oldRoot != null)
            Object.DestroyImmediate(oldRoot);
    }
}

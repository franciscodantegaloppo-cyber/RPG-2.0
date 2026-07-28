using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scatters a curated set of LowPolyMedievalPropsLite prefabs around the village as outdoor
// clutter/decoration - small clusters near each house exterior plus a light general scatter
// across the open ground between them. Interiors are already handled by HouseDecorationSetup
// (a different pack); this is exterior-only world dressing.
public static class DecorateWorldMedievalProps
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string DecorRootName = "MedievalProps_Decorations";
    const string PackRoot = "Assets/LowPolyMedievalPropsLite/Prefabs/";

    static readonly string[] HouseClusterProps = {
        "Barrel_01.prefab", "Box_01.prefab", "Firewood_01.prefab", "Fence_01.prefab",
        "Fence_02.prefab", "WoodPlank_01.prefab", "WoodPlank_02.prefab", "Stone_01.prefab",
        "Furniture_01.prefab", "Furniture_02.prefab", "Furniture_03.prefab"
    };

    static readonly string[] TabletopProps = {
        "Jug_01.prefab", "Jug_02.prefab", "Jug_03.prefab", "Jug_04.prefab",
        "Plate_01.prefab", "Plate_02.prefab", "Plate_03.prefab", "Plate_04.prefab",
        "Cup_01.prefab", "Cup_02.prefab", "Wineglass_01.prefab", "Bottle_01.prefab",
        "Food_01.prefab", "Food_02.prefab", "Food_03.prefab", "Food_04.prefab",
        "Food_05.prefab", "Food_06.prefab", "Candle_01.prefab", "Candle_02.prefab",
        "Candle_03.prefab", "Bag_01.prefab"
    };

    static readonly string[] GroundClutterProps = {
        "Barrel_01.prefab", "Box_01.prefab", "Firewood_01.prefab", "Stone_01.prefab",
        "WoodPlank_03.prefab", "Bag_01.prefab", "Bucket_01.prefab"
    };

    const float VillageRingRadius = 15f;
    const float SpawnExclusionRadius = 6f;
    const float ClutterOuterRadius = 30f;

    [MenuItem("RPG/World/Decorate Village With Medieval Props")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[DecorateWorldMedievalProps] Ignorado durante Play Mode.");
            return;
        }

        EditorSceneUtility.OpenSceneSafely(ScenePath);

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("[DecorateWorldMedievalProps] No active terrain found.");
            return;
        }

        GameObject oldDecor = GameObject.Find(DecorRootName);
        if (oldDecor != null)
            Object.DestroyImmediate(oldDecor);

        GameObject decorRoot = new GameObject(DecorRootName);
        decorRoot.transform.position = Vector3.zero;

        VillageLayout layout = Object.FindAnyObjectByType<VillageLayout>();
        int placed = 0;
        if (layout != null)
            placed += DecorateHouseExteriors(layout, decorRoot.transform, terrain);

        placed += ScatterGroundClutter(decorRoot.transform, terrain);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log($"[DecorateWorldMedievalProps] Placed {placed} props around the village.");
    }

    static int DecorateHouseExteriors(VillageLayout layout, Transform parent, Terrain terrain)
    {
        GameObject clusterParent = new GameObject("HouseExteriorClutter");
        clusterParent.transform.SetParent(parent, false);

        int index = 0;
        int placed = 0;
        foreach (Transform house in layout.transform)
        {
            if (!house.name.StartsWith("House_", System.StringComparison.Ordinal))
                continue;

            // Place a small cluster off to one side of the house, outside the walls, alternating
            // sides per house so it doesn't look copy-pasted.
            Vector3 side = index % 2 == 0 ? house.right : -house.right;
            Vector3 basePos = house.position + side * 3.5f - house.forward * 1.5f;

            int propCount = 2 + (index % 3);
            for (int i = 0; i < propCount; i++)
            {
                string prefabName = HouseClusterProps[(index * 3 + i) % HouseClusterProps.Length];
                Vector3 jitter = new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));
                Vector3 pos = basePos + jitter;
                if (PlaceProp(PackRoot + prefabName, clusterParent.transform, pos, terrain, randomizeYaw: true))
                    placed++;
            }

            index++;
        }

        return placed;
    }

    static int ScatterGroundClutter(Transform parent, Terrain terrain)
    {
        GameObject scatterParent = new GameObject("VillageGroundClutter");
        scatterParent.transform.SetParent(parent, false);

        Vector3 spawnPos = Vector3.zero;
        int placed = 0;
        int attempts = 0;
        int target = 28;

        while (placed < target && attempts < target * 20)
        {
            attempts++;
            Vector2 randCircle = Random.insideUnitCircle * ClutterOuterRadius;
            Vector3 testPos = spawnPos + new Vector3(randCircle.x, 0f, randCircle.y);

            if (Vector3.Distance(testPos, spawnPos) < SpawnExclusionRadius)
                continue;

            string[] pool = Random.value < 0.55f ? TabletopProps : GroundClutterProps;
            string prefabName = pool[Random.Range(0, pool.Length)];
            if (PlaceProp(PackRoot + prefabName, scatterParent.transform, testPos, terrain, randomizeYaw: true))
                placed++;
        }

        return placed;
    }

    static bool PlaceProp(string prefabPath, Transform parent, Vector3 position, Terrain terrain, bool randomizeYaw)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (asset == null)
        {
            Debug.LogWarning("[DecorateWorldMedievalProps] Prefab not found: " + prefabPath);
            return false;
        }

        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        instance.name = asset.name + "_Decor";
        instance.transform.position = position;
        instance.transform.rotation = randomizeYaw ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : Quaternion.identity;

        return true;
    }
}

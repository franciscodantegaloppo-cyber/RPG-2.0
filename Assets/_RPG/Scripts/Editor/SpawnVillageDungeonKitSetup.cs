#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SpawnVillageDungeonKitSetup
{
    const string RootName = "Dungeon_Modular_DoorReady";
    const string Pack = "Assets/Gridness Studios/Elementary Dungeon Pack Lite/Prefabs/";
    const float Module = 4.8f;

    static SpawnVillageDungeonKitSetup()
    {
        EditorApplication.delayCall += EnsureInstalled;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += EnsureInstalled;
        };
    }

    [MenuItem("RPG/World/Agregar kit modular de dungeon cerca del spawn")]
    public static void EnsureInstalled()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "SpawnVillage") return;

        GameObject existing = FindInScene(scene, RootName);
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            return;
        }

        Transform spawn = FindSpawn();
        if (spawn == null)
        {
            Debug.LogWarning("[Dungeon Kit] No se encontró SpawnPoint en SpawnVillage.");
            return;
        }

        GameObject root = new GameObject(RootName);
        root.transform.position = FindGround(
            spawn.position + spawn.right * 24f + spawn.forward * 22f);

        // Fifteen floor modules form a useful room immediately, while every piece remains an
        // individual prefab child that can be moved, duplicated or deleted in the Inspector.
        for (int x = -2; x <= 2; x++)
        for (int z = -1; z <= 1; z++)
            AddPiece("Floor_Flat.prefab", root.transform,
                new Vector3(x * Module, 0f, z * Module), 0f, Module * .98f,
                $"Floor_{x + 3}_{z + 2}");

        // Long sides.
        for (int x = -2; x <= 2; x++)
        {
            AddPiece("Wall_Chiseled.prefab", root.transform,
                new Vector3(x * Module, 0f, -1.5f * Module), 0f, Module,
                $"Wall_South_{x + 3}");
            AddPiece("Wall_Chiseled.prefab", root.transform,
                new Vector3(x * Module, 0f, 1.5f * Module), 180f, Module,
                $"Wall_North_{x + 3}");
        }

        // Side walls; their central modules are doorway stands without a door. These are the
        // two clearly marked places where the user can later drop the desired door prefabs.
        for (int z = -1; z <= 1; z++)
        {
            string prefab = z == 0 ? "Wall_DoorStand.prefab" : "Wall_Chiseled.prefab";
            AddPiece(prefab, root.transform,
                new Vector3(-2.5f * Module, 0f, z * Module), 90f, Module,
                z == 0 ? "DOOR_SLOT_West" : $"Wall_West_{z + 2}");
            AddPiece(prefab, root.transform,
                new Vector3(2.5f * Module, 0f, z * Module), -90f, Module,
                z == 0 ? "DOOR_SLOT_East" : $"Wall_East_{z + 2}");
        }

        AddPiece("Column_Half.prefab", root.transform,
            new Vector3(-2.55f * Module, 0f, -1.55f * Module), 0f, 1.25f, "Column_SW");
        AddPiece("Column_Half.prefab", root.transform,
            new Vector3(2.55f * Module, 0f, -1.55f * Module), 0f, 1.25f, "Column_SE");
        AddPiece("Column_Half.prefab", root.transform,
            new Vector3(-2.55f * Module, 0f, 1.55f * Module), 180f, 1.25f, "Column_NW");
        AddPiece("Column_Half.prefab", root.transform,
            new Vector3(2.55f * Module, 0f, 1.55f * Module), 180f, 1.25f, "Column_NE");
        AddPiece("Stair.prefab", root.transform,
            new Vector3(0f, 0f, -2.05f * Module), 0f, Module, "Entrance_Stairs");
        AddPiece("Barrel_C.prefab", root.transform,
            new Vector3(-1.9f * Module, 0f, .8f * Module), 20f, 1.2f, "Prop_Barrel_1");
        AddPiece("Barrel_C.prefab", root.transform,
            new Vector3(-1.55f * Module, 0f, .95f * Module), -18f, 1.05f, "Prop_Barrel_2");
        AddPiece("Cauldron.prefab", root.transform,
            new Vector3(1.8f * Module, 0f, .8f * Module), 0f, 1.8f, "Prop_Cauldron");

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Dungeon Kit] Sala modular agregada como 'Dungeon_Modular_DoorReady'. Los accesos DOOR_SLOT_West/East están preparados para colocar puertas.");
    }

    static GameObject AddPiece(string prefabName, Transform parent, Vector3 localCenter,
        float yaw, float targetHorizontalSize, string objectName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + prefabName);
        if (prefab == null)
        {
            Debug.LogWarning("[Dungeon Kit] Falta prefab: " + prefabName);
            return null;
        }

        GameObject piece = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (piece == null) piece = Object.Instantiate(prefab);
        piece.name = objectName;
        piece.transform.SetParent(parent, true);
        piece.transform.SetPositionAndRotation(parent.position,
            parent.rotation * Quaternion.Euler(0f, yaw, 0f));
        piece.transform.localScale = Vector3.one;

        Bounds bounds = VisualBounds(piece);
        float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
        if (horizontal > .001f)
            piece.transform.localScale *= targetHorizontalSize / horizontal;

        bounds = VisualBounds(piece);
        Vector3 desired = parent.TransformPoint(localCenter);
        piece.transform.position += new Vector3(
            desired.x - bounds.center.x,
            parent.position.y - bounds.min.y,
            desired.z - bounds.center.z);
        EnsureCollider(piece);
        SetStatic(piece.transform);
        return piece;
    }

    static void EnsureCollider(GameObject piece)
    {
        if (piece.GetComponentInChildren<Collider>(true) != null) return;
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds world = VisualBounds(piece);
        Vector3[] corners =
        {
            new(world.min.x, world.min.y, world.min.z),
            new(world.max.x, world.min.y, world.min.z),
            new(world.min.x, world.max.y, world.min.z),
            new(world.min.x, world.min.y, world.max.z),
            new(world.max.x, world.max.y, world.min.z),
            new(world.max.x, world.min.y, world.max.z),
            new(world.min.x, world.max.y, world.max.z),
            new(world.max.x, world.max.y, world.max.z)
        };
        Vector3 localMin = piece.transform.InverseTransformPoint(corners[0]);
        Vector3 localMax = localMin;
        foreach (Vector3 corner in corners)
        {
            Vector3 local = piece.transform.InverseTransformPoint(corner);
            localMin = Vector3.Min(localMin, local);
            localMax = Vector3.Max(localMax, local);
        }
        BoxCollider box = piece.AddComponent<BoxCollider>();
        box.center = (localMin + localMax) * .5f;
        box.size = localMax - localMin;
    }

    static Bounds VisualBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static Vector3 FindGround(Vector3 position)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        else if (Physics.Raycast(position + Vector3.up * 500f, Vector3.down,
                     out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y;
        return position;
    }

    static Transform FindSpawn()
    {
        GameObject tagged = GameObject.FindWithTag("SpawnPoint");
        if (tagged != null) return tagged.transform;
        GameObject named = GameObject.Find("SpawnPoint");
        return named != null ? named.transform : null;
    }

    static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child.gameObject;
        return null;
    }

    static void SetStatic(Transform root)
    {
        root.gameObject.isStatic = true;
        foreach (Transform child in root) SetStatic(child);
    }
}
#endif

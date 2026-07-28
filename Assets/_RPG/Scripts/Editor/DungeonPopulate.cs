using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DungeonPopulate
{
    const string DungeonScenePath = "Assets/_RPG/Scenes/dungeon_1.unity";
    const string PackPrefabPath = "Assets/Gridness Studios/Elementary Dungeon Pack Lite/Prefabs/";
    const string SkeletonPrefabPath = "Assets/_RPG/Prefabs/Enemies/SkeletonEnemy.prefab";

    static readonly string[] DecorationNames =
    {
        "Barrel_C", "Book_Holy_C", "Cauldron", "Chain_Metal.002", "Chain_Metal",
        "Chair_Small.001", "Club", "Flag.004", "Pot", "Potion_Blue", "Potion_Red",
        "Sawblade", "Shelf_Framed", "SquareChest_Bottom", "Table.003", "Traphome_Mid"
    };

    const int SkeletonCount = 7;
    const int DecorationCount = 26;
    const float KeepClearRadius = 3.5f; // around the entrance/exit marker

    [MenuItem("RPG/Dungeon/Populate dungeon_1 (Doors, Decor, Skeletons)")]
    public static void Populate()
    {
        var scene = EditorSceneManager.OpenScene(DungeonScenePath, OpenSceneMode.Single);

        GameObject root = GameObject.Find("DungeonRoot");
        if (root == null)
        {
            Debug.LogError("[DungeonPopulate] No se encontro DungeonRoot en dungeon_1. Corre primero 'RPG/Dungeon/Generate dungeon_1'.");
            return;
        }

        Transform floors = root.transform.Find("Floors");
        Transform doors = root.transform.Find("Doors");
        Transform props = root.transform.Find("Props");

        // Safe to re-run: clear anything a previous pass already scattered before adding fresh ones.
        RemoveIfExists(props, "Enemies");
        RemoveIfExists(props, "Decoration");

        int doorsFixed = AddDoorHinges(doors);

        GameObject exitMarker = GameObject.Find("DungeonExit_Rendija");
        Vector3 keepClear = exitMarker != null ? exitMarker.transform.position : Vector3.zero;

        List<Vector3> floorPositions = floors == null
            ? new List<Vector3>()
            : floors.Cast<Transform>()
                .Select(t => t.position)
                .Where(p => Vector3.Distance(p, keepClear) > KeepClearRadius)
                .ToList();

        Shuffle(floorPositions);

        int skeletons = SpawnSkeletons(floorPositions, props);
        int decor = SpawnDecoration(floorPositions, props, skeletons);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[DungeonPopulate] {doorsFixed} puertas con bisagra, {skeletons} esqueletos, {decor} props de decoracion agregados a dungeon_1.");
    }

    const string RequestPath = "Assets/_RPG/Generated/DungeonPopulate.generate";

    [InitializeOnLoadMethod]
    static void RunRequestedPopulate()
    {
        EditorApplication.delayCall += TryRunRequested;
    }

    static void TryRunRequested()
    {
        string absolutePath = System.IO.Path.GetFullPath(RequestPath);
        if (!System.IO.File.Exists(absolutePath) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        System.IO.File.Delete(absolutePath);
        string metaPath = absolutePath + ".meta";
        if (System.IO.File.Exists(metaPath))
            System.IO.File.Delete(metaPath);

        Populate();
    }

    static void RemoveIfExists(Transform parent, string name)
    {
        if (parent == null) return;
        Transform existing = parent.Find(name);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
    }

    static int AddDoorHinges(Transform doors)
    {
        if (doors == null) return 0;

        int count = 0;
        var interactableLayer = LayerMask.NameToLayer("Interactable");

        foreach (Transform child in doors)
        {
            if (!child.name.StartsWith("Door_Middle")) continue;

            MeshFilter mf = child.GetComponent<MeshFilter>();
            Bounds localBounds = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(0.8f, 2f, 0.1f));
            // Hinge on the actual mesh edge (not just half the width), so it works even if the
            // door's pivot isn't centered on the mesh.
            float hingeEdgeX = localBounds.max.x * child.localScale.x;

            HouseDoor door = child.GetComponent<HouseDoor>();
            if (door == null) door = Undo.AddComponent<HouseDoor>(child.gameObject);
            var so = new SerializedObject(door);
            so.FindProperty("hingeOffsetX").floatValue = hingeEdgeX;
            so.ApplyModifiedProperties();

            // DungeonGenerator marks the whole dungeon (including doors) BatchingStatic for
            // performance. Static-batched meshes ignore runtime transform changes, so the
            // hinge rotation would run with zero visual effect unless we opt this door back
            // out of the batch (same reason the village house doors, which were never marked
            // static, animate correctly).
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, (StaticEditorFlags)0);

            // Solid physical blocker on the Default layer so the player collides with it like
            // any other wall until it's opened. Remove any stale collider first (e.g. one that
            // was previously placed on the Interactable layer by an older version of this tool).
            foreach (var oldCollider in child.GetComponents<Collider>())
                Object.DestroyImmediate(oldCollider);
            child.gameObject.layer = 0; // Default
            var box = child.gameObject.AddComponent<BoxCollider>();
            box.center = localBounds.center;
            box.size = localBounds.size;

            // Separate trigger-only child on the Interactable layer purely for the "[E] Abrir
            // puerta" prompt/detection — keeps the solid collider above off that layer, since
            // some layer pairs (e.g. Interactable vs Player) may not physically collide.
            if (interactableLayer >= 0 && child.Find("InteractZone") == null)
            {
                var zone = new GameObject("InteractZone");
                zone.layer = interactableLayer;
                zone.transform.SetParent(child, false);
                zone.transform.localPosition = localBounds.center;
                var trigger = zone.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = localBounds.size + new Vector3(0.6f, 0.3f, 0.6f);
            }

            count++;
        }

        return count;
    }

    static int SpawnSkeletons(List<Vector3> positions, Transform parent)
    {
        GameObject skeletonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPrefabPath);
        if (skeletonPrefab == null)
        {
            Debug.LogWarning("[DungeonPopulate] No se encontro SkeletonEnemy.prefab. Corre 'RPG/Enemies/Setup Skeleton Enemy' primero.");
            return 0;
        }

        GameObject enemiesRoot = new GameObject("Enemies");
        enemiesRoot.transform.SetParent(parent, true);

        int spawned = 0;
        for (int i = 0; i < positions.Count && spawned < SkeletonCount; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(skeletonPrefab, enemiesRoot.transform);
            instance.transform.position = positions[i] + Vector3.up * 0.05f;
            instance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            spawned++;
        }

        return spawned;
    }

    static int SpawnDecoration(List<Vector3> positions, Transform parent, int offset)
    {
        var prefabs = DecorationNames
            .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(PackPrefabPath + n + ".prefab"))
            .Where(p => p != null)
            .ToList();

        if (prefabs.Count == 0) return 0;

        GameObject decorRoot = new GameObject("Decoration");
        decorRoot.transform.SetParent(parent, true);

        int spawned = 0;
        for (int i = 0; i < DecorationCount; i++)
        {
            int posIndex = offset + i;
            if (posIndex >= positions.Count) break;

            GameObject prefab = prefabs[i % prefabs.Count];
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, decorRoot.transform);

            Vector2 jitter = Random.insideUnitCircle * 0.6f;
            instance.transform.position = positions[posIndex] + new Vector3(jitter.x, 0f, jitter.y);
            instance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            spawned++;
        }

        return spawned;
    }

    static void Shuffle(List<Vector3> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Places a WagonBackpack prop near spawn as a lootable chest (same IInteractable/ChestUI system
// as VillageChest) pre-stocked with a starter sword. Mirrors ChestSetup.cs's pattern closely.
public static class WagonBackpackChestSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string BackpackPrefabPath = "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Wagon/Wagon Parts/WagonBackpack.001.prefab";
    const string SwordItemPath = "Assets/_RPG/ScriptableObjects/Items/StarterSword.asset";
    const string ObjectName = "WagonBackpackChest";

    [MenuItem("RPG/Setup Wagon Backpack Chest")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[WagonBackpackChestSetup] Ignorado durante Play Mode. Sali de Play para colocar el cofre.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject existing = GameObject.Find(ObjectName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackpackPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[WagonBackpackChestSetup] No se encontro el prefab en " + BackpackPrefabPath);
            return;
        }

        GameObject backpack = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        backpack.name = ObjectName;
        backpack.transform.position = FindSpawnCenter() + new Vector3(2.2f, 0f, -1.5f);
        backpack.transform.rotation = Quaternion.Euler(0f, -50f, 0f);

        SnapToTerrain(backpack.transform);

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
            SetLayerRecursive(backpack.transform, interactableLayer);

        if (backpack.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider box = backpack.AddComponent<BoxCollider>();
            Bounds bounds = ComputeBounds(backpack.transform);
            if (bounds.size.sqrMagnitude > 0.0001f)
            {
                box.center = backpack.transform.InverseTransformPoint(bounds.center);
                box.size = Vector3.Scale(bounds.size, InvertScale(backpack.transform.lossyScale)) * 1.15f;
            }
            else
            {
                box.size = new Vector3(1f, 1f, 0.7f);
            }
        }

        ItemData sword = AssetDatabase.LoadAssetAtPath<ItemData>(SwordItemPath);
        if (sword == null)
            Debug.LogWarning("[WagonBackpackChestSetup] No se encontro StarterSword.asset - el cofre quedara vacio.");

        ChestContainer chest = backpack.AddComponent<ChestContainer>();
        var serialized = new SerializedObject(chest);
        serialized.FindProperty("chestName").stringValue = "Mochila de Armas";
        if (sword != null)
        {
            SerializedProperty items = serialized.FindProperty("startingItems");
            items.arraySize = 1;
            items.GetArrayElementAtIndex(0).objectReferenceValue = sword;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(backpack);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[WagonBackpackChestSetup] Mochila de armas colocada en " + backpack.transform.position + " con una espada inicial.");
    }

    static Vector3 FindSpawnCenter()
    {
        VillageLayout layout = Object.FindAnyObjectByType<VillageLayout>();
        if (layout != null)
            return layout.transform.position + new Vector3(0f, 0f, 3.5f);

        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform.position : Vector3.zero;
    }

    static void SnapToTerrain(Transform t)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;
        Vector3 pos = t.position;
        pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
        t.position = pos;
    }

    static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t)
            SetLayerRecursive(child, layer);
    }

    static Vector3 InvertScale(Vector3 scale)
    {
        return new Vector3(
            scale.x != 0f ? 1f / scale.x : 1f,
            scale.y != 0f ? 1f / scale.y : 1f,
            scale.z != 0f ? 1f / scale.z : 1f);
    }

    static Bounds ComputeBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(root.position, Vector3.zero);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }
}

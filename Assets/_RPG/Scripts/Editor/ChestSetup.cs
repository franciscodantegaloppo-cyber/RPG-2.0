using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Places one interactable storage chest at the village center. Follows the same
// find-scene / instantiate / snap-to-terrain / save pattern as SnapNPCsToTerrainSetup.
public static class ChestSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string ChestPrefabPath = "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chest/ChestL.001 1.prefab";
    const string ChestObjectName = "VillageChest";

    [MenuItem("RPG/Setup Village Chest")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[ChestSetup] Ignorado durante Play Mode. Sali de Play para colocar el cofre.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject existing = GameObject.Find(ChestObjectName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[ChestSetup] No se encontro el prefab del cofre en " + ChestPrefabPath);
            return;
        }

        GameObject chest = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        chest.name = ChestObjectName;
        chest.transform.position = FindSpawnCenter();
        chest.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

        SnapToTerrain(chest.transform);

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
            SetLayerRecursive(chest.transform, interactableLayer);

        if (chest.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider box = chest.AddComponent<BoxCollider>();
            Bounds bounds = ComputeBounds(chest.transform);
            if (bounds.size.sqrMagnitude > 0.0001f)
            {
                box.center = chest.transform.InverseTransformPoint(bounds.center);
                box.size = Vector3.Scale(bounds.size, InvertScale(chest.transform.lossyScale)) * 1.15f;
            }
            else
            {
                box.size = new Vector3(1f, 1f, 0.7f);
            }
        }

        chest.AddComponent<ChestContainer>();

        EditorUtility.SetDirty(chest);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[ChestSetup] Cofre colocado en " + chest.transform.position);
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

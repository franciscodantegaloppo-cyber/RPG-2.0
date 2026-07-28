using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Converts the imported pack prefabs into game-ready Resources and places one editable
// environmental fire in the current scene. Generated prefabs keep nested links to the pack.
public static class FreeFireVFXSetup
{
    const string PackFirePath = "Assets/VFXPACK_FIRE_WALLCOEUR/Prefab/VFX_Fire.prefab";
    const string PackTorchPath = "Assets/VFXPACK_FIRE_WALLCOEUR/Prefab/VFX_TorchLight.prefab";
    const string ResourcesFolder = "Assets/_RPG/Resources";
    const string VfxFolder = ResourcesFolder + "/VFX";
    const string ProjectilePath = VfxFolder + "/FreeFireProjectileVFX.prefab";
    const string PlaceablePath = "Assets/_RPG/Prefabs/World/PlaceableFire_FreeFireVFX.prefab";
    const string SceneObjectName = "Placeable_Fire_FreeFireVFX";

    [MenuItem("RPG/VFX/Preparar Free Fire VFX")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[FreeFireVFXSetup] Sali de Play Mode para preparar los efectos.");
            return;
        }

        GameObject packFire = AssetDatabase.LoadAssetAtPath<GameObject>(PackFirePath);
        GameObject packTorch = AssetDatabase.LoadAssetAtPath<GameObject>(PackTorchPath);
        if (packFire == null)
        {
            Debug.LogError("[FreeFireVFXSetup] No se encontro VFX_Fire.prefab del pack Free Fire VFX.");
            return;
        }

        EnsureFolder(ResourcesFolder);
        EnsureFolder(VfxFolder);
        EnsureFolder("Assets/_RPG/Prefabs");
        EnsureFolder("Assets/_RPG/Prefabs/World");

        CreateProjectilePrefab(packFire);
        CreatePlaceableFirePrefab(packTorch != null ? packTorch : packFire);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        PlaceFireInOpenScene();
        Debug.Log("[FreeFireVFXSetup] Bolas de fuego y fuego ambiental configurados con Free Fire VFX.");
    }

    static void CreateProjectilePrefab(GameObject source)
    {
        GameObject root = new GameObject("FreeFireProjectileVFX");
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
        visual.name = "VFX_Fire_Pack";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        RemoveColliders(root);
        PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
        Object.DestroyImmediate(root);
    }

    static void CreatePlaceableFirePrefab(GameObject source)
    {
        GameObject root = new GameObject(SceneObjectName);
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
        visual.name = "FreeFireVFX_FlamesAndSmoke";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        GameObject lightObject = new GameObject("Fire_PointLight");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        Light fireLight = lightObject.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.32f, 0.06f);
        fireLight.range = 7f;
        fireLight.intensity = 2.6f;
        fireLight.shadows = LightShadows.None;

        root.AddComponent<PlaceableFireVFX>();
        RemoveColliders(root);
        PrefabUtility.SaveAsPrefabAsset(root, PlaceablePath);
        Object.DestroyImmediate(root);
    }

    static void PlaceFireInOpenScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == SceneObjectName)
                return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceablePath);
        if (prefab == null)
            return;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = SceneObjectName;
        instance.transform.position = FindPlacementPosition();
        Undo.RegisterCreatedObjectUndo(instance, "Agregar fuego ambiental Free Fire VFX");
        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static Vector3 FindPlacementPosition()
    {
        Vector3 position = Vector3.zero;
        if (Selection.activeTransform != null && Selection.activeTransform.gameObject.scene.IsValid())
            position = Selection.activeTransform.position + Selection.activeTransform.forward * 2f;
        else if (SceneView.lastActiveSceneView != null)
            position = SceneView.lastActiveSceneView.pivot;

        RaycastHit hit;
        if (Physics.Raycast(position + Vector3.up * 500f, Vector3.down, out hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y + 0.03f;
        else if (Terrain.activeTerrain != null)
            position.y = Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.transform.position.y + 0.03f;

        return position;
    }

    static void RemoveColliders(GameObject root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        int slash = folder.LastIndexOf('/');
        string parent = folder.Substring(0, slash);
        string name = folder.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    [InitializeOnLoadMethod]
    static void SetupAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                AssetDatabase.LoadAssetAtPath<GameObject>(PackFirePath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath) == null)
                Setup();
        };
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WaterFountainSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string SourcePrefabPath = "Assets/GVOZDY/Round Four-Tier Water Fountain/Prefabs/fountain_4_light.prefab";
    const string GeneratedPrefabPath = "Assets/_RPG/Prefabs/World/WaterFountain_RoundFourTier.prefab";
    const string ObjectName = "WaterFountain_RoundFourTier_SpawnVillage";
    const string RequestPath = "Assets/_RPG/Generated/WaterFountainHealingSetup.generate";

    [MenuItem("RPG/World/Place Round Water Fountain")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[WaterFountainSetup] Sali de Play Mode para colocar la fuente.");
            return;
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (source == null)
        {
            Debug.LogError("[WaterFountainSetup] No se encontro el prefab importado de la fuente.");
            return;
        }

        EnsureFolder("Assets/_RPG/Prefabs");
        EnsureFolder("Assets/_RPG/Prefabs/World");
        GameObject preparedPrefab = CreateColliderPrefab(source);
        if (preparedPrefab == null)
            return;

        Scene scene = EditorSceneUtility.OpenSceneSafely(ScenePath);
        GameObject fountain = GameObject.Find(ObjectName);
        if (fountain == null)
        {
            fountain = (GameObject)PrefabUtility.InstantiatePrefab(preparedPrefab, scene);
            fountain.name = ObjectName;
        }

        fountain.transform.SetPositionAndRotation(FindVillagePosition(), Quaternion.identity);
        fountain.transform.localScale = Vector3.one;
        SnapBaseToGround(fountain);
        SetStaticRecursively(fountain);

        EditorUtility.SetDirty(fountain);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Bounds bounds = GetBounds(fountain);
        Debug.Log("[WaterFountainSetup] Fuente colocada en SpawnVillage con " +
            fountain.GetComponentsInChildren<MeshCollider>(true).Length +
            " MeshCollider. Posicion=" + fountain.transform.position + " Tamano=" + bounds.size);
    }

    static GameObject CreateColliderPrefab(GameObject source)
    {
        GameObject root = new GameObject("WaterFountain_RoundFourTier");
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
        model.name = "Fountain_Visual_And_Collision";
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        // Unpack only the generated wrapper copy. The mesh, material and textures remain
        // references to the imported pack while collider components can be added safely.
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        foreach (Collider oldCollider in root.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(oldCollider);

        int colliderCount = 0;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;

            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
            collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation |
                MeshColliderCookingOptions.EnableMeshCleaning |
                MeshColliderCookingOptions.WeldColocatedVertices |
                MeshColliderCookingOptions.UseFastMidphase;
            colliderCount++;
        }

        if (colliderCount == 0)
        {
            Debug.LogError("[WaterFountainSetup] El modelo no contiene mallas para crear colliders.");
            Object.DestroyImmediate(root);
            return null;
        }

        if (root.GetComponent<HealingFountainInteractable>() == null)
            root.AddComponent<HealingFountainInteractable>();

        Bounds interactionBounds = GetBounds(root);
        GameObject interaction = new GameObject("HealingInteraction");
        interaction.transform.SetParent(root.transform, false);
        interaction.layer = LayerMask.NameToLayer("Interactable");
        SphereCollider trigger = interaction.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.center = root.transform.InverseTransformPoint(interactionBounds.center);
        trigger.radius = Mathf.Max(interactionBounds.extents.x,
            interactionBounds.extents.z) + 1.25f;

        SetStaticRecursively(root);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, GeneratedPrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    static Vector3 FindVillagePosition()
    {
        VillageLayout village = Object.FindAnyObjectByType<VillageLayout>();
        if (village != null)
        {
            // Six metres to one side keeps the central spawn point unobstructed while the
            // fountain remains inside the house ring and is easy to reposition later.
            return village.transform.position + village.transform.right * 6f;
        }

        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform.position + player.transform.right * 6f : new Vector3(6f, 0f, 0f);
    }

    static void SnapBaseToGround(GameObject fountain)
    {
        Bounds bounds = GetBounds(fountain);
        Vector3 sample = bounds.center;
        float groundY;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            groundY = terrain.SampleHeight(sample) + terrain.transform.position.y;
        else if (Physics.Raycast(sample + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore))
            groundY = hit.point.y;
        else
            groundY = fountain.transform.position.y;

        fountain.transform.position += Vector3.up * (groundY + 0.02f - bounds.min.y);
    }

    static Bounds GetBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void SetStaticRecursively(GameObject root)
    {
        StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.ReflectionProbeStatic;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        int slash = folder.LastIndexOf('/');
        string parent = folder.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
    }

    [InitializeOnLoadMethod]
    static void PlaceAfterCompile()
    {
        EditorApplication.delayCall += TryPlaceAfterCompile;
    }

    static void TryPlaceAfterCompile()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath) == null)
            return;

        string absoluteRequest = Path.GetFullPath(RequestPath);
        bool requested = File.Exists(absoluteRequest);
        if (requested)
        {
            File.Delete(absoluteRequest);
            if (File.Exists(absoluteRequest + ".meta"))
                File.Delete(absoluteRequest + ".meta");
        }

        Scene active = EditorSceneManager.GetActiveScene();
        if (requested || active.path != ScenePath || GameObject.Find(ObjectName) == null)
            Setup();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.delayCall += TryPlaceAfterCompile;
    }
}

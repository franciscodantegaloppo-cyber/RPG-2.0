#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GiantTreeTwoSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RootName = "giant_tree_2";
    const string ConfiguredMarker = "_GiantTree2_Configured";
    const string ModelPath =
        "Assets/MeshyImports/Meshy_Model_20260729_223534/" +
        "Meshy_AI_crea_un_arbol_gigante_0730013501_texture.fbx";
    const string MaterialPath =
        "Assets/_RPG/Materials/GiantTree2_URP.mat";
    const string TextureFolder =
        "Assets/MeshyImports/Meshy_Model_20260729_223534/";
    const float DesiredHeight = 34f;

    static GiantTreeTwoSetup()
    {
        EditorApplication.delayCall += EnsureInstalled;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += EnsureInstalled;
        };
    }

    [MenuItem("RPG/World/Agregar giant_tree_2")]
    public static void EnsureInstalled()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            return;

        GameObject existing = FindSceneRoot(scene, RootName);
        if (existing != null)
        {
            ApplyMaterial(existing);
            if (existing.transform.Find(ConfiguredMarker) == null)
            {
                NormalizeHeight(existing);
                PlaceNearSpawn(existing);
                GroundOnTerrain(existing);
                AddConfiguredMarker(existing);
            }

            EnsureTrunkCollider(existing);
            SetLayerAndStatic(existing);
            Save(existing, scene);

            Bounds existingBounds = VisualBounds(existing);
            Debug.Log("[GiantTree2] Instancia existente verificada y " +
                      "guardada. Altura=" +
                      existingBounds.size.y.ToString("0.0") + " m.");
            return;
        }

        GameObject model =
            AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogWarning("[GiantTree2] El modelo todavia no esta " +
                             "listo para ser cargado: " + ModelPath);
            return;
        }

        GameObject tree =
            PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (tree == null)
            tree = Object.Instantiate(model);

        tree.name = RootName;
        tree.SetActive(true);
        tree.transform.localScale = Vector3.one;

        ApplyMaterial(tree);
        NormalizeHeight(tree);
        PlaceNearSpawn(tree);
        GroundOnTerrain(tree);
        EnsureTrunkCollider(tree);
        SetLayerAndStatic(tree);
        AddConfiguredMarker(tree);
        Save(tree, scene);

        SceneView.lastActiveSceneView?.FrameSelected();
        Bounds bounds = VisualBounds(tree);
        Debug.Log("[GiantTree2] Instalado con modelo y texturas URP. " +
                  "Posicion=" + tree.transform.position +
                  ", altura=" + bounds.size.y.ToString("0.0") +
                  " m. El collider esta ajustado al tronco.");
    }

    static void Save(GameObject tree, Scene scene)
    {
        EditorUtility.SetDirty(tree);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = tree;
    }

    static void NormalizeHeight(GameObject tree)
    {
        Bounds bounds = VisualBounds(tree);
        if (bounds.size.y <= .01f)
            return;
        tree.transform.localScale *= DesiredHeight / bounds.size.y;
    }

    static void AddConfiguredMarker(GameObject tree)
    {
        if (tree.transform.Find(ConfiguredMarker) != null)
            return;

        GameObject marker = new GameObject(ConfiguredMarker);
        marker.transform.SetParent(tree.transform, false);
        marker.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;
    }

    static void PlaceNearSpawn(GameObject tree)
    {
        Transform spawn = FindSpawn();
        Vector3 position = spawn != null
            ? spawn.position - spawn.right * 48f +
              spawn.forward * 34f
            : new Vector3(-48f, 0f, 34f);
        tree.transform.SetPositionAndRotation(
            position, Quaternion.Euler(0f, -31f, 0f));
    }

    static Transform FindSpawn()
    {
        try
        {
            GameObject spawn =
                GameObject.FindGameObjectWithTag("SpawnPoint");
            if (spawn != null)
                return spawn.transform;
        }
        catch (UnityException) { }

        GameObject named = GameObject.Find("SpawnPoint") ??
                           GameObject.Find("PlayerSpawn");
        if (named != null)
            return named.transform;

        PlayerController player =
            Object.FindAnyObjectByType<PlayerController>(
                FindObjectsInactive.Include);
        return player != null ? player.transform : null;
    }

    static void GroundOnTerrain(GameObject tree)
    {
        Bounds bounds = VisualBounds(tree);
        Terrain terrain = ClosestTerrain(bounds.center);
        float ground = terrain != null
            ? terrain.SampleHeight(bounds.center) +
              terrain.transform.position.y
            : tree.transform.position.y;
        tree.transform.position +=
            Vector3.up * (ground - bounds.min.y + .03f);
    }

    static Terrain ClosestTerrain(Vector3 position)
    {
        Terrain best = null;
        float closest = float.PositiveInfinity;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
                continue;

            Vector3 center = terrain.transform.position +
                             terrain.terrainData.size * .5f;
            center.y = position.y;
            float distance = (center - position).sqrMagnitude;
            if (distance >= closest)
                continue;
            closest = distance;
            best = terrain;
        }
        return best;
    }

    static void ApplyMaterial(GameObject tree)
    {
        Material material = GetOrCreateMaterial();
        if (material == null)
            return;

        foreach (Renderer renderer in
                 tree.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            if (slots == null || slots.Length == 0)
                slots = new Material[1];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = material;

            renderer.sharedMaterials = slots;
            renderer.enabled = true;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    static Material GetOrCreateMaterial()
    {
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            return null;

        if (material == null)
        {
            material = new Material(shader)
            {
                name = "GiantTree2_URP"
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(
            TextureFolder + "meshy_basecolor.png");
        Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(
            TextureFolder + "meshy_normal.png");
        Texture2D metallicMap =
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                TextureFolder + "meshy_metallic_smoothness.png");

        material.enableInstancing = true;
        material.SetTexture("_BaseMap", baseMap);
        material.SetTexture("_BumpMap", normalMap);
        material.SetTexture("_MetallicGlossMap", metallicMap);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_BumpScale", 1f);
        material.SetFloat("_Metallic", .04f);
        material.SetFloat("_Smoothness", .28f);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    static void EnsureTrunkCollider(GameObject tree)
    {
        foreach (Collider childCollider in
                 tree.GetComponentsInChildren<Collider>(true))
        {
            if (childCollider.gameObject != tree)
                Object.DestroyImmediate(childCollider);
        }

        CapsuleCollider collider = tree.GetComponent<CapsuleCollider>();
        if (collider == null)
            collider = tree.AddComponent<CapsuleCollider>();

        Bounds bounds = VisualBounds(tree);
        float trunkWorldHeight = Mathf.Max(4f, bounds.size.y * .62f);
        float canopyWidth = Mathf.Max(bounds.size.x, bounds.size.z);
        float trunkWorldRadius =
            Mathf.Clamp(canopyWidth * .075f, 1.15f, 3.4f);
        Vector3 worldCenter = new Vector3(
            bounds.center.x,
            bounds.min.y + trunkWorldHeight * .5f,
            bounds.center.z);
        Vector3 scale = tree.transform.lossyScale;

        collider.direction = 1;
        collider.center = tree.transform.InverseTransformPoint(worldCenter);
        collider.radius = trunkWorldRadius /
                          Mathf.Max(Mathf.Abs(scale.x),
                              Mathf.Abs(scale.z), .0001f);
        collider.height = Mathf.Max(
            trunkWorldHeight /
            Mathf.Max(Mathf.Abs(scale.y), .0001f),
            collider.radius * 2f);
        collider.isTrigger = false;
        collider.enabled = true;
    }

    static Bounds VisualBounds(GameObject tree)
    {
        Renderer[] renderers =
            tree.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(tree.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    static void SetLayerAndStatic(GameObject tree)
    {
        StaticEditorFlags flags =
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.ReflectionProbeStatic;

        foreach (Transform child in
                 tree.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = 0;
            GameObjectUtility.SetStaticEditorFlags(
                child.gameObject, flags);
        }
    }

    static GameObject FindSceneRoot(Scene scene, string rootName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root != null && root.name == rootName)
                return root;
        }
        return null;
    }
}
#endif

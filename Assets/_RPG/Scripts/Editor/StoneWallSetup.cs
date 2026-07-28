#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StoneWallSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/StoneWallSetup.generate";
    const string ModelPath = "Assets/MeshyImports/Meshy_Model_20260718_150100/Meshy_AI_Crea_una_muralla_de_t_0718180043_texture.fbx";
    const string MaterialPath = "Assets/_RPG/Materials/StoneWall_Meshy_URP.mat";
    const string BaseMapPath = "Assets/MeshyImports/Meshy_Model_20260718_150100/meshy_basecolor.png";
    const string NormalMapPath = "Assets/MeshyImports/Meshy_Model_20260718_150100/meshy_normal.png";
    const string MetallicMapPath = "Assets/MeshyImports/Meshy_Model_20260718_150100/meshy_metallic_smoothness.png";

    [MenuItem("RPG/World/Place StoneWall Meshy")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject previous = FindExistingWall();
        bool wasAlreadyInScene = previous != null;
        Vector3 position = previous != null ? previous.transform.position : Vector3.zero;
        Quaternion rotation = previous != null ? previous.transform.rotation : Quaternion.identity;

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError("[StoneWall] No se encontro el modelo Meshy en " + ModelPath);
            return;
        }

        // Reemplazar el visual anterior evita conservar escalas cero o referencias de mesh rotas.
        if (previous != null) Object.DestroyImmediate(previous);
        GameObject wall = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (wall == null) wall = Object.Instantiate(model);
        wall.name = "stonewall";
        wall.SetActive(true);
        wall.transform.localScale = Vector3.one;
        if (wasAlreadyInScene) wall.transform.SetPositionAndRotation(position, rotation);
        else PlaceNearSpawn(wall);
        NormalizeNewWallSize(wall, 10f);
        GroundOnTerrain(wall);

        Material material = GetOrCreateMaterial();
        int rendererCount = 0;
        foreach (Renderer renderer in wall.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            if (slots == null || slots.Length == 0) slots = new Material[1];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
            renderer.enabled = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            rendererCount++;
        }

        BoxCollider collider = EnsureFittedCollider(wall);
        SetLayerRecursively(wall, LayerMask.NameToLayer("Default"));
        SetStaticRecursively(wall);

        EditorUtility.SetDirty(wall);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[StoneWall] Listo. " + (wasAlreadyInScene ? "Se conservo la instancia existente" : "Se agrego una nueva instancia") +
                  " en " + wall.transform.position + ", escala=" + wall.transform.lossyScale + ", renderers=" + rendererCount +
                  ", colliderLocal=" + collider.size + ", colliderMundo=" + collider.bounds.size + ", material=" + MaterialPath);
        Selection.activeGameObject = wall;
    }

    static GameObject FindExistingWall()
    {
        foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (!transform.gameObject.scene.IsValid()) continue;
            string normalized = Normalize(transform.name);
            if (normalized.Contains("stonewall") || normalized.Contains("muralladepiedra") || normalized.Contains("pareddepiedra"))
                return transform.gameObject;
        }
        return null;
    }

    static string Normalize(string value)
    {
        return value.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "").Replace(".", "");
    }

    static Material GetOrCreateMaterial()
    {
        Material result = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (result == null)
        {
            result = new Material(urpLit);
            result.name = "StoneWall_Meshy_URP";
            AssetDatabase.CreateAsset(result, MaterialPath);
        }
        else result.shader = urpLit;

        Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMapPath);
        Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalMapPath);
        Texture2D metallicMap = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicMapPath);

        result.enableInstancing = true;
        if (result.HasProperty("_BaseMap")) result.SetTexture("_BaseMap", baseMap);
        if (result.HasProperty("_MainTex")) result.SetTexture("_MainTex", baseMap);
        if (result.HasProperty("_BumpMap")) result.SetTexture("_BumpMap", normalMap);
        if (result.HasProperty("_MetallicGlossMap")) result.SetTexture("_MetallicGlossMap", metallicMap);
        if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", Color.white);
        if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", .32f);
        if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic", .08f);
        if (result.HasProperty("_EmissionColor")) result.SetColor("_EmissionColor", Color.black);
        result.EnableKeyword("_NORMALMAP");
        result.EnableKeyword("_METALLICSPECGLOSSMAP");
        result.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(result);
        return result;
    }

    static void PlaceNearSpawn(GameObject wall)
    {
        Transform anchor = null;
        TonioQuestGiver tonio = Object.FindAnyObjectByType<TonioQuestGiver>(FindObjectsInactive.Include);
        if (tonio != null) anchor = tonio.transform;
        if (anchor == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) anchor = player.transform;
        }

        Vector3 position = anchor != null
            ? anchor.position + anchor.right * 13f + anchor.forward * 8f
            : new Vector3(12f, 0f, 12f);
        Quaternion rotation = anchor != null ? Quaternion.LookRotation(anchor.right, Vector3.up) : Quaternion.identity;
        wall.transform.SetPositionAndRotation(position, rotation);
    }

    static void NormalizeNewWallSize(GameObject wall, float desiredWidth)
    {
        if (!TryGetVisualBounds(wall, out Bounds bounds)) return;
        float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
        if (horizontal < .01f) return;
        float factor = desiredWidth / horizontal;
        wall.transform.localScale *= factor;
    }

    static void GroundOnTerrain(GameObject wall)
    {
        if (!TryGetVisualBounds(wall, out Bounds bounds)) return;
        Terrain terrain = Terrain.activeTerrain;
        float ground = terrain != null
            ? terrain.SampleHeight(bounds.center) + terrain.transform.position.y
            : wall.transform.position.y;
        wall.transform.position += Vector3.up * (ground - bounds.min.y + .02f);
    }

    static BoxCollider EnsureFittedCollider(GameObject wall)
    {
        BoxCollider collider = wall.GetComponent<BoxCollider>();
        if (collider == null) collider = wall.AddComponent<BoxCollider>();
        collider.isTrigger = false;

        bool hasPoint = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        foreach (Renderer renderer in wall.GetComponentsInChildren<Renderer>(true))
        {
            Bounds bounds = renderer.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 point = wall.transform.InverseTransformPoint(center + Vector3.Scale(extents, new Vector3(x, y, z)));
                if (!hasPoint) { min = max = point; hasPoint = true; }
                else { min = Vector3.Min(min, point); max = Vector3.Max(max, point); }
            }
        }

        if (hasPoint)
        {
            collider.center = (min + max) * .5f;
            collider.size = max - min;
            Vector3 size = collider.size;
            Vector3 scale = wall.transform.lossyScale;
            size.x = Mathf.Max(size.x, .3f / Mathf.Max(Mathf.Abs(scale.x), .0001f));
            size.y = Mathf.Max(size.y, .3f / Mathf.Max(Mathf.Abs(scale.y), .0001f));
            size.z = Mathf.Max(size.z, .3f / Mathf.Max(Mathf.Abs(scale.z), .0001f));
            collider.size = size;
        }
        return collider;
    }

    static bool TryGetVisualBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { bounds = default; return false; }
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    static void SetLayerRecursively(GameObject root, int layer)
    {
        if (layer < 0) layer = 0;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
    }

    static void SetStaticRecursively(GameObject root)
    {
        StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                  StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
    }

    [InitializeOnLoadMethod]
    static void QueueSetup()
    {
        EditorApplication.delayCall += TryRun;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += TryRun;
        };
    }

    static void TryRun()
    {
        string absolute = Path.GetFullPath(RequestPath);
        if (!File.Exists(absolute) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(absolute);
        if (File.Exists(absolute + ".meta")) File.Delete(absolute + ".meta");
        Setup();
    }
}
#endif

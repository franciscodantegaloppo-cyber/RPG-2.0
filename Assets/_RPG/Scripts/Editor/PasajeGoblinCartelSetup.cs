using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PasajeGoblinCartelSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/PasajeGoblinCartel.generate";
    const string ObjectName = "Pasajegoblin_cartel";
    const string Root = "Assets/MeshyImports/Meshy_Model_20260721_180856";
    const string ModelPath = Root + "/Meshy_AI_crea_un_cartel_de_mad_0721210848_texture.fbx";
    const string BaseColorPath = Root + "/meshy_basecolor.png";
    const string NormalPath = Root + "/meshy_normal.png";
    const string MetallicPath = Root + "/meshy_metallic_smoothness.png";
    const string EmissionPath = Root + "/meshy_emission.png";
    const string MaterialPath = "Assets/_RPG/Materials/Pasajegoblin_cartel_URP.mat";

    [MenuItem("RPG/World/Setup Pasaje Goblin Cartel")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[PasajeGoblinCartelSetup] Se configurará al salir de Play Mode.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject sign = FindSceneObject();
        bool created = sign == null;
        if (created)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError("[PasajeGoblinCartelSetup] No se encontró el modelo Meshy.");
                return;
            }
            sign = (GameObject)PrefabUtility.InstantiatePrefab(model);
            sign.name = ObjectName;
            Transform goblinAnchor = FindGoblinAnchor();
            sign.transform.position = goblinAnchor != null
                ? goblinAnchor.position + new Vector3(-4f, 0f, -4f)
                : new Vector3(-25f, 0f, 32f);
            sign.transform.rotation = Quaternion.Euler(0f, 135f, 0f);
            NormalizeNewSignHeight(sign, 2.8f);
            SnapToTerrain(sign.transform);
        }

        GameObject meshyVisual = EnsureMeshyVisual(sign);
        if (meshyVisual == null)
        {
            Debug.LogError("[PasajeGoblinCartelSetup] No se pudo vincular el modelo Meshy al cartel.");
            return;
        }

        // Repair old placeholder objects too: a disabled renderer or a zero scale made the
        // cartel look correctly linked in the hierarchy while remaining completely invisible.
        sign.SetActive(true);
        if (Mathf.Abs(sign.transform.localScale.x) < .0001f ||
            Mathf.Abs(sign.transform.localScale.y) < .0001f ||
            Mathf.Abs(sign.transform.localScale.z) < .0001f)
            sign.transform.localScale = Vector3.one;

        foreach (Renderer renderer in sign.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            renderer.forceRenderingOff = false;
            renderer.gameObject.SetActive(true);
        }

        NormalizeSignHeight(sign, 2.8f);
        SnapToTerrain(sign.transform);

        Material material = EnsureMaterial();
        foreach (Renderer renderer in sign.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }

        BoxCollider volume = sign.GetComponent<BoxCollider>();
        if (volume == null) volume = sign.AddComponent<BoxCollider>();
        FitColliderToRenderers(sign.transform, volume);

        EditorUtility.SetDirty(sign);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[PasajeGoblinCartelSetup] " + sign.name +
                  " tiene textura URP y collider volumétrico" +
                  (created ? " y fue colocado junto al pasaje goblin." : "; se conservó su posición actual."));
    }

    [InitializeOnLoadMethod]
    static void RunRequestedSetup() => EditorApplication.delayCall += TryRunRequestedSetup;

    static void TryRunRequestedSetup()
    {
        string absolute = Path.GetFullPath(RequestPath);
        if (!File.Exists(absolute)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRunRequestedSetup;
            return;
        }
        File.Delete(absolute);
        if (File.Exists(absolute + ".meta")) File.Delete(absolute + ".meta");
        Setup();
    }

    static GameObject FindSceneObject()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform candidate in all)
            if (candidate.gameObject.scene.IsValid() &&
                string.Equals(candidate.name, ObjectName, System.StringComparison.OrdinalIgnoreCase))
                return candidate.gameObject;
        return null;
    }

    static Transform FindGoblinAnchor()
    {
        GoblinSpawner[] spawners = Object.FindObjectsByType<GoblinSpawner>(FindObjectsInactive.Include);
        return spawners.Length > 0 ? spawners[0].transform : null;
    }

    static GameObject EnsureMeshyVisual(GameObject sign)
    {
        foreach (MeshFilter filter in sign.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            string meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
            if (string.Equals(meshPath, ModelPath, System.StringComparison.OrdinalIgnoreCase))
                return filter.gameObject;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) return null;
        GameObject visual = PrefabUtility.InstantiatePrefab(model, sign.scene) as GameObject;
        if (visual == null) visual = Object.Instantiate(model);
        visual.name = "Pasajegoblin_cartel_MeshyVisual";
        visual.transform.SetParent(sign.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        Bounds visualBounds = RendererBounds(visual.transform);
        if (visualBounds.size.y > .001f)
        {
            float targetWorldHeight = 2.8f;
            visual.transform.localScale *= Mathf.Clamp(targetWorldHeight / visualBounds.size.y, .01f, 100f);
            visualBounds = RendererBounds(visual.transform);
            visual.transform.position += Vector3.up * (sign.transform.position.y - visualBounds.min.y);
        }
        return visual;
    }

    static Material EnsureMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader) { name = "Pasajegoblin_cartel_URP" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else material.shader = shader;

        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
        Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseColor);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", baseColor);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
        if (material.HasProperty("_MetallicGlossMap")) material.SetTexture("_MetallicGlossMap", metallic);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .15f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .32f);
        if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", emission);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.white * .35f);
        material.EnableKeyword("_NORMALMAP");
        if (emission != null) material.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    static void NormalizeNewSignHeight(GameObject sign, float targetHeight)
    {
        Bounds bounds = RendererBounds(sign.transform);
        if (bounds.size.y <= .001f) return;
        float scale = Mathf.Clamp(targetHeight / bounds.size.y, .01f, 100f);
        sign.transform.localScale *= scale;
    }

    static void NormalizeSignHeight(GameObject sign, float targetHeight)
    {
        Bounds bounds = RendererBounds(sign.transform);
        if (bounds.size.y <= .001f)
        {
            sign.transform.localScale = Vector3.one;
            bounds = RendererBounds(sign.transform);
        }
        if (bounds.size.y <= .001f) return;
        float scale = Mathf.Clamp(targetHeight / bounds.size.y, .02f, 50f);
        sign.transform.localScale *= scale;

        // Keep the lowest visible point resting on the terrain/object origin.
        bounds = RendererBounds(sign.transform);
        sign.transform.position += Vector3.up * (sign.transform.position.y - bounds.min.y);
    }

    static void FitColliderToRenderers(Transform root, BoxCollider collider)
    {
        Bounds bounds = RendererBounds(root);
        collider.center = root.InverseTransformPoint(bounds.center);
        Vector3 scale = root.lossyScale;
        collider.size = new Vector3(
            bounds.size.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(.001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(.001f, Mathf.Abs(scale.z)));
        collider.isTrigger = false;
    }

    static Bounds RendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void SnapToTerrain(Transform target)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;
        Vector3 position = target.position;
        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        target.position = position;
    }
}

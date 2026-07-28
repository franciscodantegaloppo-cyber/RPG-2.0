using System.Collections.Generic;
using UnityEngine;

public static class PreviewRenderIsolation
{
    public const int PreviewLayer = 30;
    public const int PreviewMask = 1 << PreviewLayer;
    static int stageIndex;
    static readonly Dictionary<Material, Material> previewMaterialCache = new Dictionary<Material, Material>();
    static Material nullPreviewMaterial;

    // Every preview camera uses the same isolated layer, so spatial separation
    // must be deterministic and larger than the camera far plane. Random
    // positions occasionally placed two previews together, mixing a sword with
    // gloves/armour in both inventory and crafting.
    public static Vector3 AllocateStagePosition()
    {
        int index = stageIndex++;
        int column = index % 256;
        int row = index / 256;
        return new Vector3(5000f + column * 64f, 5000f + row * 64f, 5000f);
    }

    public static void Configure(Camera camera, Light light)
    {
        if (camera != null)
            camera.cullingMask = PreviewMask;
        DisablePreviewLight(light);
    }

    public static void DisablePreviewLight(Light light)
    {
        if (light == null)
            return;

        light.enabled = false;
        light.intensity = 0f;
        light.cullingMask = 0;
    }

    public static void SetLayerRecursive(GameObject root)
    {
        if (root == null)
            return;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = PreviewLayer;
    }

    public static void ApplyUnlitPreviewMaterials(GameObject root)
    {
        if (root == null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Texture") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Standard");

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            // sharedMaterials avoids Unity cloning every source material before we even replace
            // it. Identical swords/armour now reuse one cached unlit preview material.
            Material[] sourceMaterials = renderer.sharedMaterials;
            for (int i = 0; i < sourceMaterials.Length; i++)
                sourceMaterials[i] = GetOrCreatePreviewMaterial(sourceMaterials[i], shader);
            renderer.sharedMaterials = sourceMaterials;
        }
    }

    static Material GetOrCreatePreviewMaterial(Material source, Shader shader)
    {
        if (source == null)
        {
            if (nullPreviewMaterial == null)
                nullPreviewMaterial = CreatePreviewMaterial(null, shader);
            return nullPreviewMaterial;
        }

        if (!previewMaterialCache.TryGetValue(source, out Material preview) || preview == null)
        {
            preview = CreatePreviewMaterial(source, shader);
            previewMaterialCache[source] = preview;
        }
        return preview;
    }

    static Material CreatePreviewMaterial(Material source, Shader shader)
    {
        Material preview = new Material(shader);
        if (source == null)
            return preview;

        Texture texture = null;
        if (source.HasProperty("_BaseMap"))
            texture = source.GetTexture("_BaseMap");
        if (texture == null && source.HasProperty("_MainTex"))
            texture = source.GetTexture("_MainTex");

        Color color = Color.white;
        if (source.HasProperty("_BaseColor"))
            color = source.GetColor("_BaseColor");
        else if (source.HasProperty("_Color"))
            color = source.GetColor("_Color");

        if (preview.HasProperty("_BaseMap"))
            preview.SetTexture("_BaseMap", texture);
        if (preview.HasProperty("_MainTex"))
            preview.SetTexture("_MainTex", texture);
        if (preview.HasProperty("_BaseColor"))
            preview.SetColor("_BaseColor", color);
        if (preview.HasProperty("_Color"))
            preview.SetColor("_Color", color);
        return preview;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void SanitizeExistingPreviewLights()
    {
        stageIndex = 0;
        foreach (Light sceneLight in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            string objectName = sceneLight.name;
            if (objectName.Contains("PreviewLight") || objectName.Contains("ItemIconRendererLight"))
                DisablePreviewLight(sceneLight);
        }

        foreach (Camera sceneCamera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            string objectName = sceneCamera.name;
            if (objectName.Contains("PreviewCamera") || objectName.Contains("ItemIconRendererCamera"))
            {
                sceneCamera.cullingMask = PreviewMask;
                // Preview components call Camera.Render explicitly. Continuous camera rendering
                // is both unnecessary and extremely costly once many inventory slots exist.
                sceneCamera.enabled = false;
            }
        }
    }
}

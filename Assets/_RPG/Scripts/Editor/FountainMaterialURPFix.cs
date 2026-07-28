using UnityEditor;
using UnityEngine;

public static class FountainMaterialURPFix
{
    const string Root = "Assets/GVOZDY/Round Four-Tier Water Fountain";

    [MenuItem("RPG/World/Fix Water Fountain URP Materials")]
    public static void Fix()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("[FountainMaterialURPFix] No se encontro el shader URP/Lit.");
            return;
        }

        FixVariant("light", urpLit);
        FixVariant("dark", urpLit);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FountainMaterialURPFix] Texturas y materiales URP de la fuente reparados.");
    }

    static void FixVariant(string variant, Shader shader)
    {
        string textureRoot = Root + "/Textures/T_fountain_4_" + variant;
        string materialPath = Root + "/Materials/M_Fountain_4_" + variant + ".mat";
        string albedoPath = textureRoot + "_albedo.png";
        string normalPath = textureRoot + "_Normal.png";
        string metallicPath = textureRoot + "_MetallicSmoothness.png";
        string aoPath = textureRoot + "_ao.png";

        ConfigureTexture(normalPath, TextureImporterType.NormalMap, false);
        ConfigureTexture(metallicPath, TextureImporterType.Default, false);
        ConfigureTexture(aoPath, TextureImporterType.Default, false);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath);
        Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath);
        if (material == null || albedo == null)
            return;

        material.shader = shader;
        material.SetTexture("_BaseMap", albedo);
        material.SetTexture("_MainTex", albedo);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);

        material.SetTexture("_BumpMap", normal);
        material.SetFloat("_BumpScale", 1f);
        material.SetTexture("_MetallicGlossMap", metallic);
        material.SetFloat("_Metallic", metallic != null ? 1f : 0f);
        material.SetFloat("_Smoothness", 0.7f);
        material.SetTexture("_OcclusionMap", ao);
        material.SetFloat("_OcclusionStrength", 1f);

        if (normal != null) material.EnableKeyword("_NORMALMAP");
        else material.DisableKeyword("_NORMALMAP");
        if (metallic != null) material.EnableKeyword("_METALLICSPECGLOSSMAP");
        else material.DisableKeyword("_METALLICSPECGLOSSMAP");

        EditorUtility.SetDirty(material);
    }

    static void ConfigureTexture(string path, TextureImporterType type, bool srgb)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || (importer.textureType == type && importer.sRGBTexture == srgb))
            return;

        importer.textureType = type;
        importer.sRGBTexture = srgb;
        importer.SaveAndReimport();
    }

    [InitializeOnLoadMethod]
    static void FixAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_Fountain_4_light.mat");
            if (material != null && (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit"))
                Fix();
        };
    }
}

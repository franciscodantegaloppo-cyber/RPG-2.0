using UnityEditor;
using UnityEngine;

public static class FixStylizedDarkCastleMaterials
{
    private const string MaterialFolder = "Assets/StylizedDarkCastle/Materials/";
    private const string TextureFolder = "Assets/StylizedDarkCastle/Textures/";

    private static readonly string[] MaterialNames =
    {
        "mat_brick", "mat_dirt", "mat_floorTile", "mat_interior", "mat_roof", "mat_wood"
    };

    [MenuItem("RPG/World/Fix Stylized Dark Castle Materials (Convert to URP)")]
    public static void Fix()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("URP Lit shader not found.");
            return;
        }

        int converted = 0;
        foreach (var name in MaterialNames)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat");
            if (mat == null)
            {
                Debug.LogWarning("Material not found: " + name);
                continue;
            }

            var albedo = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") as Texture2D : null;
            var normal = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") as Texture2D : null;
            var albedoColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

            // Fall back to the textures folder using the pack's naming convention.
            string texKey = name.Replace("mat_", "tex_");
            if (albedo == null)
            {
                albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + texKey + ".png");
            }
            if (normal == null)
            {
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + texKey + "_normal.png");
            }

            mat.shader = shader;
            mat.color = albedoColor;

            if (albedo != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTexture("_MainTex", albedo);
            }

            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", 1f);
            }

            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", name == "mat_wood" ? 0.28f : 0.22f);

            EditorUtility.SetDirty(mat);
            converted++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Converted {converted} StylizedDarkCastle materials to URP/Lit.");
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class LittleEnchantDarkURPSetup
{
    const string SourcePrefab =
        "Assets/AC Little Enchant Mesh VFX/Prefabs/Dark Lvl 3.prefab";
    const string ResourceFolder = "Assets/_RPG/Resources/VFX";
    const string OutputPrefab =
        "Assets/_RPG/Resources/VFX/LittleEnchant_DarkLvl3_URP.prefab";
    const string MaterialFolder = "Assets/_RPG/Materials/LittleEnchantDarkURP";
    const string TextureFolder = "Assets/AC Little Enchant Mesh VFX/Textures/";
    const string RequestPath =
        "Assets/_RPG/Generated/LittleEnchantDarkURPSetup.generate";

    [InitializeOnLoadMethod]
    static void QueueSetup() => EditorApplication.delayCall += TryRun;

    static void TryRun()
    {
        string request = Path.GetFullPath(RequestPath);
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        File.Delete(request);
        if (File.Exists(request + ".meta")) File.Delete(request + ".meta");
        Setup();
    }

    [MenuItem("RPG/VFX/Setup Little Enchant Dark Aura URP")]
    public static void Setup()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
        if (source == null)
        {
            Debug.LogError("[LittleEnchantDarkURPSetup] No se encontro Dark Lvl 3.");
            return;
        }

        EnsureFolder("Assets/_RPG/Resources", "VFX");
        EnsureFolder("Assets/_RPG/Materials", "LittleEnchantDarkURP");

        Material glowAdd = EnsureMaterial("Glow_Add_URP", "Glow.png",
            new Color(.72f, .24f, 1f, .82f), true);
        Material glowFade = EnsureMaterial("Glow_Fade_URP", "Glow.png",
            new Color(.42f, .06f, .68f, .62f), false);
        Material smoke = EnsureMaterial("Smoke_URP", "smoke.png",
            new Color(.16f, .025f, .24f, .72f), false);
        Material smoke1 = EnsureMaterial("Smoke1_URP", "smoke2.png",
            new Color(.24f, .04f, .36f, .66f), false);
        Material smoke2 = EnsureMaterial("Smoke2_URP", "smoke3.png",
            new Color(.09f, .008f, .14f, .76f), false);

        GameObject contents = PrefabUtility.LoadPrefabContents(SourcePrefab);
        contents.name = "LittleEnchant_DarkLvl3_URP";
        foreach (ParticleSystemRenderer renderer in
                 contents.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            Material original = renderer.sharedMaterial;
            string materialName = original != null ? original.name : "";
            if (materialName.Contains("Smoke2"))
                renderer.sharedMaterial = smoke2;
            else if (materialName.Contains("Smoke1"))
                renderer.sharedMaterial = smoke1;
            else if (materialName.Contains("Smoke"))
                renderer.sharedMaterial = smoke;
            else if (materialName.Contains("Fade"))
                renderer.sharedMaterial = glowFade;
            else
                renderer.sharedMaterial = glowAdd;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        PrefabUtility.SaveAsPrefabAsset(contents, OutputPrefab);
        PrefabUtility.UnloadPrefabContents(contents);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LittleEnchantDarkURPSetup] Dark Lvl 3 adaptado a URP y listo para armas encantadas.");
    }

    static Material EnsureMaterial(string name, string textureName,
        Color tint, bool additive)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else material.shader = shader;

        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + textureName);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", additive ? 2f : 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend",
                additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}

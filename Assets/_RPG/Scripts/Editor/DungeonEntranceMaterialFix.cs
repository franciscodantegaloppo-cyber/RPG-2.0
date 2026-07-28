using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// DungeonEntrance's crackParticles ParticleSystemRenderer shipped with no material assigned,
// which URP renders as scattered magenta squares (Hidden/InternalErrorShader) instead of the
// intended gold particle glow. Builds a small reusable glow material/texture and assigns it to
// every DungeonEntrance* particle renderer that's missing one.
public static class DungeonEntranceMaterialFix
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string MaterialDir = "Assets/_RPG/Generated/Materials";
    const string TexturePath = MaterialDir + "/DungeonCrackGlow_SoftDot.png";
    const string MaterialPath = MaterialDir + "/DungeonCrackGlow.mat";

    [MenuItem("RPG/Dungeon/Fix Entrance Particle Material")]
    public static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[DungeonEntranceMaterialFix] Ignorado durante Play Mode.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        Material glowMaterial = LoadOrCreateMaterial();
        int fixedCount = 0;

        foreach (ParticleSystemRenderer psr in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include))
        {
            if (!psr.gameObject.name.StartsWith("DungeonEntrance")) continue;
            if (psr.sharedMaterial != null) continue;

            Undo.RecordObject(psr, "Assign DungeonEntrance glow material");
            psr.sharedMaterial = glowMaterial;
            EditorUtility.SetDirty(psr);
            fixedCount++;
        }

        if (fixedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        Debug.Log("[DungeonEntranceMaterialFix] " + fixedCount + " renderer(s) reparados con DungeonCrackGlow.");
    }

    static Material LoadOrCreateMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null) return existing;

        Directory.CreateDirectory(Path.GetFullPath(MaterialDir));

        Texture2D tex = BuildSoftDotTexture();
        byte[] png = tex.EncodeToPNG();
        File.WriteAllBytes(Path.GetFullPath(TexturePath), png);
        AssetDatabase.ImportAsset(TexturePath);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        Texture2D importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        Material mat = new Material(shader);
        mat.name = "DungeonCrackGlow";
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", importedTex);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", importedTex);

        AssetDatabase.CreateAsset(mat, MaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static Texture2D BuildSoftDotTexture()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    const string RequestPath = "Assets/_RPG/Generated/DungeonEntranceMaterialFix.generate";

    [InitializeOnLoadMethod]
    static void RunRequestedFix()
    {
        EditorApplication.delayCall += TryRun;
    }

    static void TryRun()
    {
        string absolutePath = Path.GetFullPath(RequestPath);
        if (!File.Exists(absolutePath) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        File.Delete(absolutePath);
        string metaPath = absolutePath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        Fix();
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;

public static class ImportedStatusEffectsSetup
{
    const string RequestPath =
        "Assets/_RPG/Generated/ImportedStatusEffectsSetup.generate";
    const string PrefabFolder =
        "Assets/Travis Game Assets/Status Effects/Prefabs/";
    const string VfxDestination = "Assets/_RPG/Resources/VFX/StatusEffects";
    const string IconDestination = "Assets/_RPG/Resources/StatusIcons";

    [InitializeOnLoadMethod]
    static void Queue()
    {
        EditorApplication.delayCall += TryRun;
        EditorApplication.delayCall += EnsureMissionSevenDarkAura;
    }

    static void EnsureMissionSevenDarkAura()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string destination =
            VfxDestination + "/DarkAura.prefab";
        if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
            return;
        EnsurePath(VfxDestination);
        CopyAsset(PrefabFolder + "Debuff_03_Aura.prefab",
            destination);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void TryRun()
    {
        if (!File.Exists(Path.GetFullPath(RequestPath)) ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // The URP replacement package is imported before this stage. Running ImportPackage
        // again here causes a domain reload that discards the queued Finish callback.
        Finish();
    }

    static void Finish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsurePath(VfxDestination);
        EnsurePath(IconDestination);
        CopyAsset(PrefabFolder + "Buff_03a_Aura.prefab",
            VfxDestination + "/LevelUp.prefab");
        CopyAsset(PrefabFolder + "Buff_01a.prefab",
            VfxDestination + "/Healing.prefab");

        CopyIcon("skill_079.png", "LevelUp.png");
        CopyIcon("skill_354.png", "Healing.png");
        CopyIcon("skill_255.png", "Buff.png");
        CopyIcon("skill_034.png", "Fire.png");
        CopyIcon("skill_046.png", "Ice.png");
        CopyIcon("skill_013.png", "Lightning.png");
        CopyIcon("skill_168.png", "DarkAura.png");
        CopyIcon("skill_059.png", "Wind.png");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string request = Path.GetFullPath(RequestPath);
        if (File.Exists(request)) File.Delete(request);
        if (File.Exists(request + ".meta")) File.Delete(request + ".meta");
        Debug.Log("[ImportedStatusEffectsSetup] VFX URP e iconos de estado configurados.");
    }

    static void CopyIcon(string sourceName, string destinationName)
    {
        string source = "Assets/500FreeSkillIcons/Icons/" + sourceName;
        string destination = IconDestination + "/" + destinationName;
        CopyAsset(source, destination);
        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(destination) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    static void CopyAsset(string source, string destination)
    {
        if (AssetDatabase.LoadMainAssetAtPath(source) == null)
        {
            Debug.LogWarning("[ImportedStatusEffectsSetup] No se encontró " + source);
            return;
        }
        if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
            AssetDatabase.DeleteAsset(destination);
        if (!AssetDatabase.CopyAsset(source, destination))
            Debug.LogError("[ImportedStatusEffectsSetup] No se pudo copiar " + source);
    }

    static void EnsurePath(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}

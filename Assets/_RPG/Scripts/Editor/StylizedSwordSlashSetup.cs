using System.IO;
using UnityEditor;
using UnityEngine;

public static class StylizedSwordSlashSetup
{
    const string SourceHorizontal =
        "Assets/NamuFX/Simple Stylized Slash vol2/Prefabs/Slash_A.prefab";
    const string SourceVertical =
        "Assets/NamuFX/Simple Stylized Slash vol2/Prefabs/Slash_B_vertical Variant.prefab";
    const string DestinationFolder =
        "Assets/_RPG/Resources/VFX/SwordSlashes";
    const string DestinationHorizontal = DestinationFolder + "/Slash_A.prefab";
    const string DestinationVertical = DestinationFolder + "/Slash_B_Vertical.prefab";
    const string RequestPath =
        "Assets/_RPG/Generated/StylizedSwordSlashSetup.generate";

    [InitializeOnLoadMethod]
    static void Queue() => EditorApplication.delayCall += TryRun;

    static void TryRun()
    {
        string request = Path.GetFullPath(RequestPath);
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        File.Delete(request);
        if (File.Exists(request + ".meta")) File.Delete(request + ".meta");
        Setup();
    }

    [MenuItem("RPG/VFX/Setup Simple Stylized Sword Slashes")]
    public static void Setup()
    {
        EnsureFolder("Assets/_RPG/Resources", "VFX");
        EnsureFolder("Assets/_RPG/Resources/VFX", "SwordSlashes");
        CopyPrefab(SourceHorizontal, DestinationHorizontal);
        CopyPrefab(SourceVertical, DestinationVertical);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StylizedSwordSlashSetup] Slash_A y Slash_B vertical listos para todas las espadas.");
    }

    static void CopyPrefab(string source, string destination)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(source) == null)
        {
            Debug.LogError("[StylizedSwordSlashSetup] No se encontró " + source);
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(destination) != null)
            AssetDatabase.DeleteAsset(destination);
        if (!AssetDatabase.CopyAsset(source, destination))
            Debug.LogError("[StylizedSwordSlashSetup] No se pudo copiar " + source);
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}

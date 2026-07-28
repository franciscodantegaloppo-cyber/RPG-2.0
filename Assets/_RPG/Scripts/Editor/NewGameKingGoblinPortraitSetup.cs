#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad]
public static class NewGameKingGoblinPortraitSetup
{
    const string Source = "Assets/_RPG/Prefabs/Enemies/KingGoblinBoss.prefab";
    const string DestinationFolder = "Assets/_RPG/Resources/Enemies";
    const string Destination = DestinationFolder + "/KingGoblinPortrait.prefab";

    static NewGameKingGoblinPortraitSetup()
    {
        EditorApplication.delayCall += EnsureResourceCopy;
    }

    static void EnsureResourceCopy()
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(Destination) != null ||
            AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(Source) == null)
            return;

        EnsureFolder("Assets/_RPG/Resources", "Enemies");
        AssetDatabase.CopyAsset(Source, Destination);
        AssetDatabase.ImportAsset(Destination, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif

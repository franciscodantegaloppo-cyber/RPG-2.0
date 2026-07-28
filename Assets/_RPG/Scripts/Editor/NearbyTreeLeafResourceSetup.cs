#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class NearbyTreeLeafResourceSetup
{
    const string Source = "Assets/Proxy Games/Stylized Nature Kit Lite/Materials/Bush Leaves.mat";
    const string ResourcesRoot = "Assets/_RPG/Resources";
    const string Folder = ResourcesRoot + "/NatureFX";
    const string Destination = Folder + "/FlyingLeaves.mat";

    static NearbyTreeLeafResourceSetup()
    {
        EditorApplication.delayCall += EnsureMaterial;
    }

    static void EnsureMaterial()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!AssetDatabase.IsValidFolder(ResourcesRoot)) AssetDatabase.CreateFolder("Assets/_RPG", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(ResourcesRoot, "NatureFX");
        if (AssetDatabase.LoadAssetAtPath<Material>(Destination) == null)
            AssetDatabase.CopyAsset(Source, Destination);
    }
}
#endif

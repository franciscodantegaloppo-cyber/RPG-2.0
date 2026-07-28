#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RpgAnimationPackSetup
{
    const string CatalogFolder = "Assets/_RPG/Resources/Animation";
    const string CatalogPath =
        CatalogFolder + "/RpgAnimationPackCatalog.asset";
    const string AnimFolder = "Assets/DoubleL/Demo/Anim";
    const string UnityFbxFolder = "Assets/DoubleL/FBX Unity";
    const string ShieldPath = "Assets/DoubleL/Model/SM_Wep_Shield_01.fbx";

    static RpgAnimationPackSetup()
    {
        EditorApplication.delayCall += EnsureCatalog;
    }

    [MenuItem("RPG/Player/Rebuild RPG Animation Pack Catalog")]
    public static void EnsureCatalog()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureFolders();

        RpgAnimationPackCatalog catalog =
            AssetDatabase.LoadAssetAtPath<RpgAnimationPackCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<RpgAnimationPackCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        Dictionary<string, AnimationClip> clips =
            new Dictionary<string, AnimationClip>();
        AddClips(AnimFolder, clips);
        AddClips(UnityFbxFolder, clips);
        catalog.clips = clips.Values
            .OrderBy(clip => clip.name)
            .ToArray();
        catalog.shieldPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(ShieldPath);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("[RPGAnimationPack] Catálogo listo: " +
                  catalog.clips.Length +
                  " animaciones y escudo DoubleL.");
    }

    static void AddClips(string folder,
        Dictionary<string, AnimationClip> clips)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip",
                     new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                AnimationClip clip = asset as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__") ||
                    clip.name.Contains("T-Pose")) continue;
                if (!clips.ContainsKey(clip.name))
                    clips.Add(clip.name, clip);
            }
        }
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Resources"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Resources");
        if (!AssetDatabase.IsValidFolder(CatalogFolder))
            AssetDatabase.CreateFolder("Assets/_RPG/Resources", "Animation");
    }
}
#endif

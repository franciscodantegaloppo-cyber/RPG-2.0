#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Installs the imported FS017 panorama set in the open gameplay scene. It only ever changes
// scenes while Unity is in Edit Mode, preventing the play-mode scene-dirty exception.
[InitializeOnLoad]
public static class FantasySkyboxDayNightSetup
{
    const string RootName = "FantasySkyboxDayNight";

    static FantasySkyboxDayNightSetup()
    {
        EditorApplication.delayCall += EnsureInstalled;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += EnsureInstalled;
        };
    }

    [MenuItem("RPG/World/Instalar Fantasy Skybox en ciclo dia/noche")]
    public static void EnsureInstalled()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;

        GameObject root = GameObject.Find(RootName);
        bool changed = false;
        if (root == null) { root = new GameObject(RootName); changed = true; }
        var library = root.GetComponent<FantasySkyboxDayNightLibrary>();
        if (library == null) { library = root.AddComponent<FantasySkyboxDayNightLibrary>(); changed = true; }

        changed |= Assign(ref library.sunrise, "Assets/Fantasy Skybox FREE/Panoramics/FS017/FS017_Sunrise.mat");
        changed |= Assign(ref library.day, "Assets/Fantasy Skybox FREE/Panoramics/FS017/FS017_Day.mat");
        changed |= Assign(ref library.sunset, "Assets/Fantasy Skybox FREE/Panoramics/FS017/FS017_Sunset.mat");
        changed |= Assign(ref library.night, "Assets/Fantasy Skybox FREE/Panoramics/FS017/FS017_Night.mat");
        changed |= Assign(ref library.moonlessNight, "Assets/Fantasy Skybox FREE/Panoramics/FS017/FS017_Night_Moonless.mat");
        CopyRuntimeMaterial("FS017_Sunrise");
        CopyRuntimeMaterial("FS017_Day");
        CopyRuntimeMaterial("FS017_Sunset");
        CopyRuntimeMaterial("FS017_Night");
        CopyRuntimeMaterial("FS017_Night_Moonless");

        // Apply the daytime panorama in Edit Mode too. Previously the material was only set
        // after pressing Play, while the scene camera remained Solid Color from the mountain
        // generator and hid all of the imported Fantasy Skybox clouds in the editor.
        if (library.day != null && RenderSettings.skybox != library.day)
        {
            RenderSettings.skybox = library.day;
            changed = true;
        }
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (camera == null || !camera.CompareTag("MainCamera")) continue;
            if (camera.clearFlags != CameraClearFlags.Skybox) { camera.clearFlags = CameraClearFlags.Skybox; changed = true; }
            Skybox cameraSkybox = camera.GetComponent<Skybox>();
            if (cameraSkybox != null && cameraSkybox.material != library.day)
            {
                cameraSkybox.material = library.day;
                changed = true;
            }
        }

        if (!changed) return;
        EditorUtility.SetDirty(library);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Fantasy Skybox] FS017 vinculado al ciclo día/noche. Guardá la escena para conservarlo.");
    }

    static bool Assign(ref Material slot, string path)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null || slot == material) return false;
        slot = material;
        return true;
    }

    static void CopyRuntimeMaterial(string materialName)
    {
        const string sourceFolder = "Assets/Fantasy Skybox FREE/Panoramics/FS017/";
        const string resourceFolder = "Assets/_RPG/Resources/FantasySkybox";
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Resources")) AssetDatabase.CreateFolder("Assets/_RPG", "Resources");
        if (!AssetDatabase.IsValidFolder(resourceFolder)) AssetDatabase.CreateFolder("Assets/_RPG/Resources", "FantasySkybox");
        string destination = resourceFolder + "/" + materialName + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(destination) == null)
            AssetDatabase.CopyAsset(sourceFolder + materialName + ".mat", destination);
    }
}
#endif

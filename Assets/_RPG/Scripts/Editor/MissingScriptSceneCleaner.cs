#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MissingScriptSceneCleaner
{
    // Runs only when the one-shot request asset exists.
    const string CleanupRequest =
        "Assets/_RPG/Generated/CleanMissingScripts.generate";
    const string CleanupReport =
        "Temp/MissingScriptCleanupReport.txt";

    static MissingScriptSceneCleaner()
    {
        EditorApplication.delayCall += RunRequestedCleanup;
    }

    static void RunRequestedCleanup()
    {
        string absoluteRequest = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            CleanupRequest);
        if (!File.Exists(absoluteRequest)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RunRequestedCleanup;
                return;
            }

            AssetDatabase.DeleteAsset(CleanupRequest);
            AssetDatabase.Refresh();
            CleanActiveScene(false);
        };
    }

    [MenuItem("RPG/Fix/Limpiar scripts faltantes de la escena")]
    public static void CleanActiveScene()
    {
        CleanActiveScene(true);
    }

    static void CleanActiveScene(bool showDialog)
    {
        Scene scene = SceneManager.GetActiveScene();
        int removed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                child.gameObject);

        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("[MissingScriptCleaner] Referencias eliminadas: " + removed);
        Directory.CreateDirectory("Temp");
        File.WriteAllText(CleanupReport,
            scene.path + "\nReferencias eliminadas: " + removed);

        if (showDialog)
            EditorUtility.DisplayDialog("Scripts faltantes",
                removed == 0
                    ? "No se encontraron referencias faltantes."
                    : "Se eliminaron " + removed +
                      " referencias de scripts inexistentes y se guardó la escena.",
                "Aceptar");
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SceneBrokenReferenceRepair
{
    static bool repairing;

    static SceneBrokenReferenceRepair()
    {
        EditorApplication.delayCall += RepairLoadedScenes;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += RepairLoadedScenes;
        };
    }

    [MenuItem("RPG/Repair/Reparar referencias rotas de escenas")]
    public static void RepairLoadedScenes()
    {
        if (repairing || EditorApplication.isPlayingOrWillChangePlaymode) return;
        repairing = true;
        try
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded) continue;

                bool changed = false;
                int removedMissing = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    {
                        GameObject go = child.gameObject;
                        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                        if (missing <= 0) continue;
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                        removedMissing += missing;
                        changed = true;
                    }
                }

                GameObject sky = FindInScene(scene, "FantasySkyboxDayNight");
                if (sky != null)
                {
                    FantasySkyboxDayNightLibrary[] libraries =
                        sky.GetComponents<FantasySkyboxDayNightLibrary>();
                    for (int i = libraries.Length - 1; i >= 1; i--)
                    {
                        Object.DestroyImmediate(libraries[i]);
                        changed = true;
                    }
                }

                GameObject statue = FindInScene(scene, "AnomalyDemonStatue");
                if (statue != null)
                {
                    foreach (BoxCollider box in statue.GetComponentsInChildren<BoxCollider>(true))
                    {
                        Vector3 size = box.size;
                        Vector3 positive = new Vector3(
                            Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                        if (size == positive) continue;
                        box.size = positive;
                        EditorUtility.SetDirty(box);
                        changed = true;
                    }
                }

                if (!changed) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!string.IsNullOrEmpty(scene.path))
                    EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Scene Repair] '{scene.name}': {removedMissing} referencias de scripts faltantes eliminadas y componentes serializados normalizados.");
            }

            // A missing legacy sky component may have been removed above. Reinstall the single,
            // correctly serialized component and its material references afterwards.
            FantasySkyboxDayNightSetup.EnsureInstalled();
            AnomalyDemonStatueSetup.AddToScene();
        }
        finally
        {
            repairing = false;
        }
    }

    static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child.gameObject;
        }
        return null;
    }
}
#endif

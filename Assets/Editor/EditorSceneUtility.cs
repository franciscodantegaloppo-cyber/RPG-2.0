using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Shared helper so world-generator scripts (castle, forest, etc.) never silently discard the
// user's in-progress manual edits. EditorSceneManager.OpenScene(path, Single) always reloads
// from disk - if the target scene is already open and has unsaved changes, that reload throws
// them away with no warning. Re-running a generator after hand-editing the scene was wiping
// out the hand edits because of this.
public static class EditorSceneUtility
{
    public static Scene OpenSceneSafely(string scenePath)
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.path == scenePath)
        {
            if (active.isDirty)
            {
                UnityEngine.Debug.Log($"'{scenePath}' has unsaved changes - saving them before regenerating so nothing is lost.");
                EditorSceneManager.SaveScene(active);
            }
            return active;
        }

        return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    }
}

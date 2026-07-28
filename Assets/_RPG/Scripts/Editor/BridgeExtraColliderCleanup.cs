using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BridgeExtraColliderCleanup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";

    public static void RemoveExtraBridgeColliders()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorSceneUtility.OpenSceneSafely(ScenePath);
        int removed = 0;
        foreach (Transform child in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (child.name != "BridgeWalkableSurface" && child.name != "BridgeWalkableBoxCollider") continue;
            Object.DestroyImmediate(child.gameObject);
            removed++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[BridgeExtraColliderCleanup] Colliders auxiliares eliminados: " + removed);
    }
}

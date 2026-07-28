using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DungeonTransitionSetup
{
    const string VillagePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string DungeonPath = "Assets/_RPG/Scenes/dungeon_1.unity";

    [MenuItem("RPG/Dungeon/Setup Scene Transition Spawn Points")]
    public static void Setup()
    {
        SetupInScene(VillagePath, "DungeonEntrance_Dungeon1");
        SetupInScene(DungeonPath, "DungeonExit_Rendija");
    }

    static void SetupInScene(string scenePath, string markerObjectName)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject marker = GameObject.Find(markerObjectName);
        if (marker == null)
        {
            Debug.LogWarning($"[DungeonTransitionSetup] No se encontro '{markerObjectName}' en {scenePath}.");
            return;
        }

        string spawnName = markerObjectName + "_SpawnPoint";
        GameObject spawnGO = GameObject.Find(spawnName);
        if (spawnGO == null)
        {
            spawnGO = new GameObject(spawnName);
            Undo.RegisterCreatedObjectUndo(spawnGO, "Create SpawnPoint");
        }

        spawnGO.tag = "SpawnPoint";
        spawnGO.transform.position = marker.transform.position + marker.transform.forward * 1.5f + Vector3.up * 0.05f;
        spawnGO.transform.rotation = Quaternion.LookRotation(-marker.transform.forward, Vector3.up);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[DungeonTransitionSetup] SpawnPoint listo en {scenePath} junto a {markerObjectName}.");
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// SpawnVillage has two DungeonEntrance objects: the original one (index 1, Dungeon.unity - has
// no NavMesh-free enemy population and no populate tool) and DungeonEntrance_Dungeon1 (meant for
// dungeon_1.unity, the procedurally generated scene DungeonPopulate actually fills with
// skeletons). Both should point at the populated scene so neither entrance walks the player into
// an empty dungeon.
public static class DungeonEntranceFix
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const int PopulatedDungeonBuildIndex = 2; // dungeon_1.unity

    [MenuItem("RPG/Dungeon/Fix Entrance Target Scene")]
    public static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[DungeonEntranceFix] Ignorado durante Play Mode.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        int fixedCount = 0;
        foreach (DungeonEntrance entrance in Object.FindObjectsByType<DungeonEntrance>(FindObjectsInactive.Include))
        {
            SerializedObject so = new SerializedObject(entrance);
            SerializedProperty prop = so.FindProperty("dungeonSceneIndex");
            if (prop == null) continue;

            if (prop.intValue != PopulatedDungeonBuildIndex)
            {
                prop.intValue = PopulatedDungeonBuildIndex;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(entrance);
                fixedCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[DungeonEntranceFix] " + fixedCount + " entrada(s) redirigidas a dungeon_1 (build index " + PopulatedDungeonBuildIndex + ").");
    }

    [InitializeOnLoadMethod]
    static void RunRequestedFix()
    {
        EditorApplication.delayCall += TryRun;
    }

    const string RequestPath = "Assets/_RPG/Generated/DungeonEntranceFix.generate";

    static void TryRun()
    {
        string absolutePath = System.IO.Path.GetFullPath(RequestPath);
        if (!System.IO.File.Exists(absolutePath) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        System.IO.File.Delete(absolutePath);
        string metaPath = absolutePath + ".meta";
        if (System.IO.File.Exists(metaPath))
            System.IO.File.Delete(metaPath);

        Fix();
    }
}

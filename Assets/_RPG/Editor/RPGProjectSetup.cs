using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public class RPGProjectSetup : EditorWindow
{
    [MenuItem("RPG/Setup Project")]
    public static void SetupProject()
    {
        SetupTags();
        SetupLayers();
        CreateScenes();
        SetupBuildSettings();
        Debug.Log("RPG Project Setup completo!");
    }

    static void SetupTags()
    {
        string[] tags = { "Enemy", "SpawnPoint", "Interactable", "DungeonEntrance" };
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        foreach (string tag in tags)
        {
            bool found = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                { found = true; break; }
            }
            if (!found)
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            }
        }
        tagManager.ApplyModifiedProperties();
        Debug.Log("Tags configurados.");
    }

    static void SetupLayers()
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layersProp = tagManager.FindProperty("layers");

        // Layer 8=Player, 9=Enemy, 10=Interactable, 11=Ground
        string[] layerNames = { "", "", "", "", "", "", "", "", "Player", "Enemy", "Interactable", "Ground" };
        for (int i = 8; i < 12; i++)
        {
            var lp = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(lp.stringValue))
                lp.stringValue = layerNames[i];
        }
        tagManager.ApplyModifiedProperties();
        Debug.Log("Layers configurados.");
    }

    static void CreateScenes()
    {
        string scenesPath = "Assets/_RPG/Scenes";
        if (!Directory.Exists(scenesPath))
            Directory.CreateDirectory(scenesPath);

        string[] sceneNames = { "SpawnVillage", "Dungeon" };
        foreach (string name in sceneNames)
        {
            string path = $"{scenesPath}/{name}.unity";
            if (!File.Exists(path))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scene, path);
                EditorSceneManager.CloseScene(scene, true);
                Debug.Log($"Escena creada: {path}");
            }
        }
        AssetDatabase.Refresh();
    }

    static void SetupBuildSettings()
    {
        var scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/_RPG/Scenes/SpawnVillage.unity", true),
            new EditorBuildSettingsScene("Assets/_RPG/Scenes/Dungeon.unity", true),
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("Build settings configurados.");
    }
}

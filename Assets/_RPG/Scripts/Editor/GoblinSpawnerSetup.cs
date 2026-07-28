using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

public static class GoblinSpawnerSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/GoblinSpawnerSetup.generate";
    static readonly string[] GoblinNames = { "GoblinEnemy1", "GoblinEnemy2", "GoblinEnemy3" };
    static readonly string[] GoblinPrefabPaths =
    {
        "Assets/_RPG/Prefabs/Enemies/GoblinEnemy1.prefab",
        "Assets/_RPG/Prefabs/Enemies/GoblinEnemy2.prefab",
        "Assets/_RPG/Prefabs/Enemies/GoblinEnemy3.prefab",
    };

    [MenuItem("RPG/Enemies/Setup Goblin Spawner")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[GoblinSpawnerSetup] Ignorado durante Play Mode. Sali de Play para ejecutar el setup.");
            return;
        }

        EditorSceneUtility.OpenSceneSafely(ScenePath);

        EnemyStats[] initialGoblins = FindInitialGoblins();
        if (initialGoblins.Length == 0)
        {
            Debug.LogError("[GoblinSpawnerSetup] No se encontraron GoblinEnemy1/2/3 en la escena. Corre 'RPG/Enemies/Setup Goblin Enemies' primero.");
            return;
        }

        GameObject[] prefabs = LoadPrefabs();

        GameObject spawnerGo = GameObject.Find("GoblinSpawner");
        if (spawnerGo == null)
        {
            spawnerGo = new GameObject("GoblinSpawner");
        }
        spawnerGo.transform.position = Centroid(initialGoblins);

        GoblinSpawner spawner = spawnerGo.GetComponent<GoblinSpawner>();
        if (spawner == null)
            spawner = spawnerGo.AddComponent<GoblinSpawner>();

        var serialized = new SerializedObject(spawner);
        SerializedProperty initialProp = serialized.FindProperty("initialGoblins");
        initialProp.arraySize = initialGoblins.Length;
        for (int i = 0; i < initialGoblins.Length; i++)
            initialProp.GetArrayElementAtIndex(i).objectReferenceValue = initialGoblins[i];

        SerializedProperty prefabsProp = serialized.FindProperty("goblinPrefabs");
        prefabsProp.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++)
            prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];

        SetSerialized(serialized, "requireInitialGoblinsDefeated", false);
        SetSerialized(serialized, "activationDelay", 0f);
        SetSerialized(serialized, "spawnInterval", 10f);
        SetSerialized(serialized, "maxGoblinsInRadius", 10);
        SetSerialized(serialized, "spawnRadius", 10f);
        SetSerialized(serialized, "pinkGoblinEvery", 5);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawnerGo);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log($"[GoblinSpawnerSetup] Spawner listo en {spawnerGo.transform.position}, vinculado a {initialGoblins.Length} goblins iniciales y {prefabs.Length} prefabs.");
    }

    [InitializeOnLoadMethod]
    static void RunRequestedSetup()
    {
        EditorApplication.delayCall += TryRunRequestedSetup;
    }

    static void TryRunRequestedSetup()
    {
        string absoluteRequestPath = Path.GetFullPath(RequestPath);
        if (!File.Exists(absoluteRequestPath))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        File.Delete(absoluteRequestPath);
        string metaPath = absoluteRequestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        Setup();
    }

    static EnemyStats[] FindInitialGoblins()
    {
        var found = new System.Collections.Generic.List<EnemyStats>();
        foreach (string name in GoblinNames)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
                continue;
            EnemyStats stats = go.GetComponent<EnemyStats>();
            if (stats != null)
                found.Add(stats);
        }
        return found.ToArray();
    }

    static GameObject[] LoadPrefabs()
    {
        var found = new System.Collections.Generic.List<GameObject>();
        foreach (string path in GoblinPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                found.Add(prefab);
            else
                Debug.LogWarning("[GoblinSpawnerSetup] Prefab no encontrado: " + path);
        }
        return found.ToArray();
    }

    static Vector3 Centroid(EnemyStats[] goblins)
    {
        Vector3 sum = Vector3.zero;
        foreach (EnemyStats g in goblins)
            sum += g.transform.position;
        return sum / goblins.Length;
    }

    static void SetSerialized(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    static void SetSerialized(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    static void SetSerialized(SerializedObject serialized, string propertyName, int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SuperiorTreeAnomalySpawnerSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string PrefabPath = "Assets/_RPG/Prefabs/Enemies/SuperiorTreeAnomaly.prefab";
    const string RequestPath = "Assets/_RPG/Generated/SuperiorTreeAnomalySpawnerSetup.generate";

    [MenuItem("RPG/Enemies/Setup Superior Tree Anomaly Spawner")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[SuperiorTreeAnomalySpawner] No se encontro " + PrefabPath);
            return;
        }

        GameObject existing = GameObject.Find("Superior_tree_anomaly");
        GameObject spawnerObject = GameObject.Find("SuperiorTreeAnomalySpawner")
            ?? new GameObject("SuperiorTreeAnomalySpawner");
        spawnerObject.transform.position = existing != null
            ? existing.transform.position
            : new Vector3(29f, 0f, -18f);

        SuperiorTreeAnomalySpawner spawner = spawnerObject.GetComponent<SuperiorTreeAnomalySpawner>();
        if (spawner == null) spawner = spawnerObject.AddComponent<SuperiorTreeAnomalySpawner>();

        SerializedObject serialized = new SerializedObject(spawner);
        serialized.FindProperty("superiorAnomalyPrefab").objectReferenceValue = prefab;
        serialized.FindProperty("spawnRadius").floatValue = 20f;
        serialized.FindProperty("maxAnomaliesInRadius").intValue = 5;
        serialized.FindProperty("spawnInterval").floatValue = 120f;
        serialized.FindProperty("oneStarChance").floatValue = 0.05f;
        serialized.FindProperty("twoStarChance").floatValue = 0.01f;
        serialized.FindProperty("threeStarChance").floatValue = 0.001f;
        serialized.FindProperty("oneStarMultiplier").floatValue = 10f;
        serialized.FindProperty("twoStarMultiplier").floatValue = 50f;
        serialized.FindProperty("threeStarMultiplier").floatValue = 100f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawnerObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[SuperiorTreeAnomalySpawner] Spawner listo: radio 20 m, maximo 5, intervalo 120 s.");
    }

    [InitializeOnLoadMethod]
    static void StartRequest() => EditorApplication.delayCall += RunRequest;

    static void RunRequest()
    {
        string path = Path.GetFullPath(RequestPath);
        if (!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(path);
        if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
        Setup();
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TreeAnomalySpawnerSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string PrefabPath = "Assets/_RPG/Prefabs/Enemies/TreeAnomaly.prefab";

    [MenuItem("RPG/Enemies/Setup Tree Anomaly Spawner")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[TreeAnomalySpawnerSetup] Ignorado durante Play Mode.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[TreeAnomalySpawnerSetup] No se encontro " + PrefabPath);
            return;
        }

        GameObject existing = GameObject.Find("TreeAnomaly");
        GameObject spawnerGo = GameObject.Find("TreeAnomalySpawner") ?? new GameObject("TreeAnomalySpawner");
        spawnerGo.transform.position = existing != null ? existing.transform.position : new Vector3(18f, 0f, -12f);

        TreeAnomalySpawner spawner = spawnerGo.GetComponent<TreeAnomalySpawner>();
        if (spawner == null) spawner = spawnerGo.AddComponent<TreeAnomalySpawner>();

        SerializedObject serialized = new SerializedObject(spawner);
        serialized.FindProperty("treeAnomalyPrefab").objectReferenceValue = prefab;
        serialized.FindProperty("spawnRadius").floatValue = 10f;
        serialized.FindProperty("maxAnomaliesInRadius").intValue = 5;
        serialized.FindProperty("spawnInterval").floatValue = 20f;
        serialized.FindProperty("oneStarChance").floatValue = 0.05f;
        serialized.FindProperty("oneStarMultiplier").floatValue = 10f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawnerGo);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[TreeAnomalySpawnerSetup] Spawner de anomalias listo.");
    }
}

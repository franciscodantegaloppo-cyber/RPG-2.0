using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SkeletonSpawnerSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string SkeletonPrefabPath = "Assets/_RPG/Prefabs/Enemies/SkeletonEnemy.prefab";

    [MenuItem("RPG/Enemies/Setup Skeleton Spawner")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[SkeletonSpawnerSetup] Ignorado durante Play Mode. Sali de Play para ejecutar el setup.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[SkeletonSpawnerSetup] No se encontro " + SkeletonPrefabPath + ". Corre el setup del esqueleto primero.");
            return;
        }

        GameObject existingSkeleton = GameObject.Find("SkeletonEnemy");
        Vector3 center = existingSkeleton != null ? existingSkeleton.transform.position : FindSpawnCenter() + new Vector3(10f, 0f, -10f);

        GameObject spawnerGo = GameObject.Find("SkeletonSpawner");
        if (spawnerGo == null)
            spawnerGo = new GameObject("SkeletonSpawner");
        spawnerGo.transform.position = center;

        SkeletonSpawner spawner = spawnerGo.GetComponent<SkeletonSpawner>();
        if (spawner == null)
            spawner = spawnerGo.AddComponent<SkeletonSpawner>();

        var serialized = new SerializedObject(spawner);
        SerializedProperty prefabsProp = serialized.FindProperty("skeletonPrefabs");
        prefabsProp.arraySize = 1;
        prefabsProp.GetArrayElementAtIndex(0).objectReferenceValue = prefab;

        SetBool(serialized, "requireInitialSkeletonsDefeated", false);
        SetFloat(serialized, "activationDelay", 0f);
        SetFloat(serialized, "spawnInterval", 10f);
        SetInt(serialized, "maxSkeletonsInRadius", 5);
        SetFloat(serialized, "spawnRadius", 10f);
        SetInt(serialized, "strongSkeletonEvery", 10);
        SetFloat(serialized, "star1Chance", 0.05f);
        SetFloat(serialized, "star2Chance", 0.01f);
        SetFloat(serialized, "star3Chance", 0.000001f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawnerGo);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log($"[SkeletonSpawnerSetup] Spawner listo en {spawnerGo.transform.position}.");
    }

    static Vector3 FindSpawnCenter()
    {
        VillageLayout layout = Object.FindAnyObjectByType<VillageLayout>();
        return layout != null ? layout.transform.position : Vector3.zero;
    }

    static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
    }

    static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
    }

    static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.intValue = value;
    }
}

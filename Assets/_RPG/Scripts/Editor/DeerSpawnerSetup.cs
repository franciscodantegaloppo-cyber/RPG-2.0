using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeerSpawnerSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string DeerPrefabPath = "Assets/ithappy/Animals_FREE/Prefabs/Deer_001.prefab";

    [MenuItem("RPG/Animals/Setup Deer Spawner")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[DeerSpawnerSetup] Ignorado durante Play Mode. Sali de Play para ejecutar el setup.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeerPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[DeerSpawnerSetup] No se encontro " + DeerPrefabPath);
            return;
        }

        GameObject existingDeer = GameObject.Find("Animal_Deer");
        Vector3 center = existingDeer != null ? existingDeer.transform.position : FindSpawnCenter() + new Vector3(13f, 0f, 20f);

        GameObject spawnerGo = GameObject.Find("DeerSpawner");
        if (spawnerGo == null)
            spawnerGo = new GameObject("DeerSpawner");
        spawnerGo.transform.position = center;

        DeerSpawner spawner = spawnerGo.GetComponent<DeerSpawner>();
        if (spawner == null)
            spawner = spawnerGo.AddComponent<DeerSpawner>();

        var serialized = new SerializedObject(spawner);
        serialized.FindProperty("deerPrefab").objectReferenceValue = prefab;
        serialized.FindProperty("spawnInterval").floatValue = 50f;
        serialized.FindProperty("maxDeerInRadius").intValue = 3;
        serialized.FindProperty("spawnRadius").floatValue = 10f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawnerGo);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log($"[DeerSpawnerSetup] Spawner listo en {spawnerGo.transform.position}.");
    }

    static Vector3 FindSpawnCenter()
    {
        VillageLayout layout = Object.FindAnyObjectByType<VillageLayout>();
        return layout != null ? layout.transform.position : Vector3.zero;
    }
}

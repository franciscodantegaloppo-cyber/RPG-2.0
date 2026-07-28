using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlaceGeneratedCastleInSpawnVillage
{
    private const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    private const string CastleAssetPath = "Assets/Generated/MedievalCastleLowPoly/Medieval_Castle_Complete_LowPoly.obj";
    private const string CastleObjectName = "Generated_Medieval_Castle_LowPoly_FarFromSpawn";

    [MenuItem("Tools/Generated Assets/Place Medieval Castle In Spawn Village")]
    public static void PlaceCastle()
    {
        Execute();
    }

    public static void Execute()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"Scene not found: {ScenePath}");
            return;
        }

        if (!File.Exists(CastleAssetPath))
        {
            Debug.LogError($"Castle asset not found: {CastleAssetPath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var existing = GameObject.Find(CastleObjectName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var castleAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CastleAssetPath);
        if (castleAsset == null)
        {
            AssetDatabase.ImportAsset(CastleAssetPath, ImportAssetOptions.ForceSynchronousImport);
            castleAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CastleAssetPath);
        }

        if (castleAsset == null)
        {
            Debug.LogError($"Unity could not load the castle model: {CastleAssetPath}");
            return;
        }

        var castle = (GameObject)PrefabUtility.InstantiatePrefab(castleAsset, scene);
        castle.name = CastleObjectName;

        // The player starts at the origin in SpawnVillage, so the castle sits well away from spawn.
        castle.transform.position = new Vector3(260f, 0f, 260f);
        castle.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
        castle.transform.localScale = Vector3.one;

        var marker = new GameObject("Castle_MapMarker_FarFromSpawn");
        marker.transform.SetParent(castle.transform, false);
        marker.transform.localPosition = new Vector3(0f, 28f, 0f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"Placed {CastleObjectName} in {ScenePath} at {castle.transform.position}.");
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SnapNPCsToTerrainSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/SnapNPCsToTerrain.generate";
    const float DefaultGroundOffset = 0f;

    [MenuItem("RPG/Fix/Snap Merchant And Herrero To Terrain")]
    public static void Snap()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[SnapNPCsToTerrain] Ignorado durante Play Mode. Sali de Play para ejecutar el snap.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        SnapAll<NPCMerchant>("NPCMerchant");
        SnapAll<NPCHerrero>("NPCHerrero");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SnapNPCsToTerrain] NPCMerchant y NPCHerrero apoyados sobre el terreno.");
    }

    [InitializeOnLoadMethod]
    static void RunRequestedSnap()
    {
        EditorApplication.delayCall += TryRunRequestedSnap;
    }

    static void TryRunRequestedSnap()
    {
        string absoluteRequestPath = Path.GetFullPath(RequestPath);
        if (!File.Exists(absoluteRequestPath))
            return;

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        File.Delete(absoluteRequestPath);
        string metaPath = absoluteRequestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        Snap();
    }

    static void SnapAll<T>(string label) where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        foreach (T component in components)
            SnapOne(component.gameObject, label);
    }

    static void SnapOne(GameObject npc, string label)
    {
        CharacterController controller = npc.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = npc.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.95f, 0f);
            controller.height = 1.9f;
            controller.radius = 0.35f;
        }

        NPCWander wander = npc.GetComponent<NPCWander>();
        if (wander != null)
            SetSerialized(wander, "terrainStickOffset", DefaultGroundOffset);

        GroundSnapOnStart snapper = npc.GetComponent<GroundSnapOnStart>();
        if (snapper == null)
            snapper = npc.AddComponent<GroundSnapOnStart>();
        snapper.UseVisualFooting(DefaultGroundOffset);

        NPCVisualGroundAligner aligner = npc.GetComponent<NPCVisualGroundAligner>();
        if (aligner == null)
            aligner = npc.AddComponent<NPCVisualGroundAligner>();
        aligner.Configure(DefaultGroundOffset);

        snapper.SnapNow();
        aligner.AlignNow();
        EditorUtility.SetDirty(npc);

        float groundDelta = 0f;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null && snapper.TryGetVisualBottomY(out float bottomY))
        {
            float terrainY = terrain.SampleHeight(npc.transform.position) + terrain.transform.position.y;
            groundDelta = bottomY - (terrainY + DefaultGroundOffset);
        }

        Debug.Log("[SnapNPCsToTerrain] " + label + " -> " + npc.transform.position +
            " visual/terreno delta: " + groundDelta.ToString("0.000"));
    }

    static void SetSerialized(Object target, string propertyName, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

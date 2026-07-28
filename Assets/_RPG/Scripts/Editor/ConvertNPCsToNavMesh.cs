using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class ConvertNPCsToNavMesh
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";

    [MenuItem("RPG/Fix/Convert Herrero And Merchant To NavMesh")]
    public static void Convert()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        int converted = 0;
        converted += ConvertAll<NPCMerchant>("NPCMerchant");
        converted += ConvertAll<NPCHerrero>("NPCHerrero");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log($"[ConvertNPCsToNavMesh] Simplificados {converted} NPCs: ahora usan proyección directa " +
            "sobre el terreno (mismo método que EnemyAI.ProjectToGround), sin CharacterController ni scripts de snap visual duplicados.");
    }

    static int ConvertAll<T>(string label) where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        int count = 0;
        foreach (T component in components)
        {
            ConvertOne(component.gameObject, label);
            count++;
        }
        return count;
    }

    static void ConvertOne(GameObject npc, string label)
    {
        Undo.RegisterFullObjectHierarchyUndo(npc, "Simplify NPC Grounding");

        Vector3 position = npc.transform.position;

        CharacterController cc = npc.GetComponent<CharacterController>();
        if (cc != null) Object.DestroyImmediate(cc, true);

        NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();
        if (agent != null) Object.DestroyImmediate(agent, true);

        GroundSnapOnStart groundSnap = npc.GetComponent<GroundSnapOnStart>();
        if (groundSnap != null) Object.DestroyImmediate(groundSnap, true);

        NPCVisualGroundAligner aligner = npc.GetComponent<NPCVisualGroundAligner>();
        if (aligner != null) Object.DestroyImmediate(aligner, true);

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            npc.transform.position = position;
        }

        NPCWander wander = npc.GetComponent<NPCWander>();
        if (wander == null) wander = npc.AddComponent<NPCWander>();

        EditorUtility.SetDirty(npc);
        Debug.Log($"[ConvertNPCsToNavMesh] {label} -> {npc.transform.position}");
    }
}

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Creates the visible Hierarchy entry for the otherwise invisible one-shot spawn thought zone.
public static class SpawnMysteryThoughtSetup
{
    [MenuItem("RPG/World/Add Spawn Mystery Thought Trigger")]
    public static void AddToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (SceneManager.GetActiveScene().name != "SpawnVillage")
        {
            Debug.LogWarning("[SpawnMysteryThought] Abre SpawnVillage para agregar el activador.");
            return;
        }

        GameObject trigger = GameObject.Find("SpawnMysteryThoughtTrigger");
        if (trigger == null)
        {
            GameObject spawn = GameObject.FindWithTag("SpawnPoint");
            if (spawn == null)
            {
                Debug.LogError("[SpawnMysteryThought] No se encontro el SpawnPoint.");
                return;
            }

            trigger = new GameObject("SpawnMysteryThoughtTrigger");
            Vector3 forward = spawn.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            trigger.transform.position = spawn.transform.position + forward.normalized * 3.5f;
            BoxCollider box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 1.4f, 0f);
            box.size = new Vector3(6f, 3f, 4f);
            trigger.AddComponent<SpawnMysteryThoughtTrigger>();
        }

        Selection.activeGameObject = trigger;
        EditorSceneManager.MarkSceneDirty(trigger.scene);
        Debug.Log("[SpawnMysteryThought] Activador persistente agregado junto al spawn.");
    }
}

[InitializeOnLoad]
static class SpawnMysteryThoughtSetupOnCompile
{
    static SpawnMysteryThoughtSetupOnCompile()
    {
        EditorApplication.delayCall += SpawnMysteryThoughtSetup.AddToScene;
    }
}
#endif

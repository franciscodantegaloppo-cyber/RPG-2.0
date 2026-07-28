using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FixNamedNpcGrounding
{
    private const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";

    static readonly string[] TargetNames =
    {
        "NPCTonio",
        "NPCHerrero",
        "NPCMerchant"
    };

    [MenuItem("Tools/RPG/Fix Tonio Herrero Merchant Grounding")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int fixedCount = 0;

        foreach (string npcName in TargetNames)
        {
            var npc = GameObject.Find(npcName);
            if (npc == null)
            {
                Debug.LogWarning($"NPC not found for grounding fix: {npcName}");
                continue;
            }

            EnsureLayer(npc, "Interactable", 10);
            ConfigureGroundingComponents(npc);
            SnapRootToGround(npc);
            ConfigureController(npc);
            ConfigureWander(npc);
            AlignVisual(npc);
            fixedCount++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Fixed terrain grounding for named NPCs. Count: {fixedCount}.");
    }

    private static void EnsureLayer(GameObject npc, string layerName, int fallback)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
            layer = fallback;
        npc.layer = layer;
    }

    private static void ConfigureGroundingComponents(GameObject npc)
    {
        var snap = npc.GetComponent<GroundSnapOnStart>() ?? npc.AddComponent<GroundSnapOnStart>();
        snap.UseVisualFooting(0f);

        var aligner = npc.GetComponent<NPCVisualGroundAligner>() ?? npc.AddComponent<NPCVisualGroundAligner>();
        aligner.Configure(0f);
    }

    private static void SnapRootToGround(GameObject npc)
    {
        var snap = npc.GetComponent<GroundSnapOnStart>();
        if (snap != null)
        {
            snap.SnapNow();
        }

        float groundY = GroundUtility.GetGroundY(npc.transform.position, npc.transform, 80f, 140f);
        if (float.IsNegativeInfinity(groundY))
            return;

        if (snap != null && snap.TryGetVisualBottomY(out float bottomY))
        {
            var position = npc.transform.position;
            position.y += groundY - bottomY;
            npc.transform.position = position;
        }
        else
        {
            var position = npc.transform.position;
            position.y = groundY;
            npc.transform.position = position;
        }
    }

    private static void ConfigureController(GameObject npc)
    {
        var controller = npc.GetComponent<CharacterController>();
        if (controller == null)
            return;

        controller.enabled = false;
        controller.skinWidth = Mathf.Max(controller.skinWidth, 0.06f);
        controller.stepOffset = Mathf.Max(controller.stepOffset, 0.35f);
        controller.slopeLimit = Mathf.Max(controller.slopeLimit, 50f);
        float groundY = GroundUtility.GetGroundY(npc.transform.position, npc.transform, 80f, 140f);
        if (!float.IsNegativeInfinity(groundY))
        {
            Vector3 center = controller.center;
            center.y = groundY - npc.transform.position.y + controller.height * 0.5f;
            controller.center = center;
        }
        controller.enabled = true;
    }

    private static void ConfigureWander(GameObject npc)
    {
        var wander = npc.GetComponent<NPCWander>();
        if (wander == null)
            return;

        SetPrivate(wander, "terrainStickOffset", 0f);
        EditorUtility.SetDirty(wander);
    }

    private static void AlignVisual(GameObject npc)
    {
        var aligner = npc.GetComponent<NPCVisualGroundAligner>();
        aligner?.AlignNow();
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }
}

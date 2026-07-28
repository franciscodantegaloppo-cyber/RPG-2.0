using UnityEngine;

// Runtime one-shot placed where the three passage rocks collapsed. A distance check is used
// in addition to the trigger callback because the player is driven by CharacterController and
// can otherwise miss trigger messages during a low-framerate physics step.
public sealed class FifthMissionBridgeThoughtTrigger : MonoBehaviour
{
    const float TriggerRadius = 5.5f;
    Transform player;
    bool shown;

    public static void Create(Vector3 worldPosition)
    {
        GameObject go = new GameObject("FifthMission_BridgeThoughtTrigger");
        go.transform.position = worldPosition;
        SphereCollider trigger = go.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = TriggerRadius;
        go.AddComponent<FifthMissionBridgeThoughtTrigger>();
    }

    void Update()
    {
        if (shown) return;
        if (player == null)
        {
            GameObject found = null;
            try { found = GameObject.FindWithTag("Player"); }
            catch (UnityException) { }
            if (found != null) player = found.transform;
        }

        if (player != null &&
            (player.position - transform.position).sqrMagnitude <= TriggerRadius * TriggerRadius)
            ShowThought(player);
    }

    void OnTriggerEnter(Collider other)
    {
        if (shown) return;
        Transform target = other.GetComponentInParent<PlayerStats>()?.transform;
        if (target != null) ShowThought(target);
    }

    void ShowThought(Transform target)
    {
        shown = true;
        QuestOverheadThought.Show(target,
            "\u00bfPero este puente lo hicieron para gigantes? \u00bfQu\u00e9 es esto?", 5.5f);
        Destroy(gameObject);
    }
}

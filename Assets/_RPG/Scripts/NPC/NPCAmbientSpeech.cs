using UnityEngine;

public class NPCAmbientSpeech : MonoBehaviour
{
    [SerializeField] string[] messages;
    [SerializeField] float triggerDistance = 4.5f;
    [SerializeField] float repeatSeconds = 9f;

    Transform player;
    float nextMessageTime;
    int messageIndex;
    bool wasNear;

    public void Configure(params string[] configuredMessages)
    {
        messages = configuredMessages;
    }

    void Update()
    {
        if (messages == null || messages.Length == 0) return;
        if (player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null) player = found.transform;
        }
        if (player == null) return;

        bool near = (player.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance;
        if (near && (!wasNear || Time.time >= nextMessageTime))
        {
            QuestOverheadThought.Show(transform, messages[messageIndex % messages.Length], 5.5f);
            messageIndex = (messageIndex + 1) % messages.Length;
            nextMessageTime = Time.time + repeatSeconds;
        }
        wasNear = near;
    }
}

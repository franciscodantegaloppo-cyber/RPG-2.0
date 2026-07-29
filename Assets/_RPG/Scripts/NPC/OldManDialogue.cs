using UnityEngine;

public class OldManDialogue : MonoBehaviour, IInteractable
{
    const string NpcName = "El viejo";
    static readonly string[] Pages =
    {
        "Dicen que no mata de inmediato. Primero transforma.",
        "Las piedras aparecen donde nunca estuvieron, y los árboles despiertan con formas imposibles.",
        "Las raíces atraviesan las casas y el agua de los arroyos refleja paisajes que no existen.",
        "Todo aquello que debería permanecer inmóvil comienza a deformarse...",
        "Como si la naturaleza hubiera olvidado cuál era su verdadera forma."
    };

    NPCWander wander;
    System.Action missionSevenCompletion;

    void Awake()
    {
        wander = GetComponent<NPCWander>();
        if (wander == null) wander = gameObject.AddComponent<NPCWander>();
        wander.Configure(speed: .38f, radius: 1.6f, step: .65f, minWait: 4f, maxWait: 8f);
        wander.UseTransformMovement(true);
        if (GetComponent<OldManFireballCombat>() == null) gameObject.AddComponent<OldManFireballCombat>();
        EnsureInteractionTrigger();
    }

    public string GetInteractionText() => "[E] Hablar con " + NpcName;
    public bool CanInteract(PlayerInteraction player) => true;

    public void Interact(PlayerInteraction player)
    {
        BeginStory(player != null ? player.transform : null);
    }

    public void BeginMissionSevenStory(Transform player,
        System.Action onCompleted = null)
    {
        missionSevenCompletion = onCompleted;
        BeginStory(player);
    }

    void BeginStory(Transform player)
    {
        wander?.PauseForInteraction();
        Vector3 direction = player != null
            ? player.position - transform.position
            : transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(direction);

        MerchantDialoguePanel panel = MerchantDialoguePanel.EnsureRuntime();
        if (panel == null)
        {
            wander?.ResumeWander();
            return;
        }
        ShowPage(panel, 0);
    }

    void ShowPage(MerchantDialoguePanel panel, int index)
    {
        bool last = index >= Pages.Length - 1;
        panel.Show(NpcName, Pages[index], last ? "Cerrar" : "Siguiente",
            last ? (System.Action)EndConversation : () => ShowPage(panel, index + 1));
    }

    void EndConversation()
    {
        MerchantDialoguePanel.EnsureRuntime()?.Hide();
        OldManFireballCombat combat = GetComponent<OldManFireballCombat>();
        if (combat == null || !combat.HasTarget) wander?.ResumeWander();
        System.Action completed = missionSevenCompletion;
        missionSevenCompletion = null;
        completed?.Invoke();
    }

    void EnsureInteractionTrigger()
    {
        CapsuleCollider trigger = GetComponent<CapsuleCollider>();
        if (trigger == null) trigger = gameObject.AddComponent<CapsuleCollider>();
        trigger.isTrigger = true;
        trigger.radius = .78f;
        trigger.height = 2f;
        trigger.center = new Vector3(0f, 1f, 0f);
    }
}

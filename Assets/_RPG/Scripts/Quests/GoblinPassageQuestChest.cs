using UnityEngine;

public class GoblinPassageQuestChest : MonoBehaviour
{
    [SerializeField] ItemData runeItem;
    [SerializeField] ItemData diamondItem;
    [SerializeField] float discoveryDistance = 7f;

    ChestContainer chest;
    Transform player;

    public bool IsAvailable
    {
        get
        {
            QuestManager quests = QuestManager.Instance;
            return quests != null && quests.MerchantIntroductionState == PrimaryQuestState.LootGoblinPassageChest;
        }
    }

    void Awake()
    {
        chest = GetComponent<ChestContainer>();
        QuestNpcAttentionIcon icon = GetComponent<QuestNpcAttentionIcon>();
        if (icon == null) icon = gameObject.AddComponent<QuestNpcAttentionIcon>();
        icon.Configure(QuestNpcAttentionRole.GoblinPassageChest);
    }

    void OnEnable()
    {
        if (chest == null) chest = GetComponent<ChestContainer>();
        if (chest != null) chest.OnChanged += CheckLootTaken;
    }

    void OnDisable()
    {
        if (chest != null) chest.OnChanged -= CheckLootTaken;
    }

    void Update()
    {
        QuestManager quests = QuestManager.Instance;
        if (quests == null || quests.MerchantIntroductionState != PrimaryQuestState.InvestigateGoblinPassage)
            return;

        if (player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null) player = found.transform;
        }

        if (player != null && (player.position - transform.position).sqrMagnitude <= discoveryDistance * discoveryDistance)
            quests.ReachGoblinPassageEnd();
    }

    void CheckLootTaken()
    {
        QuestManager quests = QuestManager.Instance;
        if (quests == null || quests.MerchantIntroductionState != PrimaryQuestState.LootGoblinPassageChest || chest == null)
            return;

        if (Count(runeItem) == 0 && Count(diamondItem) == 0)
            quests.NotifyGoblinChestLootTaken();
    }

    int Count(ItemData item)
    {
        if (item == null) return 0;
        int total = 0;
        foreach (InventorySlot slot in chest.Slots)
            if (slot != null && slot.item == item) total += slot.quantity;
        return total;
    }
}

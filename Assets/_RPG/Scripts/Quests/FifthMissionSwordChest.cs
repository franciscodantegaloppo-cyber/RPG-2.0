using UnityEngine;

[RequireComponent(typeof(ChestContainer))]
public class FifthMissionSwordChest : MonoBehaviour
{
    [SerializeField] ItemData darkSaber;
    [SerializeField] int excellenceLevel = 7;

    ChestContainer chest;
    bool populated;

    public bool IsAvailable
    {
        get
        {
            QuestManager quests = QuestManager.Instance;
            return quests != null &&
                   (quests.MerchantIntroductionState == PrimaryQuestState.RetrieveDarkSaberChest ||
                    quests.MerchantIntroductionState == PrimaryQuestState.RetrieveForgottenSwordChest);
        }
    }

    void Awake()
    {
        chest = GetComponent<ChestContainer>();
        QuestNpcAttentionIcon icon = GetComponent<QuestNpcAttentionIcon>();
        if (icon == null) icon = gameObject.AddComponent<QuestNpcAttentionIcon>();
        icon.Configure(QuestNpcAttentionRole.FifthMissionChest);
        Populate();
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

    void Populate()
    {
        if (populated || chest == null || darkSaber == null) return;
        populated = true;
        foreach (InventorySlot slot in chest.Slots)
            if (slot?.instance?.template == darkSaber) return;

        ItemInstance reward = new ItemInstance(darkSaber)
        {
            excellenceLevel = Mathf.Max(7, excellenceLevel),
            extraAttackPercent = 70f
        };
        chest.TryAddInstance(reward);
    }

    void CheckLootTaken()
    {
        if (!IsAvailable || chest == null) return;
        foreach (InventorySlot slot in chest.Slots)
            if (slot?.instance?.template == darkSaber) return;
        QuestManager.Instance?.NotifyDarkSaberTaken();
    }
}

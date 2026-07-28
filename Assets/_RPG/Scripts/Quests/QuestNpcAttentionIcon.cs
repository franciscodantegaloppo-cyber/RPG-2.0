using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum QuestNpcAttentionRole { Merchant, Tonio, Nahue, CraftingTable, KingGoblinStatue, GoblinPassageChest, DungeonKeyPickup, FifthMissionChest, RedDiamond }

// World-space quest marker used only when the current objective requires talking to this NPC.
public class QuestNpcAttentionIcon : MonoBehaviour
{
    [SerializeField] QuestNpcAttentionRole role;
    [SerializeField] float heightPadding = .48f;

    GameObject marker;
    CanvasGroup group;
    TextMeshProUGUI glowText;
    Vector3 baseScale;
    float nextRefresh;

    public void Configure(QuestNpcAttentionRole newRole)
    {
        role = newRole;
        RefreshVisibility();
    }

    void Awake() => BuildMarker();

    void Update()
    {
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + .15f;
            RefreshVisibility();
        }

        if (marker != null && marker.activeSelf)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.2f) * .055f;
            marker.transform.localScale = baseScale * pulse;
            if (group != null) group.alpha = .9f + Mathf.Sin(Time.unscaledTime * 4.2f) * .1f;
            if (glowText != null)
            {
                Color glow = glowText.color;
                glow.a = .34f + (Mathf.Sin(Time.unscaledTime * 5.1f) + 1f) * .18f;
                glowText.color = glow;
            }
        }
    }

    void LateUpdate()
    {
        if (marker == null || !marker.activeSelf) return;
        Camera camera = Camera.main;
        if (camera != null) marker.transform.rotation = camera.transform.rotation;
    }

    void RefreshVisibility()
    {
        if (marker == null) return;
        QuestManager quests = QuestManager.Instance;
        bool dialogueOpen = MerchantDialoguePanel.Instance != null && MerchantDialoguePanel.Instance.IsOpen;
        bool visible = quests != null && !dialogueOpen && MustTalkToThisNpc(quests);
        if (marker.activeSelf != visible) marker.SetActive(visible);
    }

    bool MustTalkToThisNpc(QuestManager quests)
    {
        if (role == QuestNpcAttentionRole.Merchant)
            return quests.MerchantIntroductionState == PrimaryQuestState.GetNoviceEquipment;

        if (role == QuestNpcAttentionRole.Nahue)
            return quests.MerchantIntroductionState == PrimaryQuestState.TalkToNahueSecondQuest ||
                   quests.MerchantIntroductionState == PrimaryQuestState.ReturnToNahueWithDarkSaber ||
                   quests.MerchantIntroductionState == PrimaryQuestState.SeventhTalkToNahue;

        if (role == QuestNpcAttentionRole.CraftingTable)
            return quests.MerchantIntroductionState == PrimaryQuestState.UpgradeWeaponAtCraftingTable;

        if (role == QuestNpcAttentionRole.KingGoblinStatue)
            return quests.MerchantIntroductionState == PrimaryQuestState.GoToKingGoblinStatue ||
                   quests.MerchantIntroductionState == PrimaryQuestState.ReturnToStatueForReliefs;

        if (role == QuestNpcAttentionRole.GoblinPassageChest)
            return quests.MerchantIntroductionState == PrimaryQuestState.LootGoblinPassageChest;

        if (role == QuestNpcAttentionRole.DungeonKeyPickup)
            return quests.MerchantIntroductionState == PrimaryQuestState.HuntEliteSkeletonAtNight;

        if (role == QuestNpcAttentionRole.FifthMissionChest)
            return quests.MerchantIntroductionState == PrimaryQuestState.RetrieveForgottenSwordChest ||
                   quests.MerchantIntroductionState == PrimaryQuestState.RetrieveDarkSaberChest;

        if (role == QuestNpcAttentionRole.RedDiamond)
            return quests.MerchantIntroductionState == PrimaryQuestState.SixthFindRedDiamond;

        return quests.MerchantIntroductionState == PrimaryQuestState.ReturnToTonioWithEquipment ||
               quests.MerchantIntroductionState == PrimaryQuestState.ReturnToTonioAfterPassage ||
               quests.MerchantIntroductionState == PrimaryQuestState.TalkToTonioAfterNahue ||
               quests.MerchantIntroductionState == PrimaryQuestState.InvestigatePlagueTalkToTonio ||
               quests.MerchantIntroductionState == PrimaryQuestState.ReturnToTonioAfterTrial ||
               quests.MerchantIntroductionState == PrimaryQuestState.TalkToTonioFourthMission ||
               quests.MerchantIntroductionState == PrimaryQuestState.ReturnToTonioWithDungeonKey ||
               quests.MerchantIntroductionState == PrimaryQuestState.TalkToTonioFifthMission ||
               quests.MerchantIntroductionState == PrimaryQuestState.SixthTalkToPetrifiedTonio ||
               quests.SkeletonState == QuestTaskState.ReadyToTurnIn ||
               quests.DeerState == QuestTaskState.ReadyToTurnIn;
    }

    void BuildMarker()
    {
        marker = new GameObject("QuestAttentionIcon", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        marker.transform.SetParent(transform, false);
        marker.transform.position = FindTopPosition();
        marker.transform.rotation = Quaternion.identity;
        baseScale = Vector3.one * .0085f;
        marker.transform.localScale = baseScale;

        RectTransform rect = marker.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(82f, 108f);
        Canvas canvas = marker.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 700;
        group = marker.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject glowObject = new GameObject("ExclamationGlow", typeof(RectTransform), typeof(TextMeshProUGUI));
        glowObject.transform.SetParent(marker.transform, false);
        glowText = glowObject.GetComponent<TextMeshProUGUI>();
        glowText.text = "!";
        glowText.fontSize = 96f;
        glowText.fontStyle = FontStyles.Bold;
        glowText.color = new Color(1f, .7f, .03f, .48f);
        glowText.alignment = TextAlignmentOptions.Center;
        glowText.outlineColor = new Color(1f, .45f, 0f, .45f);
        glowText.outlineWidth = .38f;
        glowText.raycastTarget = false;
        RectTransform glowRect = glowText.rectTransform;
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-10f, -10f);
        glowRect.offsetMax = new Vector2(10f, 10f);

        GameObject textObject = new GameObject("Exclamation", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(marker.transform, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = "!";
        text.fontSize = 82f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, .78f, .05f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.outlineColor = new Color(.08f, .035f, 0f, 1f);
        text.outlineWidth = .22f;
        text.raycastTarget = false;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        marker.SetActive(false);
    }

    Vector3 FindTopPosition()
    {
        float top = transform.position.y + 2.1f;
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null) top = controller.bounds.max.y;
        else
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
                if (renderer.bounds.max.y > top) top = renderer.bounds.max.y;
        }
        return new Vector3(transform.position.x, top + heightPadding, transform.position.z);
    }
}

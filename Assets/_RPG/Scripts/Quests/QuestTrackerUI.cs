using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Compact tracker; the full categorized journal is available with J.
public class QuestTrackerUI : MonoBehaviour
{
    GameObject panel;
    TextMeshProUGUI label;
    string lastText;
    bool lastChestOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "NewGame") return;
        if (FindAnyObjectByType<QuestTrackerUI>(FindObjectsInactive.Include) != null) return;
        new GameObject("QuestTrackerUI").AddComponent<QuestTrackerUI>();
    }

    void Awake() { Build(); Refresh(); }
    void Update()
    {
        bool chestOpen = ChestUI.IsAnyOpen;
        if (chestOpen != lastChestOpen)
        {
            lastChestOpen = chestOpen;
            lastText = null;
        }
        Refresh();
    }

    void Build()
    {
        Canvas canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null)
        {
            GameObject go = new GameObject("QuestCanvas");
            canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
        }
        panel = new GameObject("QuestTrackerPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-22f, -178f); rect.sizeDelta = new Vector2(340f, 236f);
        Image image = panel.GetComponent<Image>();
        Sprite frame = Resources.Load<Sprite>("UI/SharpUI/Panel");
        if (frame != null) { image.sprite = frame; image.type = Image.Type.Sliced; image.color = Color.white; }
        else image.color = new Color(.05f, .04f, .03f, .96f);

        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(panel.transform, false);
        TextMeshProUGUI title = titleObject.GetComponent<TextMeshProUGUI>();
        title.text = "MISIONES"; title.fontSize = 17f; title.fontStyle = FontStyles.Bold;
        title.color = new Color(1f, .73f, .30f); title.alignment = TextAlignmentOptions.Center; title.raycastTarget = false;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f); titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(.5f, 1f); titleRect.sizeDelta = new Vector2(0f, 34f);
        titleRect.anchoredPosition = new Vector2(0f, -10f);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        label = textObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 14; label.enableAutoSizing = false;
        label.color = new Color(.88f, .84f, .75f); label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal; label.lineSpacing = 14f;
        RectTransform tr = label.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(22f, 18f); tr.offsetMax = new Vector2(-22f, -50f);
    }

    void Refresh()
    {
        QuestManager q = QuestManager.Instance;
        if (q == null || panel == null) { if (panel != null) panel.SetActive(false); return; }
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        switch (q.MerchantIntroductionState)
        {
            case PrimaryQuestState.GetNoviceEquipment:
                text.Append("Principal: habla con el Mercader");
                break;
            case PrimaryQuestState.ReturnToTonioWithEquipment:
                text.Append("Principal: vuelve con Tonio");
                break;
            case PrimaryQuestState.InvestigateGoblinPassage:
                text.Append("Principal: investiga el pasaje goblin");
                break;
            case PrimaryQuestState.LootGoblinPassageChest:
                text.Append("Principal: recoge 20 runas y 5 diamantes");
                break;
            case PrimaryQuestState.ReturnToTonioAfterPassage:
                text.Append("Principal: lleva las runas y diamantes a Tonio");
                break;
            case PrimaryQuestState.TalkToNahueSecondQuest: text.Append("Principal: habla con Nahue"); break;
            case PrimaryQuestState.TalkToTonioAfterNahue: text.Append("Principal: vuelve con Tonio"); break;
            case PrimaryQuestState.UpgradeWeaponAtCraftingTable: text.Append("Principal: mejora un arma en la mesa de crafteo"); break;
            case PrimaryQuestState.PostCraftReflection: text.Append("Principal: siente el nuevo poder..."); break;
            case PrimaryQuestState.InvestigatePlagueTalkToTonio: text.Append("Principal: investiga el origen de la peste - habla con Tonio"); break;
            case PrimaryQuestState.GoToKingGoblinStatue: text.Append("Principal: activa la estatua del Rey Goblin"); break;
            case PrimaryQuestState.StatueTrialActive: text.Append("Prueba de la peste: derrota a los 10 goblins"); break;
            case PrimaryQuestState.ReturnToTonioAfterTrial: text.Append("Principal: vuelve con Tonio por tu recompensa"); break;
            case PrimaryQuestState.ThirdMissionReflectionPending: text.Append("Principal: reflexiona sobre la estatua..."); break;
            case PrimaryQuestState.ReturnToStatueForReliefs: text.Append("Principal: vuelve a revisar la estatua"); break;
            case PrimaryQuestState.TalkToTonioFourthMission: text.Append("Principal: habla con Tonio sobre los relieves"); break;
            case PrimaryQuestState.HuntEliteSkeletonAtNight: text.Append("Principal: mata un esqueleto elite de noche"); break;
            case PrimaryQuestState.ReturnToTonioWithDungeonKey: text.Append("Principal: lleva la llave a Tonio"); break;
            case PrimaryQuestState.TalkToTonioFifthMission: text.Append("Principal: habla con Tonio"); break;
            case PrimaryQuestState.RetrieveForgottenSwordChest:
            case PrimaryQuestState.RetrieveDarkSaberChest:
                text.Append("Principal: recoge el Sable Oscuro exe +7 del cofre");
                break;
            case PrimaryQuestState.DefeatInsectoidCrabBoss:
                text.Append("Principal: derrota al Demonio Cangrejo");
                break;
            case PrimaryQuestState.ReturnToNahueWithDarkSaber:
                text.Append("Principal: lleva el Sable Oscuro a Nahue");
                break;
            case PrimaryQuestState.FifthQuestCompleted:
                text.Append("Principal: Sable Oscuro encantado con energ\u00eda oscura");
                break;
            case PrimaryQuestState.SixthMissionWarningPending:
                text.Append("Principal: escucha el llamado de la oscuridad");
                break;
            case PrimaryQuestState.SixthTalkToPetrifiedTonio:
                text.Append("Principal: habla con Tonio");
                break;
            case PrimaryQuestState.SixthFindRedDiamond:
                text.Append("Principal: sigue las manchas y encuentra el Diamante Rojo");
                break;
            case PrimaryQuestState.SixthQuestCompleted:
                text.Append("Principal: el Diamante Rojo devolvi\u00f3 la vida a la aldea");
                break;
            case PrimaryQuestState.SeventhUseRedDiamond:
                text.Append("Principal: usa el Diamante Rojo desde el inventario");
                break;
            case PrimaryQuestState.SeventhFindTonio:
                text.Append("Principal: busca a Tonio");
                break;
            case PrimaryQuestState.SeventhFollowBloodTrail:
                text.Append("Principal: sigue el rastro de sangre");
                break;
            case PrimaryQuestState.SeventhTalkToNahue:
                text.Append("Principal: habla con Nahue");
                break;
            case PrimaryQuestState.SeventhWindAftermath:
                text.Append("Principal: sobrevive a la r\u00e1faga");
                break;
            case PrimaryQuestState.SeventhQuestCompleted:
                text.Append("Principal: despierta junto al Viejo");
                break;
        }
        if (q.SkeletonState == QuestTaskState.Active || q.SkeletonState == QuestTaskState.ReadyToTurnIn)
        {
            if (text.Length > 0) text.Append('\n');
            text.Append("Esqueletos: ").Append(q.SkeletonKills).Append('/').Append(QuestManager.SkeletonTarget);
            if (q.SkeletonState == QuestTaskState.ReadyToTurnIn) text.Append(" - vuelve con Tonio");
        }
        if (q.DeerState == QuestTaskState.Active || q.DeerState == QuestTaskState.ReadyToTurnIn)
        {
            if (text.Length > 0) text.Append('\n');
            text.Append("Ciervos: ").Append(q.DeerKills).Append('/').Append(QuestManager.DeerTarget);
            if (q.DeerState == QuestTaskState.ReadyToTurnIn) text.Append(" - vuelve con Tonio");
        }
        string current = text.ToString();
        if (current == lastText) return;
        lastText = current;
        label.text = current;
        panel.SetActive(current.Length > 0 && !lastChestOpen);
    }
}

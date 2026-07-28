using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class QuestJournalUI : MonoBehaviour
{
    GameObject panel;
    GameObject primaryCard;
    GameObject skeletonCard;
    GameObject deerCard;
    TextMeshProUGUI sectionTitle;
    TextMeshProUGUI primaryContent;
    TextMeshProUGUI skeletonContent;
    TextMeshProUGUI deerContent;
    Button primaryButton;
    Button secondaryButton;
    bool showingPrimary = true;
    bool isOpen;
    QuestManager subscribedManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "NewGame") return;
        if (FindAnyObjectByType<QuestJournalUI>(FindObjectsInactive.Include) != null) return;
        new GameObject("QuestJournalUI").AddComponent<QuestJournalUI>();
    }

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Build();
        panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (subscribedManager != null) subscribedManager.OnQuestChanged -= Refresh;
    }

    void Update()
    {
        EnsureSubscription();
    }

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    void EnsureSubscription()
    {
        if (subscribedManager == QuestManager.Instance) return;
        if (subscribedManager != null) subscribedManager.OnQuestChanged -= Refresh;
        subscribedManager = QuestManager.Instance;
        if (subscribedManager != null) subscribedManager.OnQuestChanged += Refresh;
        Refresh();
    }

    void Open()
    {
        isOpen = true;
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        Refresh();
        GameManager.Instance?.SetState(GameState.InMenu);
    }

    void Close()
    {
        isOpen = false;
        panel.SetActive(false);
        GameManager.Instance?.SetState(GameState.Exploration);
    }

    void ShowPrimary() { showingPrimary = true; Refresh(); }
    void ShowSecondary() { showingPrimary = false; Refresh(); }

    void Refresh()
    {
        if (primaryContent == null || sectionTitle == null) return;
        ApplyPersistentLayout();
        QuestManager quests = QuestManager.Instance;
        sectionTitle.text = showingPrimary ? "MISIONES PRIMARIAS" : "MISIONES SECUNDARIAS";
        SetSelected(primaryButton, showingPrimary);
        SetSelected(secondaryButton, !showingPrimary);
        primaryCard.SetActive(showingPrimary);
        skeletonCard.SetActive(!showingPrimary);
        deerCard.SetActive(!showingPrimary);

        if (quests == null)
        {
            primaryContent.text = "Cargando misiones...";
            return;
        }

        primaryContent.text = BuildPrimaryText(quests);
        skeletonContent.text = BuildOptional("LIMPIEZA DEL CEMENTERIO", quests.SkeletonState,
            quests.SkeletonKills, QuestManager.SkeletonTarget, QuestManager.SkeletonReward,
            "Habla con Tonio y elige ¿Quieres trabajo extra? para aceptar o entregar.");
        deerContent.text = BuildOptional("PROTEGER LAS COSECHAS", quests.DeerState,
            quests.DeerKills, QuestManager.DeerTarget, QuestManager.DeerReward,
            "Caza ciervos. Es una misión completamente opcional.");
    }

    static string BuildPrimaryText(QuestManager quests)
    {
        PrimaryQuestState state = quests.MerchantIntroductionState;
        string status = state == PrimaryQuestState.SecondQuestCompleted ||
                        state == PrimaryQuestState.FourthQuestCompleted ||
                        state == PrimaryQuestState.FifthQuestCompleted ||
                        state == PrimaryQuestState.SixthQuestCompleted ||
                        state == PrimaryQuestState.SeventhQuestCompleted
            ? "<color=#79e58a>[COMPLETADA]</color>"
            : "<color=#ffd66b>[ACTIVA]</color>";
        string title = state == PrimaryQuestState.GetNoviceEquipment || state == PrimaryQuestState.ReturnToTonioWithEquipment
            ? "EL PRIMER EQUIPAMIENTO"
            : state <= PrimaryQuestState.ReturnToTonioAfterPassage
                ? "INVESTIGA EL PASAJE GOBLIN"
                : state <= PrimaryQuestState.ReturnToTonioAfterTrial
                    ? "EL ORIGEN DE LA PESTE"
                    : state <= PrimaryQuestState.ReturnToStatueForReliefs
                        ? "LOS RELIEVES DE LA ESTATUA"
                        : state <= PrimaryQuestState.ReturnToTonioWithDungeonKey
                            ? "LA LLAVE DEL CALABOZO"
                            : state <= PrimaryQuestState.ReturnToNahueWithDarkSaber ||
                              state == PrimaryQuestState.FifthQuestCompleted
                                ? "LA ESPADA OLVIDADA"
                                : state <= PrimaryQuestState.SixthQuestCompleted
                                    ? "EL CORAZ\u00d3N DE LA OSCURIDAD"
                                    : "EL ALMA ROBADA";
        string objective;
        switch (state)
        {
            case PrimaryQuestState.GetNoviceEquipment:
                objective = "Habla con el <color=#ffd66b>Mercader</color> y acepta su equipo de novato.";
                break;
            case PrimaryQuestState.ReturnToTonioWithEquipment:
                objective = "Regresa con <color=#ffd66b>Tonio</color> para mostrarle el equipo básico.";
                break;
            case PrimaryQuestState.InvestigateGoblinPassage:
                objective = "<b>Investiga el pasaje goblin.</b> Llega hasta el final y busca la mochila de carga.";
                break;
            case PrimaryQuestState.LootGoblinPassageChest:
                objective = "Retira de la <color=#ffd66b>WagonBackpackChest</color> las 20 runas y los 5 diamantes.";
                break;
            case PrimaryQuestState.ReturnToTonioAfterPassage:
                objective = "Ya recuperaste las runas y diamantes. Vuelve a hablar con <color=#ffd66b>Tonio</color>.";
                break;
            case PrimaryQuestState.TalkToNahueSecondQuest:
                objective = "Habla con <color=#ffd66b>Nahue</color> cerca del pueblo.";
                break;
            case PrimaryQuestState.TalkToTonioAfterNahue:
                objective = "Regresa con <color=#ffd66b>Tonio</color> y cuéntale lo que dijo Nahue.";
                break;
            case PrimaryQuestState.UpgradeWeaponAtCraftingTable:
                objective = "Ve a la <color=#ffd66b>mesa de crafteo</color> y mejora un arma al menos una vez.";
                break;
            case PrimaryQuestState.PostCraftReflection:
                objective = "Siente el nuevo poder del arma y espera un momento...";
                break;
            case PrimaryQuestState.InvestigatePlagueTalkToTonio:
                objective = "<b>Investiga el origen de la peste.</b> Vuelve a hablar con <color=#ffd66b>Tonio</color>.";
                break;
            case PrimaryQuestState.GoToKingGoblinStatue:
                objective = "Dirígete a la <color=#ffd66b>estatua del Rey Goblin</color> fuera del pueblo y presiona E.";
                break;
            case PrimaryQuestState.StatueTrialActive:
                objective = "<b>Prueba de la peste:</b> derrota a los 10 goblins antes de que termine el tiempo.";
                break;
            case PrimaryQuestState.ReturnToTonioAfterTrial:
                objective = "Superaste la prueba. Regresa con <color=#ffd66b>Tonio</color>.";
                break;
            case PrimaryQuestState.ThirdMissionReflectionPending:
                objective = "Espera y reflexiona sobre lo ocurrido en la prueba de la peste.";
                break;
            case PrimaryQuestState.ReturnToStatueForReliefs:
                objective = "Vuelve a examinar la <color=#ffd66b>estatua del Rey Goblin</color>.";
                break;
            case PrimaryQuestState.TalkToTonioFourthMission:
                objective = "Habla con <color=#ffd66b>Tonio</color> sobre los tres relieves de la corona.";
                break;
            case PrimaryQuestState.HuntEliteSkeletonAtNight:
                objective = "Ve al cementerio de noche, elimina un <color=#ffd66b>esqueleto elite</color> y recoge la llave del calabozo.";
                break;
            case PrimaryQuestState.ReturnToTonioWithDungeonKey:
                objective = "Conseguiste la llave. Regresa con <color=#ffd66b>Tonio</color> por tu recompensa.";
                break;
            case PrimaryQuestState.FourthQuestCompleted:
                objective = "Conseguiste la llave del calabozo y la espada de Tonio.";
                break;
            case PrimaryQuestState.TalkToTonioFifthMission:
                objective = "Habla con <color=#ffd66b>Tonio</color> para conocer la siguiente tarea.";
                break;
            case PrimaryQuestState.RetrieveForgottenSwordChest:
            case PrimaryQuestState.RetrieveDarkSaberChest:
                objective = "El Demonio Cangrejo fue eliminado. Abre el cofre marcado y recupera el <color=#a45cff>Sable Oscuro exe +7</color>.";
                break;
            case PrimaryQuestState.DefeatInsectoidCrabBoss:
                objective = "Las rocas del pasaje cayeron. Avanza y derrota al <color=#a45cff>Demonio Cangrejo</color> para liberar su cofre.";
                break;
            case PrimaryQuestState.ReturnToNahueWithDarkSaber:
                objective = "Lleva el <color=#a45cff>Sable Oscuro exe +7</color> a <color=#ffd66b>Nahue</color> para que lo encante con energ\u00eda oscura.";
                break;
            case PrimaryQuestState.FifthQuestCompleted:
                objective = "Nahue encant\u00f3 el <color=#a45cff>Sable Oscuro exe +7</color>. Sus golpes incendian con da\u00f1o verdadero.";
                break;
            case PrimaryQuestState.SixthMissionWarningPending:
                objective = "La oscuridad responde al Sable Oscuro. Equ\u00edpalo y presta atenci\u00f3n a su llamado.";
                break;
            case PrimaryQuestState.SixthTalkToPetrifiedTonio:
                objective = "Algo terrible sucedi\u00f3 en la aldea. Habla con <color=#ffd66b>Tonio</color>.";
                break;
            case PrimaryQuestState.SixthFindRedDiamond:
                objective = "Todos fueron convertidos en piedra. Equipa el <color=#a45cff>Sable Oscuro</color>, sigue las manchas y encuentra el <color=#ff5252>Diamante Rojo</color>.";
                break;
            case PrimaryQuestState.SixthQuestCompleted:
                objective = "Recuperaste el <color=#ff5252>Diamante Rojo</color> y liberaste la vida atrapada en la aldea.";
                break;
            case PrimaryQuestState.SeventhUseRedDiamond:
                objective = "Haz clic derecho sobre el <color=#ff5252>Diamante Rojo</color> y libera el hechizo Fireball.";
                break;
            case PrimaryQuestState.SeventhFindTonio:
                objective = "Busca a <color=#ffd66b>Tonio</color>. Algo ocurri\u00f3 en el lugar donde estaba.";
                break;
            case PrimaryQuestState.SeventhFollowBloodTrail:
                objective = "Sigue el <color=#d63030>rastro de sangre</color> desde Tonio hasta Nahue.";
                break;
            case PrimaryQuestState.SeventhTalkToNahue:
                objective = "Habla con <color=#ffd66b>Nahue</color> sobre la desaparici\u00f3n de Tonio.";
                break;
            case PrimaryQuestState.SeventhWindAftermath:
                objective = "Busca refugio y sobrevive a la <color=#8fdcff>r\u00e1faga extrema</color>.";
                break;
            case PrimaryQuestState.SeventhQuestCompleted:
                objective = "Despertaste junto al Viejo. Nahue muri\u00f3 y el alma de Tonio sigue desaparecida.";
                break;
            case PrimaryQuestState.SecondQuestCompleted:
                objective = "Completaste la segunda misi\u00f3n principal y sobreviviste a la prueba de la peste.";
                break;
            default:
                objective = "Completaste la primera misión principal y ayudaste a detener la amenaza goblin.";
                break;
        }
        string reward = state <= PrimaryQuestState.ReturnToTonioAfterPassage ? "1000 de experiencia" :
            state <= PrimaryQuestState.ReturnToTonioAfterTrial ? "5000 de experiencia" :
            state <= PrimaryQuestState.ReturnToTonioWithDungeonKey ? "Acceso al calabozo y una espada" :
            state <= PrimaryQuestState.ReturnToNahueWithDarkSaber ||
            state == PrimaryQuestState.FifthQuestCompleted
                ? "Sable Oscuro exe +7 encantado"
                : state <= PrimaryQuestState.SixthQuestCompleted
                    ? "Diamante Rojo"
                    : "Hechizo Fireball";
        return status + "\n<size=28><b>" + title + "</b></size>\n\n" + objective +
               "\n\n<color=#b8a98b>Recompensa:</color> " + reward + ".";
    }

    static string BuildOptional(string title, QuestTaskState state, int progress, int target, int reward, string hint)
    {
        string status;
        switch (state)
        {
            case QuestTaskState.Active: status = "<color=#ffd66b>[ACTIVA]</color>"; break;
            case QuestTaskState.ReadyToTurnIn: status = "<color=#79e58a>[LISTA PARA ENTREGAR]</color>"; break;
            case QuestTaskState.Completed: status = "<color=#79e58a>[COMPLETADA]</color>"; break;
            default: status = "<color=#aaa096>[DISPONIBLE CON TONIO]</color>"; break;
        }
        int shownProgress = state == QuestTaskState.Completed ? target : progress;
        return "<size=16>" + status + "</size>\n" +
               "<size=21><b>" + title + "</b></size>\n\n" +
               "<b>Progreso:</b> <color=#ffffff>" + shownProgress + "/" + target + "</color>" +
               "      <color=#7d6a4d>•</color>      " +
               "<b>Recompensa:</b> <color=#ffd66b>" + reward + " monedas</color>\n\n" +
               "<size=15><color=#c8baa0>" + hint + "</color></size>";
    }

    void Build()
    {
        GameObject canvasObject = new GameObject("QuestJournalCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2300;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        panel = new GameObject("QuestJournalPanel", typeof(RectTransform), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(.15f, .06f);
        panelRect.anchorMax = new Vector2(.85f, .94f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        ApplyFrame(panel.GetComponent<Image>(), "UI/SharpUI/Panel", Color.white);
        Canvas topCanvas = panel.GetComponent<Canvas>();
        topCanvas.overrideSorting = true;
        topCanvas.sortingOrder = 900;

        primaryButton = CreateButton(panel.transform, "PrimaryTab", "PRIMARIAS", new Vector2(.08f, .79f), new Vector2(.48f, .875f));
        secondaryButton = CreateButton(panel.transform, "SecondaryTab", "SECUNDARIAS", new Vector2(.52f, .79f), new Vector2(.92f, .875f));
        primaryButton.onClick.AddListener(ShowPrimary);
        secondaryButton.onClick.AddListener(ShowSecondary);

        GameObject contentFrame = new GameObject("MissionContent", typeof(RectTransform), typeof(Image));
        contentFrame.transform.SetParent(panel.transform, false);
        RectTransform contentRect = contentFrame.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(.07f, .13f);
        contentRect.anchorMax = new Vector2(.93f, .765f);
        contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;
        ApplyFrame(contentFrame.GetComponent<Image>(), "UI/SharpUI/Panel", Color.white);

        sectionTitle = CreateText(contentFrame.transform, "SectionTitle", "", 24,
            new Vector2(.07f, .85f), new Vector2(.93f, .965f), new Color(1f, .77f, .25f),
            TextAlignmentOptions.Center, FontStyles.Bold);
        primaryCard = CreateMissionCard(contentFrame.transform, "PrimaryMissionCard",
            new Vector2(.055f, .06f), new Vector2(.945f, .81f), out primaryContent);
        skeletonCard = CreateMissionCard(contentFrame.transform, "SkeletonMissionCard",
            new Vector2(.055f, .47f), new Vector2(.945f, .81f), out skeletonContent);
        deerCard = CreateMissionCard(contentFrame.transform, "DeerMissionCard",
            new Vector2(.055f, .06f), new Vector2(.945f, .40f), out deerContent);

        Button close = CreateButton(panel.transform, "CloseButton", "CERRAR  [J]", new Vector2(.34f, .025f), new Vector2(.66f, .105f));
        close.onClick.AddListener(Close);
        SharpUIWindowChrome.Attach(panel, "DIARIO DE MISIONES", Close);
        ApplyPersistentLayout();
    }

    // This UI is built at runtime. Reapplying the layout whenever it refreshes
    // keeps the same spacing after opening it, changing tabs or updating quests.
    void ApplyPersistentLayout()
    {
        SetAnchors(sectionTitle != null ? sectionTitle.rectTransform : null,
            new Vector2(.07f, .85f), new Vector2(.93f, .965f));

        SetAnchors(primaryCard != null ? primaryCard.GetComponent<RectTransform>() : null,
            new Vector2(.055f, .055f), new Vector2(.945f, .81f));
        SetAnchors(skeletonCard != null ? skeletonCard.GetComponent<RectTransform>() : null,
            new Vector2(.055f, .445f), new Vector2(.945f, .81f));
        SetAnchors(deerCard != null ? deerCard.GetComponent<RectTransform>() : null,
            new Vector2(.055f, .055f), new Vector2(.945f, .42f));

        ConfigureMissionText(primaryContent, 18f, 14f, 8f,
            new Vector4(34f, 25f, 34f, 24f));
        ConfigureMissionText(skeletonContent, 17f, 15f, 7f,
            new Vector4(34f, 20f, 34f, 18f));
        ConfigureMissionText(deerContent, 17f, 15f, 7f,
            new Vector4(34f, 20f, 34f, 18f));
    }

    static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        if (rect == null) return;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void ConfigureMissionText(TextMeshProUGUI text, float maxSize, float minSize,
        float lineSpacing, Vector4 margin)
    {
        if (text == null) return;
        SetAnchors(text.rectTransform, Vector2.zero, Vector2.one);
        text.margin = margin;
        text.fontSize = maxSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.lineSpacing = lineSpacing;
        text.paragraphSpacing = 5f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    static GameObject CreateMissionCard(Transform parent, string name, Vector2 min, Vector2 max,
        out TextMeshProUGUI text)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        ApplyFrame(card.GetComponent<Image>(), "UI/SharpUI/ListItem", Color.white);
        text = CreateText(card.transform, "MissionText", "", 18,
            Vector2.zero, Vector2.one, Color.white,
            TextAlignmentOptions.TopLeft, FontStyles.Normal);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.richText = true;
        return card;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size,
        Vector2 min, Vector2 max, Color color, TextAlignmentOptions alignment, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value; text.fontSize = size; text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(12f, size * .62f); text.fontSizeMax = size;
        text.color = color; text.alignment = alignment; text.fontStyle = style; text.raycastTarget = false;
        return text;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 min, Vector2 max)
    {
        Button button = SharpUIRuntimeFactory.CreateRectButton(
            parent, name, label, null, 48f);
        GameObject go = button.gameObject;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = label;
            text.fontSize = 19f;
            text.color = new Color(1f, .82f, .35f);
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
        }
        return button;
    }

    static void ApplyFrame(Image image, string resource, Color fallback)
    {
        Sprite sprite = Resources.Load<Sprite>(resource);
        if (sprite != null) { image.sprite = sprite; image.type = Image.Type.Sliced; image.color = Color.white; }
        else image.color = fallback;
    }

    static void SetSelected(Button button, bool selected)
    {
        if (button == null) return;
        Image image = button.targetGraphic as Image;
        if (image != null) image.color = selected ? new Color(1f, .82f, .42f, 1f) : new Color(.68f, .68f, .68f, 1f);
    }
}

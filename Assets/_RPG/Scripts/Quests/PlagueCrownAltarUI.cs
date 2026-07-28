using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlagueCrownAltarUI : MonoBehaviour
{
    const int DiamondSlot = 1;
    static readonly Color Gold = new Color(1f, .78f, .27f);
    static readonly Color PaleGold = new Color(.92f, .84f, .67f);

    static PlagueCrownAltarUI instance;
    GameObject panel;
    GameObject reliefsPage;
    GameObject inscriptionPage;
    TextMeshProUGUI status;
    Button reliefsTab;
    Button inscriptionTab;
    Button activateButton;
    readonly InventorySlot[] selected = new InventorySlot[3];
    readonly TextMeshProUGUI[] slotLabels = new TextMeshProUGUI[3];
    readonly Image[] slotIcons = new Image[3];

    public static void Open()
    {
        if (instance == null)
            instance = new GameObject("PlagueCrownAltarUI").AddComponent<PlagueCrownAltarUI>();
        instance.Show();
    }

    void Awake()
    {
        Build();
        panel.SetActive(false);
    }

    void Update()
    {
        if (panel != null && panel.activeSelf && Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    void Show()
    {
        for (int i = 0; i < selected.Length; i++)
        {
            selected[i] = null;
            UpdateSlot(i);
        }

        status.text = "Selecciona cada relieve para colocar una ofrenda.";
        ShowTab(true);
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        GameManager.Instance?.SetState(GameState.InMenu);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Close()
    {
        panel.SetActive(false);
        GameManager.Instance?.SetState(GameState.Exploration);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void ShowTab(bool showReliefs)
    {
        reliefsPage.SetActive(showReliefs);
        inscriptionPage.SetActive(!showReliefs);
        TintTab(reliefsTab, showReliefs);
        TintTab(inscriptionTab, !showReliefs);
    }

    static void TintTab(Button button, bool selectedTab)
    {
        Image image = button != null ? button.GetComponent<Image>() : null;
        if (image != null)
            image.color = selectedTab ? Color.white : new Color(.54f, .5f, .44f, .92f);
    }

    void SelectForSlot(int index)
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
            return;

        int start = selected[index] != null
            ? inventory.Slots.IndexOf(selected[index]) + 1
            : 0;
        for (int offset = 0; offset < inventory.Slots.Count; offset++)
        {
            InventorySlot candidate = inventory.Slots[(start + offset) % inventory.Slots.Count];
            if (candidate == null || candidate.item == null)
                continue;
            if (index == DiamondSlot && candidate.item.itemType != ItemType.Diamond)
                continue;
            if (candidate == selected[0] || candidate == selected[1] ||
                candidate == selected[2])
                continue;

            selected[index] = candidate;
            UpdateSlot(index);
            status.text = candidate.item.itemName + " colocado en " +
                          (index == DiamondSlot ? "el relieve central." : "el relieve lateral.");
            return;
        }

        selected[index] = null;
        UpdateSlot(index);
        status.text = index == DiamondSlot
            ? "El relieve central sólo acepta un diamante."
            : "No tienes otra ofrenda disponible para este relieve.";
    }

    void Activate()
    {
        bool complete = selected[0] != null && selected[1] != null && selected[2] != null;
        status.text = complete
            ? "La corona responde a las ofrendas, pero su mecanismo continúa sellado."
            : "Los relieves permanecen inertes. Debo preguntarle a Tonio.";

        activateButton.interactable = false;
        QuestManager.Instance?.NotifyStatueReliefsInspected();
        Invoke(nameof(Close), 1.45f);
    }

    void UpdateSlot(int index)
    {
        if (slotLabels[index] == null || slotIcons[index] == null)
            return;

        ItemData item = selected[index]?.item;
        slotLabels[index].text = item != null
            ? item.itemName
            : index == DiamondSlot
                ? "DIAMANTE\nREQUERIDO"
                : "CLIC PARA\nCOLOCAR";
        slotIcons[index].sprite = item != null ? item.icon : null;
        slotIcons[index].enabled = item != null && item.icon != null;
        if (activateButton != null)
            activateButton.interactable = true;
    }

    void Build()
    {
        Canvas canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("AltarCanvas", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
        }

        panel = new GameObject("PlagueCrownAltarPanel", typeof(RectTransform),
            typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform root = panel.GetComponent<RectTransform>();
        root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
        root.pivot = new Vector2(.5f, .5f);
        root.sizeDelta = new Vector2(820f, 620f);
        root.anchoredPosition = new Vector2(0f, 18f);
        ApplyFrame(panel.GetComponent<Image>(), "UI/SharpUI/Panel",
            new Color(.08f, .06f, .045f, .98f));
        Canvas top = panel.GetComponent<Canvas>();
        top.overrideSorting = true;
        top.sortingOrder = 2600;

        GameObject introduction = CreateFrame(panel.transform, "Introduction",
            "UI/SharpUI/DialogHeader", new Vector2(.055f, .72f), new Vector2(.945f, .865f));
        Label(introduction.transform,
            "<b>LA CORONA DEL REY GOBLIN</b>\n" +
            "<size=78%>Tres cavidades antiguas reaccionan ante objetos cargados de energía.</size>",
            21f, new Vector2(.045f, .12f), new Vector2(.955f, .9f), PaleGold,
            TextAlignmentOptions.Center);

        reliefsTab = AnchoredSharpButton(panel.transform, "ReliefsTab", "RELIEVES",
            new Vector2(.075f, .625f), new Vector2(.49f, .705f), () => ShowTab(true));
        inscriptionTab = AnchoredSharpButton(panel.transform, "InscriptionTab", "INSCRIPCIÓN",
            new Vector2(.51f, .625f), new Vector2(.925f, .705f), () => ShowTab(false));

        reliefsPage = CreateFrame(panel.transform, "ReliefsPage", "UI/SharpUI/Panel",
            new Vector2(.055f, .205f), new Vector2(.945f, .61f));
        BuildReliefs(reliefsPage.transform);

        inscriptionPage = CreateFrame(panel.transform, "InscriptionPage", "UI/SharpUI/Panel",
            new Vector2(.055f, .205f), new Vector2(.945f, .61f));
        Label(inscriptionPage.transform, "INSCRIPCIÓN DESGASTADA", 22f,
            new Vector2(.08f, .72f), new Vector2(.92f, .9f), Gold,
            TextAlignmentOptions.Center);
        Label(inscriptionPage.transform,
            "«Tres marcas coronan al soberano. La gema despierta el centro; " +
            "las ofrendas laterales revelan el camino.»\n\n" +
            "<color=#B9AA91>El grabado está incompleto. Tonio quizá pueda interpretar " +
            "el verdadero propósito de los relieves.</color>",
            17f, new Vector2(.11f, .16f), new Vector2(.89f, .7f), PaleGold,
            TextAlignmentOptions.TopLeft);

        GameObject statusFrame = CreateFrame(panel.transform, "Status",
            "UI/SharpUI/DialogHeader", new Vector2(.075f, .105f), new Vector2(.925f, .185f));
        status = Label(statusFrame.transform, "", 15f, new Vector2(.04f, .12f),
            new Vector2(.96f, .88f), PaleGold, TextAlignmentOptions.Center);

        activateButton = AnchoredSharpButton(panel.transform, "ActivateButton", "ACTIVAR",
            new Vector2(.18f, .025f), new Vector2(.49f, .09f), Activate);
        AnchoredSharpButton(panel.transform, "CloseButton", "CERRAR",
            new Vector2(.51f, .025f), new Vector2(.82f, .09f), Close);

        SharpUIWindowChrome.Attach(panel, "ALTAR DE LOS TRES RELIEVES", Close);
    }

    void BuildReliefs(Transform parent)
    {
        string[] names = { "RELIEVE IZQUIERDO", "NÚCLEO DE LA CORONA", "RELIEVE DERECHO" };
        for (int i = 0; i < 3; i++)
        {
            int captured = i;
            float centerX = .19f + i * .31f;
            bool diamond = i == DiamondSlot;

            Label(parent, names[i], 13f,
                new Vector2(centerX - .135f, .76f), new Vector2(centerX + .135f, .92f),
                diamond ? Gold : PaleGold, TextAlignmentOptions.Center);

            GameObject slot = new GameObject("CrownRelief_" + i, typeof(RectTransform),
                typeof(Image), typeof(Button));
            slot.transform.SetParent(parent, false);
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = slotRect.anchorMax = new Vector2(centerX, .5f);
            slotRect.pivot = new Vector2(.5f, .5f);
            slotRect.sizeDelta = diamond ? new Vector2(118f, 118f) : new Vector2(142f, 128f);
            ApplyFrame(slot.GetComponent<Image>(), "UI/SharpUI/Slot", Color.white);
            if (diamond)
                slotRect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            GameObject iconObject = new GameObject("ItemIcon", typeof(RectTransform),
                typeof(Image));
            iconObject.transform.SetParent(slot.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(.2f, .26f);
            iconRect.anchorMax = new Vector2(.8f, .86f);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            slotIcons[i] = iconObject.GetComponent<Image>();
            slotIcons[i].preserveAspect = true;
            slotIcons[i].raycastTarget = false;

            slotLabels[i] = Label(slot.transform, "", 14f,
                new Vector2(.07f, .05f), new Vector2(.93f, .95f), Gold,
                TextAlignmentOptions.Center);
            slotLabels[i].raycastTarget = false;
            if (diamond)
            {
                iconRect.localRotation = Quaternion.Euler(0f, 0f, -45f);
                slotLabels[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                slotLabels[i].rectTransform.anchorMin = new Vector2(-.12f, -.12f);
                slotLabels[i].rectTransform.anchorMax = new Vector2(1.12f, 1.12f);
            }

            slot.GetComponent<Button>().onClick.AddListener(() => SelectForSlot(captured));
            Label(parent, diamond ? "Sólo acepta diamantes" : "Acepta una ofrenda",
                12f, new Vector2(centerX - .135f, .08f),
                new Vector2(centerX + .135f, .22f), new Color(.7f, .65f, .55f),
                TextAlignmentOptions.Center);
            UpdateSlot(i);
        }
    }

    static GameObject CreateFrame(Transform parent, string name, string resource,
        Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        ApplyFrame(go.GetComponent<Image>(), resource, new Color(.09f, .07f, .05f, .96f));
        return go;
    }

    static Button AnchoredSharpButton(Transform parent, string name, string text,
        Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        Button button = SharpUIRuntimeFactory.CreateRectButton(parent, name, text, action, 48f);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout != null)
            layout.ignoreLayout = true;
        return button;
    }

    static TextMeshProUGUI Label(Transform parent, string value, float size,
        Vector2 min, Vector2 max, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    static void ApplyFrame(Image image, string resource, Color fallback)
    {
        Sprite sprite = Resources.Load<Sprite>(resource);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
            image.color = fallback;
    }
}

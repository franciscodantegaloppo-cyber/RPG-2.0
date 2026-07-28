using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class CraftingTableUI : MonoBehaviour
{
    static CraftingTableUI _instance;
    public static CraftingTableUI Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<CraftingTableUI>(FindObjectsInactive.Include);
            return _instance;
        }
    }

    public static CraftingTableUI GetOrCreateInstance()
    {
        CraftingTableUI existing = Instance;
        if (existing != null)
            return existing;

        GameObject go = new GameObject("RuntimeCraftingTableUI");
        DontDestroyOnLoad(go);
        return go.AddComponent<CraftingTableUI>();
    }

    public bool IsOpen { get; private set; }

    GameObject panel;
    Canvas panelCanvas;
    TextMeshProUGUI itemText;
    TextMeshProUGUI enhancerText;
    TextMeshProUGUI resultText;
    TextMeshProUGUI goldText;
    CraftingSlot itemSlot;
    CraftingSlot enhancerSlot;
    CraftingSlot resultSlot;
    Image successFlash;
    Coroutine successFlashRoutine;
    Transform bagGrid;
    Transform availableEnhancersGrid;

    InventorySlot itemInventorySlot;
    ChestSlotHandler itemChestSource;
    InventorySlot enhancerInventorySlot;
    ChestSlotHandler enhancerChestSource;
    InventorySlot pendingResultSlot;
    bool suppressBagRebuild;
    bool bagRebuildRequested;

    static Rect RectFromBounds(float xMin, float yMin, float xMax, float yMax) =>
        new Rect(xMin, yMin, xMax - xMin, yMax - yMin);

    const int InvColumns = 6;
    const int InvRows = 8;
    const int InvMax = 48;
    const int EnhancerColumns = 6;
    const int EnhancerRows = 2;
    const int EnhancerMax = 12;
    static readonly Rect ItemSlotAnchor = RectFromBounds(.035f, .18f, .275f, .72f);
    static readonly Rect EnhancerSlotAnchor = RectFromBounds(.38f, .18f, .62f, .72f);
    static readonly Rect ResultSlotAnchor = RectFromBounds(.725f, .18f, .965f, .72f);

    static Rect InvSlotAnchor(int index)
    {
        return GridSlotAnchor(index, InvColumns, InvRows);
    }

    static Rect AvailableEnhancerSlotAnchor(int index)
    {
        return GridSlotAnchor(index, EnhancerColumns, EnhancerRows);
    }

    static Rect GridSlotAnchor(int index, int columns, int rows)
    {
        int col = index % columns;
        int row = index / columns;
        float cellWidth = 1f / columns;
        float cellHeight = 1f / rows;
        float xMin = col * cellWidth;
        float yMax = 1f - row * cellHeight;
        return RectFromBounds(xMin, yMax - cellHeight, xMin + cellWidth, yMax);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureExists()
    {
        if (FindAnyObjectByType<CraftingTableUI>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("RuntimeCraftingTableUI");
        DontDestroyOnLoad(go);
        go.AddComponent<CraftingTableUI>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        BuildIfNeeded();
        Close();
    }

    void Update()
    {
        if (!IsOpen)
            return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            Close();
    }

    public void Open()
    {
        BuildIfNeeded();
        if (panel == null)
        {
            Debug.LogError("[CraftingTableUI] BuildIfNeeded() left panel null - cannot open.");
            return;
        }

        ClearSlots(pendingResultSlot == null);
        IsOpen = true;

        // This panel owns its canvas, so opening must guarantee the canvas actually renders.
        // Something else in the runtime UI stack disables the Canvas COMPONENT (the GameObject,
        // CanvasScaler and GraphicRaycaster all stay enabled), which left the panel fully built,
        // correctly sized and on-screen while drawing nothing at all.
        if (panelCanvas == null) panelCanvas = panel.GetComponentInParent<Canvas>();
        if (panelCanvas != null)
        {
            if (!panelCanvas.gameObject.activeSelf) panelCanvas.gameObject.SetActive(true);
            panelCanvas.enabled = true;
        }

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        GameManager.Instance?.PauseGameplay();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= RequestBagRebuild;
            InventoryManager.Instance.OnInventoryChanged += RequestBagRebuild;
            InventoryManager.Instance.OnGoldChanged -= RefreshGold;
            InventoryManager.Instance.OnGoldChanged += RefreshGold;
        }
        RebuildBag();
        RefreshGold();
        RestorePendingResultVisual();
    }

    public void Close()
    {
        IsOpen = false;
        StopSuccessFlash();
        panel?.SetActive(false);
        GameManager.Instance?.ResumeGameplay();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= RequestBagRebuild;
            InventoryManager.Instance.OnGoldChanged -= RefreshGold;
        }
    }

    void BuildIfNeeded()
    {
        if (panel != null)
            return;

        EnsureEventSystem();

        GameObject canvasGo = new GameObject("CraftingTableCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2400;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        panelCanvas = canvas;

        panel = new GameObject("CraftingTablePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
        panelRect.pivot = new Vector2(.5f, .5f);
        panelRect.sizeDelta = new Vector2(1320f, 820f);
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        panelImage.type = Image.Type.Sliced;
        panelImage.color = Color.white;

        RectTransform inventorySection = CreateSection(panel.transform, "InventorySection",
            "INVENTARIO", RectFromBounds(.018f, .075f, .39f, .925f));
        RectTransform processSection = CreateSection(panel.transform, "CraftingProcessSection",
            "FORJA Y MEJORA", RectFromBounds(.405f, .485f, .982f, .925f));
        RectTransform enhancersSection = CreateSection(panel.transform, "EnhancersSection",
            "RUNAS Y DIAMANTES DISPONIBLES", RectFromBounds(.405f, .185f, .982f, .465f));
        RectTransform messageSection = CreateSection(panel.transform, "CraftingMessageSection",
            "ESTADO DEL PROCESO", RectFromBounds(.405f, .035f, .785f, .165f));
        RectTransform goldSection = CreateSection(panel.transform, "GoldSection",
            "ORO", RectFromBounds(.018f, .015f, .39f, .066f), false);

        GameObject bagGridGO = new GameObject("BagGrid");
        bagGridGO.transform.SetParent(inventorySection, false);
        RectTransform bagGridRect = bagGridGO.AddComponent<RectTransform>();
        bagGridRect.anchorMin = Vector2.zero;
        bagGridRect.anchorMax = Vector2.one;
        bagGridRect.offsetMin = new Vector2(14f, 14f);
        bagGridRect.offsetMax = new Vector2(-14f, -54f);
        bagGrid = bagGridGO.transform;

        GameObject enhancersGO = new GameObject("AvailableEnhancers");
        enhancersGO.transform.SetParent(enhancersSection, false);
        RectTransform enhancersRect = enhancersGO.AddComponent<RectTransform>();
        enhancersRect.anchorMin = Vector2.zero;
        enhancersRect.anchorMax = Vector2.one;
        enhancersRect.offsetMin = new Vector2(18f, 14f);
        enhancersRect.offsetMax = new Vector2(-18f, -54f);
        availableEnhancersGrid = enhancersGO.transform;

        itemSlot = CreateCraftingSlot(processSection, "ItemSlot", ItemSlotAnchor, false);
        enhancerSlot = CreateCraftingSlot(processSection, "EnhancerSlot", EnhancerSlotAnchor, true);
        resultSlot = CreateCraftingSlot(processSection, "ResultSlot", ResultSlotAnchor, false, true);
        CreateOverlayText(processSection, "ItemSlotLabel",
            RectFromBounds(.025f, .74f, .285f, .88f), "OBJETO");
        CreateOverlayText(processSection, "EnhancerSlotLabel",
            RectFromBounds(.36f, .74f, .64f, .88f), "MEJORADOR");
        CreateOverlayText(processSection, "ResultSlotLabel",
            RectFromBounds(.705f, .74f, .985f, .88f), "RESULTADO");
        CreateOverlayText(processSection, "PlusSymbol",
            RectFromBounds(.285f, .33f, .37f, .61f), "+", 34f, SharpUIThemeApplicator.Accent);
        CreateOverlayText(processSection, "ArrowSymbol",
            RectFromBounds(.63f, .33f, .715f, .61f), ">", 30f, SharpUIThemeApplicator.Accent);
        TextMeshProUGUI hint = CreateOverlayText(processSection, "CraftingHint",
            RectFromBounds(.04f, .02f, .96f, .145f),
            "Arrastra un objeto y una runa o diamante a sus casillas.");
        hint.color = SharpUIThemeApplicator.Muted;

        resultText = CreateOverlayText(messageSection, "ResultText",
            RectFromBounds(.025f, .08f, .975f, .68f), "");
        resultText.fontSize = 15f;
        resultText.enableAutoSizing = true;
        resultText.fontSizeMin = 10f;
        resultText.fontSizeMax = 15f;
        resultText.textWrappingMode = TextWrappingModes.Normal;

        GameObject flashObject = new GameObject("CraftSuccessFlash");
        flashObject.transform.SetParent(canvas.transform, false);
        RectTransform flashRect = flashObject.AddComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;
        successFlash = flashObject.AddComponent<Image>();
        successFlash.color = Color.clear;
        successFlash.raycastTarget = false;

        goldText = CreateOverlayText(goldSection, "GoldText",
            RectFromBounds(.32f, .02f, .94f, .98f), "0");
        goldText.alignment = TextAlignmentOptions.Center;
        goldText.fontSize = 18f;
        goldText.fontStyle = FontStyles.Bold;
        goldText.color = SharpUIThemeApplicator.Title;
        goldText.textWrappingMode = TextWrappingModes.NoWrap;

        Button accept = SharpUIRuntimeFactory.CreateRectButton(panel.transform,
            "CraftButton", "MEJORAR OBJETO", ApplyCraft, 58f);
        RectTransform acceptRect = accept.GetComponent<RectTransform>();
        acceptRect.anchorMin = new Vector2(.805f, .055f);
        acceptRect.anchorMax = new Vector2(.982f, .145f);
        acceptRect.offsetMin = Vector2.zero;
        acceptRect.offsetMax = Vector2.zero;
        LayoutElement acceptLayout = accept.GetComponent<LayoutElement>();
        if (acceptLayout != null) acceptLayout.ignoreLayout = true;

        SharpUIWindowChrome.Attach(panel, "MESA DE CRAFTEO", Close);
    }

    RectTransform CreateSection(Transform parent, string name, string title, Rect anchor,
        bool showHeader = true)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(anchor.xMin, anchor.yMin);
        rect.anchorMax = new Vector2(anchor.xMax, anchor.yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        image.type = Image.Type.Sliced;
        image.color = Color.white;

        if (showHeader)
        {
            GameObject header = new GameObject("SectionHeader", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(go.transform, false);
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = Vector2.one;
            headerRect.pivot = new Vector2(.5f, 1f);
            headerRect.offsetMin = new Vector2(7f, -43f);
            headerRect.offsetMax = new Vector2(-7f, -6f);
            Image headerImage = header.GetComponent<Image>();
            headerImage.sprite = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
            headerImage.type = Image.Type.Sliced;
            headerImage.color = Color.white;
            headerImage.raycastTarget = false;
            CreateOverlayText(header.transform, "HeaderLabel",
                RectFromBounds(.04f, .04f, .96f, .96f), title, 17f,
                SharpUIThemeApplicator.Title);
        }
        else
        {
            CreateOverlayText(go.transform, "InlineLabel",
                RectFromBounds(.05f, .05f, .3f, .95f), title, 15f,
                SharpUIThemeApplicator.Muted);
        }
        return rect;
    }

    CraftingSlot CreateCraftingSlot(Transform parent, string name, Rect anchor, bool acceptsEnhancer, bool readOnly = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(anchor.xMin, anchor.yMin);
        rect.anchorMax = new Vector2(anchor.xMax, anchor.yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = go.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/SharpUI/Slot");
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        CraftingSlot slot = go.AddComponent<CraftingSlot>();
        slot.Owner = this;
        slot.AcceptsEnhancer = acceptsEnhancer;
        slot.ReadOnly = readOnly;
        TextMeshProUGUI placeholder = CreateOverlayText(go.transform, "Placeholder",
            RectFromBounds(.12f, .12f, .88f, .88f),
            readOnly ? "RESULTADO" : acceptsEnhancer ? "RUNA\nO DIAMANTE" : "OBJETO",
            12f, SharpUIThemeApplicator.Muted);
        placeholder.fontStyle = FontStyles.Bold;
        return slot;
    }

    TextMeshProUGUI CreateOverlayText(Transform parent, string name, Rect anchor)
        => CreateOverlayText(parent, name, anchor, "", 15f, SharpUIThemeApplicator.Body);

    TextMeshProUGUI CreateOverlayText(Transform parent, string name, Rect anchor,
        string value, float fontSize = 15f, Color? color = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(anchor.xMin, anchor.yMin);
        rect.anchorMax = new Vector2(anchor.xMax, anchor.yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = 15;
        text.fontSize = fontSize;
        text.color = color ?? SharpUIThemeApplicator.Body;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    void RebuildBag()
    {
        if (bagGrid == null)
            return;

        for (int i = bagGrid.childCount - 1; i >= 0; i--)
            Destroy(bagGrid.GetChild(i).gameObject);

        InventoryUI invUI = InventoryUI.Instance;
        InventoryManager inv = InventoryManager.Instance;
        if (invUI == null)
            return;

        for (int i = 0; i < InvMax; i++)
        {
            InventorySlot slot = inv != null && i < inv.Slots.Count ? inv.Slots[i] : null;
            GameObject slotGO = invUI.CreateBagSlotVisual(bagGrid, slot, i);
            Rect anchor = InvSlotAnchor(i);
            RectTransform slotRect = slotGO.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(anchor.xMin, anchor.yMin);
            slotRect.anchorMax = new Vector2(anchor.xMax, anchor.yMax);
            slotRect.offsetMin = new Vector2(3f, 3f);
            slotRect.offsetMax = new Vector2(-3f, -3f);
        }

        RebuildAvailableEnhancers(invUI, inv);
        RefreshGold();
    }

    void RequestBagRebuild()
    {
        if (suppressBagRebuild)
        {
            bagRebuildRequested = true;
            return;
        }
        RebuildBag();
    }

    void FinishSuppressedBagChanges()
    {
        suppressBagRebuild = false;
        if (!bagRebuildRequested)
            return;
        bagRebuildRequested = false;
        RebuildBag();
    }

    void RebuildAvailableEnhancers(InventoryUI invUI, InventoryManager inv)
    {
        if (availableEnhancersGrid == null) return;
        for (int i = availableEnhancersGrid.childCount - 1; i >= 0; i--)
            Destroy(availableEnhancersGrid.GetChild(i).gameObject);

        if (inv == null) return;
        int visualIndex = 0;
        for (int inventoryIndex = 0; inventoryIndex < inv.Slots.Count && visualIndex < EnhancerMax; inventoryIndex++)
        {
            InventorySlot slot = inv.Slots[inventoryIndex];
            if (slot?.item == null || (slot.item.itemType != ItemType.Rune && slot.item.itemType != ItemType.Diamond))
                continue;

            GameObject slotGO = invUI.CreateBagSlotVisual(availableEnhancersGrid, slot, inventoryIndex);
            Rect anchor = AvailableEnhancerSlotAnchor(visualIndex++);
            RectTransform rect = slotGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(anchor.xMin, anchor.yMin);
            rect.anchorMax = new Vector2(anchor.xMax, anchor.yMax);
            rect.offsetMin = new Vector2(19f, 3f);
            rect.offsetMax = new Vector2(-19f, -3f);
        }
    }

    void RefreshGold()
    {
        if (goldText != null)
            goldText.text = (InventoryManager.Instance != null ? InventoryManager.Instance.Gold : 0).ToString();
    }

    public void AcceptDrop(CraftingSlot target, InventorySlotHandler source)
    {
        if (source == null || source.BagSlot == null)
            return;
        SetSlot(target, source.BagSlot, null);
    }

    public void AcceptDrop(CraftingSlot target, ChestSlotHandler source)
    {
        if (source == null || source.Slot == null)
            return;
        SetSlot(target, source.Slot, source);
    }

    void SetSlot(CraftingSlot target, InventorySlot slot, ChestSlotHandler chestSource)
    {
        if (pendingResultSlot != null)
        {
            resultText.text = "Retira primero el objeto terminado de Resultado.";
            return;
        }
        ClearResult();
        if (target.AcceptsEnhancer)
        {
            if (slot.item == null || (slot.item.itemType != ItemType.Rune && slot.item.itemType != ItemType.Diamond))
            {
                resultText.text = "El mejorador debe ser runa o diamante.";
                return;
            }
            enhancerInventorySlot = slot;
            enhancerChestSource = chestSource;
            enhancerSlot.ShowItem(slot.item);
        }
        else
        {
            if (slot.instance == null)
            {
                resultText.text = "Coloca un arma o equipo con instancia.";
                return;
            }
            itemInventorySlot = slot;
            itemChestSource = chestSource;
            itemSlot.ShowItem(slot.item, slot.instance);
        }
    }

    void ApplyCraft()
    {
        if (pendingResultSlot != null)
        {
            resultText.text = "Retira primero el objeto terminado de Resultado.";
            return;
        }

        if (itemInventorySlot?.instance == null || enhancerInventorySlot?.item == null)
        {
            resultText.text = "Coloca el item y el mejorador.";
            return;
        }

        ItemInstance item = itemInventorySlot.instance;
        ItemData enhancer = enhancerInventorySlot.item;
        ItemUpgradeSystem.UpgradeResult result;

        if (enhancer.itemType == ItemType.Diamond)
            result = ItemUpgradeSystem.ApplyDiamondExcellence(item);
        else if (enhancer.itemType == ItemType.Rune)
            result = ItemUpgradeSystem.ApplyRunePercent(item, true);
        else
        {
            resultText.text = "Mejorador invalido.";
            return;
        }

        if (!result.success && !result.destroyed)
        {
            ClearResult();
            resultText.text = result.message;
            return;
        }

        InventorySlot craftedSlot = itemInventorySlot;
        Color upgradeColor = GetUpgradeColor(item);
        // Consuming the enhancer and detaching the crafted item emit two inventory events. Treat
        // them as one visual transaction so the 48-slot crafting grid is rebuilt only once.
        suppressBagRebuild = true;
        try
        {
            ConsumeEnhancer();
            if (result.destroyed)
                RemoveCraftedItem();
            else
            {
                // A successful craft becomes a real table output. Detach the exact same slot from
                // its inventory/chest source so it cannot simultaneously exist in both places.
                RemoveCraftedItem();
                pendingResultSlot = craftedSlot;
            }
        }
        finally
        {
            FinishSuppressedBagChanges();
        }

        ClearSlots(false);
        resultText.text = result.success && !result.destroyed
            ? "RESULTADO LISTO\nPasa el mouse para ver sus detalles."
            : result.message;
        if (result.success && !result.destroyed)
        {
            resultText.color = upgradeColor;
            resultSlot.ShowResult(pendingResultSlot);
            PlaySuccessFlash(upgradeColor);
            QuestManager.Instance?.NotifyWeaponCraftedAtTable();
        }
        else
        {
            resultText.color = new Color(1f, 0.28f, 0.22f);
            resultSlot.ClearItem();
        }
    }

    void ConsumeEnhancer()
    {
        if (enhancerChestSource != null && enhancerChestSource.IsChestSlot && ChestUI.Instance?.CurrentChest != null)
        {
            enhancerInventorySlot.quantity--;
            if (enhancerInventorySlot.quantity <= 0)
                ChestUI.Instance.CurrentChest.RemoveSlot(enhancerInventorySlot);
        }
        else
        {
            InventoryManager.Instance?.ConsumeSlotItem(enhancerInventorySlot, 1);
        }
    }

    void RemoveCraftedItem()
    {
        if (itemChestSource != null && itemChestSource.IsChestSlot && ChestUI.Instance?.CurrentChest != null)
            ChestUI.Instance.CurrentChest.RemoveSlot(itemInventorySlot);
        else
            InventoryManager.Instance?.RemoveSlot(itemInventorySlot);
    }

    void NotifyItemSourceChanged()
    {
        if (itemChestSource != null && itemChestSource.IsChestSlot)
            ChestUI.Instance?.CurrentChest?.MoveSlotToIndex(itemInventorySlot, itemChestSource.SlotIndex);
        else
            InventoryManager.Instance?.NotifyChanged();
    }

    public void TakeResultToInventory(InventorySlotHandler target)
    {
        if (pendingResultSlot == null || target == null)
            return;
        if (target.IsEquipmentSlot)
        {
            resultText.text = "Lleva primero el resultado a una casilla del inventario.";
            return;
        }

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null || !inventory.PlaceExternalSlotAtIndex(pendingResultSlot, target.BagIndex))
        {
            resultText.text = "Esa casilla esta ocupada. Elige una casilla vacia.";
            return;
        }

        pendingResultSlot = null;
        ClearResult();
        resultText.text = "Objeto retirado al inventario.";
    }

    void RestorePendingResultVisual()
    {
        if (pendingResultSlot == null || resultSlot == null)
            return;

        Color color = GetUpgradeColor(pendingResultSlot.instance);
        resultSlot.ShowResult(pendingResultSlot);
        resultText.color = color;
        resultText.text = "RESULTADO LISTO\nPasa el mouse para ver sus detalles.";
    }

    void ClearSlots(bool clearResult = true)
    {
        itemInventorySlot = null;
        itemChestSource = null;
        enhancerInventorySlot = null;
        enhancerChestSource = null;
        if (itemText != null) itemText.text = "";
        if (enhancerText != null) enhancerText.text = "";
        itemSlot?.ClearItem();
        enhancerSlot?.ClearItem();
        if (clearResult)
            ClearResult();
    }

    void ClearResult()
    {
        if (resultText != null)
        {
            resultText.text = "";
            resultText.color = new Color(0.92f, 0.85f, 0.7f);
        }
        resultSlot?.ClearItem();
    }

    static Color GetUpgradeColor(ItemInstance item)
    {
        if (item == null)
            return Color.white;

        Color color = item.ExcellenceGlowColor;
        if (color.a > 0.01f)
            return color;

        WeaponData weapon = item.template != null ? item.template.weaponData : null;
        if (weapon != null && weapon.hasIntrinsicGlow && weapon.intrinsicGlowColor.a > 0.01f)
            return weapon.intrinsicGlowColor;

        return ItemRarityUtil.GetColor(item.Rarity);
    }

    void PlaySuccessFlash(Color color)
    {
        if (successFlash == null)
            return;
        StopSuccessFlash();
        successFlash.transform.SetAsLastSibling();
        successFlashRoutine = StartCoroutine(SuccessFlashRoutine(color));
    }

    IEnumerator SuccessFlashRoutine(Color color)
    {
        const float duration = 1f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            // Fast bright ignition, a smaller second pulse, then a smooth disappearance.
            float firstPulse = Mathf.Exp(-Mathf.Pow((normalized - 0.10f) / 0.10f, 2f));
            float secondPulse = 0.48f * Mathf.Exp(-Mathf.Pow((normalized - 0.39f) / 0.13f, 2f));
            float alpha = Mathf.Clamp01(Mathf.Max(firstPulse, secondPulse)) * 0.56f * (1f - normalized * 0.28f);
            successFlash.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        successFlash.color = Color.clear;
        successFlashRoutine = null;
    }

    void StopSuccessFlash()
    {
        if (successFlashRoutine != null)
        {
            StopCoroutine(successFlashRoutine);
            successFlashRoutine = null;
        }
        if (successFlash != null)
            successFlash.color = Color.clear;
    }

    void OnDestroy()
    {
        // Safety net for scene/UI destruction: closing the panel deliberately keeps the result
        // in the table, but destroying the table must never delete a finished upgraded item.
        if (pendingResultSlot?.instance != null && InventoryManager.Instance != null)
            InventoryManager.Instance.AddItemInstance(pendingResultSlot.instance);
        pendingResultSlot = null;
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}

public class CraftingSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public CraftingTableUI Owner;
    public bool AcceptsEnhancer;
    public bool ReadOnly;
    RuntimeItemPreviewUI preview;
    RawImage previewImage;
    GameObject previewBackdrop;
    CraftingResultDragHandler resultDragHandler;
    ItemData shownItem;
    ItemInstance shownInstance;

    public void ShowItem(ItemData item, ItemInstance instance = null)
    {
        if (preview == null)
        {
            previewBackdrop = new GameObject("PreviewBackdrop");
            previewBackdrop.transform.SetParent(transform, false);
            Image backdrop = previewBackdrop.AddComponent<Image>();
            // The SharpUI slot on the parent supplies the frame; keep the preview surface
            // transparent so the imported item render preserves its silhouette.
            backdrop.color = Color.clear;
            backdrop.raycastTarget = false;
            RectTransform backdropRect = backdrop.rectTransform;
            backdropRect.anchorMin = new Vector2(0.075f, 0.075f);
            backdropRect.anchorMax = new Vector2(0.925f, 0.925f);
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;

            GameObject icon = new GameObject("Large3DPreview");
            icon.transform.SetParent(transform, false);
            previewImage = icon.AddComponent<RawImage>();
            previewImage.raycastTarget = false;
            RectTransform rect = previewImage.rectTransform;
            rect.anchorMin = new Vector2(0.03f, 0.05f);
            rect.anchorMax = new Vector2(0.97f, 0.97f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            preview = icon.AddComponent<RuntimeItemPreviewUI>();
        }
        previewBackdrop.SetActive(true);
        Transform placeholder = transform.Find("Placeholder");
        if (placeholder != null) placeholder.gameObject.SetActive(false);
        preview.Show(item, 2.38f);
        shownItem = item;
        shownInstance = instance;
    }

    public void ShowResult(InventorySlot slot)
    {
        if (slot?.item == null)
        {
            ClearItem();
            return;
        }

        ShowItem(slot.item, slot.instance);
        if (resultDragHandler == null)
            resultDragHandler = gameObject.AddComponent<CraftingResultDragHandler>();
        resultDragHandler.Configure(Owner, slot, previewImage);
    }

    public void ClearItem()
    {
        if (preview != null)
            preview.Show(null);
        if (previewBackdrop != null)
            previewBackdrop.SetActive(false);
        Transform placeholder = transform.Find("Placeholder");
        if (placeholder != null) placeholder.gameObject.SetActive(true);
        resultDragHandler?.Clear();
        shownItem = null;
        shownInstance = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (shownItem != null)
            InventoryUI.Instance?.ShowTooltip(shownItem, shownInstance, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData) => InventoryUI.Instance?.HideTooltip();

    public void OnDrop(PointerEventData eventData)
    {
        if (ReadOnly)
            return;
        if (eventData.pointerDrag == null)
            return;

        InventorySlotHandler inv = eventData.pointerDrag.GetComponent<InventorySlotHandler>();
        if (inv != null)
        {
            Owner?.AcceptDrop(this, inv);
            return;
        }

        ChestSlotHandler chest = eventData.pointerDrag.GetComponent<ChestSlotHandler>();
        if (chest != null)
            Owner?.AcceptDrop(this, chest);
    }
}

public class CraftingResultDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CraftingTableUI Owner { get; private set; }
    public InventorySlot Slot { get; private set; }
    RawImage iconImage;

    public void Configure(CraftingTableUI owner, InventorySlot slot, RawImage icon)
    {
        Owner = owner;
        Slot = slot;
        iconImage = icon;
    }

    public void Clear()
    {
        Slot = null;
        iconImage = null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Slot?.item == null)
            return;
        InventoryUI.Instance?.HideTooltip();
        Texture texture = iconImage != null ? iconImage.texture :
            (Slot.item.icon != null ? Slot.item.icon.texture : null);
        InventoryUI.Instance?.BeginDrag(texture, eventData.position);
    }

    public void OnDrag(PointerEventData eventData) => InventoryUI.Instance?.UpdateDrag(eventData.position);
    public void OnEndDrag(PointerEventData eventData) => InventoryUI.Instance?.EndDrag();

}

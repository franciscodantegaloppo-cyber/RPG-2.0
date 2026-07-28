using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    static InventoryUI _instance;
    public static InventoryUI Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<InventoryUI>(FindObjectsInactive.Include);
            return _instance;
        }
    }

    [SerializeField] GameObject panel;

    RectTransform generatedRoot;
    TextMeshProUGUI goldText;
    TextMeshProUGUI statsText;
    Transform bagGrid;
    Transform equipmentGrid;
    Transform skillsGrid;
    TextMeshProUGUI bagStatusText;
    TextMeshProUGUI bagSortText;
    readonly Dictionary<BagFilter, Image> bagFilterImages = new Dictionary<BagFilter, Image>();
    BagFilter bagFilter = BagFilter.All;
    BagSort bagSort = BagSort.Position;
    readonly Dictionary<string, TextMeshProUGUI> skillButtonLabels =
        new Dictionary<string, TextMeshProUGUI>();
    readonly Dictionary<string, string> skillNames = new Dictionary<string, string>();
    readonly Dictionary<string, string> skillDescriptions =
        new Dictionary<string, string>();

    GameObject actionPopup;
    TextMeshProUGUI actionText;
    Button useButton;
    Button equipButton;
    Button upgradeButton;
    Button splitButton;
    InventorySlot actionTarget;

    GameObject tooltip;
    TextMeshProUGUI tooltipText;
    RectTransform tooltipRect;

    GameObject dragIcon;
    RawImage dragIconImage;
    RuntimeItemPreviewUI dragItemPreview;
    GameObject dragQuantityBadge;
    TextMeshProUGUI dragQuantityText;
    TextMeshProUGUI dragFallbackText;

    // ── Stack splitting ("right-click a stack -> pick a quantity -> it rides the cursor
    // until you click a bag slot to drop it there") ────────────────────────────────────
    GameObject splitDialog;
    Slider splitSlider;
    TextMeshProUGUI splitDialogText;
    InventorySlot splitTargetSlot;
    bool holdingSplitStack;
    ItemData heldSplitItem;
    int heldSplitQuantity;

    PlayerStats playerStats;
    Transform playerTransform;
    RuntimePlayerPreviewUI playerPreview;
    bool isOpen;
    bool visualRefreshPending;
    bool draggingInventoryItem;
    bool summaryOnlyRefresh;
    bool suppressNextInventoryRefresh;
    bool suppressVisualRefreshEvents;
    public bool IsOpen => isOpen;

    enum BagFilter { All, Equipment, Consumables, Materials }
    enum BagSort { Position, Power, Rarity, Name }

    static readonly ItemType[] EquipmentSlots =
    {
        ItemType.Helmet,
        ItemType.Chest,
        ItemType.Gloves,
        ItemType.Legs,
        ItemType.Boots,
        ItemType.Shield,
        ItemType.Weapon
    };

    // The AI Toolkit "inventory grid" 3D asset, baked to a single transparent front-view PNG
    // (Resources/UI/InventoryGridBake.png) and used whole as the panel's art - equipment icons
    // and the bag grid are already drawn into it. Real slot buttons are invisible overlays
    // anchored to match the artwork's own layout (see EquipAnchors/BagGridAnchor below), rather
    // than generating separate flat-colored boxes next to it.
    static Sprite _fullArtSprite;
    static Sprite FullArtSprite
    {
        get
        {
            if (_fullArtSprite == null)
                _fullArtSprite = Resources.Load<Sprite>("UI/InventoryGridBake");
            return _fullArtSprite;
        }
    }

    // Fractional anchor rects (bottom-up, matching Unity's anchorMin/anchorMax convention) for
    // each equipment icon in the baked art, re-measured against the tightened crop of
    // InventoryGridBake.png (the old full render had ~20% of its height as pure decorative
    // padding above/below the useful content - cropping that out is what actually made the
    // panel bigger on screen, not just widening the outer panel bounds). Only 7 of the art's
    // ~15 icon slots (rings/necklace/cape/charm have no matching ItemType) map to real
    // equipment types; the rest are left as pure decoration.
    static readonly System.Collections.Generic.Dictionary<ItemType, Rect> EquipAnchors = new System.Collections.Generic.Dictionary<ItemType, Rect>
    {
        // Rect(xMin, yMin, width, height) in 0-1 anchor space
        { ItemType.Helmet, RectFromBounds(0.415f, 0.8743f, 0.591f, 0.9893f) },
        { ItemType.Weapon, RectFromBounds(0.0859f, 0.6475f, 0.2637f, 0.8352f) },
        { ItemType.Chest,  RectFromBounds(0.415f, 0.6456f, 0.591f, 0.8536f) },
        { ItemType.Shield, RectFromBounds(0.7207f, 0.6475f, 0.8965f, 0.8352f) },
        { ItemType.Gloves, RectFromBounds(0.0859f, 0.5104f, 0.2637f, 0.6245f) },
        { ItemType.Legs,   RectFromBounds(0.415f, 0.5104f, 0.591f, 0.6245f) },
        { ItemType.Boots,  RectFromBounds(0.7207f, 0.5104f, 0.8965f, 0.6245f) },
    };

    static Rect RectFromBounds(float xMin, float yMin, float xMax, float yMax) =>
        new Rect(xMin, yMin, xMax - xMin, yMax - yMin);

    // The art's grid is a real 8-column layout (measured directly off the pixel-line boundaries
    // in InventoryGridBake.png) - the previous 10-column overlay didn't match it, which is why
    // clicks only landed correctly on some cells. The artwork contains all 6 rows of that
    // 8-column grid, so the interactive overlay must cover the complete 48-slot bag.
    static readonly Rect BagGridAnchor = RectFromBounds(0.0986f, 0.0852f, 0.8955f, 0.4701f);
    const int BagColumns = 8;
    const int BagRows = 6;
    const int BagMax = 48;

    // Per-slot anchor rect within BagGridAnchor's own space, for bag slot index i (row-major,
    // top row first) - replaces GridLayoutGroup, which needed bagGridRect's resolved pixel size
    // at the moment cellSize was computed and could read a stale/wrong size if that ran before
    // the art image's AspectRatioFitter had settled, on top of not matching the art's real
    // column count in the first place.
    static Rect BagSlotAnchor(int index)
    {
        int col = index % BagColumns;
        int row = index / BagColumns;
        float colWidth = BagGridAnchor.width / BagColumns;
        float rowHeight = BagGridAnchor.height / BagRows;
        float xMin = BagGridAnchor.xMin + col * colWidth;
        float yMax = BagGridAnchor.yMax - row * rowHeight;
        return RectFromBounds(xMin, yMax - rowHeight, xMin + colWidth, yMax);
    }

    void Awake()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerStats = player.GetComponent<PlayerStats>();
        }

        BuildIfNeeded();
        CloseInventory();
    }

    void OnEnable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += RequestVisualRefresh;
            InventoryManager.Instance.OnGoldChanged += RefreshSummaryOnly;
        }

        if (playerStats != null)
            playerStats.OnSkillsChanged += RefreshSummaryOnly;

        if (EquipmentManager.Instance != null)
            EquipmentManager.Instance.OnEquipmentChanged += RequestEquipmentRefresh;
    }

    void OnDisable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= RequestVisualRefresh;
            InventoryManager.Instance.OnGoldChanged -= RefreshSummaryOnly;
        }

        if (playerStats != null)
            playerStats.OnSkillsChanged -= RefreshSummaryOnly;

        if (EquipmentManager.Instance != null)
            EquipmentManager.Instance.OnEquipmentChanged -= RequestEquipmentRefresh;
    }

    void Update()
    {
        // A single drop can emit several inventory/equipment notifications. Coalesce them
        // and never rebuild the slot hierarchy while its dragged object is still alive.
        if (visualRefreshPending && !draggingInventoryItem)
        {
            visualRefreshPending = false;
            Refresh();
        }

        // Mu/Diablo-style: track the cursor continuously while the tooltip is up, not just once
        // at hover-enter, so it stays glued above the mouse as it moves across the item. This
        // must run even if Keyboard.current is null because ChestUI reuses this tooltip too.
        if (tooltip != null && tooltip.activeSelf && Mouse.current != null)
            PositionTooltipAt(Mouse.current.position.ReadValue());

        if (holdingSplitStack && Mouse.current != null)
        {
            Vector2 pos = Mouse.current.position.ReadValue();
            UpdateDrag(pos);
            if (Mouse.current.leftButton.wasPressedThisFrame)
                TryPlaceHeldSplit(pos);
            else if (Mouse.current.rightButton.wasPressedThisFrame)
                CancelHeldSplit();
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        BlacksmithShopPanel shop = BlacksmithShopPanel.Instance;
        if (shop != null && shop.IsOpen)
            return;

        if (kb.escapeKey.wasPressedThisFrame && isOpen)
            CloseInventory();

    }

    public void ToggleInventory()
    {
        if (isOpen) CloseInventory();
        else OpenInventory();
    }

    public void OpenInventory()
    {
        BuildIfNeeded();
        isOpen = true;
        panel.SetActive(true);
        Refresh();
        panel.GetComponent<InventoryWindowAutoSizer>()?.Recalculate(true);
        GameManager.Instance?.SetState(GameState.InMenu);
        ReleaseCursor();
    }

    public void CloseInventory()
    {
        if (holdingSplitStack)
            CancelHeldSplit();
        isOpen = false;
        actionPopup?.SetActive(false);
        HideTooltip();
        if (panel != null)
            panel.SetActive(false);
        GameManager.Instance?.SetState(GameState.Exploration);
        LockCursor();
    }

    static void ReleaseCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void BuildIfNeeded()
    {
        if (generatedRoot != null)
            return;

        if (panel == null)
            panel = CreatePanelRoot();

        generatedRoot = panel.GetComponent<RectTransform>();
        ConfigurePanelBounds(generatedRoot);
        HideLegacyInventoryChildren();
        Image bg = panel.GetComponent<Image>() ?? panel.AddComponent<Image>();
        // The three sections already have their own complete MU frame. Keeping a fourth frame
        // stretched across the whole screen made the unused responsive width look like large
        // empty panels between them. The root remains only as a layout container.
        bg.color = Color.clear;
        bg.raycastTarget = false;

        var layout = panel.GetComponent<HorizontalLayoutGroup>() ?? panel.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 92, 0);
        layout.spacing = 4;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        InventoryWindowAutoSizer autoSizer =
            panel.GetComponent<InventoryWindowAutoSizer>() ??
            panel.AddComponent<InventoryWindowAutoSizer>();
        autoSizer.Configure(1210f, 790f, 24f, 136f);

        // Left-to-right: a live mirror of the equipped character, the inventory artwork and
        // finally the attributes.  Adding the character column deliberately shifts the actual
        // inventory to the right while keeping it large enough for all 48 interactive cells.
        // Keep the side panels predictable and give the inventory artwork all remaining width.
        // The old flexible 1.15 / 3.35 / .95 split let the character mirror consume more than
        // one fifth of the screen and made the actual inventory feel cramped.
        // Responsive widths: fixed minimums prevent internal overlap on small resolutions while
        // flexible weights use the extra room cleanly on wide screens.
        // Fixed preferred widths keep all three framed blocks touching in the middle of the
        // screen. Previously their flexible weights absorbed every spare pixel and created
        // apparently empty gaps inside the frames.
        RectTransform characterColumn = CreateColumn(
            panel.transform, "Personaje", 0f, 230f, 190f, 724f);
        RectTransform artColumn = CreateArtColumn(
            panel.transform, 0f, 570f, 470f, 724f);
        RectTransform right = CreateColumn(
            panel.transform, "Habilidades", 0f, 390f, 350f, 724f);

        BuildBagToolbar();

        BuildPlayerPreview(characterColumn);

        // Everything below lives on top of the single baked art image, positioned via fractional
        // anchors measured against that art (see EquipAnchors/BagGridAnchor) instead of a
        // generic flexible grid, so it lines up with the equipment icons/grid cells already
        // drawn into it.
        GameObject artGO = new GameObject("InventoryArt");
        artGO.transform.SetParent(artColumn, false);
        RectTransform artRect = artGO.AddComponent<RectTransform>();
        // AspectRatioFitter only computes sizeDelta correctly against a single anchor POINT, not
        // stretched (0,0)-(1,1) anchors - with stretch anchors it fights the anchor-driven size
        // every layout pass and the image ends up mispositioned/cropped instead of centered and
        // fully visible within artColumn.
        artRect.anchorMin = new Vector2(0.5f, 0.5f);
        artRect.anchorMax = new Vector2(0.5f, 0.5f);
        artRect.pivot = new Vector2(0.5f, 0.5f);
        artRect.anchoredPosition = Vector2.zero;
        Image artImage = artGO.AddComponent<Image>();
        artImage.sprite = FullArtSprite;
        artImage.preserveAspect = false;
        AspectRatioFitter fitter = artGO.AddComponent<AspectRatioFitter>();
        // FitInParent (contain) - EnvelopeParent (cover) fills the wider dimension and lets the
        // narrower one overflow, which on this tall narrow art meant it overflowed vertically
        // past both the top and bottom of artColumn, clipping the helmet row and part of the bag
        // grid off-screen even though the anchor math itself was correct.
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        if (FullArtSprite != null)
            fitter.aspectRatio = FullArtSprite.rect.width / FullArtSprite.rect.height;

        // Just the number - the art already draws the coin icon at the left of this bar, so a
        // separate "Oro:" label would be redundant with it.
        goldText = CreateText(artGO.transform, "OroText", "0", 20, FontStyles.Bold);
        goldText.alignment = TextAlignmentOptions.Left;
        goldText.color = new Color(0.92f, 0.8f, 0.45f);
        RectTransform goldRect = goldText.GetComponent<RectTransform>();
        goldRect.anchorMin = new Vector2(0.205f, 0.015f);
        goldRect.anchorMax = new Vector2(0.55f, 0.06f);
        goldRect.offsetMin = Vector2.zero;
        goldRect.offsetMax = Vector2.zero;

        GameObject equipSlotsGO = new GameObject("EquipSlots");
        equipSlotsGO.transform.SetParent(artGO.transform, false);
        RectTransform equipSlotsRect = equipSlotsGO.AddComponent<RectTransform>();
        equipSlotsRect.anchorMin = Vector2.zero;
        equipSlotsRect.anchorMax = Vector2.one;
        equipSlotsRect.offsetMin = Vector2.zero;
        equipSlotsRect.offsetMax = Vector2.zero;
        equipmentGrid = equipSlotsGO.transform; // each equip slot is placed with its own explicit anchors

        // No GridLayoutGroup - each of the 48 slots gets its own explicit anchor rect from
        // BagSlotAnchor() (see RebuildBag()), which matches the art's real 8-column grid exactly
        // and needs no resolved-pixel-size lookup at all.
        GameObject bagGridGO = new GameObject("BagGrid");
        bagGridGO.transform.SetParent(artGO.transform, false);
        RectTransform bagGridRect = bagGridGO.AddComponent<RectTransform>();
        bagGridRect.anchorMin = Vector2.zero;
        bagGridRect.anchorMax = Vector2.one;
        bagGridRect.offsetMin = Vector2.zero;
        bagGridRect.offsetMax = Vector2.zero;
        bagGrid = bagGridGO.transform;

        CreateTitle(right, "Habilidades");
        statsText = CreateText(right, "StatsText", "", 16, FontStyles.Normal);
        statsText.enableAutoSizing = true;
        statsText.fontSizeMin = 10f;
        statsText.fontSizeMax = 15f;
        statsText.lineSpacing = 4f;
        LayoutElement statsLayout = statsText.gameObject.AddComponent<LayoutElement>();
        statsLayout.preferredHeight = 315f;
        statsLayout.flexibleHeight = 0f;
        skillsGrid = CreateSkillGrid(right);
        AddSkillButton("Fuerza", "strength", "+2 ataque");
        AddSkillButton("Agilidad", "agility", "+0.5 defensa");
        AddSkillButton("Velocidad", "speed", "+1% antes de merma");
        AddSkillButton("Vitalidad", "vitality", "+5 vida máxima");
        AddSkillButton("Resistencia", "endurance", "+4 estamina máxima");
        AddSkillButton("Precisión", "precision", "+0.5 penetración");
        AddSkillButton("Crítico", "critical", "+0.25% probabilidad");
        AddSkillButton("Daño crítico", "critical_damage", "+1.5% daño crítico");
        AddSkillButton("Recuperación", "recovery", "+2% regeneración");
        AddSkillButton("Poder arcano", "magic", "+2% poder mágico");

        BuildActionPopup();
        BuildTooltip();
        BuildDragIcon();
        SharpUIWindowChrome.Attach(panel, "INVENTARIO Y PERSONAJE", CloseInventory);
    }

    void BuildBagToolbar()
    {
        GameObject toolbar = new GameObject("BagToolbar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        toolbar.transform.SetParent(panel.transform, false);
        LayoutElement ignore = toolbar.GetComponent<LayoutElement>();
        ignore.ignoreLayout = true;
        RectTransform rect = toolbar.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.sizeDelta = new Vector2(790f, 34f);
        rect.anchoredPosition = new Vector2(0f, -51f);
        Image bg = toolbar.GetComponent<Image>();
        bg.sprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(1f, 1f, 1f, .96f);

        HorizontalLayoutGroup layout = toolbar.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;

        AddBagFilterButton(toolbar.transform, "TODOS", BagFilter.All, 74f);
        AddBagFilterButton(toolbar.transform, "EQUIPO", BagFilter.Equipment, 82f);
        AddBagFilterButton(toolbar.transform, "CONSUMIBLES", BagFilter.Consumables, 112f);
        AddBagFilterButton(toolbar.transform, "MATERIALES", BagFilter.Materials, 104f);

        Button sort = CreateCompactToolbarButton(toolbar.transform, "BagSortButton", "ORDEN: POSICIÓN", 148f);
        bagSortText = sort.GetComponentInChildren<TextMeshProUGUI>();
        sort.onClick.AddListener(() =>
        {
            bagSort = (BagSort)(((int)bagSort + 1) % 4);
            Refresh();
        });

        bagStatusText = CreateText(toolbar.transform, "BagStatus", "", 11, FontStyles.Normal);
        bagStatusText.alignment = TextAlignmentOptions.MidlineRight;
        bagStatusText.color = new Color(.78f, .72f, .62f);
        LayoutElement statusLayout = bagStatusText.gameObject.AddComponent<LayoutElement>();
        statusLayout.preferredWidth = 210f;
        statusLayout.flexibleWidth = 1f;
        RefreshBagToolbar();
    }

    void AddBagFilterButton(Transform parent, string label, BagFilter filter, float width)
    {
        Button button = CreateCompactToolbarButton(parent, "Filter" + filter, label, width);
        bagFilterImages[filter] = button.GetComponent<Image>();
        button.onClick.AddListener(() =>
        {
            bagFilter = filter;
            Refresh();
        });
    }

    Button CreateCompactToolbarButton(Transform parent, string name, string label, float width)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/SharpUI/Button");
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = 26f;
        TextMeshProUGUI text = CreateText(go.transform, "Label", label, 10, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 7f;
        text.fontSizeMax = 10f;
        RectTransform tr = text.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(5f, 2f);
        tr.offsetMax = new Vector2(-5f, -2f);
        return go.GetComponent<Button>();
    }

    void RefreshBagToolbar()
    {
        foreach (var entry in bagFilterImages)
            if (entry.Value != null)
                entry.Value.color = entry.Key == bagFilter
                    ? new Color(1f, .76f, .34f, 1f)
                    : Color.white;

        if (bagSortText != null)
        {
            string label = bagSort switch
            {
                BagSort.Power => "ORDEN: PODER",
                BagSort.Rarity => "ORDEN: RAREZA",
                BagSort.Name => "ORDEN: NOMBRE",
                _ => "ORDEN: POSICIÓN"
            };
            bagSortText.text = label;
        }
    }

    void HideLegacyInventoryChildren()
    {
        // Some scenes still serialize the original inventory boxes under this same panel. The
        // runtime layout is complete, so leaving those active draws two inventories on top of
        // each other. Preserve them in the scene but keep them out of rendering and layout.
        for (int i = panel.transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = panel.transform.GetChild(i).gameObject;
            child.SetActive(false);
        }
    }

    static void ConfigurePanelBounds(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(1080f, 760f);
        rect.anchoredPosition = new Vector2(0f, 34f);
        rect.localScale = Vector3.one;
    }

    void BuildPlayerPreview(Transform parent)
    {
        CreateTitle(parent, "Personaje");

        // The layout owns this neutral area; the actual render surface below aspect-fits inside
        // it. This prevents RawImage from being stretched into a very tall/wide column.
        GameObject previewArea = new GameObject("PlayerPreviewArea");
        previewArea.transform.SetParent(parent, false);
        previewArea.AddComponent<RectTransform>();
        LayoutElement areaLayout = previewArea.AddComponent<LayoutElement>();
        areaLayout.minHeight = 240f;
        areaLayout.flexibleHeight = 1f;

        GameObject viewport = new GameObject("Player3DMirror");
        viewport.transform.SetParent(previewArea.transform, false);
        RectTransform rect = viewport.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        AspectRatioFitter previewAspect = viewport.AddComponent<AspectRatioFitter>();
        previewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        previewAspect.aspectRatio = 2f / 3f;

        RawImage image = viewport.AddComponent<RawImage>();
        image.color = Color.white;
        // The mannequin keeps rotating automatically, and can also be rotated/zoomed manually.
        image.raycastTarget = true;
        playerPreview = viewport.AddComponent<RuntimePlayerPreviewUI>();

        // The SharpUI panel is a background, not an overlay. It used to be a child of the
        // RawImage, so its opaque centre rendered after the RenderTexture and hid the player.
        GameObject frameObject = new GameObject("MirrorFrame");
        frameObject.transform.SetParent(previewArea.transform, false);
        frameObject.transform.SetAsFirstSibling();
        RectTransform frameRect = frameObject.AddComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(.04f, .02f);
        frameRect.anchorMax = new Vector2(.96f, .98f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        Image frame = frameObject.AddComponent<Image>();
        frame.raycastTarget = false;
        Sprite frameSprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        if (frameSprite != null)
        {
            frame.sprite = frameSprite;
            frame.type = Image.Type.Sliced;
            frame.color = Color.white;
        }
        else
        {
            frame.color = new Color(0.22f, 0.18f, 0.1f, 0.55f);
        }
    }

    GameObject CreatePanelRoot()
    {
        EnsureEventSystem();

        GameObject canvasGo = new GameObject("InventoryCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2200;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        GameObject go = new GameObject("InventoryPanel");
        go.transform.SetParent(canvas.transform, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        // Was 0.08-0.92 (84% of screen) - the art column's height is the binding constraint on
        // how big the whole panel art renders (FitInParent scales to whichever axis is tighter),
        // so a bigger panel is the only lever that actually makes the art/text bigger and
        // readable, not just widening columns.
        // Reserve the lower strip for the always-visible quickbar. Besides keeping its icons
        // visible, this leaves its slots free to receive drag-and-drop from the inventory.
        // Reserve an exact UI-height strip instead of a percentage of the screen. The old
        // 13% lower anchor made the inventory dramatically smaller on short/wide resolutions.
        // Quickbar is 42 units high at Y=6, so 54 leaves a clean 6-unit separation.
        rect.anchorMin = new Vector2(0.07f, 0f);
        rect.anchorMax = new Vector2(0.93f, 1f);
        rect.offsetMin = new Vector2(0f, 128f);
        rect.offsetMax = new Vector2(0f, -8f);
        return go;
    }

    RectTransform CreateColumn(Transform parent, string name, float flexibleWidth,
        float preferredWidth = -1f, float minWidth = -1f,
        float preferredHeight = -1f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(9, 9, 12, 10);
        layout.spacing = 5;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = flexibleWidth;
        if (preferredWidth >= 0f) le.preferredWidth = preferredWidth;
        if (minWidth >= 0f) le.minWidth = minWidth;
        if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
        Image image = go.AddComponent<Image>();
        // MU-style ornate panel frame (Bitszer "botones y recuadros mu_1") instead of a flat
        // fill - gives the skills column a proper backdrop consistent with the rest of the new
        // UI direction, with enough transparency to still read as part of the same panel.
        // _Clean variant: the source crop had a non-functional red X button baked into the
        // top-right corner (this panel isn't closable), so the clean asset mirrors the good
        // bottom corners over it instead of showing a stray close-looking icon.
        Sprite frameSprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        if (frameSprite != null)
        {
            image.sprite = frameSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.09f, 0.105f, 0.115f, 0.92f);
        }
        return rect;
    }

    RectTransform CreateArtColumn(Transform parent, float flexibleWidth,
        float preferredWidth = -1f, float minWidth = -1f,
        float preferredHeight = -1f)
    {
        GameObject go = new GameObject("InventoryArtColumn");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = flexibleWidth;
        if (preferredWidth >= 0f) le.preferredWidth = preferredWidth;
        if (minWidth >= 0f) le.minWidth = minWidth;
        if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
        Image frame = go.AddComponent<Image>();
        Sprite frameSprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        if (frameSprite != null)
        {
            frame.sprite = frameSprite;
            frame.type = Image.Type.Sliced;
            frame.color = Color.white;
        }
        else frame.color = new Color(0.09f, 0.07f, 0.04f, 0.94f);
        return rect;
    }

    Transform CreateGrid(Transform parent, string name, int columns, float cellSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cellSize, cellSize);
        grid.spacing = new Vector2(8, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;
        return go.transform;
    }

    Transform CreateVertical(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        return go.transform;
    }

    Transform CreateSkillGrid(Transform parent)
    {
        GameObject go = new GameObject("SkillButtons");
        go.transform.SetParent(parent, false);
        GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(178f, 57f);
        grid.spacing = new Vector2(7f, 7f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;
        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = 313f;
        layout.flexibleHeight = 0f;
        return go.transform;
    }

    void CreateTitle(Transform parent, string text)
    {
        TextMeshProUGUI title = CreateText(parent, text + "Title", text, 22, FontStyles.Bold);
        title.enableAutoSizing = true;
        title.fontSizeMin = 13f;
        title.fontSizeMax = 22f;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        LayoutElement le = title.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 32f;
        le.preferredHeight = 32f;
        le.flexibleHeight = 0f;
    }

    TextMeshProUGUI CreateText(Transform parent, string name, string value, int size, FontStyles style)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    void AddSkillButton(string label, string skillId, string description)
    {
        Button button = CreateButton(skillsGrid, label + "Button", label + "\n" + description);
        TextMeshProUGUI buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>();
        skillButtonLabels[skillId] = buttonLabel;
        skillNames[skillId] = label;
        skillDescriptions[skillId] = description;
        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout != null)
            layout.preferredHeight = 57f;
        button.onClick.AddListener(() =>
        {
            // UpgradeSkill already raises OnSkillsChanged. That event only refreshes the
            // numbers now; rebuilding the 48 bag slots here caused a second full refresh.
            playerStats?.UpgradeSkill(skillId);
        });
    }

    Button CreateButton(Transform parent, string name, string label)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        Sprite buttonFrame = Resources.Load<Sprite>("UI/SharpUI/Button");
        if (buttonFrame != null)
        {
            image.sprite = buttonFrame;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.16f, 0.18f, 0.19f, 1f);
        }
        Button button = go.AddComponent<Button>();
        TextMeshProUGUI text = CreateText(go.transform, "Label", label, 15, FontStyles.Normal);
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = 15f;
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24, 10);
        textRect.offsetMax = new Vector2(-24, -10);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 76;
        return button;
    }

    void Refresh()
    {
        if (panel == null || !isOpen)
            return;

        InventoryManager inv = InventoryManager.Instance;
        EquipmentManager equipment = EquipmentManager.Instance;

        if (goldText != null)
            goldText.text = (inv != null ? inv.Gold : 0).ToString();

        if (statsText != null && playerStats != null)
        {
            statsText.text =
                "<b><color=#D5B96B>PUNTOS DISPONIBLES</color></b>" +
                "\n<size=130%><b><color=#FFF0A6>" +
                playerStats.AvailableSkillPoints + "</color></b></size>" +
                "<pos=45%>Nivel <b>" + playerStats.Level + "</b>" +
                "\n\n<b><color=#D5B96B>COMBATE</color></b>" +
                "\nAtaque total<pos=64%><b>" + playerStats.TotalAttack.ToString("0.0") + "</b>" +
                "\nDefensa total<pos=64%><b>" + playerStats.TotalDefense.ToString("0.0") + "</b>" +
                "\nProb. crítico<pos=64%><b>" + playerStats.CriticalChancePercent.ToString("0.00") + "%</b>" +
                "\nDaño crítico<pos=64%><b>+" + playerStats.CriticalDamagePercent.ToString("0.0") + "%</b>" +
                "\nPenetración<pos=64%><b>" + playerStats.ArmorPenetration.ToString("0.0") + "</b>" +
                "\nPoder mágico<pos=64%><b>+" + playerStats.MagicPowerPercent.ToString("0.0") + "%</b>" +
                "\nVel. de ataque<pos=64%><b>x" + playerStats.AttackSpeedMultiplier.ToString("0.00") + "</b>" +
                "\nAlcance arma<pos=64%><b>+" + playerStats.WeaponRangePercent.ToString("0.0") + "%</b>" +
                "\n\n<b><color=#D5B96B>SUPERVIVENCIA Y MOVIMIENTO</color></b>" +
                "\nVida máxima<pos=64%><b>" + playerStats.MaxHealth.ToString("0") + "</b>" +
                "\nEstamina máxima<pos=64%><b>" + playerStats.MaxStamina.ToString("0") + "</b>" +
                "\nRegeneración<pos=64%><b>" + playerStats.StaminaRegenPerSecond.ToString("0.0") + "/s</b>" +
                "\nMovimiento<pos=64%><b>x" + playerStats.MoveSpeedMultiplier.ToString("0.00") +
                " <color=#7F8A91>/ x3.00</color></b>" +
                "\n\n<b><color=#D5B96B>RECURSOS</color></b>" +
                "\nRunas<pos=64%><b>" + (inv != null ? inv.RuneCount() : 0) + "</b>" +
                "\nDiamantes<pos=64%><b>" + (inv != null ? inv.DiamondCount() : 0) + "</b>";

            foreach (KeyValuePair<string, TextMeshProUGUI> entry in skillButtonLabels)
            {
                if (entry.Value == null)
                    continue;
                string id = entry.Key;
                entry.Value.text =
                    "<b>" + skillNames[id] + "</b>  <color=#FFF0A6>Nv." +
                    playerStats.GetSkillLevel(id) + "</color>\n<size=82%>" +
                    skillDescriptions[id] + "</size>";
            }
        }

        if (summaryOnlyRefresh)
            return;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
        if (playerPreview != null && playerTransform != null)
            playerPreview.Show(playerTransform);

        RebuildEquipment(equipment);
        RebuildBag(inv);
        RefreshBagToolbar();
    }

    void RequestVisualRefresh()
    {
        if (suppressVisualRefreshEvents)
            return;
        if (suppressNextInventoryRefresh)
        {
            suppressNextInventoryRefresh = false;
            return;
        }
        if (isOpen)
            visualRefreshPending = true;
    }

    void RequestEquipmentRefresh()
    {
        if (suppressVisualRefreshEvents || !isOpen)
            return;

        if (CanRefreshEquipmentInPlace())
        {
            foreach (ItemType type in EquipmentSlots)
                RefreshEquipmentSlot(type);
            RefreshSummaryOnly();
            RefreshPlayerPreviewOnly();
            return;
        }

        visualRefreshPending = true;
    }

    void RefreshSummaryOnly()
    {
        if (!isOpen)
            return;

        summaryOnlyRefresh = true;
        try
        {
            Refresh();
        }
        finally
        {
            summaryOnlyRefresh = false;
        }
    }

    void RebuildEquipment(EquipmentManager equipment)
    {
        ClearChildren(equipmentGrid);
        foreach (ItemType type in EquipmentSlots)
        {
            if (!EquipAnchors.TryGetValue(type, out Rect anchor))
                continue; // no matching icon baked into the art for this type

            ItemInstance instance = equipment != null ? equipment.GetEquippedInstance(type) : null;
            System.Action onClick = instance != null ? () =>
            {
                UnequipToBag(type, instance);
            } : (System.Action)null;

            // No title label - the art already draws each slot's icon silhouette, so only the
            // equipped item's own name/preview needs to render on top of it.
            GameObject slotGO = CreateSlot(equipmentGrid, "", instance?.template, instance, 1, onClick, null, true, type, null, -1);
            RectTransform slotRect = slotGO.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(anchor.xMin, anchor.yMin);
            slotRect.anchorMax = new Vector2(anchor.xMax, anchor.yMax);
            slotRect.offsetMin = Vector2.zero;
            slotRect.offsetMax = Vector2.zero;
        }
    }

    bool CanRefreshEquipmentInPlace()
    {
        return isOpen && equipmentGrid != null && bagGrid != null;
    }

    void RefreshChangedEquipmentSlots(ItemType type, int bagIndex)
    {
        if (!CanRefreshEquipmentInPlace())
        {
            if (isOpen) visualRefreshPending = true;
            return;
        }

        RefreshEquipmentSlot(type);
        if (bagIndex >= 0)
            RefreshBagSlot(bagIndex);
        RefreshBagStatus();
        RefreshSummaryOnly();
        RefreshPlayerPreviewOnly();
    }

    void RefreshEquipmentSlot(ItemType type)
    {
        if (equipmentGrid == null || !EquipAnchors.TryGetValue(type, out Rect anchor))
            return;

        InventorySlotHandler oldHandler = null;
        foreach (InventorySlotHandler handler in
                 equipmentGrid.GetComponentsInChildren<InventorySlotHandler>(true))
        {
            if (handler != null && handler.gameObject.activeSelf &&
                handler.IsEquipmentSlot && handler.EquipType == type)
            {
                oldHandler = handler;
                break;
            }
        }

        ItemInstance instance = EquipmentManager.Instance?.GetEquippedInstance(type);
        System.Action onClick = instance != null
            ? () => UnequipToBag(type, instance)
            : (System.Action)null;
        GameObject replacement = CreateSlot(equipmentGrid, "", instance?.template,
            instance, 1, onClick, null, true, type, null, -1);
        RectTransform rect = replacement.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(anchor.xMin, anchor.yMin);
        rect.anchorMax = new Vector2(anchor.xMax, anchor.yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (oldHandler != null)
        {
            oldHandler.gameObject.SetActive(false);
            Destroy(oldHandler.gameObject);
        }
    }

    void RefreshBagSlot(int bagIndex)
    {
        if (bagGrid == null || bagIndex < 0 || bagIndex >= BagMax)
            return;

        InventorySlotHandler oldHandler = null;
        foreach (InventorySlotHandler handler in
                 bagGrid.GetComponentsInChildren<InventorySlotHandler>(true))
        {
            if (handler != null && handler.gameObject.activeSelf &&
                !handler.IsEquipmentSlot &&
                handler.BagIndex == bagIndex)
            {
                oldHandler = handler;
                break;
            }
        }

        InventoryManager inventory = InventoryManager.Instance;
        InventorySlot storedSlot = inventory != null && bagIndex < inventory.Slots.Count
            ? inventory.Slots[bagIndex]
            : null;
        InventorySlot slot = storedSlot?.item != null &&
                             MatchesBagFilter(storedSlot.item)
            ? storedSlot
            : null;
        ItemData item = slot?.item;
        System.Action onRightClick = item != null
            ? () => ShowActionPopup(slot)
            : (System.Action)null;
        GameObject replacement = CreateSlot(bagGrid, "", item, slot?.instance,
            slot != null ? slot.quantity : 1, null, onRightClick, false,
            default, slot, bagIndex);
        RectTransform rect = replacement.GetComponent<RectTransform>();
        if (oldHandler != null)
        {
            RectTransform oldRect = oldHandler.GetComponent<RectTransform>();
            rect.anchorMin = oldRect.anchorMin;
            rect.anchorMax = oldRect.anchorMax;
        }
        else
        {
            Rect anchor = BagSlotAnchor(bagIndex);
            rect.anchorMin = new Vector2(anchor.xMin, anchor.yMin);
            rect.anchorMax = new Vector2(anchor.xMax, anchor.yMax);
        }
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (oldHandler != null)
        {
            oldHandler.gameObject.SetActive(false);
            Destroy(oldHandler.gameObject);
        }
    }

    void RefreshBagStatus()
    {
        if (bagStatusText == null) return;
        InventoryManager inventory = InventoryManager.Instance;
        int occupied = inventory != null
            ? inventory.Slots.FindAll(slot => slot?.item != null).Count
            : 0;
        int visible = inventory != null
            ? inventory.Slots.FindAll(slot => slot?.item != null &&
                                             MatchesBagFilter(slot.item)).Count
            : 0;
        bagStatusText.text = visible + " visibles  |  " + occupied + "/" +
                             BagMax + " usados";
    }

    void RefreshPlayerPreviewOnly()
    {
        if (playerPreview != null && playerTransform != null)
            playerPreview.Show(playerTransform);
    }

    void UnequipToBag(ItemType type, ItemInstance instance)
    {
        if (instance == null) return;

        InventoryManager inventory = InventoryManager.Instance;
        suppressVisualRefreshEvents = true;
        try
        {
            EquipmentManager.Instance?.Unequip(instance);
            inventory?.AddItemInstance(instance);
        }
        finally
        {
            suppressVisualRefreshEvents = false;
        }

        int bagIndex = inventory != null
            ? inventory.Slots.FindIndex(slot => slot?.instance == instance)
            : -1;
        visualRefreshPending = false;
        RefreshChangedEquipmentSlots(type, bagIndex);
    }

    void RebuildBag(InventoryManager inv)
    {
        ClearChildren(bagGrid);

        var visible = new List<KeyValuePair<int, InventorySlot>>();
        var emptyIndices = new Queue<int>();
        if (inv != null)
        {
            for (int i = 0; i < BagMax; i++)
            {
                InventorySlot slot = i < inv.Slots.Count ? inv.Slots[i] : null;
                if (slot?.item == null)
                    emptyIndices.Enqueue(i);
                else if (MatchesBagFilter(slot.item))
                    visible.Add(new KeyValuePair<int, InventorySlot>(i, slot));
            }
        }

        visible.Sort((a, b) => CompareBagEntries(a, b));

        for (int visualIndex = 0; visualIndex < BagMax; visualIndex++)
        {
            InventorySlot slot = visualIndex < visible.Count ? visible[visualIndex].Value : null;
            int actualIndex = visualIndex < visible.Count
                ? visible[visualIndex].Key
                : (emptyIndices.Count > 0 ? emptyIndices.Dequeue() : -1);
            ItemData item = slot?.item;

            System.Action onRightClick = item != null ? () => ShowActionPopup(slot) : (System.Action)null;
            GameObject slotGO = CreateSlot(bagGrid, "", item, slot?.instance, slot != null ? slot.quantity : 1, null, onRightClick, false, default, slot, actualIndex);

            Rect anchor = BagSlotAnchor(visualIndex);
            RectTransform slotRect = slotGO.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(anchor.xMin, anchor.yMin);
            slotRect.anchorMax = new Vector2(anchor.xMax, anchor.yMax);
            slotRect.offsetMin = Vector2.zero;
            slotRect.offsetMax = Vector2.zero;
        }

        if (bagStatusText != null)
        {
            int occupied = inv != null ? inv.Slots.FindAll(s => s?.item != null).Count : 0;
            bagStatusText.text = visible.Count + " visibles  |  " + occupied + "/" + BagMax + " usados";
        }
    }

    bool MatchesBagFilter(ItemData item)
    {
        if (item == null || bagFilter == BagFilter.All) return item != null;
        return bagFilter switch
        {
            BagFilter.Consumables => item.itemType == ItemType.Consumable,
            BagFilter.Materials => item.itemType == ItemType.Rune || item.itemType == ItemType.Diamond,
            BagFilter.Equipment => item.itemType != ItemType.Consumable &&
                                   item.itemType != ItemType.Rune &&
                                   item.itemType != ItemType.Diamond,
            _ => true
        };
    }

    int CompareBagEntries(KeyValuePair<int, InventorySlot> a, KeyValuePair<int, InventorySlot> b)
    {
        if (bagSort == BagSort.Position) return a.Key.CompareTo(b.Key);
        if (bagSort == BagSort.Name)
            return string.Compare(a.Value.instance?.DisplayName ?? a.Value.item.itemName,
                b.Value.instance?.DisplayName ?? b.Value.item.itemName,
                System.StringComparison.CurrentCultureIgnoreCase);
        if (bagSort == BagSort.Rarity)
            return ((int)(b.Value.instance?.Rarity ?? ItemRarity.Common))
                .CompareTo((int)(a.Value.instance?.Rarity ?? ItemRarity.Common));

        float powerA = (a.Value.instance?.AttackBonus ?? 0f) +
                       (a.Value.instance?.DefenseBonus ?? 0f) +
                       (a.Value.instance?.SpeedBonus ?? 0f);
        float powerB = (b.Value.instance?.AttackBonus ?? 0f) +
                       (b.Value.instance?.DefenseBonus ?? 0f) +
                       (b.Value.instance?.SpeedBonus ?? 0f);
        return powerB.CompareTo(powerA);
    }

    void UseOrEquip(InventorySlot slot)
    {
        ItemData item = slot?.item;
        if (item == null) return;

        if (item.itemType == ItemType.Consumable)
        {
            if (item.healAmount > 0f)
                playerStats?.Heal(item.healAmount);
            if (item.staminaCostReductionPercent > 0f || item.staminaRegenBoostPercent > 0f)
                playerStats?.ApplyStaminaBuff(item.staminaCostReductionPercent, item.staminaRegenBoostPercent, item.buffDuration);
            InventoryManager.Instance?.RemoveItem(item, 1);
            Refresh();
        }
        else if (item.itemType != ItemType.Rune)
        {
            EquipFromSlot(slot);
        }
    }

    void EquipFromSlot(InventorySlot slot)
    {
        ItemData item = slot?.item;
        if (item == null) return;

        InventoryManager inventory = InventoryManager.Instance;
        int sourceIndex = inventory != null ? inventory.Slots.IndexOf(slot) : -1;
        ItemInstance previous = EquipmentManager.Instance?.GetEquippedInstance(item.itemType);
        ItemInstance toEquip = slot.instance ?? new ItemInstance(item);

        suppressVisualRefreshEvents = true;
        try
        {
            EquipmentManager.Instance?.Equip(toEquip);
            inventory?.RemoveSlot(slot);

            if (previous != null && previous != toEquip)
                inventory?.AddItemInstance(previous);
        }
        finally
        {
            suppressVisualRefreshEvents = false;
        }

        visualRefreshPending = false;
        RefreshChangedEquipmentSlots(item.itemType, sourceIndex);
    }

    // ── Right-click action popup (discard / upgrade) ─────────────────────
    void ShowActionPopup(InventorySlot slot)
    {
        if (slot?.item == null || actionPopup == null)
            return;

        actionTarget = slot;
        bool canUpgrade = slot.instance != null && slot.instance.upgradeLevel < ItemInstance.MaxUpgradeLevel;
        bool isRune = slot.item.itemType == ItemType.Rune;
        bool canEquip = InventorySlot.IsEquipType(slot.item.itemType);
        bool isQuestRedDiamond = slot.item.itemID == "red_diamond_quest" &&
            QuestManager.Instance != null &&
            QuestManager.Instance.MerchantIntroductionState ==
                PrimaryQuestState.SeventhUseRedDiamond;
        bool canUse = slot.item.itemType == ItemType.Consumable || isQuestRedDiamond;
        useButton.gameObject.SetActive(canUse);
        if (canUse)
            useButton.GetComponentInChildren<TextMeshProUGUI>().text =
                isQuestRedDiamond ? "Liberar hechizo Fireball" :
                slot.item.healAmount > 0f
                    ? "Curarse +" + Mathf.RoundToInt(slot.item.healAmount)
                    : "Usar poción";
        equipButton.gameObject.SetActive(canEquip);
        upgradeButton.gameObject.SetActive(canUpgrade || isRune);
        upgradeButton.GetComponentInChildren<TextMeshProUGUI>().text = isRune ? "Usar runa en arma equipada (+1%)" : "Mejorar (usa 1 runa)";
        splitButton.gameObject.SetActive(slot.item.stackable && slot.quantity > 1);

        actionText.text = (slot.instance != null ? slot.instance.DisplayName : slot.item.itemName) +
            (canUpgrade || isRune ? "\nRunas disponibles: " + (InventoryManager.Instance?.RuneCount() ?? 0) : "");
        actionPopup.SetActive(true);
    }

    void UseSelected()
    {
        if (actionTarget?.item == null) return;
        if (actionTarget.item.itemID == "red_diamond_quest")
        {
            if (QuestManager.Instance?.UseRedDiamondForFireball() == true)
            {
                actionTarget = null;
                actionPopup.SetActive(false);
                Refresh();
            }
            return;
        }
        if (actionTarget.item.itemType != ItemType.Consumable) return;
        if (actionTarget.item.healAmount > 0f && playerStats != null &&
            playerStats.CurrentHealth >= playerStats.MaxHealth - .01f &&
            actionTarget.item.staminaCostReductionPercent <= 0f &&
            actionTarget.item.staminaRegenBoostPercent <= 0f)
        {
            actionText.text = "Ya tienes toda la vida.";
            return;
        }

        InventorySlot used = actionTarget;
        actionTarget = null;
        actionPopup.SetActive(false);
        UseOrEquip(used);
    }

    void EquipSelected()
    {
        if (actionTarget?.item == null || !InventorySlot.IsEquipType(actionTarget.item.itemType))
            return;

        EquipFromSlot(actionTarget);
        actionTarget = null;
        actionPopup?.SetActive(false);
    }

    void UpgradeSelected()
    {
        if (actionTarget?.instance == null)
        {
            if (actionTarget?.item != null && actionTarget.item.itemType == ItemType.Rune)
            {
                ItemInstance weapon = EquipmentManager.Instance?.GetEquippedInstance(ItemType.Weapon);
                if (weapon == null)
                {
                    actionText.text = "Equipa un arma primero.";
                    return;
                }

                if (!(InventoryManager.Instance?.ConsumeSlotItem(actionTarget, 1) ?? false))
                {
                    actionText.text = "No tenes runas suficientes.";
                    return;
                }

                var runeResult = ItemUpgradeSystem.ApplyRunePercent(weapon, false);
                actionText.text = runeResult.message + "\nRuna directa: +1%.";
                actionPopup.SetActive(false);
                Refresh();
            }
            return;
        }

        if (!(InventoryManager.Instance?.ConsumeRune(1) ?? false))
        {
            actionText.text = "No tenes runas suficientes.";
            return;
        }

        var result = ItemUpgradeSystem.Upgrade(actionTarget.instance);
        actionText.text = result.message + "\nRunas disponibles: " + (InventoryManager.Instance?.RuneCount() ?? 0);
        bool canUpgrade = actionTarget.instance.upgradeLevel < ItemInstance.MaxUpgradeLevel;
        upgradeButton.gameObject.SetActive(canUpgrade);
        Refresh();
    }

    void DiscardSelected()
    {
        if (actionTarget != null)
            InventoryManager.Instance?.RemoveSlot(actionTarget);

        actionTarget = null;
        actionPopup?.SetActive(false);
        Refresh();
    }

    void BuildActionPopup()
    {
        actionPopup = new GameObject("ActionPopup");
        actionPopup.transform.SetParent(panel.transform, false);
        RectTransform rect = actionPopup.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.35f, 0.3f);
        rect.anchorMax = new Vector2(0.65f, 0.62f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        // Same fix as ItemTooltip/DragIcon: this is a direct sibling of the Equipo/Mochila/
        // Habilidades columns under panel's HorizontalLayoutGroup, so without ignoreLayout,
        // right-clicking an item to open this popup resizes the three real columns.
        LayoutElement actionPopupLayoutIgnore = actionPopup.AddComponent<LayoutElement>();
        actionPopupLayoutIgnore.ignoreLayout = true;
        Image bg = actionPopup.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.024f, 0.028f, 0.98f);

        VerticalLayoutGroup layout = actionPopup.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 10;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        actionText = CreateText(actionPopup.transform, "ActionText", "", 16, FontStyles.Bold);
        actionText.alignment = TextAlignmentOptions.Center;
        LayoutElement textLE = actionText.gameObject.AddComponent<LayoutElement>();
        textLE.preferredHeight = 80f;

        useButton = CreateButton(actionPopup.transform, "UseButton", "Usar poción");
        useButton.onClick.AddListener(UseSelected);

        equipButton = CreateButton(actionPopup.transform, "EquipButton", "Equipar");
        equipButton.onClick.AddListener(EquipSelected);

        upgradeButton = CreateButton(actionPopup.transform, "UpgradeButton", "Mejorar (usa 1 runa)");
        upgradeButton.onClick.AddListener(UpgradeSelected);

        splitButton = CreateButton(actionPopup.transform, "SplitButton", "Dividir cantidad");
        splitButton.onClick.AddListener(() => ShowSplitDialog(actionTarget));

        Button discardButton = CreateButton(actionPopup.transform, "DiscardButton", "Eliminar");
        discardButton.onClick.AddListener(DiscardSelected);

        Button cancelButton = CreateButton(actionPopup.transform, "CancelButton", "Cancelar");
        cancelButton.onClick.AddListener(() => actionPopup.SetActive(false));
        actionPopup.SetActive(false);
    }

    // ── Split a stack: pick a quantity, then it rides the cursor until placed ─────────
    void ShowSplitDialog(InventorySlot slot)
    {
        if (slot?.item == null || !slot.item.stackable || slot.quantity <= 1)
            return;

        BuildSplitDialog();
        actionPopup?.SetActive(false);
        splitTargetSlot = slot;
        splitSlider.minValue = 1;
        splitSlider.maxValue = slot.quantity - 1;
        splitSlider.wholeNumbers = true;
        splitSlider.value = 1;
        UpdateSplitDialogText();
        splitDialog.SetActive(true);
    }

    void UpdateSplitDialogText()
    {
        if (splitDialogText == null || splitTargetSlot == null)
            return;
        splitDialogText.text = splitTargetSlot.item.itemName + "\nCantidad a separar: " +
            (int)splitSlider.value + " / " + splitTargetSlot.quantity;
    }

    void ConfirmSplit()
    {
        if (splitTargetSlot == null)
        {
            splitDialog.SetActive(false);
            return;
        }

        int qty = (int)splitSlider.value;
        ItemData item = splitTargetSlot.item;
        if (qty <= 0 || qty >= splitTargetSlot.quantity ||
            !(InventoryManager.Instance?.ConsumeSlotItem(splitTargetSlot, qty) ?? false))
        {
            splitDialog.SetActive(false);
            splitTargetSlot = null;
            return;
        }

        heldSplitItem = item;
        heldSplitQuantity = qty;
        holdingSplitStack = true;
        Vector2 screenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        BeginSplitDrag(item, qty, screenPos);

        splitDialog.SetActive(false);
        splitTargetSlot = null;
        Refresh();
    }

    void CancelSplitDialog()
    {
        splitDialog.SetActive(false);
        splitTargetSlot = null;
    }

    // Cancelling (or failing to find a valid drop slot) returns the split-off quantity to the
    // bag via the normal auto-placement path rather than losing it - the player already paid
    // the "pick it up" cost once (ConsumeSlotItem), it shouldn't vanish if they change their mind.
    void CancelHeldSplit()
    {
        if (heldSplitItem != null && heldSplitQuantity > 0)
            InventoryManager.Instance?.AddItem(heldSplitItem, heldSplitQuantity);
        holdingSplitStack = false;
        heldSplitItem = null;
        heldSplitQuantity = 0;
        EndDrag();
        Refresh();
    }

    void TryPlaceHeldSplit(Vector2 screenPosition)
    {
        if (!holdingSplitStack)
            return;

        int targetIndex = FindBagIndexUnderPointer(screenPosition);
        bool placed = targetIndex >= 0 &&
            (InventoryManager.Instance?.PlaceAtIndex(heldSplitItem, heldSplitQuantity, targetIndex) ?? false);

        if (!placed)
            InventoryManager.Instance?.AddItem(heldSplitItem, heldSplitQuantity);

        holdingSplitStack = false;
        heldSplitItem = null;
        heldSplitQuantity = 0;
        EndDrag();
        Refresh();
    }

    int FindBagIndexUnderPointer(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return -1;

        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        foreach (RaycastResult hit in results)
        {
            InventorySlotHandler handler = hit.gameObject.GetComponentInParent<InventorySlotHandler>();
            if (handler != null && !handler.IsEquipmentSlot)
                return handler.BagIndex;
        }
        return -1;
    }

    void BuildSplitDialog()
    {
        if (splitDialog != null)
            return;

        splitDialog = new GameObject("SplitDialog");
        splitDialog.transform.SetParent(panel.transform, false);
        RectTransform rect = splitDialog.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.32f, 0.35f);
        rect.anchorMax = new Vector2(0.68f, 0.6f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        LayoutElement dialogLayoutIgnore = splitDialog.AddComponent<LayoutElement>();
        dialogLayoutIgnore.ignoreLayout = true;
        Image bg = splitDialog.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.024f, 0.028f, 0.98f);

        VerticalLayoutGroup layout = splitDialog.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 10;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        splitDialogText = CreateText(splitDialog.transform, "SplitText", "", 16, FontStyles.Bold);
        splitDialogText.alignment = TextAlignmentOptions.Center;
        LayoutElement textLE = splitDialogText.gameObject.AddComponent<LayoutElement>();
        textLE.preferredHeight = 50f;

        GameObject sliderGo = new GameObject("SplitSlider");
        sliderGo.transform.SetParent(splitDialog.transform, false);
        splitSlider = sliderGo.AddComponent<Slider>();
        LayoutElement sliderLE = sliderGo.AddComponent<LayoutElement>();
        sliderLE.preferredHeight = 24f;
        Image sliderBg = sliderGo.AddComponent<Image>();
        sliderBg.color = new Color(0.16f, 0.18f, 0.19f, 1f);
        splitSlider.targetGraphic = sliderBg;

        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sliderGo.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(5, 0);
        fillAreaRect.offsetMax = new Vector2(-5, 0);
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.92f, 0.8f, 0.45f);
        splitSlider.fillRect = fillRect;

        GameObject handleArea = new GameObject("HandleSlideArea");
        handleArea.transform.SetParent(sliderGo.transform, false);
        RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(5, 0);
        handleAreaRect.offsetMax = new Vector2(-5, 0);
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(16, 0);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;
        splitSlider.handleRect = handleRect;
        splitSlider.direction = Slider.Direction.LeftToRight;
        splitSlider.onValueChanged.AddListener(_ => UpdateSplitDialogText());

        Button accept = CreateButton(splitDialog.transform, "SplitAccept", "Aceptar");
        accept.onClick.AddListener(ConfirmSplit);

        Button cancel = CreateButton(splitDialog.transform, "SplitCancel", "Cancelar");
        cancel.onClick.AddListener(CancelSplitDialog);

        splitDialog.SetActive(false);
    }

    // ── Hover tooltip ──────────────────────────────────────────────────
    void BuildTooltip()
    {
        tooltip = new GameObject("ItemTooltip");
        Canvas tooltipCanvas = FindOrCreateTooltipCanvas();
        tooltip.transform.SetParent(tooltipCanvas.transform, false);
        tooltipRect = tooltip.AddComponent<RectTransform>();
        // AddComponent<RectTransform>() defaults to a top-left anchor (0,1), not center - but
        // ShowTooltip()'s screen-to-local math assumes the anchor reference point IS the parent's
        // center (matching what ScreenPointToLocalPointInRectangle returns), so without this the
        // tooltip lands hundreds of pixels off from the cursor instead of just above it.
        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0.5f, 0f);
        // Wide enough for full upgraded-item descriptions without stacking narrow wrapped lines.
        tooltipRect.sizeDelta = new Vector2(370f, 190f);

        LayoutElement tooltipLayoutIgnore = tooltip.AddComponent<LayoutElement>();
        tooltipLayoutIgnore.ignoreLayout = true;

        Image bg = tooltip.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.024f, 0.03f, 0.88f);
        bg.raycastTarget = false;

        VerticalLayoutGroup layout = tooltip.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 13, 13);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = tooltip.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        tooltipText = CreateText(tooltip.transform, "TooltipText", "", 15, FontStyles.Normal);
        tooltipText.raycastTarget = false;

        tooltip.SetActive(false);
    }

    public void ShowTooltip(ItemData item, ItemInstance instance, Vector2 screenPosition)
    {
        if (item == null)
            return;
        BuildIfNeeded();
        if (tooltip == null || tooltipText == null)
            return;

        string content = instance != null ? instance.BuildTooltip() : BuildPlainTooltip(item);
        content += BuildEquipmentComparison(item, instance);
        tooltipText.text = content;
        Color rarityColor = instance != null ? ItemRarityUtil.GetColor(instance.Rarity) : Color.white;
        tooltipText.color = rarityColor;

        tooltip.SetActive(true);
        tooltip.transform.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
        PositionTooltipAt(screenPosition);
    }

    static string BuildEquipmentComparison(ItemData item, ItemInstance candidate)
    {
        if (item == null || item.itemType == ItemType.Consumable ||
            item.itemType == ItemType.Rune || item.itemType == ItemType.Diamond)
            return "";

        ItemInstance equipped = EquipmentManager.Instance?.GetEquippedInstance(item.itemType);
        if (equipped == null || equipped == candidate)
            return "";

        float candidateAttack = candidate != null ? candidate.AttackBonus : item.attackBonus;
        float candidateDefense = candidate != null ? candidate.DefenseBonus : item.defenseBonus;
        float candidateSpeed = candidate != null ? candidate.SpeedBonus : item.speedBonus;
        if (item.weaponData != null)
            candidateAttack += item.weaponData.baseDamage;

        float equippedAttack = equipped.AttackBonus +
            (equipped.template?.weaponData != null
                ? equipped.template.weaponData.baseDamage : 0f);
        float attackDelta = candidateAttack - equippedAttack;
        float defenseDelta = candidateDefense - equipped.DefenseBonus;
        float speedDelta = candidateSpeed - equipped.SpeedBonus;

        return "\n\n<color=#D5B96B><b>COMPARADO CON EQUIPADO</b></color>" +
               BuildComparisonLine("Ataque", attackDelta) +
               BuildComparisonLine("Defensa", defenseDelta) +
               BuildComparisonLine("Velocidad", speedDelta);
    }

    static string BuildComparisonLine(string label, float delta)
    {
        string color = delta > .001f ? "#68D77A" : delta < -.001f ? "#E76B62" : "#AFA897";
        string sign = delta > .001f ? "+" : "";
        return "\n" + label + ": <color=" + color + "><b>" + sign +
               delta.ToString("0.##") + "</b></color>";
    }

    // Called from ShowTooltip on hover-enter AND every frame from Update() while the tooltip is
    // visible, so it tracks the cursor continuously (Mu/Diablo-style) instead of snapping into
    // place once and staying put while the mouse keeps moving over the same item.
    void PositionTooltipAt(Vector2 screenPosition)
    {
        if (tooltip == null)
            return;

        Vector2 desired = screenPosition + new Vector2(0f, 24f);
        Vector2 renderedSize = Vector2.Scale(tooltipRect.rect.size, tooltipRect.lossyScale);
        float halfWidth = renderedSize.x * 0.5f;
        desired.x = Mathf.Clamp(desired.x, halfWidth + 8f, Screen.width - halfWidth - 8f);
        desired.y = Mathf.Clamp(desired.y, 8f, Mathf.Max(8f, Screen.height - renderedSize.y - 8f));
        tooltip.transform.position = desired;
    }

    public void HideTooltip()
    {
        if (tooltip != null)
            tooltip.SetActive(false);
    }

    static Canvas FindOrCreateTooltipCanvas()
    {
        GameObject existing = GameObject.Find("ItemTooltipOverlayCanvas");
        if (existing != null && existing.TryGetComponent(out Canvas found))
        {
            // Re-assert sorting even on a reused canvas - if a stale instance ever persisted
            // with a different sortingOrder (e.g. from an older build), silently reusing it as-is
            // would let the tooltip render behind other UI indefinitely with no obvious cause.
            found.overrideSorting = true;
            found.sortingOrder = short.MaxValue - 1;
            return found;
        }

        GameObject go = new GameObject("ItemTooltipOverlayCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        // One below the drag canvas so an active drag always draws over a lingering tooltip
        // instead of an undefined tie-break between two canvases at the identical sortingOrder.
        canvas.sortingOrder = short.MaxValue - 1;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        go.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(go);
        return canvas;
    }

    static Canvas FindOrCreateDragCanvas()
    {
        GameObject existing = GameObject.Find("ItemDragOverlayCanvas");
        if (existing != null && existing.TryGetComponent(out Canvas found))
        {
            // Re-assert sorting even on a reused canvas - see FindOrCreateTooltipCanvas for why.
            found.overrideSorting = true;
            found.sortingOrder = short.MaxValue;
            return found;
        }

        GameObject go = new GameObject("ItemDragOverlayCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        go.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(go);
        return canvas;
    }

    static string BuildPlainTooltip(ItemData item)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(item.itemName);
        if (item.attackBonus > 0f) sb.Append("\nAtaque: +").Append(item.attackBonus.ToString("0.0"));
        if (item.defenseBonus > 0f) sb.Append("\nDefensa: +").Append(item.defenseBonus.ToString("0.0"));
        if (item.speedBonus > 0f) sb.Append("\nVelocidad: +").Append(item.speedBonus.ToString("0.00"));
        if (item.healAmount > 0f) sb.Append("\nCura: +").Append(item.healAmount.ToString("0"));
        if (item.staminaCostReductionPercent > 0f) sb.Append("\n-").Append(item.staminaCostReductionPercent.ToString("0")).Append("% costo de estamina (").Append(item.buffDuration.ToString("0")).Append("s)");
        if (item.staminaRegenBoostPercent > 0f) sb.Append("\n+").Append(item.staminaRegenBoostPercent.ToString("0")).Append("% regeneracion de estamina (").Append(item.buffDuration.ToString("0")).Append("s)");
        if (!string.IsNullOrEmpty(item.description)) sb.Append("\n").Append(item.description);
        return sb.ToString();
    }

    // ── Drag and drop ─────────────────────────────────────────────────
    void BuildDragIcon()
    {
        dragIcon = new GameObject("DragIcon");
        Canvas overlayCanvas = FindOrCreateDragCanvas();
        dragIcon.transform.SetParent(overlayCanvas.transform, false);
        RectTransform rect = dragIcon.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(72f, 72f);
        // Same fix as ItemTooltip: this is a direct sibling of the Equipo/Mochila/Habilidades
        // columns under panel's HorizontalLayoutGroup, so without ignoreLayout, activating it
        // mid-drag makes the layout group treat it as a 4th column and resize the real ones.
        LayoutElement dragLayoutIgnore = dragIcon.AddComponent<LayoutElement>();
        dragLayoutIgnore.ignoreLayout = true;
        dragIconImage = dragIcon.AddComponent<RawImage>();
        dragIconImage.raycastTarget = false;
        dragIconImage.color = Color.white;
        dragItemPreview = dragIcon.AddComponent<RuntimeItemPreviewUI>();

        dragFallbackText = CreateText(dragIcon.transform, "FallbackItemName", "", 11, FontStyles.Bold);
        dragFallbackText.alignment = TextAlignmentOptions.Center;
        dragFallbackText.enableAutoSizing = true;
        dragFallbackText.fontSizeMin = 8f;
        dragFallbackText.fontSizeMax = 12f;
        dragFallbackText.textWrappingMode = TextWrappingModes.Normal;
        dragFallbackText.raycastTarget = false;
        RectTransform fallbackRect = dragFallbackText.rectTransform;
        fallbackRect.anchorMin = Vector2.zero;
        fallbackRect.anchorMax = Vector2.one;
        fallbackRect.offsetMin = new Vector2(4f, 4f);
        fallbackRect.offsetMax = new Vector2(-4f, -4f);
        dragFallbackText.gameObject.SetActive(false);

        dragQuantityBadge = new GameObject("SplitQuantityBadge");
        dragQuantityBadge.transform.SetParent(dragIcon.transform, false);
        Image badgeBackground = dragQuantityBadge.AddComponent<Image>();
        badgeBackground.color = new Color(0.03f, 0.025f, 0.02f, 0.94f);
        badgeBackground.raycastTarget = false;
        RectTransform badgeRect = dragQuantityBadge.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.52f, 0f);
        badgeRect.anchorMax = new Vector2(1f, 0.38f);
        badgeRect.offsetMin = Vector2.zero;
        badgeRect.offsetMax = Vector2.zero;

        dragQuantityText = CreateText(dragQuantityBadge.transform, "Quantity", "", 17, FontStyles.Bold);
        dragQuantityText.alignment = TextAlignmentOptions.Center;
        dragQuantityText.color = new Color(1f, 0.86f, 0.32f, 1f);
        dragQuantityText.textWrappingMode = TextWrappingModes.NoWrap;
        dragQuantityText.raycastTarget = false;
        RectTransform quantityRect = dragQuantityText.rectTransform;
        quantityRect.anchorMin = Vector2.zero;
        quantityRect.anchorMax = Vector2.one;
        quantityRect.offsetMin = Vector2.zero;
        quantityRect.offsetMax = Vector2.zero;
        dragQuantityBadge.SetActive(false);

        dragIcon.SetActive(false);
    }

    public void BeginDrag(Texture texture, Vector2 screenPosition)
    {
        draggingInventoryItem = true;
        dragItemPreview?.ClearPreview();
        dragIconImage.texture = texture;
        // RawImage draws a solid quad when texture is null - a plain white box for items with
        // no 3D preview and no static icon. Fade it out instead of showing that.
        dragIconImage.color = texture != null ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 1f, 1f, 0f);
        dragIconImage.enabled = texture != null;
        dragQuantityBadge?.SetActive(false);
        dragFallbackText?.gameObject.SetActive(false);
        dragIcon.transform.position = screenPosition;
        dragIcon.transform.SetAsLastSibling();
        dragIcon.SetActive(true);
    }

    void BeginSplitDrag(ItemData item, int quantity, Vector2 screenPosition)
    {
        draggingInventoryItem = true;
        dragIcon.transform.position = screenPosition;
        dragIcon.transform.SetAsLastSibling();
        dragIcon.SetActive(true);

        bool hasVisual = ItemHasPreview(item);
        dragFallbackText.gameObject.SetActive(!hasVisual);
        dragFallbackText.text = !hasVisual && item != null ? item.itemName : "";
        if (hasVisual)
            dragItemPreview.Show(item, 2.3f);
        else
        {
            dragItemPreview.ClearPreview();
            dragIconImage.enabled = false;
        }

        dragQuantityText.text = "x" + Mathf.Max(1, quantity);
        dragQuantityBadge.SetActive(true);
        dragQuantityBadge.transform.SetAsLastSibling();
    }

    public void UpdateDrag(Vector2 screenPosition)
    {
        if (dragIcon != null && dragIcon.activeSelf)
            dragIcon.transform.position = screenPosition;
    }

    public void EndDrag()
    {
        draggingInventoryItem = false;
        dragItemPreview?.ClearPreview();
        dragQuantityBadge?.SetActive(false);
        dragFallbackText?.gameObject.SetActive(false);
        dragIcon?.SetActive(false);
    }

    public void HandleDrop(InventorySlotHandler source, InventorySlotHandler target)
    {
        if (source == null || target == null || source == target)
            return;

        if (!source.IsEquipmentSlot && !target.IsEquipmentSlot)
        {
            bool canSwapVisualsDirectly = bagFilter == BagFilter.All &&
                                          bagSort == BagSort.Position &&
                                          source.BagIndex >= 0 &&
                                          target.BagIndex >= 0;
            if (canSwapVisualsDirectly)
                suppressNextInventoryRefresh = true;
            bool moved = InventoryManager.Instance?.MoveSlotToIndex(
                source.BagSlot, target.BagIndex) ?? false;
            if (moved && canSwapVisualsDirectly)
                SwapSlotVisualPositions(source, target);
            else if (!moved)
                suppressNextInventoryRefresh = false;
            return;
        }

        if (source.IsEquipmentSlot == target.IsEquipmentSlot)
            return;

        if (source.IsEquipmentSlot)
        {
            ItemInstance instance = EquipmentManager.Instance?.GetEquippedInstance(source.EquipType);
            if (instance == null) return;
            UnequipToBag(source.EquipType, instance);
        }
        else
        {
            InventorySlot slot = source.BagSlot;
            if (slot?.item == null || slot.item.itemType != target.EquipType)
                return;
            EquipFromSlot(slot);
        }

    }

    static void SwapSlotVisualPositions(InventorySlotHandler source,
        InventorySlotHandler target)
    {
        RectTransform a = source.GetComponent<RectTransform>();
        RectTransform b = target.GetComponent<RectTransform>();
        Vector2 aMin = a.anchorMin;
        Vector2 aMax = a.anchorMax;
        Vector2 bMin = b.anchorMin;
        Vector2 bMax = b.anchorMax;
        a.anchorMin = bMin;
        a.anchorMax = bMax;
        b.anchorMin = aMin;
        b.anchorMax = aMax;
        int index = source.BagIndex;
        source.BagIndex = target.BagIndex;
        target.BagIndex = index;
    }

    public void HandleDrop(ChestSlotHandler source, InventorySlotHandler target)
    {
        if (source == null || target == null || source.Slot == null)
            return;

        if (target.IsEquipmentSlot)
        {
            if (source.Slot.item == null || source.Slot.item.itemType != target.EquipType)
                return;
            if (source.IsChestSlot && ChestUI.Instance?.CurrentChest != null)
            {
                InventorySlot slot = source.Slot;
                if (slot.instance != null)
                    InventoryManager.Instance?.AddItemInstance(slot.instance);
                else
                    InventoryManager.Instance?.AddItem(slot.item, slot.quantity);
                ChestUI.Instance.CurrentChest.RemoveSlot(slot);
                EquipFromSlot(FindSlotForItemInstance(slot.instance, slot.item));
            }
        }
        else
        {
            if (source.IsChestSlot && ChestUI.Instance?.CurrentChest != null)
            {
                InventorySlot slot = source.Slot;
                bool added = slot.instance != null
                    ? InventoryManager.Instance != null && InventoryManager.Instance.AddItemInstance(slot.instance)
                    : InventoryManager.Instance != null && InventoryManager.Instance.AddItem(slot.item, slot.quantity);
                if (added)
                    ChestUI.Instance.CurrentChest.RemoveSlot(slot);
            }
        }

        Refresh();
    }

    InventorySlot FindSlotForItemInstance(ItemInstance instance, ItemData fallbackItem)
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null)
            return null;

        for (int i = 0; i < inv.Slots.Count; i++)
        {
            InventorySlot slot = inv.Slots[i];
            if (slot == null)
                continue;
            if (instance != null && slot.instance == instance)
                return slot;
            if (instance == null && fallbackItem != null && slot.item == fallbackItem)
                return slot;
        }
        return null;
    }

    GameObject CreateSlot(Transform parent, string title, ItemData item, ItemInstance instance, int quantity,
        System.Action onClick, System.Action onRightClick, bool isEquipmentSlot, ItemType equipType, InventorySlot bagSlot, int bagIndex)
    {
        GameObject go = new GameObject("Slot");
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        ItemRarity rarity = instance != null ? instance.Rarity : ItemRarity.Common;
        // Fully transparent - the baked art behind this slot already draws the socket border, so
        // this Image only exists as the Button's raycast target/targetGraphic.
        image.color = new Color(0f, 0f, 0f, 0f);

        if (isEquipmentSlot)
            CreateEquipmentSlotPrint(go.transform, equipType, item == null);

        if (instance != null && rarity != ItemRarity.Common)
        {
            GameObject border = new GameObject("RarityBorder");
            border.transform.SetParent(go.transform, false);
            border.transform.SetAsFirstSibling();
            Image borderImage = border.AddComponent<Image>();
            borderImage.color = ItemRarityUtil.GetColor(rarity);
            borderImage.raycastTarget = false;
            RectTransform borderRect = border.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-3, -3);
            borderRect.offsetMax = new Vector2(3, 3);
        }

        bool hasPreview = ItemHasPreview(item);
        RawImage previewIcon = null;
        if (hasPreview)
        {
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            RawImage iconImage = iconGO.AddComponent<RawImage>();
            iconImage.raycastTarget = false;
            RuntimeItemPreviewUI preview = iconGO.AddComponent<RuntimeItemPreviewUI>();
            preview.Show(item);
            previewIcon = iconImage;
            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            // Icon fills almost the whole slot now - the item name no longer reserves a text
            // strip beneath it (only a small quantity badge overlays the corner instead).
            iconRect.anchorMin = new Vector2(0f, 0.04f);
            iconRect.anchorMax = new Vector2(1f, 0.98f);
            iconRect.offsetMin = new Vector2(4, 0);
            iconRect.offsetMax = new Vector2(-4, 0);
        }

        // When there's a preview icon, the slot's own art + icon already identify the item - the
        // full name only needs to show up in the hover tooltip (ShowTooltip), not crammed as
        // wrapping text under a small icon where it just overlaps neighboring slots. Only the
        // stack count still needs to render here.
        string displayName = instance != null ? instance.DisplayName : (item != null ? item.itemName : "");
        string label;
        if (item == null)
            label = title;
        else if (hasPreview)
            label = quantity > 1 ? "x" + quantity : "";
        else
            label = (string.IsNullOrEmpty(title) ? "" : title + "\n") + displayName + (quantity > 1 ? " x" + quantity : "");

        TextMeshProUGUI text = CreateText(go.transform, "Text", label, hasPreview ? 13 : 12, FontStyles.Normal);
        text.alignment = hasPreview ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = !hasPreview;
        if (!hasPreview) text.fontSizeMin = 8f;
        if (instance != null)
            text.color = ItemRarityUtil.GetColor(rarity);
        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, hasPreview ? 1f : 1f);
        rect.offsetMin = new Vector2(4, 3);
        rect.offsetMax = new Vector2(-4, -3);

        if (onClick != null)
        {
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
        }

        InventorySlotHandler handler = go.AddComponent<InventorySlotHandler>();
        handler.Owner = this;
        handler.OnRightClick = onRightClick;
        handler.IsEquipmentSlot = isEquipmentSlot;
        handler.EquipType = equipType;
        handler.BagSlot = bagSlot;
        handler.BagIndex = bagIndex;
        handler.ItemRef = item;
        handler.InstanceRef = instance;
        handler.IconImage = previewIcon;

        return go;
    }

    // Reuses the exact engraved equipment drawings from InventoryGridBake instead of placing
    // unrelated modern icons over the medieval panel. Each crop is an independent, non-raycast
    // layer, so it remains aligned if the outer inventory window grows or shrinks.
    static void CreateEquipmentSlotPrint(Transform parent, ItemType type, bool empty)
    {
        Sprite art = FullArtSprite;
        if (art == null || art.texture == null ||
            !EquipAnchors.TryGetValue(type, out Rect source))
            return;

        GameObject printObject = new GameObject("EquipmentPrint_" + type,
            typeof(RectTransform), typeof(RawImage));
        printObject.transform.SetParent(parent, false);
        printObject.transform.SetAsFirstSibling();
        RectTransform rect = printObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.055f, .055f);
        rect.anchorMax = new Vector2(.945f, .945f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        Rect textureRect = art.textureRect;
        Texture texture = art.texture;
        Rect uv = new Rect(
            (textureRect.x + source.xMin * textureRect.width) / texture.width,
            (textureRect.y + source.yMin * textureRect.height) / texture.height,
            source.width * textureRect.width / texture.width,
            source.height * textureRect.height / texture.height);

        RawImage print = printObject.GetComponent<RawImage>();
        print.texture = texture;
        print.uvRect = uv;
        print.raycastTarget = false;
        // Empty sockets clearly show their embossed purpose. Once equipped, the print remains
        // only as a quiet watermark behind the real 3D item preview.
        print.color = empty
            ? new Color(1f, .91f, .62f, .48f)
            : new Color(.82f, .74f, .52f, .13f);
    }

    // Lets other panels (CraftingTableUI) show the player's real bag slots - with working
    // drag/drop, tooltips and right-click actions - without duplicating CreateSlot's rarity
    // border/preview icon/quantity label logic in a second place that would drift out of sync.
    public GameObject CreateBagSlotVisual(Transform parent, InventorySlot slot, int index)
    {
        ItemData item = slot?.item;
        System.Action onRightClick = item != null ? () => ShowActionPopup(slot) : (System.Action)null;
        return CreateSlot(parent, "", item, slot?.instance, slot != null ? slot.quantity : 1, null, onRightClick, false, default, slot, index);
    }

    public static bool ItemHasPreview(ItemData item)
    {
        if (item == null)
            return false;
        if (item.icon != null)
            return true;
        if (item.equipmentPrefab != null)
            return true;
        if (item.itemType == ItemType.Weapon)
            return item.weaponData != null && item.weaponData.weaponPrefab != null;
        return item.itemType == ItemType.Helmet ||
            item.itemType == ItemType.Chest ||
            item.itemType == ItemType.Gloves ||
            item.itemType == ItemType.Legs ||
            item.itemType == ItemType.Boots;
    }

    // The panel/canvas get rebuilt fresh in whatever scene is active (they don't survive scene
    // loads), and SpawnVillage only works today because it happens to have a hand-placed
    // EventSystem already. Procedurally generated scenes like dungeon_1 don't, so without this
    // the panel renders but every Button silently ignores clicks - there's nothing to route them.
    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    // FindAnyObjectByType<Canvas>() can race with SceneLoader's Awake() (order between
    // independently-instantiated root GameObjects isn't guaranteed) and grab the fade
    // transition's Canvas instead of the HUD's. That canvas carries a CanvasGroup that sits at
    // alpha 0 outside of scene transitions, so this panel would build itself invisibly - "open"
    // (isOpen=true) but never actually seen. Skip any canvas driven by a CanvasGroup. Also
    // requires ScreenSpaceOverlay: EnemyHealthBar creates one small WorldSpace canvas per enemy
    // (scaled for a health bar, floating 2.1m above that enemy, destroyed with it), and once
    // several enemies exist those vastly outnumber the real HUD canvas - grabbing one of those
    // instead builds this panel as an invisible speck attached to a random monster's head.
    // Public (not internal) so Editor/*.cs setup scripts in the separate editor assembly can
    // reuse the same lookup instead of duplicating the fragile FindAnyObjectByType<Canvas>() call.
    public static Canvas FindReusableCanvas()
    {
        foreach (Canvas candidate in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (candidate.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;
            if (!candidate.enabled || !candidate.gameObject.activeInHierarchy)
                continue;
            if (candidate.transform.parent != null &&
                candidate.transform.parent.GetComponentInParent<Canvas>() != null)
                continue;
            if (candidate.GetComponentInParent<NewGameMainMenu>(true) != null)
                continue;
            if (candidate.GetComponent<CanvasGroup>() == null)
                return candidate;
        }
        return null;
    }

    static void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            // Destroy runs at the end of the frame. Hide outgoing slots immediately so the
            // old and new grids cannot overlap and flash as if the whole inventory reloaded.
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }
}

public class InventorySlotHandler : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public static InventorySlotHandler HoveredSlot { get; private set; }
    public InventoryUI Owner;
    public System.Action OnRightClick;
    public bool IsEquipmentSlot;
    public ItemType EquipType;
    public InventorySlot BagSlot;
    public int BagIndex;
    public ItemData ItemRef;
    public ItemInstance InstanceRef;
    public RawImage IconImage;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            OnRightClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        HoveredSlot = this;
        if (ItemRef != null)
            Owner?.ShowTooltip(ItemRef, InstanceRef, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (HoveredSlot == this) HoveredSlot = null;
        Owner?.HideTooltip();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (ItemRef == null)
            return;

        Owner?.HideTooltip();
        Texture texture = IconImage != null ? IconImage.texture : (ItemRef.icon != null ? ItemRef.icon.texture : null);
        Owner?.BeginDrag(texture, eventData.position);
    }

    public void OnDrag(PointerEventData eventData) => Owner?.UpdateDrag(eventData.position);

    public void OnEndDrag(PointerEventData eventData) => Owner?.EndDrag();

    public void OnDrop(PointerEventData eventData)
    {
        CraftingResultDragHandler craftingResult = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<CraftingResultDragHandler>() : null;
        if (craftingResult != null)
        {
            craftingResult.Owner?.TakeResultToInventory(this);
            return;
        }

        InventorySlotHandler source = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<InventorySlotHandler>() : null;
        if (source != null)
            Owner?.HandleDrop(source, this);
        else
            Owner?.HandleDrop(eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<ChestSlotHandler>() : null, this);
    }
}

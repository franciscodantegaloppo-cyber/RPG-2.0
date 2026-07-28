using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// An 8x4 chest panel next to the player's bag, so items can be clicked back and forth.
// Follows the same generated-UI / open-close-pause pattern as InventoryUI and BlacksmithShopPanel.
public class ChestUI : MonoBehaviour
{
    static ChestUI _instance;
    public static ChestUI Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<ChestUI>(FindObjectsInactive.Include);
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureChestUI()
    {
        if (FindAnyObjectByType<ChestUI>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("RuntimeChestUI");
        go.AddComponent<ChestUI>();
        Object.DontDestroyOnLoad(go);
    }

    const int BagColumns = 8;
    const int BagRows = 6;
    const int BagMax = BagColumns * BagRows;

    // Both baked whole from their 3D source assets, same approach as InventoryUI's art - the
    // chest's own grid uses "Grid panel chest" (8x4, matches ChestContainer.Columns/Rows
    // exactly), and the mirrored player-bag column reuses a 3-row crop of the same inventory
    // grid art InventoryUI uses, so the two dialogs read as the same visual language.
    static Sprite _chestArtSprite;
    static Sprite ChestArtSprite
    {
        get
        {
            if (_chestArtSprite == null)
                _chestArtSprite = Resources.Load<Sprite>("UI/ChestGridBake");
            return _chestArtSprite;
        }
    }

    static Sprite _bagArtSprite;
    static Sprite BagArtSprite
    {
        get
        {
            if (_bagArtSprite == null)
            {
                // Use the same complete 8x6 bag artwork as InventoryUI. The previous
                // InventoryBagOnly asset was an obsolete 8x3 crop while this screen still
                // generated 10x3 slots, so neither its count nor its columns could align.
                Sprite inventoryArt = Resources.Load<Sprite>("UI/InventoryGridBake");
                if (inventoryArt != null)
                {
                    Rect source = inventoryArt.rect;
                    Rect bag = InventoryBagArtAnchor;
                    Rect crop = new Rect(
                        source.x + source.width * bag.xMin,
                        source.y + source.height * bag.yMin,
                        source.width * bag.width,
                        source.height * bag.height);
                    _bagArtSprite = Sprite.Create(inventoryArt.texture, crop, new Vector2(0.5f, 0.5f),
                        inventoryArt.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    _bagArtSprite.name = "InventoryBag8x6_RuntimeCrop";
                }

                if (_bagArtSprite == null)
                    _bagArtSprite = Resources.Load<Sprite>("UI/InventoryBagOnly");
            }
            return _bagArtSprite;
        }
    }

    // Fractional anchor bounds of the chest art's own grid within ChestGridBake.png (measured
    // by detecting the bevelled grid-line pixels directly, same method as InventoryUI's anchors).
    static readonly Rect ChestGridArtAnchor = Rect.MinMaxRect(0.0361f, 0.0348f, 0.9644f, 0.9689f);
    // Exact 8x6 bag bounds inside InventoryGridBake, shared with InventoryUI.
    static readonly Rect InventoryBagArtAnchor = Rect.MinMaxRect(0.0986f, 0.0852f, 0.8955f, 0.4701f);

    // ChestGridBake is a perspective render: its eight columns are intentionally not equal.
    // These boundaries were measured from the actual bevel centres in the imported image and
    // are normalized inside ChestGridArtAnchor (left-to-right / bottom-to-top).
    static readonly float[] ChestColumnBounds =
    {
        0f, 0.1336f, 0.2493f, 0.3772f, 0.4997f, 0.6228f, 0.7528f, 0.8685f, 1f
    };
    static readonly float[] ChestRowBounds =
    {
        0f, 0.2631f, 0.5101f, 0.7520f, 1f
    };

    GameObject panel;
    TextMeshProUGUI titleText;
    Transform chestGrid;
    Transform bagGrid;

    ChestContainer currentChest;
    public ChestContainer CurrentChest => currentChest;
    public bool IsOpen { get; private set; }
    public static bool IsAnyOpen => _instance != null && _instance.IsOpen;
    float openedAt;
    static Sprite muPanelFrame;
    static Sprite muBannerFrame;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        if (!IsOpen) return;
        if (Time.unscaledTime - openedAt < 0.15f) return;

        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            Close();
    }

    public void Open(ChestContainer chest)
    {
        if (chest == null) return;

        if (IsOpen && currentChest == chest)
        {
            Close();
            return;
        }

        if (IsOpen)
            Close();

        currentChest = chest;
        BuildIfNeeded();
        titleText.text = chest.ChestName;

        currentChest.OnChanged += Refresh;
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += Refresh;

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        IsOpen = true;
        openedAt = Time.unscaledTime;
        Refresh();
        GameManager.Instance?.PauseGameplay();
        ReleaseCursor();
    }

    public void Close()
    {
        if (!IsOpen) return;

        if (currentChest != null)
            currentChest.OnChanged -= Refresh;
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= Refresh;

        currentChest = null;
        panel?.SetActive(false);
        IsOpen = false;
        GameManager.Instance?.ResumeGameplay();
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
        if (panel != null) return;

        EnsureEventSystem();

        Canvas canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("ChestCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        panel = new GameObject("ChestPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.02f, 0.03f);
        rect.anchorMax = new Vector2(0.98f, 0.97f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.035f, 0.04f, 0.045f, 0.94f);
        Canvas priorityCanvas = panel.AddComponent<Canvas>();
        priorityCanvas.overrideSorting = true;
        priorityCanvas.sortingOrder = 2200;
        panel.AddComponent<GraphicRaycaster>();
        AddMuFrame(panel.transform, false, Vector2.zero, Vector2.one, true);

        HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 64, 22);
        layout.spacing = 18;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        RectTransform chestColumn = CreateArtColumn(panel.transform, 1.6f);
        RectTransform bagColumn = CreateArtColumn(panel.transform, 1f);

        chestGrid = BuildArtPanel(chestColumn, ChestArtSprite, ChestGridArtAnchor, "Cofre", out titleText);
        bagGrid = BuildArtPanel(bagColumn, BagArtSprite, new Rect(0f, 0f, 1f, 1f), "Mochila", out _);

        SharpUIWindowChrome.Attach(panel, "COFRE E INVENTARIO", Close);
        panel.SetActive(false);
    }

    // Plain anchored RectTransform, NOT a VerticalLayoutGroup - a LayoutGroup fighting with
    // AspectRatioFitter for control of the same child's size/position leaves it with the right
    // width but badly wrong vertical placement (confirmed live: the art ended up positioned
    // mostly below the column's bottom edge). Title gets a fixed top strip via anchors instead.
    RectTransform CreateArtColumn(Transform parent, float flexibleWidth)
    {
        GameObject go = new GameObject("ArtColumn");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = flexibleWidth;
        return rect;
    }

    // Builds one "art background + overlaid slot grid" panel: a title strip, an Image showing
    // the whole baked sprite (aspect-locked via AspectRatioFitter, point-anchored so the fitter
    // doesn't fight a stretched/layout-driven anchor for control of its size), and a
    // GridLayoutGroup anchored to gridAnchor's fraction of the art for the actual slots.
    Transform BuildArtPanel(Transform column, Sprite art, Rect gridAnchor, string titleLabel, out TextMeshProUGUI title)
    {
        AddMuFrame(column, true, new Vector2(0f, 0.945f), Vector2.one, false);
        title = CreateTitle(column, titleLabel);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.945f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        GameObject artArea = new GameObject("ArtArea");
        artArea.transform.SetParent(column, false);
        RectTransform artAreaRect = artArea.AddComponent<RectTransform>();
        artAreaRect.anchorMin = new Vector2(0f, 0f);
        artAreaRect.anchorMax = new Vector2(1f, 0.93f);
        artAreaRect.offsetMin = Vector2.zero;
        artAreaRect.offsetMax = Vector2.zero;

        GameObject artGO = new GameObject("Art");
        artGO.transform.SetParent(artArea.transform, false);
        RectTransform artRect = artGO.AddComponent<RectTransform>();
        artRect.anchorMin = new Vector2(0.5f, 0.5f);
        artRect.anchorMax = new Vector2(0.5f, 0.5f);
        artRect.pivot = new Vector2(0.5f, 0.5f);
        artRect.anchoredPosition = Vector2.zero;
        Image artImage = artGO.AddComponent<Image>();
        artImage.sprite = art;
        AspectRatioFitter fitter = artGO.AddComponent<AspectRatioFitter>();
        // FitInParent (contain), not EnvelopeParent (cover) - see InventoryUI.cs's BuildIfNeeded
        // for why: cover mode overflows the narrower dimension past the container's edges.
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        if (art != null)
            fitter.aspectRatio = art.rect.width / art.rect.height;

        GameObject gridGO = new GameObject("SlotGrid");
        gridGO.transform.SetParent(artGO.transform, false);
        RectTransform gridRect = gridGO.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(gridAnchor.xMin, gridAnchor.yMin);
        gridRect.anchorMax = new Vector2(gridAnchor.xMax, gridAnchor.yMax);
        gridRect.offsetMin = Vector2.zero;
        gridRect.offsetMax = Vector2.zero;
        return gridGO.transform;
    }

    static void AddMuFrame(Transform parent, bool banner, Vector2 anchorMin, Vector2 anchorMax, bool ignoreLayout)
    {
        Sprite sprite;
        if (banner)
        {
            if (muBannerFrame == null)
                muBannerFrame = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
            sprite = muBannerFrame;
        }
        else
        {
            if (muPanelFrame == null)
                muPanelFrame = Resources.Load<Sprite>("UI/SharpUI/Panel");
            sprite = muPanelFrame;
        }

        if (sprite == null)
            return;

        GameObject frame = new GameObject(banner ? "MU_TitleFrame" : "MU_ChestPanelFrame");
        frame.transform.SetParent(parent, false);
        RectTransform rect = frame.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = frame.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(1f, 1f, 1f, 0.94f);
        image.raycastTarget = false;

        if (ignoreLayout)
        {
            LayoutElement layout = frame.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }
    }

    TextMeshProUGUI CreateTitle(Transform parent, string text)
    {
        GameObject go = new GameObject(text + "Title");
        go.transform.SetParent(parent, false);
        TextMeshProUGUI title = go.AddComponent<TextMeshProUGUI>();
        title.text = text;
        title.fontSize = 22;
        title.fontStyle = FontStyles.Bold;
        title.color = Color.white;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = 32f;
        le.preferredHeight = 32f;
        le.flexibleHeight = 0f;
        return title;
    }

    void Refresh()
    {
        if (panel == null || !IsOpen) return;

        ClearChildren(chestGrid);
        if (currentChest != null)
        {
            for (int i = 0; i < ChestContainer.MaxSlots; i++)
            {
                InventorySlot slot = i < currentChest.Slots.Count ? currentChest.Slots[i] : null;
                CreateSlot(chestGrid, slot, true, i);
            }
        }

        ClearChildren(bagGrid);
        InventoryManager inv = InventoryManager.Instance;
        for (int i = 0; i < BagMax; i++)
        {
            InventorySlot slot = inv != null && i < inv.Slots.Count ? inv.Slots[i] : null;
            CreateSlot(bagGrid, slot, false, i);
        }
    }

    void CreateSlot(Transform parent, InventorySlot slot, bool isChestSlot, int slotIndex)
    {
        ItemData item = slot?.item;

        GameObject go = new GameObject("Slot");
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        RectTransform slotRect = go.GetComponent<RectTransform>();
        Rect anchor = isChestSlot ? ChestSlotAnchor(slotIndex) : BagSlotAnchor(slotIndex);
        slotRect.anchorMin = anchor.min;
        slotRect.anchorMax = anchor.max;
        slotRect.offsetMin = Vector2.zero;
        slotRect.offsetMax = Vector2.zero;
        ItemRarity rarity = slot?.instance != null ? slot.instance.Rarity : ItemRarity.Common;
        // Fully transparent - the baked art behind this slot already draws the socket border, so
        // this Image only exists as the Button's raycast target/targetGraphic.
        image.color = new Color(0f, 0f, 0f, 0f);

        if (slot?.instance != null && rarity != ItemRarity.Common)
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

        bool hasPreview = InventoryUI.ItemHasPreview(item);
        if (hasPreview)
        {
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            RawImage iconImage = iconGO.AddComponent<RawImage>();
            iconImage.raycastTarget = false;
            iconGO.AddComponent<RuntimeItemPreviewUI>().Show(item);
            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            // Icon fills almost the whole slot - the name no longer reserves a text strip
            // beneath it (only a small quantity badge overlays the corner instead), matching
            // InventoryUI's slots.
            iconRect.anchorMin = new Vector2(0f, 0.04f);
            iconRect.anchorMax = new Vector2(1f, 0.98f);
            iconRect.offsetMin = new Vector2(4, 0);
            iconRect.offsetMax = new Vector2(-4, 0);
        }

        if (item != null)
        {
            // Full name only when there's no preview icon to identify the item by - otherwise
            // just the stack count, since the name is redundant with the hover tooltip and was
            // overlapping/wrapping across neighboring slots at this size.
            string label = hasPreview
                ? (slot.quantity > 1 ? "x" + slot.quantity : "")
                : (slot.instance != null ? slot.instance.DisplayName : item.itemName) + (slot.quantity > 1 ? " x" + slot.quantity : "");
            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = hasPreview ? 11 : 11;
            text.alignment = hasPreview ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = !hasPreview;
            if (!hasPreview) text.fontSizeMin = 7f;
            text.color = slot.instance != null ? ItemRarityUtil.GetColor(rarity) : Color.white;
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(3, 3);
            textRect.offsetMax = new Vector2(-3, -3);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Transfer(slot, isChestSlot));
        }

        ChestSlotHandler handler = go.AddComponent<ChestSlotHandler>();
        handler.Owner = this;
        handler.ItemRef = item;
        handler.InstanceRef = slot?.instance;
        handler.Slot = slot;
        handler.IsChestSlot = isChestSlot;
        handler.SlotIndex = slotIndex;
        handler.IconImage = go.GetComponentInChildren<RawImage>();
    }

    static Rect ChestSlotAnchor(int index)
    {
        int column = index % ChestContainer.Columns;
        int rowFromTop = index / ChestContainer.Columns;
        int bottomBoundary = ChestContainer.Rows - rowFromTop - 1;
        return Rect.MinMaxRect(
            ChestColumnBounds[column],
            ChestRowBounds[bottomBoundary],
            ChestColumnBounds[column + 1],
            ChestRowBounds[bottomBoundary + 1]);
    }

    static Rect BagSlotAnchor(int index)
    {
        int column = index % BagColumns;
        int rowFromTop = index / BagColumns;
        float width = 1f / BagColumns;
        float height = 1f / BagRows;
        float xMin = column * width;
        float yMax = 1f - rowFromTop * height;
        return Rect.MinMaxRect(xMin, yMax - height, xMin + width, yMax);
    }

    void Transfer(InventorySlot slot, bool fromChest)
    {
        if (slot?.item == null) return;

        if (fromChest)
        {
            if (currentChest == null) return;
            bool added = slot.instance != null
                ? InventoryManager.Instance != null && InventoryManager.Instance.AddItemInstance(slot.instance)
                : InventoryManager.Instance != null && InventoryManager.Instance.AddItem(slot.item, slot.quantity);
            if (!added) return;
            currentChest.RemoveSlot(slot);
        }
        else
        {
            if (currentChest == null || !currentChest.HasRoom && slot.instance == null) return;
            bool added = slot.instance != null
                ? currentChest.TryAddInstance(slot.instance)
                : currentChest.TryAddItem(slot.item, slot.quantity);
            if (!added) return;
            InventoryManager.Instance?.RemoveSlot(slot);
        }

        Refresh();
    }

    public void HandleDrop(Object sourceHandler, ChestSlotHandler target)
    {
        if (target == null)
            return;

        if (sourceHandler is ChestSlotHandler chestSource)
        {
            if (chestSource.Slot == null || chestSource == target)
                return;

            if (chestSource.IsChestSlot == target.IsChestSlot)
            {
                if (target.IsChestSlot)
                    currentChest?.MoveSlotToIndex(chestSource.Slot, target.SlotIndex);
                else
                    InventoryManager.Instance?.MoveSlotToIndex(chestSource.Slot, target.SlotIndex);
            }
            else
            {
                Transfer(chestSource.Slot, chestSource.IsChestSlot);
            }
        }
        else if (sourceHandler is InventorySlotHandler invSource)
        {
            if (invSource.BagSlot == null)
                return;

            if (target.IsChestSlot)
                Transfer(invSource.BagSlot, false);
            else
                InventoryManager.Instance?.MoveSlotToIndex(invSource.BagSlot, target.SlotIndex);
        }

        Refresh();
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}

public class ChestSlotHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public ChestUI Owner;
    public ItemData ItemRef;
    public ItemInstance InstanceRef;
    public InventorySlot Slot;
    public bool IsChestSlot;
    public int SlotIndex;
    public RawImage IconImage;

    // Chest panel has no tooltip of its own - it reuses InventoryUI's single floating tooltip
    // (which already opens right alongside this panel) instead of maintaining a second,
    // independent implementation.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ItemRef != null)
            InventoryUI.Instance?.ShowTooltip(ItemRef, InstanceRef, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData) => InventoryUI.Instance?.HideTooltip();

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (ItemRef == null)
            return;
        InventoryUI.Instance?.HideTooltip();
        Texture texture = IconImage != null ? IconImage.texture : (ItemRef.icon != null ? ItemRef.icon.texture : null);
        InventoryUI.Instance?.BeginDrag(texture, eventData.position);
    }

    public void OnDrag(PointerEventData eventData) => InventoryUI.Instance?.UpdateDrag(eventData.position);
    public void OnEndDrag(PointerEventData eventData) => InventoryUI.Instance?.EndDrag();

    public void OnDrop(PointerEventData eventData)
    {
        Object source = null;
        if (eventData.pointerDrag != null)
            source = eventData.pointerDrag.GetComponent<ChestSlotHandler>() as Object ??
                     eventData.pointerDrag.GetComponent<InventorySlotHandler>() as Object;
        Owner?.HandleDrop(source, this);
    }
}

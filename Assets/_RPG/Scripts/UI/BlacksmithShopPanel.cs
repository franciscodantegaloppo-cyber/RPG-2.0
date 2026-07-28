using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class ShopEntry
{
    public ItemData item;
    public int price;
}

public class BlacksmithShopPanel : MonoBehaviour
{
    static BlacksmithShopPanel _instance;
    public static BlacksmithShopPanel Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<BlacksmithShopPanel>(FindObjectsInactive.Include);
            return _instance;
        }
    }

    [Header("Legacy UI References")]
    [SerializeField] GameObject panel;
    [SerializeField] Transform itemListParent;
    [SerializeField] GameObject itemRowPrefab;
    [SerializeField] TextMeshProUGUI goldText;
    [SerializeField] TextMeshProUGUI feedbackText;
    [SerializeField] Button closeButton;

    readonly List<ShopEntry> visibleEntries = new List<ShopEntry>();
    readonly List<Button> rowButtons = new List<Button>();

    Action onClose;
    ShopEntry selectedEntry;
    RectTransform generatedRoot;
    RawImage previewImage;
    TextMeshProUGUI titleText;
    TextMeshProUGUI selectedNameText;
    TextMeshProUGUI selectedStatsText;
    TextMeshProUGUI selectedPriceText;
    TextMeshProUGUI selectedDescriptionText;
    TextMeshProUGUI confirmTitleText;
    TextMeshProUGUI confirmDetailsText;
    TextMeshProUGUI confirmPriceText;
    GameObject confirmPopup;
    Button buyButton;
    Button confirmButton;
    WeaponPreviewRenderer previewRenderer;
    ScrollRect catalogScroll;
    bool isOpen;
    float openedAt;
    public bool IsOpen => isOpen;

    static readonly Color auctionBg = new Color(0.094f, 0.063f, 0.039f, 0.98f);
    static readonly Color rowBg = new Color(0.13f, 0.105f, 0.075f, 0.96f);
    static readonly Color rowSelected = new Color(0.34f, 0.17f, 0.055f, 1f);
    static readonly Color gold = new Color(1f, 0.78f, 0.24f, 1f);
    static readonly Color redAccent = new Color(0.455f, 0f, 0f, 1f);
    static Sprite muPanelFrame;
    static Sprite muBannerFrame;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        if (panel == null) panel = gameObject;
        closeButton?.onClick.AddListener(Close);
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnGoldChanged += RefreshGold;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnGoldChanged -= RefreshGold;
        previewRenderer?.Dispose();
    }

    void Update()
    {
        if (!isOpen) return;

        previewRenderer?.Tick();

        if (Time.unscaledTime - openedAt < 0.15f) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            Close();
    }

    public void Show(List<ShopEntry> entries, Action onCloseCallback = null)
    {
        onClose = onCloseCallback;
        if (panel == null) panel = gameObject;

        EnsureBuilt();
        panel.SetActive(true);
        isOpen = true;
        openedAt = Time.unscaledTime;

        SetFeedback("");
        visibleEntries.Clear();
        visibleEntries.AddRange(CreateRuntimeCatalog());
        if (entries != null && entries.Count > 0)
            visibleEntries.AddRange(entries);

        for (int i = 0; i < visibleEntries.Count; i++)
            visibleEntries[i].price = GetPrice(visibleEntries[i]);

        visibleEntries.Sort((a, b) => GetPrice(a).CompareTo(GetPrice(b)));
        BuildCatalog();
        SelectEntry(visibleEntries.Count > 0 ? visibleEntries[0] : null);
        RefreshGold();
        GameManager.Instance?.PauseGameplay();
        ReleaseCursor();
    }

    public void Close()
    {
        if (!isOpen && panel != null && !panel.activeSelf) return;

        confirmPopup?.SetActive(false);
        panel?.SetActive(false);
        previewRenderer?.ClearItem();
        isOpen = false;
        GameManager.Instance?.ResumeGameplay();
        LockCursor();
        onClose?.Invoke();
        onClose = null;
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

    void EnsureBuilt()
    {
        if (generatedRoot != null && previewRenderer != null) return;
        if (generatedRoot != null)
            Destroy(generatedRoot.gameObject);

        if (panel == null)
            panel = gameObject;

        if (panel.GetComponent<RectTransform>() == null)
        {
            var fixedPanel = new GameObject(panel.name + "_UI", typeof(RectTransform));
            fixedPanel.transform.SetParent(panel.transform.parent, false);
            fixedPanel.SetActive(panel.activeSelf);
            panel.SetActive(false);
            panel = fixedPanel;
        }

        foreach (Transform child in panel.transform)
            Destroy(child.gameObject);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.08f, 0.08f);
        panelRect.anchorMax = new Vector2(0.92f, 0.92f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelImage = panel.GetComponent<Image>() ?? panel.AddComponent<Image>();
        panelImage.sprite = null;
        panelImage.type = Image.Type.Simple;
        panelImage.color = auctionBg;
        AddMuFrameOverlay(panel.transform, false);

        generatedRoot = CreateRect(panel.transform, "AuctionHouseBlacksmithUI", Vector2.zero, Vector2.one);
        AddImage(generatedRoot.gameObject, new Color(0.02f, 0.015f, 0.012f, 0.25f));

        titleText = CreateText(generatedRoot, "Title", "HERRERIA", 28, FontStyles.Bold, gold,
            new Vector2(0.03f, 0.89f), new Vector2(0.5f, 0.98f), TextAlignmentOptions.MidlineLeft);

        goldText = CreateText(generatedRoot, "GoldText", "Oro: 0", 18, FontStyles.Bold, gold,
            new Vector2(0.68f, 0.89f), new Vector2(0.9f, 0.98f), TextAlignmentOptions.MidlineRight);

        closeButton = CreateButton(generatedRoot, "CloseButton", "X",
            new Vector2(0.92f, 0.9f), new Vector2(0.98f, 0.98f), redAccent);
        closeButton.onClick.AddListener(Close);

        var listFrame = CreateRect(generatedRoot, "CatalogFrame", new Vector2(0.03f, 0.12f), new Vector2(0.44f, 0.86f));
        AddImage(listFrame.gameObject, new Color(0.055f, 0.04f, 0.03f, 0.95f));
        CreateText(listFrame, "CatalogTitle", "Catalogo por precio", 15, FontStyles.Bold, Color.white,
            new Vector2(0.04f, 0.91f), new Vector2(0.95f, 0.99f), TextAlignmentOptions.MidlineLeft);

        RectTransform viewport = CreateRect(listFrame, "CatalogViewport", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.89f));
        AddImage(viewport.gameObject, new Color(0.02f, 0.015f, 0.012f, 0.35f));
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        itemListParent = CreateRect(viewport, "CatalogRows", new Vector2(0f, 1f), new Vector2(1f, 1f));
        RectTransform contentRect = itemListParent.GetComponent<RectTransform>();
        contentRect.pivot = new Vector2(0.5f, 1f);
        var listLayout = itemListParent.gameObject.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 8;
        listLayout.padding = new RectOffset(4, 4, 4, 4);
        listLayout.childControlHeight = false;
        listLayout.childControlWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childForceExpandWidth = true;
        ContentSizeFitter fitter = itemListParent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        catalogScroll = listFrame.gameObject.AddComponent<ScrollRect>();
        catalogScroll.viewport = viewport;
        catalogScroll.content = contentRect;
        catalogScroll.horizontal = false;
        catalogScroll.vertical = true;
        catalogScroll.scrollSensitivity = 36f;

        var previewFrame = CreateRect(generatedRoot, "PreviewFrame", new Vector2(0.47f, 0.35f), new Vector2(0.97f, 0.86f));
        AddImage(previewFrame.gameObject, new Color(0.03f, 0.025f, 0.02f, 0.98f));
        var previewImageRect = CreateRect(previewFrame, "PreviewImage", new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
        previewImage = previewImageRect.gameObject.AddComponent<RawImage>();
        previewImage.color = Color.white;
        previewRenderer = new WeaponPreviewRenderer(previewImage);

        var infoFrame = CreateRect(generatedRoot, "InfoFrame", new Vector2(0.47f, 0.12f), new Vector2(0.97f, 0.32f));
        AddImage(infoFrame.gameObject, new Color(0.055f, 0.04f, 0.03f, 0.95f));
        selectedNameText = CreateText(infoFrame, "SelectedName", "", 20, FontStyles.Bold, gold,
            new Vector2(0.04f, 0.66f), new Vector2(0.58f, 0.95f), TextAlignmentOptions.MidlineLeft);
        selectedStatsText = CreateText(infoFrame, "SelectedStats", "", 16, FontStyles.Bold, new Color(0.75f, 0.95f, 0.68f),
            new Vector2(0.04f, 0.42f), new Vector2(0.58f, 0.66f), TextAlignmentOptions.MidlineLeft);
        selectedDescriptionText = CreateText(infoFrame, "SelectedDescription", "", 13, FontStyles.Normal, new Color(0.86f, 0.8f, 0.7f),
            new Vector2(0.04f, 0.08f), new Vector2(0.62f, 0.42f), TextAlignmentOptions.TopLeft);
        selectedPriceText = CreateText(infoFrame, "SelectedPrice", "", 21, FontStyles.Bold, gold,
            new Vector2(0.63f, 0.48f), new Vector2(0.96f, 0.95f), TextAlignmentOptions.MidlineRight);
        buyButton = CreateButton(infoFrame, "BuyButton", "Comprar",
            new Vector2(0.66f, 0.11f), new Vector2(0.96f, 0.42f), new Color(0.12f, 0.42f, 0.15f));
        buyButton.onClick.AddListener(OpenConfirm);

        feedbackText = CreateText(generatedRoot, "FeedbackText", "", 14, FontStyles.Bold, new Color(0.8f, 1f, 0.65f),
            new Vector2(0.03f, 0.03f), new Vector2(0.64f, 0.1f), TextAlignmentOptions.MidlineLeft);

        BuildConfirmPopup();
        SharpUIWindowChrome.Attach(panel, "HERRERÍA", Close);
    }

    void BuildCatalog()
    {
        foreach (Transform child in itemListParent)
            Destroy(child.gameObject);
        rowButtons.Clear();

        foreach (var entry in visibleEntries)
        {
            var row = CreateRect(itemListParent, "ShopRow", Vector2.zero, Vector2.one);
            row.sizeDelta = new Vector2(0f, 72f);
            var image = AddImage(row.gameObject, rowBg);
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 72f;

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            rowButtons.Add(button);

            var captured = entry;
            button.onClick.AddListener(() => SelectEntry(captured));

            CreateText(row, "Name", GetItemName(entry), 15, FontStyles.Bold, Color.white,
                new Vector2(0.04f, 0.48f), new Vector2(0.58f, 0.94f), TextAlignmentOptions.MidlineLeft);
            CreateText(row, "Power", $"{GetItemTypeLabel(entry.item)}  |  Poder {GetPower(entry):0}", 13, FontStyles.Bold, new Color(0.75f, 0.95f, 0.68f),
                new Vector2(0.04f, 0.08f), new Vector2(0.45f, 0.48f), TextAlignmentOptions.MidlineLeft);
            CreateText(row, "Price", $"{GetPrice(entry)} monedas", 14, FontStyles.Bold, gold,
                new Vector2(0.48f, 0.08f), new Vector2(0.96f, 0.94f), TextAlignmentOptions.MidlineRight);

            var lockText = CreateText(row, "Lock", CanAfford(entry) ? "" : "BLOQUEADO", 11, FontStyles.Bold, new Color(1f, 0.45f, 0.35f),
                new Vector2(0.58f, 0.56f), new Vector2(0.96f, 0.9f), TextAlignmentOptions.MidlineRight);
            lockText.raycastTarget = false;
        }
    }

    void SelectEntry(ShopEntry entry)
    {
        selectedEntry = entry;

        for (int i = 0; i < rowButtons.Count; i++)
        {
            var image = rowButtons[i].GetComponent<Image>();
            if (image != null)
                image.color = visibleEntries[i] == selectedEntry ? rowSelected : rowBg;
        }

        if (entry == null)
        {
            selectedNameText.text = "Sin objetos";
            selectedStatsText.text = "";
            selectedPriceText.text = "";
            selectedDescriptionText.text = "El herrero no tiene catalogo cargado.";
            buyButton.interactable = false;
            previewRenderer?.ClearItem();
            return;
        }

        selectedNameText.text = GetItemName(entry);
        selectedStatsText.text = GetItemTypeLabel(entry.item) + $"  |  Poder {GetPower(entry):0}  |  ATK +{GetAttack(entry):0}  DEF +{GetDefense(entry):0}";
        selectedPriceText.text = $"{GetPrice(entry)} monedas";
        selectedDescriptionText.text = string.IsNullOrWhiteSpace(entry.item.description)
            ? "Arma forjada para aventureros. El precio sube con el poder del arma."
            : entry.item.description;

        buyButton.interactable = CanAfford(entry);
        SetFeedback(CanAfford(entry) ? "" : "No tenes suficientes monedas para este objeto.");
        previewRenderer?.Show(entry.item);
    }

    void OpenConfirm()
    {
        if (selectedEntry == null) return;

        confirmTitleText.text = "Confirmar compra";
        confirmDetailsText.text = $"{GetItemName(selectedEntry)}\nPoder {GetPower(selectedEntry):0}";
        confirmPriceText.text = $"{GetPrice(selectedEntry)} monedas";
        confirmButton.interactable = CanAfford(selectedEntry);
        confirmPopup.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(confirmButton.gameObject);
    }

    void TryBuySelected()
    {
        if (selectedEntry == null) return;

        var inv = InventoryManager.Instance;
        if (inv == null) return;

        int price = GetPrice(selectedEntry);
        if (!inv.SpendGold(price))
        {
            confirmPopup.SetActive(false);
            SetFeedback("No tenes suficientes monedas.");
            SelectEntry(selectedEntry);
            BuildCatalog();
            return;
        }

        if (!inv.AddItem(selectedEntry.item))
        {
            inv.AddGold(price);
            confirmPopup.SetActive(false);
            SetFeedback("Inventario lleno.");
            return;
        }

        confirmPopup.SetActive(false);
        EquipPurchasedItem(selectedEntry.item);
        SetFeedback($"Compraste {GetItemName(selectedEntry)}.");
        RefreshGold();
        BuildCatalog();
        SelectEntry(selectedEntry);
    }

    void EquipPurchasedItem(ItemData item)
    {
        if (item == null || item.itemType == ItemType.Consumable)
            return;

        EquipmentManager equipment = EquipmentManager.Instance;
        InventoryManager inventory = InventoryManager.Instance;
        if (equipment == null || inventory == null)
            return;

        ItemInstance previous = equipment.GetEquippedInstance(item.itemType);
        equipment.Equip(item);
        inventory.RemoveItem(item, 1);
        if (previous != null && previous.template != item)
            inventory.AddItemInstance(previous);
    }

    void BuildConfirmPopup()
    {
        confirmPopup = CreateRect(generatedRoot, "ConfirmPopup", Vector2.zero, Vector2.one).gameObject;
        // Full-screen dimmer is not a box; only the actual confirmation box receives a frame.
        AddImage(confirmPopup, new Color(0f, 0f, 0f, 0.62f), false);

        var box = CreateRect(confirmPopup.transform, "ConfirmBox", new Vector2(0.31f, 0.28f), new Vector2(0.69f, 0.72f));
        AddImage(box.gameObject, auctionBg);
        confirmTitleText = CreateText(box, "Title", "Confirmar compra", 22, FontStyles.Bold, gold,
            new Vector2(0.07f, 0.75f), new Vector2(0.93f, 0.94f), TextAlignmentOptions.Center);
        confirmDetailsText = CreateText(box, "Details", "", 16, FontStyles.Bold, Color.white,
            new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.72f), TextAlignmentOptions.Center);
        confirmPriceText = CreateText(box, "Price", "", 18, FontStyles.Bold, gold,
            new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.42f), TextAlignmentOptions.Center);

        confirmButton = CreateButton(box, "ConfirmButton", "Confirmar",
            new Vector2(0.08f, 0.08f), new Vector2(0.47f, 0.23f), new Color(0.12f, 0.42f, 0.15f));
        confirmButton.onClick.AddListener(TryBuySelected);

        var cancelButton = CreateButton(box, "CancelButton", "Cancelar",
            new Vector2(0.53f, 0.08f), new Vector2(0.92f, 0.23f), redAccent);
        cancelButton.onClick.AddListener(() => confirmPopup.SetActive(false));

        confirmPopup.SetActive(false);
    }

    void RefreshGold()
    {
        if (goldText && InventoryManager.Instance != null)
            goldText.text = $"Monedas: {InventoryManager.Instance.Gold}";

        if (isOpen && visibleEntries.Count > 0)
        {
            BuildCatalog();
            SelectEntry(selectedEntry);
        }
    }

    void SetFeedback(string msg)
    {
        if (feedbackText) feedbackText.text = msg;
    }

    public static List<ShopEntry> CreateRuntimeCatalog()
    {
        return new List<ShopEntry>
        {
            CreateRuntimeWeapon("free_1h_sword_1_1", "Espada Oxidada", 14f, 0.62f, "Hoja corta y liviana del pack low poly.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 1 COLOR 1.prefab"),
            CreateRuntimeWeapon("free_1h_sword_1_2", "Espada de Guardia", 17f, 0.58f, "Una espada simple para pelear rapido.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 1 COLOR 2.prefab"),
            CreateRuntimeWeapon("free_1h_sword_1_3", "Espada Vieja", 20f, 0.6f, "Buen filo sin demasiado peso.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 1 COLOR 3.prefab"),
            CreateRuntimeWeapon("free_1h_sword_2_1", "Sable del Bosque", 24f, 0.66f, "Mas alcance sin perder velocidad.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 2 COLOR 1.prefab"),
            CreateRuntimeWeapon("free_1h_sword_2_2", "Sable Oscuro", 28f, 0.68f, "Una hoja que le queda bien a un aventurero serio.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 2 COLOR 2.prefab"),
            CreateRuntimeWeapon("free_1h_sword_2_3", "Sable de Mercenario", 32f, 0.7f, "Ligera, cara y confiable.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 2 COLOR 3.prefab"),
            CreateRuntimeWeapon("free_1h_sword_3_1", "Espada de Hueso", 36f, 0.74f, "Hoja perfecta para enemigos esqueleticos.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 3 COLOR 1.prefab"),
            CreateRuntimeWeapon("free_1h_sword_3_2", "Espada Negra", 42f, 0.78f, "Mas pesada, con buen dano base.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 3 COLOR 2.prefab"),
            CreateRuntimeWeapon("free_1h_sword_3_3", "Espada de Campeon", 48f, 0.82f, "La mejor espada de una mano del lote.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 3 COLOR 3.prefab"),
            CreateRuntimeWeapon("free_great_sword_1_1", "Mandoble Ligero", 56f, 1.02f, "Mandoble del pack low poly.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/GREAT SWORDS/FREE GREAT SWORD 1 COLOR 1.prefab"),
            CreateRuntimeWeapon("free_great_sword_2_1", "Mandoble del Herrero", 64f, 1.08f, "Golpea lento, pero pega fuerte.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/GREAT SWORDS/FREE GREAT SWORD 2 COLOR 1.prefab"),
            CreateRuntimeWeapon("free_great_sword_3_2", "Claymore Negra", 76f, 1.16f, "Una claymore grande para enemigos duros.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/GREAT SWORDS/FREE GREAT SWORD 3 COLOR 2.prefab"),
            CreateRuntimeWeapon("free_great_sword_4_3", "Mandoble Real", 92f, 1.28f, "La pieza mas cara del blacksmith.", "Assets/URP GanzSe Free Modular Character Pack/Prefabs/GREAT SWORDS/FREE GREAT SWORD 4 COLOR 3.prefab"),
            CreateArmor("novato_head", "Casco de Novato", ItemType.Helmet, 1, 1, 2f),
            CreateArmor("novato_chest", "Pecho de Novato", ItemType.Chest, 1, 1, 4f),
            CreateArmor("novato_gloves", "Guantes de Novato", ItemType.Gloves, 1, 1, 2f),
            CreateArmor("novato_legs", "Pantalon de Novato", ItemType.Legs, 1, 1, 3f),
            CreateArmor("novato_boots", "Botas de Novato", ItemType.Boots, 1, 1, 2f),
            CreateArmor("herrero_head", "Casco de Herrero", ItemType.Helmet, 2, 2, 5f),
            CreateArmor("herrero_chest", "Pecho de Herrero", ItemType.Chest, 2, 2, 8f),
            CreateArmor("herrero_gloves", "Guantes de Herrero", ItemType.Gloves, 2, 2, 5f),
            CreateArmor("herrero_legs", "Pantalon de Herrero", ItemType.Legs, 2, 2, 6f),
            CreateArmor("herrero_boots", "Botas de Herrero", ItemType.Boots, 2, 2, 5f),
            CreateArmor("carpintero_head", "Casco de Carpintero", ItemType.Helmet, 3, 1, 8f),
            CreateArmor("carpintero_chest", "Pecho de Carpintero", ItemType.Chest, 3, 1, 12f),
            CreateArmor("carpintero_gloves", "Guantes de Carpintero", ItemType.Gloves, 3, 1, 8f),
            CreateArmor("carpintero_legs", "Pantalon de Carpintero", ItemType.Legs, 3, 1, 9f),
            CreateArmor("carpintero_boots", "Botas de Carpintero", ItemType.Boots, 3, 1, 8f),
            CreateArmor("guerrero_head", "Casco de Guerrero", ItemType.Helmet, 4, 2, 13f),
            CreateArmor("guerrero_chest", "Pecho de Guerrero", ItemType.Chest, 4, 2, 18f),
            CreateArmor("guerrero_gloves", "Guantes de Guerrero", ItemType.Gloves, 4, 2, 13f),
            CreateArmor("guerrero_legs", "Pantalon de Guerrero", ItemType.Legs, 4, 2, 15f),
            CreateArmor("guerrero_boots", "Botas de Guerrero", ItemType.Boots, 4, 2, 13f),
            CreateArmor("campeon_head", "Casco de Campeon", ItemType.Helmet, 5, 3, 21f),
            CreateArmor("campeon_chest", "Pecho de Campeon", ItemType.Chest, 5, 3, 28f),
            CreateArmor("campeon_gloves", "Guantes de Campeon", ItemType.Gloves, 5, 3, 21f),
            CreateArmor("campeon_legs", "Pantalon de Campeon", ItemType.Legs, 5, 3, 24f),
            CreateArmor("campeon_boots", "Botas de Campeon", ItemType.Boots, 5, 3, 21f),
            CreateArmor("dios_head", "Casco Dios de la Guerra", ItemType.Helmet, 6, 3, 40f),
            CreateArmor("dios_chest", "Pecho Dios de la Guerra", ItemType.Chest, 6, 3, 55f),
            CreateArmor("dios_gloves", "Guantes Dios de la Guerra", ItemType.Gloves, 6, 3, 40f),
            CreateArmor("dios_legs", "Pantalon Dios de la Guerra", ItemType.Legs, 6, 3, 45f),
            CreateArmor("dios_boots", "Botas Dios de la Guerra", ItemType.Boots, 6, 3, 40f)
        };
    }

    static ShopEntry CreateRuntimeWeapon(string id, string name, float power, float cooldown, string description, string prefabPath)
    {
        var weapon = ScriptableObject.CreateInstance<WeaponData>();
        weapon.weaponID = id;
        weapon.weaponName = name;
        weapon.weaponType = power > 30f ? WeaponType.Sword2H : WeaponType.Sword1H;
        weapon.weaponPrefab = LoadWeaponPrefab(prefabPath);
        weapon.baseDamage = power;
        weapon.attackRange = power > 30f ? 2f : 1.55f;
        weapon.attackCooldown = cooldown;

        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemID = id;
        item.itemName = name;
        item.itemType = ItemType.Weapon;
        item.description = description;
        item.attackBonus = power;
        item.stackable = false;
        item.maxStack = 1;
        item.weaponData = weapon;

        return new ShopEntry { item = item, price = 0 };
    }

    static ShopEntry CreateArmor(string id, string name, ItemType type, int armorType, int color, float defense)
    {
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemID = id;
        item.itemName = name;
        item.itemType = type;
        item.description = "Pieza visible del set " + name + ". Se equipa por separado y reemplaza la pieza actual.";
        item.defenseBonus = defense;
        item.speedBonus = type == ItemType.Boots ? Mathf.Max(0f, defense * 0.03f) : 0f;
        item.stackable = false;
        item.maxStack = 1;
        item.equipmentPrefab = LoadEquipmentPrefab(GetArmorPath(type, armorType, color));
        item.equipmentBone = GetArmorBone(type);
        item.equipmentPositionOffset = Vector3.zero;
        item.equipmentRotationOffset = Vector3.zero;
        item.equipmentScale = Vector3.one;
        item.centerVisualOnBone = true;
        return new ShopEntry { item = item, price = Mathf.RoundToInt((40f + defense * defense * 1.15f) / 5f) * 5 };
    }

    static string GetArmorPath(ItemType type, int armorType, int color)
    {
        string prefix = type switch
        {
            ItemType.Helmet => "Head Armor",
            ItemType.Chest => "Chest Armor",
            ItemType.Gloves => "Arm Armor",
            ItemType.Legs => "Legs Armor",
            ItemType.Boots => "Feet Armor",
            _ => "Chest Armor"
        };

        return $"Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/{prefix} Type {armorType} Color {color} Part.prefab";
    }

    static HumanBodyBones GetArmorBone(ItemType type)
    {
        return type switch
        {
            ItemType.Helmet => HumanBodyBones.Head,
            ItemType.Chest => HumanBodyBones.Chest,
            ItemType.Gloves => HumanBodyBones.LeftLowerArm,
            ItemType.Legs => HumanBodyBones.Hips,
            ItemType.Boots => HumanBodyBones.LeftFoot,
            _ => HumanBodyBones.Hips
        };
    }

    static GameObject LoadEquipmentPrefab(string prefabPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
#else
        return null;
#endif
    }

    static GameObject LoadWeaponPrefab(string prefabPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
            ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_RPG/Prefabs/Weapons/StarterSword.prefab")
            ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Models/Weapons/2Hand-Sword.FBX");
#else
        return null;
#endif
    }

    static int GetPrice(ShopEntry entry)
    {
        if (entry == null) return 0;
        if (entry.price > 0) return entry.price;

        float power = GetPower(entry);
        float cooldownTax = Mathf.Max(0f, 1.1f - GetCooldown(entry)) * 35f;
        int price = Mathf.RoundToInt((35f + power * power * 1.35f + cooldownTax) / 5f) * 5;
        return Mathf.Max(25, price);
    }

    static float GetPower(ShopEntry entry)
    {
        if (entry?.item == null) return 0f;
        float power = Mathf.Max(entry.item.attackBonus, entry.item.defenseBonus);
        if (entry.item.weaponData != null)
            power = Mathf.Max(power, entry.item.weaponData.baseDamage);
        return power;
    }

    static float GetAttack(ShopEntry entry) => Mathf.Max(entry?.item?.attackBonus ?? 0f, entry?.item?.weaponData?.baseDamage ?? 0f);
    static float GetDefense(ShopEntry entry) => entry?.item?.defenseBonus ?? 0f;
    static float GetCooldown(ShopEntry entry) => entry?.item?.weaponData != null ? entry.item.weaponData.attackCooldown : 0.7f;
    static bool CanAfford(ShopEntry entry) => InventoryManager.Instance == null || InventoryManager.Instance.Gold >= GetPrice(entry);
    static string GetItemName(ShopEntry entry) => entry?.item != null ? entry.item.itemName : "";
    static string GetItemTypeLabel(ItemData item)
    {
        if (item == null) return "";
        return item.itemType switch
        {
            ItemType.Weapon => "Arma",
            ItemType.Helmet => "Casco",
            ItemType.Chest => "Pecho",
            ItemType.Gloves => "Guantes",
            ItemType.Legs => "Piernas",
            ItemType.Boots => "Botas",
            ItemType.Shield => "Escudo",
            _ => item.itemType.ToString()
        };
    }

    static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    static Image AddImage(GameObject go, Color color, bool addMuFrame = true)
    {
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.sprite = null;
        img.type = Image.Type.Simple;
        img.color = color;
        if (addMuFrame)
            AddMuFrameOverlay(go.transform, false);
        return img;
    }

    static void AddMuFrameOverlay(Transform parent, bool banner)
    {
        Sprite sprite = banner ? GetMuBannerFrame() : GetMuPanelFrame();
        if (sprite == null)
            return;

        var frame = new GameObject(banner ? "MU_BannerFrame" : "MU_PanelFrame");
        frame.transform.SetParent(parent, false);
        var rect = frame.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = frame.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(1f, 1f, 1f, banner ? 0.96f : 0.92f);
        image.raycastTarget = false;
    }

    static Sprite GetMuPanelFrame()
    {
        if (muPanelFrame == null)
            muPanelFrame = Resources.Load<Sprite>("UI/SharpUI/Panel");
        return muPanelFrame;
    }

    static Sprite GetMuBannerFrame()
    {
        if (muBannerFrame == null)
            muBannerFrame = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        return muBannerFrame;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, FontStyles style, Color color,
        Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment)
    {
        var rect = CreateRect(parent, name, anchorMin, anchorMax);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var rect = CreateRect(parent, name, anchorMin, anchorMax);
        // The imported MU banner is the actual button artwork. A saturated green/red rectangle
        // behind it reads as a second, larger frame and visually swallows the ornate inner one.
        // Keep only a quiet neutral backing so the gold MU frame is always predominant.
        var img = AddImage(rect.gameObject, new Color(0.035f, 0.027f, 0.018f, 0.82f), false);
        AddMuFrameOverlay(rect, true);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        ColorBlock states = button.colors;
        states.normalColor = Color.white;
        states.highlightedColor = new Color(1f, 0.9f, 0.66f, 1f);
        states.pressedColor = new Color(0.72f, 0.62f, 0.42f, 1f);
        states.selectedColor = states.highlightedColor;
        states.disabledColor = new Color(0.38f, 0.38f, 0.38f, 0.58f);
        states.colorMultiplier = 1f;
        button.colors = states;
        CreateText(rect, "Label", label, 15, FontStyles.Bold, Color.white, Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
        return button;
    }

    sealed class WeaponPreviewRenderer
    {
        readonly RawImage target;
        readonly RenderTexture texture;
        readonly Camera camera;
        readonly Light light;
        readonly Transform root;
        GameObject model;
        Quaternion baseRotation;
        Vector3 spinAxis = Vector3.up;
        float nextRenderTime;

        public WeaponPreviewRenderer(RawImage target)
        {
            this.target = target;
            texture = new RenderTexture(384, 384, 16, RenderTextureFormat.ARGB32);
            texture.name = "BlacksmithWeaponPreview";
            target.texture = texture;

            root = new GameObject("BlacksmithWeaponPreviewRoot").transform;
            root.position = new Vector3(2000f, 2000f, 2000f);

            var camGO = new GameObject("BlacksmithWeaponPreviewCamera");
            camGO.transform.SetParent(root, false);
            camera = camGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.025f, 0.02f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 1.8f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20f;
            camera.transform.localPosition = new Vector3(0f, 0.2f, -5f);
            camera.transform.localRotation = Quaternion.identity;
            camera.targetTexture = texture;
            camera.enabled = false;

            var lightGO = new GameObject("BlacksmithWeaponPreviewLight");
            lightGO.transform.SetParent(root, false);
            light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0f;
            light.enabled = false;
            lightGO.transform.localRotation = Quaternion.Euler(40f, -25f, 0f);
            PreviewRenderIsolation.Configure(camera, light);
        }

        public void Show(ItemData item)
        {
            ClearItem();

            var prefab = item != null && item.weaponData != null ? item.weaponData.weaponPrefab : item?.equipmentPrefab;
            model = prefab != null ? Instantiate(prefab, root) : CreateFallbackModel(root, item);
            model.name = "Preview_" + (item != null ? item.itemName : "Weapon");
            PreviewRenderIsolation.SetLayerRecursive(model);
            PreviewRenderIsolation.ApplyUnlitPreviewMaterials(model);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            FitModel(model.transform);
            spinAxis = PreviewSpinAxisUtility.FindLongitudinalAxis(model.transform);
            baseRotation = PreviewSpinAxisUtility.AlignLongitudinalAxisUp(spinAxis);
            model.transform.localRotation = baseRotation;
            camera.Render();
            nextRenderTime = Time.unscaledTime + 0.066f;
        }

        public void Tick()
        {
            if (model == null || target == null || !target.isActiveAndEnabled) return;
            model.transform.localRotation = Quaternion.AngleAxis(
                Time.unscaledTime * 48f, Vector3.up) * baseRotation;
            if (Time.unscaledTime >= nextRenderTime)
            {
                nextRenderTime = Time.unscaledTime + 0.066f;
                camera.Render();
            }
        }

        public void ClearItem()
        {
            if (model != null)
                UnityEngine.Object.Destroy(model);
            model = null;
        }

        public void Dispose()
        {
            ClearItem();
            if (camera != null) UnityEngine.Object.Destroy(camera.gameObject);
            if (light != null) UnityEngine.Object.Destroy(light.gameObject);
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            if (texture != null) texture.Release();
        }

        static GameObject CreateFallbackModel(Transform parent, ItemData item)
        {
            var holder = new GameObject("RuntimeWeaponModel");
            holder.transform.SetParent(parent, false);

            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.transform.SetParent(holder.transform, false);
            blade.transform.localScale = new Vector3(0.12f, 1.5f, 0.08f);
            blade.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            SetColor(blade, new Color(0.78f, 0.78f, 0.82f));

            var guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guard.transform.SetParent(holder.transform, false);
            guard.transform.localScale = new Vector3(0.72f, 0.08f, 0.12f);
            guard.transform.localPosition = new Vector3(0f, -0.42f, 0f);
            SetColor(guard, new Color(0.72f, 0.45f, 0.12f));

            var grip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            grip.transform.SetParent(holder.transform, false);
            grip.transform.localScale = new Vector3(0.08f, 0.28f, 0.08f);
            grip.transform.localPosition = new Vector3(0f, -0.78f, 0f);
            SetColor(grip, new Color(0.18f, 0.11f, 0.07f));
            return holder;
        }

        static void FitModel(Transform modelRoot)
        {
            var renderers = modelRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            modelRoot.position -= bounds.center - modelRoot.position;
            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxSize > 0.01f)
                modelRoot.localScale *= 2.4f / maxSize;
        }

        static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            renderer.material = new Material(shader);
            renderer.material.color = color;
        }
    }
}

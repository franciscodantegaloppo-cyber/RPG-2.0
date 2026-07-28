using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class QuickbarManager : MonoBehaviour
{
    public static QuickbarManager Instance { get; private set; }
    ItemData[] weaponSlots = new ItemData[5];
    Image[] weaponIcons = new Image[5];
    Image[] weaponFrames = new Image[5];
    TextMeshProUGUI[] weaponStateLabels = new TextMeshProUGUI[5];
    Image[] spellIcons = new Image[5];
    Image[] spellFrames = new Image[5];
    TextMeshProUGUI[] spellLockLabels = new TextMeshProUGUI[5];
    TextMeshProUGUI[] spellStateLabels = new TextMeshProUGUI[5];
    PlayerSpellCaster caster;
    PlayerStats stats;
    SkillTreeProgress skillTree;
    Transform player;
    int selectedSpell = -1;
    int selectedWeapon = -1;
    float nextSpellVisualRefresh;
    static readonly Key[] WeaponKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
    // These keys must match the letters drawn over the spell slots.
    static readonly Key[] SpellKeys = { Key.F, Key.H, Key.R, Key.C, Key.O };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "NewGame") return;
        if (FindAnyObjectByType<QuickbarManager>(FindObjectsInactive.Include) != null) return;
        new GameObject("RuntimeQuickbar").AddComponent<QuickbarManager>();
    }

    void Awake() { Instance = this; DontDestroyOnLoad(gameObject); Build(); }
    void Update()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                player = p.transform;
                stats = p.GetComponent<PlayerStats>();
                skillTree = p.GetComponent<SkillTreeProgress>() ?? p.AddComponent<SkillTreeProgress>();
                caster = p.GetComponent<PlayerSpellCaster>() ?? p.AddComponent<PlayerSpellCaster>();
            }
        }
        if (Time.unscaledTime >= nextSpellVisualRefresh)
        {
            nextSpellVisualRefresh = Time.unscaledTime + .1f;
            RefreshQuickbarVisuals();
        }
        if (Keyboard.current == null) return;

        bool gameplay = GameManager.Instance != null && GameManager.Instance.IsGameplayActive();
        for (int i = 0; i < 5; i++)
        {
            if (!Keyboard.current[WeaponKeys[i]].wasPressedThisFrame) continue;
            InventorySlotHandler hovered = InventorySlotHandler.HoveredSlot;
            if (hovered?.ItemRef != null && hovered.ItemRef.itemType == ItemType.Weapon)
                AssignWeapon(i, hovered.ItemRef);
            else if (gameplay)
                EquipWeapon(i);
        }
        if (!gameplay) return;
        for (int i = 0; i < 5; i++)
            if (Keyboard.current[SpellKeys[i]].wasPressedThisFrame) SelectSpell(i);
    }

    public void AssignWeapon(int index, ItemData item)
    {
        if (index < 0 || index >= 5 || item == null || item.itemType != ItemType.Weapon) return;
        weaponSlots[index] = item;
        if (weaponIcons[index] != null) { weaponIcons[index].sprite = item.icon; weaponIcons[index].enabled = item.icon != null; }
        RefreshQuickbarVisuals();
    }
    void EquipWeapon(int index)
    {
        ItemData weapon = weaponSlots[index];
        if (weapon == null || InventoryManager.Instance == null || !InventoryManager.Instance.HasItem(weapon)) return;
        caster?.UnequipSpell();
        selectedSpell = -1;
        selectedWeapon = index;
        RefreshQuickbarVisuals();
        InventorySlot slot = InventoryManager.Instance.Slots.Find(s => s != null && s.item == weapon);
        EquipmentManager.Instance?.Equip(slot?.instance ?? new ItemInstance(weapon));
    }

    public void SelectSpell(int index)
    {
        if (!IsSpellUnlocked(index)) return;
        selectedSpell = index;
        selectedWeapon = -1;
        caster?.Equip((PlayerSpellCaster.Spell)index);
        RefreshQuickbarVisuals();
    }

    bool IsSpellUnlocked(int index) => stats != null && (stats.GodModeEnabled || (skillTree != null && skillTree.IsSpellUnlocked(index)));

    void RefreshQuickbarVisuals()
    {
        for (int i = 0; i < 5; i++)
        {
            ItemData item = weaponSlots[i];
            bool assigned = item != null;
            bool available = assigned && InventoryManager.Instance != null && InventoryManager.Instance.HasItem(item);
            if (weaponIcons[i] != null)
            {
                weaponIcons[i].enabled = assigned && item.icon != null;
                weaponIcons[i].color = available ? Color.white : new Color(.24f, .24f, .27f, .5f);
            }
            if (weaponFrames[i] != null)
                weaponFrames[i].color = i == selectedWeapon && available
                    ? new Color(1f, .75f, .25f)
                    : (available ? Color.white : new Color(.46f, .42f, .38f));
            if (weaponStateLabels[i] != null)
            {
                weaponStateLabels[i].text = assigned && !available ? "SIN ITEM" : "";
                weaponStateLabels[i].color = new Color(1f, .38f, .25f);
            }
        }

        float cooldown = caster != null ? caster.CastCooldownRemaining : 0f;
        for (int i = 0; i < 5; i++)
        {
            bool unlocked = IsSpellUnlocked(i);
            if (spellIcons[i] != null) spellIcons[i].color = unlocked ? Color.white : new Color(.18f, .2f, .24f, .48f);
            if (spellFrames[i] != null) spellFrames[i].color = i == selectedSpell && unlocked ? new Color(1f, .82f, .34f) : (unlocked ? Color.white : new Color(.36f, .36f, .36f));
            if (spellLockLabels[i] != null) spellLockLabels[i].gameObject.SetActive(!unlocked);
            if (spellStateLabels[i] != null)
            {
                bool selected = i == selectedSpell && unlocked;
                bool insufficient = selected && stats != null && !stats.HasStamina(PlayerSpellCaster.CastStaminaCost);
                spellStateLabels[i].text = selected && cooldown > .05f
                    ? cooldown.ToString("0.0") + "s"
                    : (insufficient ? "SIN EN." : "");
                spellStateLabels[i].color = insufficient
                    ? new Color(1f, .25f, .2f)
                    : new Color(1f, .82f, .36f);
            }
        }
    }

    void Build()
    {
        GameObject canvasGo = new GameObject("QuickbarCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        // The inventory reserves a lower strip for this canvas, so it does not need to render
        // over it. Keep normal HUD ordering: the quickbar lives below the inventory, not above.
        Canvas canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 120;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        GameObject root = new GameObject("QuickbarSlots", typeof(RectTransform), typeof(Image)); root.transform.SetParent(canvasGo.transform, false);
        Image rootImage = root.GetComponent<Image>();
        Sprite muPanelFrame = Resources.Load<Sprite>("UI/SharpUI/Panel");
        rootImage.color = Color.white;
        if (muPanelFrame != null) { rootImage.sprite = muPanelFrame; rootImage.type = Image.Type.Sliced; }
        RectTransform r = root.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(.5f, 0f); r.pivot = new Vector2(.5f, 0f); r.sizeDelta = new Vector2(350f, 116f); r.anchoredPosition = new Vector2(0f, 8f);
        TextMeshProUGUI spellTitle = CreateLabel(root.transform, "HECHIZOS", new Vector2(0f, 48f));
        spellTitle.fontSize = 9f; spellTitle.color = new Color(.82f, .70f, .49f);
        TextMeshProUGUI weaponTitle = CreateLabel(root.transform, "ARMAS", new Vector2(0f, -5f));
        weaponTitle.fontSize = 9f; weaponTitle.color = new Color(.82f, .70f, .49f);
        for (int i = 0; i < 5; i++)
        {
            weaponIcons[i] = CreateSlot(root.transform, new Vector2(-112 + i * 56, -30f), (i + 1).ToString(), i, true);
            weaponFrames[i] = weaponIcons[i].transform.parent.GetComponent<Image>();
            weaponStateLabels[i] = CreateStateLabel(weaponIcons[i].transform.parent);
        }
        string[] names = { "F", "H", "R", "C", "O" };
        string[] iconPaths = { "SpellIcons/Fireball", "SpellIcons/Ice", "SpellIcons/Lightning", "SpellIcons/Heal", "SpellIcons/Shockwave" };
        for (int i = 0; i < 5; i++)
        {
            spellIcons[i] = CreateSlot(root.transform, new Vector2(-112 + i * 56, 23f), names[i], i, false);
            spellFrames[i] = spellIcons[i].transform.parent.GetComponent<Image>();
            Texture2D texture = Resources.Load<Texture2D>(iconPaths[i]);
            if (texture != null)
            {
                spellIcons[i].sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f);
                spellIcons[i].enabled = true;
            }
            spellLockLabels[i] = CreateLabel(spellIcons[i].transform.parent, "X", Vector2.zero);
            spellLockLabels[i].fontSize = 14f;
            spellLockLabels[i].color = new Color(.85f, .16f, .08f);
            spellStateLabels[i] = CreateStateLabel(spellIcons[i].transform.parent);
        }
        RefreshQuickbarVisuals();
    }
    Image CreateSlot(Transform parent, Vector2 pos, string key, int index, bool weapon)
    {
        GameObject go = new GameObject((weapon ? "Weapon" : "Spell") + "Slot", typeof(RectTransform), typeof(Image), typeof(Button), typeof(QuickbarSlotHandler)); go.transform.SetParent(parent, false);
        Image bg = go.GetComponent<Image>();
        Sprite muFrame = Resources.Load<Sprite>("UI/SharpUI/Slot");
        bg.color = Color.white;
        if (muFrame != null) { bg.sprite = muFrame; bg.type = Image.Type.Sliced; }
        RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(48f, 48f); rect.anchoredPosition = pos;
        GameObject inset = new GameObject("SharpUI_DarkInset", typeof(RectTransform), typeof(Image));
        inset.transform.SetParent(go.transform, false);
        RectTransform insetRect = inset.GetComponent<RectTransform>(); insetRect.anchorMin = Vector2.zero; insetRect.anchorMax = Vector2.one; insetRect.offsetMin = new Vector2(5f, 5f); insetRect.offsetMax = new Vector2(-5f, -5f);
        Image insetImage = inset.GetComponent<Image>(); insetImage.color = new Color(.035f, .027f, .022f, .96f); insetImage.raycastTarget = false;
        go.GetComponent<Button>().onClick.AddListener(() => { if (weapon) EquipWeapon(index); else SelectSpell(index); });
        QuickbarSlotHandler handler = go.GetComponent<QuickbarSlotHandler>(); handler.index = index; handler.weapon = weapon;
        Image icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); icon.transform.SetParent(go.transform, false); icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f,.5f); icon.rectTransform.sizeDelta = new Vector2(34f,34f); icon.preserveAspect = true; icon.enabled=false;
        TextMeshProUGUI label = CreateLabel(go.transform, key, new Vector2(15f, 15f)); label.fontSize = 10f; label.outlineWidth = .18f; return icon;
    }
    static TextMeshProUGUI CreateStateLabel(Transform parent)
    {
        TextMeshProUGUI label = CreateLabel(parent, "", new Vector2(0f, -16f));
        label.fontSize = 7f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 5f;
        label.fontSizeMax = 8f;
        label.rectTransform.sizeDelta = new Vector2(42f, 11f);
        label.outlineWidth = .22f;
        return label;
    }
    static TextMeshProUGUI CreateLabel(Transform parent, string text, Vector2 pos) { GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); var l=go.GetComponent<TextMeshProUGUI>(); l.text=text; l.fontSize=14; l.fontStyle=FontStyles.Bold; l.color=new Color(1f,.78f,.28f); l.alignment=TextAlignmentOptions.Center; l.raycastTarget=false; var r=l.rectTransform;r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(130,24);r.anchoredPosition=pos;return l; }
}

public class QuickbarSlotHandler : MonoBehaviour, IDropHandler
{
    public int index; public bool weapon;
    public void OnDrop(PointerEventData eventData)
    {
        InventorySlotHandler source = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<InventorySlotHandler>() : null;
        if (weapon && source?.ItemRef != null) QuickbarManager.Instance?.AssignWeapon(index, source.ItemRef);
    }
}

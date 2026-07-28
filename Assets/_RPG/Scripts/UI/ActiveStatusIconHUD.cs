using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Compact top-centre indicators for casts, temporary status effects and persistent auras.
public sealed class ActiveStatusIconHUD : MonoBehaviour
{
    sealed class Entry
    {
        public GameObject root;
        public float expiresAt;
        public bool persistent;
    }

    static ActiveStatusIconHUD instance;
    readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    Transform row;
    const float RowWidth = 320f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => Ensure();

    static ActiveStatusIconHUD Ensure()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<ActiveStatusIconHUD>(FindObjectsInactive.Include);
        if (instance != null) return instance;
        GameObject root = new GameObject("ActiveStatusIconHUD");
        DontDestroyOnLoad(root);
        return root.AddComponent<ActiveStatusIconHUD>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        Build();
    }

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1800;
        gameObject.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject rowObject = new GameObject("ActiveEffects",
            typeof(RectTransform), typeof(VerticalLayoutGroup));
        rowObject.transform.SetParent(transform, false);
        RectTransform rect = rowObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-22f, -20f);
        rect.sizeDelta = new Vector2(RowWidth, 145f);
        VerticalLayoutGroup layout = rowObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperRight;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        row = rowObject.transform;
    }

    public static void ShowTimed(string key, string iconName, float duration,
        string tooltip = null)
    {
        ActiveStatusIconHUD hud = Ensure();
        hud.Set(key, iconName, false, Mathf.Max(.15f, duration), tooltip);
    }

    public static void SetPersistent(string key, string iconName, bool active,
        string tooltip = null)
    {
        ActiveStatusIconHUD hud = Ensure();
        if (!active)
        {
            hud.Remove(key);
            return;
        }
        hud.Set(key, iconName, true, 0f, tooltip);
    }

    void Set(string key, string iconName, bool persistent, float duration, string tooltip)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!entries.TryGetValue(key, out Entry entry) || entry.root == null)
        {
            entry = new Entry { root = BuildIcon(iconName, tooltip) };
            entries[key] = entry;
        }
        entry.persistent = persistent;
        entry.expiresAt = persistent ? float.PositiveInfinity :
            Time.unscaledTime + duration;
    }

    GameObject BuildIcon(string iconName, string tooltip)
    {
        GameObject rootObject = new GameObject("Status_" + iconName,
            typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rootObject.transform.SetParent(row, false);
        RectTransform rect = rootObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(RowWidth, 26f);
        LayoutElement element = rootObject.GetComponent<LayoutElement>();
        element.preferredWidth = RowWidth;
        element.preferredHeight = 26f;
        Image frame = rootObject.GetComponent<Image>();
        frame.sprite = Resources.Load<Sprite>("UI/SharpUI/StatusSlot");
        frame.type = frame.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        frame.color = frame.sprite != null
            ? Color.white
            : new Color(.055f, .035f, .075f, .94f);

        GameObject iconObject = new GameObject("Icon",
            typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(rootObject.transform, false);
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = ResolveStatusIcon(iconName);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = new Vector2(0f, .5f);
        icon.rectTransform.anchorMax = new Vector2(0f, .5f);
        icon.rectTransform.pivot = new Vector2(0f, .5f);
        icon.rectTransform.anchoredPosition = new Vector2(5f, 0f);
        icon.rectTransform.sizeDelta = new Vector2(20f, 20f);

        if (!string.IsNullOrWhiteSpace(tooltip))
        {
            GameObject labelObject = new GameObject("AccessibleName",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(rootObject.transform, false);
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = tooltip;
            label.fontSize = 12f;
            label.color = new Color(.88f, .84f, .76f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(31f, 2f); labelRect.offsetMax = new Vector2(-8f, -2f);
        }
        return rootObject;
    }

    static Sprite ResolveStatusIcon(string iconName)
    {
        // Use an actual icon from the 521-skill pack for the enchanted dark aura.
        // dark_4 resolves to skill_084: a violet hand wrapped in shadow.
        if (string.Equals(iconName, "DarkAura",
                System.StringComparison.OrdinalIgnoreCase))
        {
            SkillIconCatalog catalog =
                Resources.Load<SkillIconCatalog>("UI/SkillIconCatalog");
            Sprite dark = catalog != null ? catalog.ForNode("dark_4") : null;
            if (dark != null)
                return dark;
        }
        return Resources.Load<Sprite>("StatusIcons/" + iconName);
    }

    void Update()
    {
        if (entries.Count == 0) return;
        List<string> expired = null;
        foreach (KeyValuePair<string, Entry> pair in entries)
        {
            if (pair.Value.persistent || Time.unscaledTime < pair.Value.expiresAt) continue;
            if (expired == null) expired = new List<string>();
            expired.Add(pair.Key);
        }
        if (expired == null) return;
        foreach (string key in expired) Remove(key);
    }

    void Remove(string key)
    {
        if (!entries.TryGetValue(key, out Entry entry)) return;
        if (entry.root != null) Destroy(entry.root);
        entries.Remove(key);
    }
}

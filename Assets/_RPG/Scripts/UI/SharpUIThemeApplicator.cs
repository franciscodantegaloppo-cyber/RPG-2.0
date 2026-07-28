using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Applies the imported SharpUI art to both scene-authored UI and panels generated at runtime.
// Gameplay logic stays in the existing controls; only their visual skin is replaced.
public sealed class SharpUIThemeApplicator : MonoBehaviour
{
    public static readonly Color Accent = new Color(.754717f, .5383602f, .2171591f, 1f);
    public static readonly Color Title = new Color(.8773585f, .6852915f, .3848789f, 1f);
    public static readonly Color Body = new Color(1f, 1f, 1f, .92f);
    public static readonly Color Muted = new Color(.8117647f, .7137255f, .5921569f, 1f);

    static SharpUIThemeApplicator instance;
    readonly HashSet<Image> styledImages = new HashSet<Image>();
    readonly HashSet<TextMeshProUGUI> styledTexts = new HashSet<TextMeshProUGUI>();
    Sprite panel;
    Sprite button;
    Sprite slot;
    Sprite round;
    Sprite roundSelected;
    Sprite resourceFrame;
    Sprite resourceFill;
    Sprite tooltip;
    Sprite input;
    Sprite dialogHeader;
    Sprite listItem;
    Sprite listItemSelected;
    Sprite notificationLine;
    Sprite scrollBackground;
    Sprite scrollFill;
    Sprite scrollHandle;
    Sprite checkbox;
    Sprite checkmark;
    float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        GameObject root = new GameObject("SharpUI_GlobalTheme");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<SharpUIThemeApplicator>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        panel = Resources.Load<Sprite>("UI/SharpUI/Panel");
        button = Resources.Load<Sprite>("UI/SharpUI/Button");
        slot = Resources.Load<Sprite>("UI/SharpUI/Slot");
        round = Resources.Load<Sprite>("UI/SharpUI/RoundButton");
        roundSelected = Resources.Load<Sprite>("UI/SharpUI/RoundButtonSelected");
        resourceFrame = Resources.Load<Sprite>("UI/SharpUI/ResourceFrame");
        resourceFill = Resources.Load<Sprite>("UI/SharpUI/ResourceFill");
        tooltip = Resources.Load<Sprite>("UI/SharpUI/Tooltip");
        input = Resources.Load<Sprite>("UI/SharpUI/Input");
        dialogHeader = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        listItem = Resources.Load<Sprite>("UI/SharpUI/ListItem");
        listItemSelected = Resources.Load<Sprite>("UI/SharpUI/ListItemSelected");
        notificationLine = Resources.Load<Sprite>("UI/SharpUI/NotificationLine");
        scrollBackground = Resources.Load<Sprite>("UI/SharpUI/ScrollBackground");
        scrollFill = Resources.Load<Sprite>("UI/SharpUI/ScrollFill");
        scrollHandle = Resources.Load<Sprite>("UI/SharpUI/ScrollHandle");
        checkbox = Resources.Load<Sprite>("UI/SharpUI/Checkbox");
        checkmark = Resources.Load<Sprite>("UI/SharpUI/Checkmark");
        ApplyAll();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        styledImages.Clear();
        styledTexts.Clear();
        ApplyAll();
    }

    void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .6f;
        ApplyAll();
    }

    void ApplyAll()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (canvas == null) continue;
            foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
            {
                if (image == null || styledImages.Contains(image)) continue;
                StyleImage(image);
                styledImages.Add(image);
            }
            foreach (TextMeshProUGUI text in
                     canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text == null || styledTexts.Contains(text)) continue;
                StyleText(text);
                styledTexts.Add(text);
            }
        }
        styledImages.RemoveWhere(image => image == null);
        styledTexts.RemoveWhere(text => text == null);
    }

    void StyleImage(Image image)
    {
        string name = image.gameObject.name.ToLowerInvariant();
        string hierarchy = HierarchyName(image.transform).ToLowerInvariant();
        string sourceSprite = image.sprite != null
            ? image.sprite.name.ToLowerInvariant()
            : string.Empty;
        bool resourceHierarchy = hierarchy.Contains("health") ||
                                 hierarchy.Contains("stamina") ||
                                 hierarchy.Contains("experience") ||
                                 hierarchy.Contains("resource") ||
                                 hierarchy.Contains("boss");

        if (sourceSprite.Contains("panelframe_banner") ||
            sourceSprite.Contains("panel frame banner"))
        {
            if (dialogHeader != null) SetSprite(image, dialogHeader);
            return;
        }
        if (sourceSprite.Contains("panelframe_mu") ||
            sourceSprite.Contains("panel frame mu"))
        {
            if (panel != null) SetSprite(image, panel);
            return;
        }
        if (name.Contains("icon") || name.Contains("portrait") ||
            name.Contains("itempreview") || name.Contains("drag") ||
            name.Contains("glow") || name.Contains("aura"))
            return;

        Toggle ownerToggle = image.GetComponentInParent<Toggle>();
        if (ownerToggle != null)
        {
            if (name.Contains("check") && checkmark != null)
                SetSprite(image, checkmark);
            else if (checkbox != null)
                SetSprite(image, checkbox);
            return;
        }

        if (!resourceHierarchy && (image.GetComponentInParent<Slider>() != null ||
            image.GetComponentInParent<Scrollbar>() != null))
        {
            if (name.Contains("handle") && scrollHandle != null)
                SetSprite(image, scrollHandle);
            else if (name.Contains("fill") && scrollFill != null)
                SetSprite(image, scrollFill);
            else if (scrollBackground != null)
                SetSprite(image, scrollBackground);
            return;
        }

        Button ownerButton = image.GetComponent<Button>();
        if (ownerButton != null)
        {
            if ((name.Contains("node") || hierarchy.Contains("skilltree")) && round != null)
                SetSprite(image, round);
            else if ((name.Contains("slot") || hierarchy.Contains("quickbar")) && slot != null)
                SetSprite(image, slot);
            else if (button != null)
                SetSprite(image, button);
            ownerButton.targetGraphic = image;
            ColorBlock colors = ownerButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.9607843f, .9607843f, .9607843f, 1f);
            colors.pressedColor = new Color(.7843137f, .7843137f, .7843137f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(.7843137f, .7843137f, .7843137f, .5019608f);
            ownerButton.colors = colors;
            return;
        }

        if (image.GetComponent<TMP_InputField>() != null || name.Contains("input"))
        {
            if (input != null) SetSprite(image, input);
            return;
        }
        if (name.Contains("header") || name.Contains("titlebar"))
        {
            if (dialogHeader != null) SetSprite(image, dialogHeader);
            return;
        }
        if (name.Contains("notification") || name.Contains("separator") ||
            name.Contains("divider"))
        {
            if (notificationLine != null) SetSprite(image, notificationLine);
            return;
        }
        if (name.Contains("selected") && hierarchy.Contains("list"))
        {
            if (listItemSelected != null) SetSprite(image, listItemSelected);
            return;
        }
        if ((name.Contains("row") || name.Contains("entry") ||
             name.Contains("listitem")) && hierarchy.Contains("list"))
        {
            if (listItem != null) SetSprite(image, listItem);
            return;
        }
        if (name.Contains("fill") && resourceHierarchy)
        {
            if (resourceFill != null) SetSprite(image, resourceFill);
            return;
        }
        if ((name.Contains("background") || name.Contains("frame")) &&
            resourceHierarchy)
        {
            if (resourceFrame != null) SetSprite(image, resourceFrame);
            return;
        }
        if (name.Contains("slot"))
        {
            if (slot != null) SetSprite(image, slot);
            return;
        }
        if (name.Contains("tooltip"))
        {
            if (tooltip != null) SetSprite(image, tooltip);
            return;
        }
        if (name.Contains("panel") || name.Contains("popup") ||
            name.Contains("dialog") || name.Contains("journal") ||
            name.Contains("window") || name.Contains("frame") ||
            name.Contains("inventory") || name.Contains("characterpreview") ||
            name.Contains("skills"))
        {
            if (panel != null) SetSprite(image, panel);
        }
    }

    static void SetSprite(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
    }

    static void StyleText(TextMeshProUGUI text)
    {
        string name = text.gameObject.name.ToLowerInvariant();
        text.characterSpacing = 0f;
        text.outlineWidth = 0f;
        if (name.Contains("title") || name.Contains("header"))
            text.color = Title;
        else if (text.GetComponentInParent<Button>() != null)
            text.color = Color.white;
        else if (name.Contains("placeholder") || name.Contains("hint") ||
                 name.Contains("subtitle"))
            text.color = Muted;
        else
            text.color = Body;
    }

    static string HierarchyName(Transform transform)
    {
        string result = transform.name;
        for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            result += "/" + parent.name;
        return result;
    }

    public static Sprite SkillNodeSprite(bool selected)
    {
        if (instance == null) Bootstrap();
        if (instance == null) return null;
        return selected && instance.roundSelected != null
            ? instance.roundSelected
            : instance.round;
    }
}

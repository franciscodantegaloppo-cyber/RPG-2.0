using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Independent clock overlay. It must not depend on the scene-authored HUD because that HUD can
// be replaced when entering SpawnVillage or a dungeon while the day/night cycle keeps running.
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public sealed class DayNightClockHUD : MonoBehaviour
{
    static DayNightClockHUD instance;

    TenkokuDayNightCycle cycle;
    TextMeshProUGUI label;
    Canvas canvas;
    float nextCycleLookup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInitialScene()
    {
        EnsureForScene(SceneManager.GetActiveScene());
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForScene(scene);
    }

    public static void EnsureForScene(Scene scene)
    {
        if (scene.name == "NewGame")
        {
            if (instance != null)
                instance.gameObject.SetActive(false);
            return;
        }

        if (instance == null)
        {
            // Create the Canvas before adding this behaviour. Awake runs immediately when the
            // behaviour is added, so creating an empty object first leaves a one-frame window
            // in which EnsureVisuals cannot configure a valid Canvas.
            GameObject root = new GameObject("DayNightClockHUD",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            instance = root.AddComponent<DayNightClockHUD>();
            DontDestroyOnLoad(root);
        }
        instance.gameObject.SetActive(true);
        instance.EnsureVisuals();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureVisuals();
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name == "NewGame")
        {
            if (canvas != null) canvas.enabled = false;
            return;
        }

        EnsureVisuals();
        if (canvas != null) canvas.enabled = true;

        if (cycle == null && Time.unscaledTime >= nextCycleLookup)
        {
            nextCycleLookup = Time.unscaledTime + .5f;
            cycle = TenkokuDayNightCycleBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
        }
        if (label == null)
            return;
        if (cycle == null)
        {
            label.text = "CARGANDO HORA...";
            return;
        }

        float hour = cycle.CurrentHour;
        int wholeHour = Mathf.FloorToInt(hour);
        int minutes = Mathf.FloorToInt((hour - wholeHour) * 60f);
        bool pm = wholeHour >= 12;
        int displayHour = wholeHour % 12;
        if (displayHour == 0) displayHour = 12;
        bool night = wholeHour >= 18 || wholeHour < 6;

        string godHint = cycle.GodModeTimeControlEnabled
            ? "  |  T: +30 MIN"
            : string.Empty;

        label.text = "DÍA " + (cycle.WorldDay + 1) +
                     "  |  " + displayHour.ToString("00") + ":" +
                     minutes.ToString("00") + (pm ? " PM" : " AM") +
                     "  |  " + (night ? "NOCHE" : "DÍA") + godHint;
    }

    void EnsureVisuals()
    {
        if (canvas != null && label != null)
            return;

        GameObject legacyClock = GameObject.Find("DayNightClockMU");
        if (legacyClock != null)
            Destroy(legacyClock);

        canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1900;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        Transform existing = transform.Find("DayNightClockPanel");
        GameObject panel = existing != null
            ? existing.gameObject
            : new GameObject("DayNightClockPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);
        rect.sizeDelta = new Vector2(660f, 58f);

        Image background = panel.GetComponent<Image>();
        Sprite frame = Resources.Load<Sprite>("UI/SharpUI/Panel");
        if (frame != null)
        {
            background.sprite = frame;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
        }
        else
        {
            background.color = new Color(.055f, .032f, .018f, .94f);
        }
        background.raycastTarget = false;

        Outline outline = panel.GetComponent<Outline>();
        if (outline == null)
            outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(.72f, .38f, .11f, .72f);
        outline.effectDistance = new Vector2(1f, -1f);

        Transform textTransform = panel.transform.Find("ClockText");
        GameObject textObject = textTransform != null
            ? textTransform.gameObject
            : new GameObject("ClockText", typeof(RectTransform));
        textObject.transform.SetParent(panel.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(22f, 7f);
        textRect.offsetMax = new Vector2(-22f, -7f);

        label = textObject.GetComponent<TextMeshProUGUI>();
        if (label == null)
            label = textObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 18f;
        label.enableAutoSizing = false;
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = .5f;
        label.lineSpacing = 4f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.color = new Color(1f, .82f, .45f);
        label.outlineColor = new Color(.08f, .02f, .01f, 1f);
        label.outlineWidth = .14f;
        label.raycastTarget = false;
    }
}

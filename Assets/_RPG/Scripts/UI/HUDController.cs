using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class HUDController : MonoBehaviour
{
    [Header("Health / Stamina")]
    [SerializeField] Slider healthSlider;
    [SerializeField] Slider staminaSlider;

    [Header("Experience")]
    [SerializeField] Slider experienceSlider;
    [SerializeField] TextMeshProUGUI levelText;
    [SerializeField] TextMeshProUGUI experienceRemainingText;
    [SerializeField] GameObject skillPointsAlert;
    [SerializeField] TextMeshProUGUI skillPointsAlertText;

    [Header("Wind")]
    [SerializeField] Slider windSlider;
    [SerializeField] Image windFillImage;
    [SerializeField] TextMeshProUGUI windGustBanner;
    [SerializeField] float gustBannerDuration = 3.5f;

    [Header("Water / oxygen")]
    [SerializeField] Slider oxygenSlider;
    [SerializeField] GameObject oxygenPanel;
    [SerializeField] TextMeshProUGUI oxygenLabel;

    [Header("Interaction")]
    [SerializeField] TextMeshProUGUI interactionPrompt;

    [Header("Dialogue")]
    [SerializeField] GameObject dialoguePanel;
    [SerializeField] TextMeshProUGUI dialogueText;
    [SerializeField] float dialogueDuration = 4f;

    PlayerStats playerStats;
    Coroutine dialogueCoroutine;
    Coroutine gustBannerCoroutine;
    bool windSubscribed;
    GameObject vitalsFrame;
    TextMeshProUGUI healthLabel;
    TextMeshProUGUI staminaLabel;
    TextMeshProUGUI healthValueLabel;
    TextMeshProUGUI staminaValueLabel;
    TextMeshProUGUI experienceValueLabel;
    GameObject dayNightPanel;
    TextMeshProUGUI dayNightLabel;
    TenkokuDayNightCycle dayNightCycle;

    void Start()
    {
        EnsureVitalsFrame();
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.OnHealthChanged += UpdateHealth;
                playerStats.OnStaminaChanged += UpdateStamina;
                playerStats.OnExperienceChanged += UpdateExperience;
                playerStats.OnSkillsChanged += UpdateSkillPointsAlert;
                playerStats.OnLevelUp += HandleLevelUp;
                UpdateHealth(playerStats.CurrentHealth, playerStats.MaxHealth);
                UpdateStamina(playerStats.CurrentStamina, playerStats.MaxStamina);
                UpdateExperience(playerStats.CurrentXP, playerStats.XPToNextLevel);
                UpdateSkillPointsAlert();
            }
        }

        ShowInteractionPrompt(null);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (windGustBanner != null) windGustBanner.gameObject.SetActive(false);
    }

    void Update()
    {
        // WindManager bootstraps itself at runtime; lazily hook up once it exists rather than
        // depending on script execution order.
        if (!windSubscribed && WindManager.Instance != null)
        {
            windSubscribed = true;
            WindManager.Instance.OnWindChanged += UpdateWind;
            WindManager.Instance.OnGustStart += ShowGustBanner;
            UpdateWind(WindManager.Instance.CurrentStrength01, WindManager.Instance.IsGusting);
        }
        UpdateDayNightClock();
        UpdateSkillPointsAlert();
    }

    void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -= UpdateHealth;
            playerStats.OnStaminaChanged -= UpdateStamina;
            playerStats.OnExperienceChanged -= UpdateExperience;
            playerStats.OnSkillsChanged -= UpdateSkillPointsAlert;
            playerStats.OnLevelUp -= HandleLevelUp;
        }
        if (WindManager.Instance != null)
        {
            WindManager.Instance.OnWindChanged -= UpdateWind;
            WindManager.Instance.OnGustStart -= ShowGustBanner;
        }
    }

    void UpdateHealth(float current, float max)
    {
        if (healthSlider != null) healthSlider.value = current / max;
        if (healthValueLabel != null)
            healthValueLabel.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
    }

    void UpdateStamina(float current, float max)
    {
        if (staminaSlider != null) staminaSlider.value = current / max;
        if (staminaValueLabel != null)
            staminaValueLabel.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
    }

    void UpdateExperience(float current, float max)
    {
        if (experienceSlider != null) experienceSlider.value = max > 0f ? current / max : 0f;
        RefreshExperienceLabels(current, max);
    }

    void HandleLevelUp(int _)
    {
        if (playerStats == null) return;
        RefreshExperienceLabels(playerStats.CurrentXP, playerStats.XPToNextLevel);
        UpdateSkillPointsAlert();
    }

    void RefreshExperienceLabels(float current, float max)
    {
        EnsureVitalsFrame();

        if (playerStats != null && levelText != null)
            levelText.text = playerStats.Level.ToString();

        if (experienceRemainingText != null)
            experienceRemainingText.text = "XP";
        if (experienceValueLabel != null)
            experienceValueLabel.text = Mathf.FloorToInt(current) + " / " + Mathf.CeilToInt(max);
    }

    void UpdateSkillPointsAlert()
    {
        EnsureSkillAlert();
        if (playerStats == null || skillPointsAlert == null)
            return;

        int points = playerStats.AvailableSkillPoints;
        bool hasPoints = points > 0 && !ChestUI.IsAnyOpen;
        skillPointsAlert.SetActive(hasPoints);
        if (hasPoints && skillPointsAlertText != null)
            skillPointsAlertText.text = points == 1
                ? "1 punto de habilidad pendiente"
                : points + " puntos de habilidad pendientes";
    }

    void UpdateWind(float strength01, bool isGusting)
    {
        if (windSlider != null) windSlider.value = strength01;
        if (windFillImage != null)
        {
            Color calm = new Color(0.55f, 0.75f, 0.85f);
            Color gust = new Color(0.9f, 0.35f, 0.2f);
            windFillImage.color = Color.Lerp(calm, gust, isGusting ? 1f : Mathf.Clamp01(strength01 * 0.5f));
        }
    }

    void UpdateDayNightClock()
    {
        if (dayNightCycle == null) dayNightCycle = FindAnyObjectByType<TenkokuDayNightCycle>();
        if (dayNightCycle == null) return;
        EnsureDayNightClock();
        if (dayNightLabel == null) return;
        float hour = dayNightCycle.CurrentHour;
        int wholeHour = Mathf.FloorToInt(hour);
        int minutes = Mathf.FloorToInt((hour - wholeHour) * 60f);
        bool pm = wholeHour >= 12;
        int displayHour = wholeHour % 12;
        if (displayHour == 0) displayHour = 12;
        dayNightLabel.text = "DÍA " + (dayNightCycle.WorldDay + 1) + "  ·  " + displayHour.ToString("00") + ":" + minutes.ToString("00") + (pm ? " PM" : " AM");
    }

    void EnsureDayNightClock()
    {
        if (dayNightPanel != null) return;
        RectTransform windRect = windSlider != null
            ? windSlider.GetComponent<RectTransform>()
            : null;
        Transform clockParent = windRect != null && windRect.parent != null
            ? windRect.parent
            : null;
        if (clockParent == null)
        {
            Canvas hudCanvas = GetComponentInParent<Canvas>();
            if (hudCanvas == null)
            {
                foreach (Canvas candidate in
                         FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                {
                    if (candidate != null &&
                        candidate.renderMode != RenderMode.WorldSpace &&
                        candidate.gameObject.activeInHierarchy)
                    {
                        hudCanvas = candidate;
                        break;
                    }
                }
            }
            clockParent = hudCanvas != null ? hudCanvas.transform : transform;
        }
        dayNightPanel = new GameObject("DayNightClockMU", typeof(RectTransform), typeof(Image));
        dayNightPanel.transform.SetParent(clockParent, false);
        RectTransform rect = dayNightPanel.GetComponent<RectTransform>();
        if (windRect != null)
        {
            rect.anchorMin = windRect.anchorMin;
            rect.anchorMax = windRect.anchorMax;
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition =
                windRect.anchoredPosition + new Vector2(-115f, -38f);
        }
        else
        {
            // SharpUI can rebuild the HUD without the old serialized wind slider. Keep the
            // clock independently visible instead of silently skipping its creation.
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-270f, -126f);
        }
        rect.sizeDelta = new Vector2(235f, 34f);
        Image background = dayNightPanel.GetComponent<Image>();
        Sprite frame = Resources.Load<Sprite>("UI/SharpUI/Panel");
        if (frame != null) { background.sprite = frame; background.type = Image.Type.Sliced; background.color = Color.white; }
        else background.color = new Color(.06f, .025f, .10f, .94f);
        background.raycastTarget = false;
        Outline frameOutline = dayNightPanel.AddComponent<Outline>();
        frameOutline.effectColor = new Color(.01f, 0f, .025f, .9f);
        frameOutline.effectDistance = new Vector2(1f, -1f);

        GameObject textGo = new GameObject("ClockText", typeof(RectTransform));
        textGo.transform.SetParent(dayNightPanel.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 3f); textRect.offsetMax = new Vector2(-14f, -3f);
        dayNightLabel = textGo.AddComponent<TextMeshProUGUI>();
        dayNightLabel.alignment = TextAlignmentOptions.Center;
        dayNightLabel.fontSize = 13;
        dayNightLabel.enableAutoSizing = true;
        dayNightLabel.fontSizeMin = 8f; dayNightLabel.fontSizeMax = 13f;
        dayNightLabel.fontStyle = FontStyles.Bold;
        dayNightLabel.color = new Color(1f, .84f, .48f);
        dayNightLabel.outlineColor = new Color(.08f, .01f, .12f, 1f);
        dayNightLabel.outlineWidth = .16f;
    }

    public void SetOxygenMeter(bool visible, float oxygen01, float remainingSeconds)
    {
        EnsureOxygenPanel();
        if (oxygenPanel == null) return;

        oxygenPanel.SetActive(visible);
        if (!visible) return;

        if (oxygenSlider != null) oxygenSlider.value = Mathf.Clamp01(oxygen01);
        if (oxygenLabel != null)
            oxygenLabel.text = "OXÍGENO  " + Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds)) + "s";
    }

    void ShowGustBanner()
    {
        if (windGustBanner == null) return;
        if (gustBannerCoroutine != null) StopCoroutine(gustBannerCoroutine);
        gustBannerCoroutine = StartCoroutine(GustBannerRoutine());
    }

    IEnumerator GustBannerRoutine()
    {
        windGustBanner.text = "¡VIENTO FUERTE!";
        windGustBanner.gameObject.SetActive(true);
        yield return new WaitForSeconds(gustBannerDuration);
        windGustBanner.gameObject.SetActive(false);
    }

    public void ShowInteractionPrompt(string text)
    {
        if (interactionPrompt == null) return;
        bool show = !string.IsNullOrEmpty(text);
        interactionPrompt.gameObject.SetActive(show);
        if (show) interactionPrompt.text = text;
    }

    public void ShowDialogue(string text)
    {
        if (dialoguePanel == null) return;
        if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
        dialogueCoroutine = StartCoroutine(ShowDialogueRoutine(text));
    }

    IEnumerator ShowDialogueRoutine(string text)
    {
        dialoguePanel.SetActive(true);
        if (dialogueText != null) dialogueText.text = text;
        yield return new WaitForSeconds(dialogueDuration);
        dialoguePanel.SetActive(false);
    }

    void EnsureVitalsFrame()
    {
        if (vitalsFrame != null || healthSlider == null || staminaSlider == null || experienceSlider == null)
            return;

        Canvas canvas = healthSlider.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null) return;

        vitalsFrame = new GameObject("PlayerVitalsSharpUI", typeof(RectTransform));
        vitalsFrame.transform.SetParent(canvas.transform, false);
        RectTransform frameRect = (RectTransform)vitalsFrame.transform;
        frameRect.anchorMin = new Vector2(0f, 1f);
        frameRect.anchorMax = new Vector2(0f, 1f);
        frameRect.pivot = new Vector2(0f, 1f);
        frameRect.anchoredPosition = new Vector2(22f, -18f);
        frameRect.sizeDelta = new Vector2(360f, 112f);

        Image frameImage = vitalsFrame.AddComponent<Image>();
        Sprite frameSprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        if (frameSprite != null)
        {
            frameImage.sprite = frameSprite;
            frameImage.type = Image.Type.Sliced;
            frameImage.color = Color.white;
        }
        else frameImage.color = new Color(0.04f, 0.035f, 0.025f, 1f);
        frameImage.raycastTarget = false;

        TextMeshProUGUI playerName = CreateHudLabel(vitalsFrame.transform, "PlayerName",
            PlayerPrefs.GetString("PlayerName", "Frank"), 15f,
            new Vector2(14f, -15f), new Vector2(250f, 20f));
        playerName.alignment = TextAlignmentOptions.MidlineLeft;
        playerName.color = new Color(.92f, .88f, .79f);

        PlaceSlider(healthSlider, vitalsFrame.transform, new Vector2(48f, -42f), new Vector2(296f, 20f));
        PlaceSlider(staminaSlider, vitalsFrame.transform, new Vector2(48f, -66f), new Vector2(296f, 17f));
        PlaceSlider(experienceSlider, vitalsFrame.transform, new Vector2(48f, -87f), new Vector2(296f, 13f));
        PolishVitalBar(healthSlider, new Color(0.08f, 0.58f, 0.09f), new Color(0.45f, 1f, 0.36f));
        PolishVitalBar(staminaSlider, new Color(0.04f, 0.46f, 0.80f), new Color(0.38f, 0.88f, 1f));
        PolishVitalBar(experienceSlider, new Color(0.82f, 0.38f, 0.06f), new Color(1f, 0.75f, 0.22f));

        healthValueLabel = CreateHudLabel(healthSlider.transform, "HealthValue", "", 11f,
            Vector2.zero, new Vector2(296f, 20f));
        healthValueLabel.rectTransform.anchorMin = Vector2.zero; healthValueLabel.rectTransform.anchorMax = Vector2.one;
        healthValueLabel.rectTransform.offsetMin = healthValueLabel.rectTransform.offsetMax = Vector2.zero;
        healthValueLabel.alignment = TextAlignmentOptions.Center; healthValueLabel.color = Color.white;
        staminaValueLabel = CreateHudLabel(staminaSlider.transform, "StaminaValue", "", 10f,
            Vector2.zero, new Vector2(296f, 17f));
        staminaValueLabel.rectTransform.anchorMin = Vector2.zero; staminaValueLabel.rectTransform.anchorMax = Vector2.one;
        staminaValueLabel.rectTransform.offsetMin = staminaValueLabel.rectTransform.offsetMax = Vector2.zero;
        staminaValueLabel.alignment = TextAlignmentOptions.Center; staminaValueLabel.color = Color.white;
        experienceValueLabel = CreateHudLabel(experienceSlider.transform, "ExperienceValue", "", 9f,
            Vector2.zero, new Vector2(296f, 13f));
        experienceValueLabel.rectTransform.anchorMin = Vector2.zero; experienceValueLabel.rectTransform.anchorMax = Vector2.one;
        experienceValueLabel.rectTransform.offsetMin = experienceValueLabel.rectTransform.offsetMax = Vector2.zero;
        experienceValueLabel.alignment = TextAlignmentOptions.Center; experienceValueLabel.color = Color.white;

        GameObject levelBox = new GameObject("LevelBox", typeof(RectTransform));
        levelBox.transform.SetParent(vitalsFrame.transform, false);
        RectTransform levelBoxRect = (RectTransform)levelBox.transform;
        levelBoxRect.anchorMin = new Vector2(0f, 1f);
        levelBoxRect.anchorMax = new Vector2(0f, 1f);
        levelBoxRect.pivot = new Vector2(0f, 0.5f);
        levelBoxRect.anchoredPosition = new Vector2(10f, -64f);
        levelBoxRect.sizeDelta = new Vector2(32f, 58f);
        Image levelFrame = levelBox.AddComponent<Image>();
        Sprite banner = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        if (banner != null)
        {
            levelFrame.sprite = banner;
            levelFrame.type = Image.Type.Sliced;
            levelFrame.color = Color.white;
        }
        else levelFrame.color = new Color(0.14f, 0.09f, 0.025f, 1f);
        levelFrame.raycastTarget = false;

        if (levelText == null)
        {
            GameObject go = new GameObject("PlayerLevelLabel");
            go.transform.SetParent(levelBox.transform, false);
            levelText = go.AddComponent<TextMeshProUGUI>();
        }
        else levelText.transform.SetParent(levelBox.transform, false);
        ConfigureFillLabel(levelText, 15f, 10f, TextAlignmentOptions.Center);

        if (experienceRemainingText == null)
        {
            GameObject go = new GameObject("ExperienceLabel");
            go.transform.SetParent(vitalsFrame.transform, false);
            experienceRemainingText = go.AddComponent<TextMeshProUGUI>();
        }
        else experienceRemainingText.transform.SetParent(vitalsFrame.transform, false);
        ConfigureHudLabel(experienceRemainingText, "XP", 9f,
            new Vector2(12f, -88f), new Vector2(28f, 16f));

        // Keep the ornate frame behind all bars and labels.
        vitalsFrame.transform.SetAsFirstSibling();
    }

    static void PlaceSlider(Slider slider, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = slider.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // Builds a polished, layered treatment using regular UI Images so it
    // remains crisp at any HUD resolution and does not need raster assets.
    static void PolishVitalBar(Slider slider, Color baseColor, Color highlightColor)
    {
        if (slider == null) return;

        Image background = slider.targetGraphic as Image;
        if (background == null)
        {
            Transform backgroundTransform = slider.transform.Find("Background");
            if (backgroundTransform != null) background = backgroundTransform.GetComponent<Image>();
        }

        if (background != null)
        {
            // The empty portion is a dark glass channel, not a flat black gap.
            background.sprite = CreateEmptyBarGradientSprite(highlightColor);
            background.type = Image.Type.Simple;
            background.color = Color.white;
            background.raycastTarget = false;
            Outline backgroundOutline = background.GetComponent<Outline>() ?? background.gameObject.AddComponent<Outline>();
            backgroundOutline.effectColor = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.36f);
            backgroundOutline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (fill == null) return;

        // A real sampled gradient gives the bar a smooth, multi-step colour
        // transition instead of relying only on a few flat UI overlays.
        fill.sprite = CreateBarGradientSprite(baseColor, highlightColor);
        fill.type = Image.Type.Simple;
        fill.color = Color.white;
        fill.raycastTarget = false;
        Outline fillOutline = fill.GetComponent<Outline>() ?? fill.gameObject.AddComponent<Outline>();
        fillOutline.effectColor = new Color(0.015f, 0.01f, 0.01f, 0.65f);
        fillOutline.effectDistance = new Vector2(0.8f, -0.8f);

        // Layered tints create a richer vertical colour gradient and a rounded,
        // glass-like volume without requiring a low-resolution bar texture.
        Color deepTone = Color.Lerp(baseColor, new Color(0.01f, 0.015f, 0.035f), 0.55f);
        Color middleTone = Color.Lerp(baseColor, highlightColor, 0.38f);
        AddBarLayer(fill.rectTransform, "DeepCore", new Color(deepTone.r, deepTone.g, deepTone.b, 0.10f),
            Vector2.zero, new Vector2(1f, 0.22f));
        AddBarLayer(fill.rectTransform, "LowerTint", new Color(baseColor.r, baseColor.g, baseColor.b, 0.06f),
            new Vector2(0f, 0.20f), new Vector2(1f, 0.45f));
        AddBarLayer(fill.rectTransform, "MiddleGlow", new Color(middleTone.r, middleTone.g, middleTone.b, 0.07f),
            new Vector2(0f, 0.43f), new Vector2(1f, 0.70f));
        AddBarLayer(fill.rectTransform, "UpperColour", new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.08f),
            new Vector2(0f, 0.67f), new Vector2(1f, 0.86f));
        AddBarLayer(fill.rectTransform, "TopGloss", new Color(1f, 1f, 1f, 0.09f),
            new Vector2(0.04f, 0.83f), new Vector2(0.96f, 0.96f));
        AddBarLayer(fill.rectTransform, "SpecularLine", new Color(1f, 1f, 1f, 0.07f),
            new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.77f));
        AddBarLayer(fill.rectTransform, "BottomShade", new Color(0.005f, 0.012f, 0.025f, 0.11f),
            Vector2.zero, new Vector2(1f, 0.14f));
        AddBarLayer(fill.rectTransform, "EdgeGlow", new Color(1f, 1f, 1f, 0.06f),
            new Vector2(0f, 0.10f), new Vector2(0.028f, 0.90f));
    }

    static Sprite CreateBarGradientSprite(Color baseColor, Color highlightColor)
    {
        const int width = 64;
        const int height = 256;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "HUD_Bar_MultiGradient",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color deep = Color.Lerp(baseColor, new Color(0.004f, 0.008f, 0.018f), 0.62f);
        Color upper = Color.Lerp(baseColor, highlightColor, 0.72f);
        for (int y = 0; y < height; y++)
        {
            float t = y / (height - 1f);
            float vertical = Mathf.SmoothStep(0f, 1f, t);
            Color verticalColor = Color.Lerp(deep, upper, vertical);
            float gloss = Mathf.Exp(-Mathf.Pow((t - 0.73f) / 0.18f, 2f));
            verticalColor = Color.Lerp(verticalColor, Color.white, gloss * 0.13f);
            for (int x = 0; x < width; x++)
            {
                float u = x / (width - 1f);
                float roundedLight = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.42f);
                Color color = Color.Lerp(verticalColor * 0.72f, verticalColor, roundedLight);
                color.a = 1f;
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite CreateEmptyBarGradientSprite(Color highlightColor)
    {
        const int width = 48;
        const int height = 128;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "HUD_Bar_EmptyGlassGradient",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color deep = new Color(0.004f, 0.008f, 0.016f, 0.96f);
        Color middle = Color.Lerp(new Color(0.025f, 0.045f, 0.07f, 0.96f), highlightColor, 0.10f);
        Color upper = Color.Lerp(new Color(0.04f, 0.065f, 0.09f, 0.96f), highlightColor, 0.18f);
        for (int y = 0; y < height; y++)
        {
            float t = y / (height - 1f);
            Color color = t < 0.3f ? Color.Lerp(deep, middle, t / 0.3f) :
                t < 0.76f ? Color.Lerp(middle, upper, (t - 0.3f) / 0.46f) :
                Color.Lerp(upper, middle, (t - 0.76f) / 0.24f);
            for (int x = 0; x < width; x++)
            {
                float u = x / (width - 1f);
                float roundedLight = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.5f);
                Color shaded = Color.Lerp(color * 0.68f, color, roundedLight);
                texture.SetPixel(x, y, shaded);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    static void AddBarLayer(RectTransform parent, string layerName, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        Transform existing = parent.Find(layerName);
        Image layer;
        if (existing != null)
            layer = existing.GetComponent<Image>();
        else
        {
            GameObject go = new GameObject(layerName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            layer = go.GetComponent<Image>();
        }

        RectTransform rect = layer.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        layer.color = color;
        layer.raycastTarget = false;
        layer.transform.SetAsLastSibling();
    }

    void EnsureOxygenPanel()
    {
        if (oxygenPanel != null || windSlider == null) return;

        RectTransform windRect = windSlider.GetComponent<RectTransform>();
        Transform parent = windRect.parent;
        if (parent == null) return;

        oxygenPanel = new GameObject("OxygenMeter", typeof(RectTransform), typeof(Image));
        oxygenPanel.transform.SetParent(parent, false);
        RectTransform panelRect = (RectTransform)oxygenPanel.transform;
        panelRect.anchorMin = windRect.anchorMin;
        panelRect.anchorMax = windRect.anchorMax;
        panelRect.pivot = windRect.pivot;
        panelRect.sizeDelta = new Vector2(Mathf.Max(210f, windRect.rect.width), 42f);
        panelRect.anchoredPosition = windRect.anchoredPosition + new Vector2(0f, -48f);
        MoveOxygenPanelToFreeSpot(panelRect, windRect);

        Image panelImage = oxygenPanel.GetComponent<Image>();
        panelImage.color = new Color(0.015f, 0.04f, 0.07f, 0.88f);
        panelImage.raycastTarget = false;
        Outline panelOutline = oxygenPanel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.22f, 0.78f, 1f, 0.52f);
        panelOutline.effectDistance = new Vector2(1f, -1f);

        oxygenLabel = CreateHudLabel(oxygenPanel.transform, "OxygenLabel", "OXÍGENO", 14f,
            new Vector2(8f, -10f), new Vector2(194f, 18f));
        oxygenLabel.color = new Color(0.68f, 0.92f, 1f, 1f);

        GameObject sliderObject = new GameObject("OxygenBar", typeof(RectTransform), typeof(Image), typeof(Slider));
        sliderObject.transform.SetParent(oxygenPanel.transform, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.offsetMin = new Vector2(10f, 7f);
        sliderRect.offsetMax = new Vector2(-10f, 17f);
        Image background = sliderObject.GetComponent<Image>();
        background.color = new Color(0.01f, 0.03f, 0.06f, 0.92f);
        background.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = new Color(0.08f, 0.58f, 0.94f, 1f);
        fillImage.raycastTarget = false;

        oxygenSlider = sliderObject.GetComponent<Slider>();
        oxygenSlider.targetGraphic = background;
        oxygenSlider.fillRect = fillRect;
        oxygenSlider.direction = Slider.Direction.LeftToRight;
        oxygenSlider.minValue = 0f;
        oxygenSlider.maxValue = 1f;
        oxygenSlider.value = 1f;
        oxygenSlider.transition = Selectable.Transition.None;
        PolishVitalBar(oxygenSlider, new Color(0.08f, 0.58f, 0.94f), new Color(0.58f, 0.96f, 1f));
        oxygenPanel.SetActive(false);
    }

    static void MoveOxygenPanelToFreeSpot(RectTransform oxygenRect, RectTransform windRect)
    {
        // The meter starts beneath the wind UI. If another direct sibling panel
        // already occupies that spot, keep moving down in compact HUD rows.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Rect candidate = LocalRect(oxygenRect);
            bool overlaps = false;
            foreach (RectTransform sibling in oxygenRect.parent.GetComponentsInChildren<RectTransform>(true))
            {
                if (sibling.parent != oxygenRect.parent || sibling == oxygenRect || sibling == windRect ||
                    !sibling.gameObject.activeInHierarchy || sibling.GetComponent<Image>() == null)
                    continue;

                if (candidate.Overlaps(LocalRect(sibling)))
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps) return;
            oxygenRect.anchoredPosition += new Vector2(0f, -48f);
        }
    }

    static Rect LocalRect(RectTransform rect)
    {
        Vector2 size = rect.rect.size;
        return new Rect(rect.anchoredPosition - Vector2.Scale(size, rect.pivot), size);
    }

    static TextMeshProUGUI CreateHudLabel(Transform parent, string name, string value,
        float size, Vector2 position, Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        ConfigureHudLabel(label, value, size, position, dimensions);
        return label;
    }

    static void ConfigureHudLabel(TextMeshProUGUI label, string value, float size,
        Vector2 position, Vector2 dimensions)
    {
        label.text = value;
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMin = 9f;
        label.fontSizeMax = size;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(1f, 0.82f, 0.3f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
    }

    static void ConfigureFillLabel(TextMeshProUGUI label, float size, float minSize,
        TextAlignmentOptions alignment)
    {
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMin = minSize;
        label.fontSizeMax = size;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(1f, 0.82f, 0.3f, 1f);
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 8f);
        rect.offsetMax = new Vector2(-8f, -8f);
    }

    void EnsureSkillAlert()
    {
        if (skillPointsAlert != null)
        {
            PositionSkillAlert(skillPointsAlert.GetComponent<RectTransform>());
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null)
            return;

        GameObject panel = new GameObject("SkillPointsAlert");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        PositionSkillAlert(rect);
        // Wider than before - the MU sprite font's letter-spacing fix (added so its ornate
        // serifs stop touching) makes the same message noticeably wider, and it was wrapping to
        // a second line that overlapped the first inside the old 46px-tall box.

        Image image = panel.AddComponent<Image>();
        // MU-style ornate banner frame (Bitszer "botones y recuadros mu_1", cropped to the plain
        // bar shape) instead of a flat color fill - matches the new UI direction and gives the
        // gold sprite-font text a consistent dark backdrop with real contrast.
        // _Clean variant: the source crop had a non-functional red X button baked into one end
        // (this banner isn't closable), so the clean asset mirrors the good end over it.
        Sprite bannerSprite = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        if (bannerSprite != null)
        {
            image.sprite = bannerSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.18f, 0.12f, 0.04f, 0.92f);
        }

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(panel.transform, false);
        skillPointsAlertText = textGo.AddComponent<TextMeshProUGUI>();
        skillPointsAlertText.fontSize = 18f;
        skillPointsAlertText.enableAutoSizing = true;
        skillPointsAlertText.fontSizeMin = 10f;
        skillPointsAlertText.fontSizeMax = 18f;
        skillPointsAlertText.fontStyle = FontStyles.Bold;
        skillPointsAlertText.color = new Color(1f, 0.86f, 0.42f, 1f);
        skillPointsAlertText.alignment = TextAlignmentOptions.Center;
        // Single-line banner - wrapping to a second line overlapped the first inside this short box.
        skillPointsAlertText.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform textRect = skillPointsAlertText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(34f, 10f);
        textRect.offsetMax = new Vector2(-34f, -10f);

        skillPointsAlert = panel;
    }

    static void PositionSkillAlert(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        // Dedicated second row below PlayerVitalsMU: appearing/disappearing can
        // never cover HP, ST, level or XP.
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 126f);
        rect.sizeDelta = new Vector2(460f, 44f);
    }
}

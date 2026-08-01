using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Freezes the game with Time.timeScale so every Update/animation/coroutine driven by
// Time.deltaTime stops uniformly (NPCs, animals, animations) without each system needing its
// own GameManager.IsGameplayActive() check. Bootstraps itself like RuntimeInventoryBootstrap.
public class PauseManager : MonoBehaviour
{
    static PauseManager instance;

    GameObject panel;
    Slider fogSlider;
    TextMeshProUGUI fogValueLabel;
    TextMeshProUGUI graphicsValueLabel;
    TextMeshProUGUI particleOptionLabel;
    TextMeshProUGUI shadowOptionLabel;
    GameObject fogControl;
    GameObject graphicsControl;
    GameObject audioControl;
    GameState stateBeforePause;
    bool isPaused;
    bool particlesEnabled = true;
    int shadowQualityLevel = 2;
    float nextParticleSweep;
    readonly Dictionary<ParticleSystem, bool> particleWasPlaying =
        new Dictionary<ParticleSystem, bool>();
    readonly Dictionary<ParticleSystemRenderer, bool> particleRendererState =
        new Dictionary<ParticleSystemRenderer, bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<PauseManager>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("RuntimePauseManager");
        go.AddComponent<PauseManager>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        GraphicsProfile profile = (GraphicsProfile)Mathf.Clamp(
            PlayerPrefs.GetInt("GraphicsProfile", 1), 0, 2);
        ApplyGraphicsProfile(profile, false);
        int defaultShadow = profile == GraphicsProfile.Low ? 0 :
            profile == GraphicsProfile.Medium ? 2 : 3;
        ApplyShadowQuality(Mathf.Clamp(
            PlayerPrefs.GetInt("ShadowQualityLevel", defaultShadow), 0, 3),
            false);
        particlesEnabled = PlayerPrefs.GetInt("ParticlesEnabled", 1) != 0;
        ApplyParticlesEnabled(particlesEnabled, false);
    }

    void Update()
    {
        // New VFX can be instantiated after the option was changed. Sweep at
        // a low unscaled frequency so disabled particles stay disabled without
        // adding meaningful per-frame cost.
        if (!particlesEnabled && Time.unscaledTime >= nextParticleSweep)
        {
            nextParticleSweep = Time.unscaledTime + .4f;
            DisableCurrentParticleSystems();
        }

        Keyboard kb = Keyboard.current;
        if (RuntimeChatConsole.IsTyping)
            return;
        if (kb != null && kb.pKey.wasPressedThisFrame)
            TogglePause();
    }

    void TogglePause()
    {
        if (isPaused) Resume();
        else Pause();
    }

    void Pause()
    {
        if (isPaused) return;
        isPaused = true;

        stateBeforePause = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Exploration;
        GameManager.Instance?.SetState(GameState.Paused);
        Time.timeScale = 0f;

        // Every other runtime panel (Inventory/Help/Chest/Blacksmith) unlocks the cursor on open -
        // ThirdPersonCamera/GameManager keep it Locked+invisible during normal gameplay, so without
        // this the fog slider (and anything else in the pause menu) is impossible to see or drag:
        // the OS cursor never appears and mouse motion just gets eaten as camera look input instead.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        BuildIfNeeded();
        SyncFogSliderToCurrentValue();
        SyncGraphicsLabel();
        SyncAdvancedGraphicsLabels();
        panel.SetActive(true);
    }

    void Resume()
    {
        if (!isPaused) return;
        isPaused = false;

        Time.timeScale = 1f;
        GameState restoreState = stateBeforePause == GameState.Paused ? GameState.Exploration : stateBeforePause;
        GameManager.Instance?.SetState(restoreState);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        panel?.SetActive(false);
    }

    void BuildIfNeeded()
    {
        if (panel != null) return;

        // A script reload while Unity is running resets this private reference but can leave
        // the previous runtime canvas alive. Two pause canvases then draw their labels on top
        // of each other. Remove that stale copy before rebuilding the single MU window.
        foreach (Canvas existingCanvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (existingCanvas != null && existingCanvas.gameObject.name == "PauseCanvas")
            {
                existingCanvas.gameObject.SetActive(false);
                Destroy(existingCanvas.gameObject);
            }
        }

        // Always build a dedicated canvas rather than reusing InventoryUI.FindReusableCanvas() -
        // that helper includes INACTIVE canvases (Inventory/Chest/etc. start hidden and only
        // activate on demand), so parenting the pause panel under whichever one happened to be
        // found first meant it sometimes landed under an inactive ancestor: panel.SetActive(true)
        // then does nothing since activeInHierarchy still depends on that ancestor, matching the
        // "P freezes the screen but no panel appears" report.
        GameObject canvasGo = new GameObject("PauseCanvas", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        panel = new GameObject("PausePanel", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.68f);
        bg.raycastTarget = false;

        Transform content = BuildSharpUIFrame(panel.transform);
        TextMeshProUGUI pauseTitle = CreatePauseLabel(content, "PauseTitle", "OPCIONES", 36f, 24f,
            new Vector2(0f, 268f), new Vector2(620f, 56f));
        pauseTitle.color = new Color(.96f, .74f, .25f);
        pauseTitle.fontStyle = FontStyles.Bold;
        TextMeshProUGUI instruction = CreatePauseLabel(content, "PauseInstruction", "Presiona P para continuar", 15f, 11f,
            new Vector2(0f, -292f), new Vector2(720f, 34f));
        instruction.color = new Color(.70f, .77f, .86f);

        BuildTabs(content);
        BuildFogControl(content);
        BuildGraphicsControl(content);
        BuildAudioControl(content);
        ShowTab(1);
        CentralUIWindowManager.Register(content.gameObject, "PAUSA", Resume, true);

        Canvas topCanvas = panel.AddComponent<Canvas>();
        topCanvas.overrideSorting = true;
        // Above every other runtime panel (chest/inventory 500-600, merchant dialogue 1000) -
        // pause must always render on top of whatever else happened to be open when P was
        // pressed, or it renders invisibly behind it despite being active.
        topCanvas.sortingOrder = 4000;
        // A Canvas on `panel` becomes the nearest-ancestor canvas for every child Graphic (bg,
        // text, the fog slider's images), so they register in GraphicRegistry under topCanvas -
        // not canvasGo. Without a raycaster here too, canvasGo's GraphicRaycaster queries the
        // wrong bucket and finds nothing: every click/drag on the pause panel silently misses,
        // which is exactly the reported "the fog slider won't drag" symptom.
        panel.AddComponent<GraphicRaycaster>();

        panel.SetActive(false);
    }

    static Transform BuildSharpUIFrame(Transform parent)
    {
        GameObject frame = new GameObject("SharpUI_PausePanel", typeof(RectTransform));
        frame.transform.SetParent(parent, false);
        Image frameImage = frame.AddComponent<Image>();
        frameImage.sprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        frameImage.type = Image.Type.Sliced;
        frameImage.color = Color.white;
        frameImage.raycastTarget = false;
        RectTransform rect = frame.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(1120f, 680f);
        return frame.transform;
    }

    void BuildTabs(Transform parent)
    {
        CreateGraphicsButton(parent, "GameplayTab", "JUEGO", new Vector2(-210f, 218f), () => ShowTab(0));
        CreateGraphicsButton(parent, "GraphicsTab", "GRÁFICOS", new Vector2(0f, 218f), () => ShowTab(1));
        CreateGraphicsButton(parent, "AudioTab", "AUDIO", new Vector2(210f, 218f), () => ShowTab(2));
    }

    void ShowTab(int index)
    {
        if (fogControl != null) fogControl.SetActive(index == 0);
        if (graphicsControl != null) graphicsControl.SetActive(index == 1);
        if (audioControl != null) audioControl.SetActive(index == 2);
    }

    static TextMeshProUGUI CreatePauseLabel(Transform parent, string name, string value,
        float maxSize, float minSize, Vector2 position, Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = maxSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = minSize;
        label.fontSizeMax = maxSize;
        label.alignment = TextAlignmentOptions.Center;
        label.characterSpacing = 0f;
        label.wordSpacing = 0f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Truncate;
        label.color = Color.white;
        label.raycastTarget = false;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.62f);
        rect.anchorMax = new Vector2(0.5f, 0.62f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        return label;
    }

    void BuildFogControl(Transform parent)
    {
        GameObject group = new GameObject("FogControl", typeof(RectTransform));
        fogControl = group;
        group.transform.SetParent(parent, false);
        Image groupBackground = group.AddComponent<Image>();
        groupBackground.sprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        groupBackground.type = Image.Type.Sliced;
        groupBackground.color = Color.white;
        groupBackground.raycastTarget = false;
        RectTransform groupRect = group.GetComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0.5f, 0.46f);
        groupRect.anchorMax = new Vector2(0.5f, 0.46f);
        groupRect.pivot = new Vector2(0.5f, 0.5f);
        groupRect.sizeDelta = new Vector2(900f, 250f);
        groupRect.anchoredPosition = Vector2.zero;

        GameObject titleGo = new GameObject("FogTitle", typeof(RectTransform));
        titleGo.transform.SetParent(group.transform, false);
        TextMeshProUGUI title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "Niebla (distancia de dibujado)";
        title.fontSize = 22;
        title.enableAutoSizing = true;
        title.fontSizeMin = 12f;
        title.fontSizeMax = 22f;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(.94f, .72f, .24f);
        title.raycastTarget = false;
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 30f);
        titleRect.anchoredPosition = Vector2.zero;

        GameObject sliderGo = new GameObject("FogSlider", typeof(RectTransform));
        sliderGo.transform.SetParent(group.transform, false);
        RectTransform sliderRect = sliderGo.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(1f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.sizeDelta = new Vector2(-90f, 20f);
        sliderRect.anchoredPosition = new Vector2(0f, -10f);

        fogSlider = sliderGo.AddComponent<Slider>();
        fogSlider.minValue = FogController.MinDistance;
        fogSlider.maxValue = FogController.MaxDistance;
        fogSlider.wholeNumbers = true;

        GameObject sliderBg = new GameObject("Background", typeof(RectTransform));
        sliderBg.transform.SetParent(sliderGo.transform, false);
        Image sliderBgImg = sliderBg.AddComponent<Image>();
        sliderBgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        RectTransform sliderBgRect = sliderBg.GetComponent<RectTransform>();
        sliderBgRect.anchorMin = Vector2.zero;
        sliderBgRect.anchorMax = Vector2.one;
        sliderBgRect.offsetMin = Vector2.zero;
        sliderBgRect.offsetMax = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(5f, 0f);
        fillAreaRect.offsetMax = new Vector2(-5f, 0f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(.30f, .61f, .76f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fogSlider.fillRect = fillRect;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGo.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = Vector2.zero;
        handleAreaRect.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle", typeof(RectTransform));
        handle.transform.SetParent(handleArea.transform, false);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(1f, .79f, .30f, 1f);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(16f, 26f);
        fogSlider.handleRect = handleRect;
        fogSlider.targetGraphic = handleImg;

        GameObject valueGo = new GameObject("FogValue", typeof(RectTransform));
        valueGo.transform.SetParent(group.transform, false);
        fogValueLabel = valueGo.AddComponent<TextMeshProUGUI>();
        fogValueLabel.fontSize = 20;
        fogValueLabel.enableAutoSizing = true;
        fogValueLabel.fontSizeMin = 11f;
        fogValueLabel.fontSizeMax = 20f;
        fogValueLabel.textWrappingMode = TextWrappingModes.NoWrap;
        fogValueLabel.alignment = TextAlignmentOptions.Center;
        fogValueLabel.color = Color.white;
        fogValueLabel.raycastTarget = false;
        RectTransform valueRect = fogValueLabel.GetComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0f, 0f);
        valueRect.anchorMax = new Vector2(1f, 0f);
        valueRect.pivot = new Vector2(0.5f, 0f);
        valueRect.sizeDelta = new Vector2(0f, 25f);
        valueRect.anchoredPosition = Vector2.zero;

        fogSlider.onValueChanged.AddListener(OnFogSliderChanged);
    }

    void OnFogSliderChanged(float value)
    {
        FogController.Instance?.SetDistance(value);
        if (fogValueLabel != null) fogValueLabel.text = $"{value:0} m";
    }

    void SyncFogSliderToCurrentValue()
    {
        if (fogSlider == null) return;
        float current = FogController.Instance != null ? FogController.Instance.CurrentDistance : FogController.MaxDistance * 0.7f;
        fogSlider.SetValueWithoutNotify(current);
        if (fogValueLabel != null) fogValueLabel.text = $"{current:0} m";
    }

    void BuildGraphicsControl(Transform parent)
    {
        GameObject group = new GameObject("GraphicsControl", typeof(RectTransform));
        graphicsControl = group;
        group.transform.SetParent(parent, false);
        Image groupBackground = group.AddComponent<Image>();
        groupBackground.sprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        groupBackground.type = Image.Type.Sliced;
        groupBackground.color = Color.white;
        groupBackground.raycastTarget = false;
        RectTransform groupRect = group.GetComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0.5f, 0.46f);
        groupRect.anchorMax = new Vector2(0.5f, 0.46f);
        groupRect.pivot = new Vector2(0.5f, 0.5f);
        groupRect.sizeDelta = new Vector2(900f, 360f);

        TextMeshProUGUI title = CreatePauseLabel(group.transform, "GraphicsTitle", "GRAFICOS", 26f, 14f,
            new Vector2(0f, 145f), new Vector2(520f, 30f));
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        title.color = new Color(.94f, .72f, .24f);

        CreateGraphicsButton(group.transform, "LowGraphics", "BAJO", new Vector2(-180f, 88f), () => ApplyGraphicsProfile(GraphicsProfile.Low));
        CreateGraphicsButton(group.transform, "MediumGraphics", "MEDIO", new Vector2(0f, 88f), () => ApplyGraphicsProfile(GraphicsProfile.Medium));
        CreateGraphicsButton(group.transform, "HighGraphics", "ALTO", new Vector2(180f, 88f), () => ApplyGraphicsProfile(GraphicsProfile.High));

        TextMeshProUGUI particleTitle = CreatePauseLabel(group.transform,
            "ParticleTitle", "EFECTOS DE PARTICULAS", 17f, 12f,
            new Vector2(-245f, 22f), new Vector2(300f, 30f));
        particleTitle.rectTransform.anchorMin = particleTitle.rectTransform.anchorMax =
            new Vector2(.5f, .5f);
        Button particleButton = CreateGraphicsButton(group.transform,
            "ParticleToggle", "PARTICULAS", new Vector2(180f, 22f),
            ToggleParticles);
        particleButton.GetComponent<RectTransform>().sizeDelta =
            new Vector2(260f, 38f);
        particleOptionLabel =
            particleButton.GetComponentInChildren<TextMeshProUGUI>(true);

        TextMeshProUGUI shadowTitle = CreatePauseLabel(group.transform,
            "ShadowTitle", "CALIDAD DE SOMBRAS", 17f, 12f,
            new Vector2(-245f, -42f), new Vector2(300f, 30f));
        shadowTitle.rectTransform.anchorMin = shadowTitle.rectTransform.anchorMax =
            new Vector2(.5f, .5f);
        Button shadowButton = CreateGraphicsButton(group.transform,
            "ShadowQuality", "SOMBRAS", new Vector2(180f, -42f),
            CycleShadowQuality);
        shadowButton.GetComponent<RectTransform>().sizeDelta =
            new Vector2(260f, 38f);
        shadowOptionLabel =
            shadowButton.GetComponentInChildren<TextMeshProUGUI>(true);

        GameObject valueGo = new GameObject("GraphicsValue", typeof(RectTransform));
        valueGo.transform.SetParent(group.transform, false);
        graphicsValueLabel = valueGo.AddComponent<TextMeshProUGUI>();
        graphicsValueLabel.fontSize = 15f;
        graphicsValueLabel.alignment = TextAlignmentOptions.Center;
        graphicsValueLabel.color = new Color(.82f, .88f, 1f);
        graphicsValueLabel.raycastTarget = false;
        RectTransform valueRect = graphicsValueLabel.rectTransform;
        valueRect.anchorMin = valueRect.anchorMax = new Vector2(.5f, .5f);
        valueRect.sizeDelta = new Vector2(540f, 26f);
        valueRect.anchoredPosition = new Vector2(0f, -137f);
        SyncAdvancedGraphicsLabels();
    }

    void BuildAudioControl(Transform parent)
    {
        audioControl = new GameObject("AudioControl", typeof(RectTransform), typeof(Image));
        audioControl.transform.SetParent(parent, false);
        RectTransform rect = audioControl.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .46f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(900f, 250f);
        Image image = audioControl.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        image.type = Image.Type.Sliced;
        image.color = Color.white;

        TextMeshProUGUI title = CreatePauseLabel(audioControl.transform, "AudioTitle", "SONIDO", 26f, 14f,
            new Vector2(0f, 78f), new Vector2(520f, 30f));
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(.5f, .5f);
        title.color = new Color(.94f, .72f, .24f);

        TextMeshProUGUI master = CreatePauseLabel(audioControl.transform, "MasterLabel", "VOLUMEN GENERAL", 17f, 12f,
            new Vector2(-265f, 8f), new Vector2(230f, 30f));
        master.rectTransform.anchorMin = master.rectTransform.anchorMax = new Vector2(.5f, .5f);
        Slider slider = CreateAudioSlider(audioControl.transform, new Vector2(90f, 8f));
        slider.value = AudioListener.volume;
        slider.onValueChanged.AddListener(value => AudioListener.volume = value);
    }

    static Slider CreateAudioSlider(Transform parent, Vector2 position)
    {
        GameObject root = new GameObject("MasterVolume", typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(430f, 18f);
        rect.anchoredPosition = position;
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(root.transform, false);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(.10f, .075f, .055f, .95f);
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(root.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.offsetMin = new Vector2(2f,2f); fillRect.offsetMax = new Vector2(-2f,-2f);
        fill.GetComponent<Image>().color = new Color(.72f, .38f, .13f);
        Slider slider = root.GetComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 1f; slider.fillRect = fillRect;
        return slider;
    }

    static Button CreateGraphicsButton(Transform parent, string name, string text, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        Button button = SharpUIRuntimeFactory.CreateRectButton(
            parent, name, text, action, 38f);
        GameObject go = button.gameObject;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(132f, 38f);
        rect.anchoredPosition = position;

        TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = text;
            label.fontSize = 18f;
            label.color = Color.white;
        }
        return button;
    }

    enum GraphicsProfile { Low, Medium, High }

    void ApplyGraphicsProfile(GraphicsProfile profile, bool persist = true)
    {
        // These settings are supported in URP and apply immediately without recreating the
        // scene. Render scale especially gives a large performance gain on weaker hardware.
        switch (profile)
        {
            case GraphicsProfile.Low:
                ScalableBufferManager.ResizeBuffers(.70f, .70f);
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.shadowDistance = 0f;
                QualitySettings.lodBias = .65f;
                QualitySettings.maximumLODLevel = 1;
                QualitySettings.antiAliasing = 0;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                Application.targetFrameRate = 60;
                break;
            case GraphicsProfile.Medium:
                ScalableBufferManager.ResizeBuffers(.85f, .85f);
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowDistance = 35f;
                QualitySettings.lodBias = .9f;
                QualitySettings.maximumLODLevel = 0;
                QualitySettings.antiAliasing = 2;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                Application.targetFrameRate = 60;
                break;
            default:
                ScalableBufferManager.ResizeBuffers(1f, 1f);
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowDistance = 75f;
                QualitySettings.lodBias = 1.35f;
                QualitySettings.maximumLODLevel = 0;
                QualitySettings.antiAliasing = 4;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                Application.targetFrameRate = -1;
                break;
        }
        if (persist)
        {
            PlayerPrefs.SetInt("GraphicsProfile", (int)profile);
            ApplyShadowQuality(profile == GraphicsProfile.Low ? 0 :
                profile == GraphicsProfile.Medium ? 2 : 3, true);
            PlayerPrefs.Save();
        }
        SyncGraphicsLabel();
    }

    void SyncGraphicsLabel()
    {
        if (graphicsValueLabel == null) return;
        int profile = PlayerPrefs.GetInt("GraphicsProfile", 1);
        graphicsValueLabel.text = profile switch
        {
            0 => "Bajo: maximo rendimiento (sombras desactivadas)",
            2 => "Alto: maxima calidad visual",
            _ => "Medio: calidad y rendimiento equilibrados"
        };
    }

    void ToggleParticles()
    {
        ApplyParticlesEnabled(!particlesEnabled, true);
    }

    void ApplyParticlesEnabled(bool enabled, bool persist)
    {
        particlesEnabled = enabled;
        if (enabled)
            RestoreParticleSystems();
        else
            DisableCurrentParticleSystems();

        if (persist)
        {
            PlayerPrefs.SetInt("ParticlesEnabled", enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
        SyncAdvancedGraphicsLabels();
    }

    void DisableCurrentParticleSystems()
    {
        foreach (ParticleSystem system in
                 FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include))
        {
            if (system == null)
                continue;
            if (!particleWasPlaying.ContainsKey(system))
                particleWasPlaying[system] = system.isPlaying;

            ParticleSystemRenderer renderer =
                system.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                if (!particleRendererState.ContainsKey(renderer))
                    particleRendererState[renderer] = renderer.enabled;
                renderer.enabled = false;
            }
            system.Stop(true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void RestoreParticleSystems()
    {
        foreach (KeyValuePair<ParticleSystemRenderer, bool> pair in
                 particleRendererState)
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;

        foreach (KeyValuePair<ParticleSystem, bool> pair in particleWasPlaying)
            if (pair.Key != null && pair.Value && pair.Key.gameObject.activeInHierarchy)
                pair.Key.Play(true);

        particleRendererState.Clear();
        particleWasPlaying.Clear();
    }

    void CycleShadowQuality()
    {
        // Each click reduces one step; after Off it returns to High.
        int next = shadowQualityLevel <= 0 ? 3 : shadowQualityLevel - 1;
        ApplyShadowQuality(next, true);
    }

    void ApplyShadowQuality(int level, bool persist)
    {
        level = Mathf.Clamp(level, 0, 3);
        shadowQualityLevel = level;
        switch (level)
        {
            case 0:
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.shadowDistance = 0f;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.shadowCascades = 0;
                break;
            case 1:
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowDistance = 22f;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.shadowCascades = 0;
                break;
            case 2:
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowDistance = 45f;
                QualitySettings.shadowResolution = ShadowResolution.Medium;
                QualitySettings.shadowCascades = 2;
                break;
            default:
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowDistance = 75f;
                QualitySettings.shadowResolution = ShadowResolution.High;
                QualitySettings.shadowCascades = 4;
                break;
        }

        if (persist)
        {
            PlayerPrefs.SetInt("ShadowQualityLevel", level);
            PlayerPrefs.Save();
        }
        SyncAdvancedGraphicsLabels();
    }

    void SyncAdvancedGraphicsLabels()
    {
        if (particleOptionLabel != null)
            particleOptionLabel.text = particlesEnabled
                ? "PARTICULAS: SI"
                : "PARTICULAS: NO";

        if (shadowOptionLabel == null)
            return;
        shadowOptionLabel.text = shadowQualityLevel switch
        {
            0 => "SOMBRAS: NO",
            1 => "SOMBRAS: BAJA",
            2 => "SOMBRAS: MEDIA",
            _ => "SOMBRAS: ALTA"
        };
    }
}

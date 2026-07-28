using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class NewGameMainMenu : MonoBehaviour
{
    const string VillageScene = "SpawnVillage";
    static readonly Color Gold = SharpUIThemeApplicator.Accent;
    static readonly Color Cream = SharpUIThemeApplicator.Body;

    GameObject mainPanel;
    GameObject characterPanel;
    GameObject optionsPanel;
    GameObject loadingPanel;
    TMP_InputField nameInput;
    TextMeshProUGUI archetypeText;
    TextMeshProUGUI graphicsText;
    TextMeshProUGUI resolutionText;
    TextMeshProUGUI volumeText;
    TextMeshProUGUI sensitivityText;
    Button continueButton;
    Slider volumeSlider;
    Slider sensitivitySlider;
    Toggle fullscreenToggle;
    Toggle vsyncToggle;
    TMP_Dropdown resolutionDropdown;
    TMP_Dropdown fpsDropdown;
    Slider loadingBar;
    TextMeshProUGUI loadingText;
    Canvas canvas;
    Transform preview;
    readonly List<Canvas> suppressedCanvases = new List<Canvas>();
    readonly HashSet<Transform> menuCanvasRoots = new HashSet<Transform>();
    float nextCanvasSweep;
    bool isLoading;
    int selectedArchetype;
    int selectedGraphics;

    void Awake()
    {
        Time.timeScale = 1f;
        KeepMenuCursorAvailable();
        preview = GameObject.Find("CharacterPreview")?.transform;
        ApplySavedOptions();
        Build();
        SuppressForeignCanvases();
        ShowMain();
    }

    void Update()
    {
        // GameManager and the third-person camera can be created a frame after this menu.
        // Both normally lock the cursor for gameplay, so the NewGame scene must explicitly
        // retain menu ownership every frame.
        KeepMenuCursorAvailable();
        if (Time.unscaledTime >= nextCanvasSweep)
        {
            nextCanvasSweep = Time.unscaledTime + .5f;
            SuppressForeignCanvases();
        }
        if (Keyboard.current != null && characterPanel != null &&
            characterPanel.activeSelf && Keyboard.current.enterKey.wasPressedThisFrame)
            StartNewGame();
        if (preview != null && (Mouse.current == null || !Mouse.current.leftButton.isPressed))
            preview.Rotate(Vector3.up, 9f * Time.unscaledDeltaTime, Space.World);
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) KeepMenuCursorAvailable();
    }

    static void KeepMenuCursorAvailable()
    {
        if (SceneManager.GetActiveScene().name != "NewGame") return;
        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState != GameState.InMenu)
            GameManager.Instance.SetState(GameState.InMenu);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void SuppressForeignCanvases()
    {
        DestroyGameplayUi<HelpPanelUI>();
        DestroyGameplayUi<QuestTrackerUI>();
        DestroyGameplayUi<QuickbarManager>();

        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvas.transform.GetChild(i);
            if (!menuCanvasRoots.Contains(child))
                Destroy(child.gameObject);
        }

        Canvas[] sceneCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (Canvas candidate in sceneCanvases)
        {
            if (candidate == null || candidate == canvas ||
                candidate.transform.IsChildOf(transform) || !candidate.enabled)
                continue;
            suppressedCanvases.Add(candidate);
            candidate.enabled = false;
        }
    }

    static void DestroyGameplayUi<T>() where T : Component
    {
        T[] components = FindObjectsByType<T>(FindObjectsInactive.Include);
        foreach (T component in components)
            if (component != null)
                Destroy(component.gameObject);
    }

    void RestoreForeignCanvases()
    {
        foreach (Canvas candidate in suppressedCanvases)
            if (candidate != null)
                candidate.enabled = true;
        suppressedCanvases.Clear();
    }

    void Build()
    {
        GameObject canvasObject = new GameObject("NewGameCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        GameObject goblinPortrait = new GameObject("KingGoblinFaceBackground",
            typeof(RectTransform), typeof(RawImage), typeof(NewGameKingGoblinPortrait));
        goblinPortrait.transform.SetParent(canvas.transform, false);
        RectTransform portraitRect = goblinPortrait.GetComponent<RectTransform>();
        // The 3D character projects around the centre-right of the camera. Reserve the far-right
        // column exclusively for the King's head instead of drawing it behind the player.
        SetRect(portraitRect, new Vector2(.765f, .43f), new Vector2(.998f, .985f));
        RawImage portraitImage = goblinPortrait.GetComponent<RawImage>();
        portraitImage.raycastTarget = false;

        Image vignette = ImageObject(canvas.transform, "Vignette",
            new Color(.015f, .008f, .018f, .54f), Vector2.zero, Vector2.one);
        vignette.raycastTarget = false;

        TextMeshProUGUI title = Text(canvas.transform, "Title",
            "EL LATIDO DE LA PESTE", 58f, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(.06f, .82f), new Vector2(.64f, .96f));
        title.alignment = TextAlignmentOptions.Left;
        title.color = SharpUIThemeApplicator.Title;
        title.characterSpacing = 0f;

        TextMeshProUGUI subtitle = Text(canvas.transform, "Subtitle",
            "EL DIAMANTE ROJO", 21f, FontStyles.Normal);
        SetRect(subtitle.rectTransform, new Vector2(.065f, .78f), new Vector2(.48f, .84f));
        subtitle.alignment = TextAlignmentOptions.Left;
        subtitle.color = new Color(.75f, .65f, .55f);
        subtitle.characterSpacing = 0f;

        mainPanel = Panel(canvas.transform, "MainMenuPanel",
            new Vector2(.055f, .16f), new Vector2(.38f, .72f));
        BuildMainPanel(mainPanel.transform);

        characterPanel = Panel(canvas.transform, "CharacterCreationPanel",
            new Vector2(.055f, .08f), new Vector2(.48f, .75f));
        BuildCharacterPanel(characterPanel.transform);

        optionsPanel = Panel(canvas.transform, "OptionsPanel",
            new Vector2(.055f, .06f), new Vector2(.53f, .76f));
        BuildOptionsPanel(optionsPanel.transform);

        GameObject rotateArea = new GameObject("CharacterRotationArea",
            typeof(RectTransform), typeof(Image), typeof(CharacterPreviewDrag));
        rotateArea.transform.SetParent(canvas.transform, false);
        RectTransform rotateRect = rotateArea.GetComponent<RectTransform>();
        SetRect(rotateRect, new Vector2(.55f, .08f), new Vector2(.96f, .88f));
        Image rotateImage = rotateArea.GetComponent<Image>();
        rotateImage.color = new Color(0f, 0f, 0f, .001f);
        rotateArea.GetComponent<CharacterPreviewDrag>().SetTarget(preview);

        TextMeshProUGUI rotateHint = Text(canvas.transform, "RotateHint",
            "ARRASTRÁ PARA GIRAR EL PERSONAJE", 14f, FontStyles.Normal);
        SetRect(rotateHint.rectTransform, new Vector2(.62f, .055f), new Vector2(.93f, .095f));
        rotateHint.color = new Color(.78f, .68f, .54f, .85f);

        BuildLoadingPanel();
        foreach (Transform child in canvas.transform)
            menuCanvasRoots.Add(child);
    }

    void BuildMainPanel(Transform parent)
    {
        TextMeshProUGUI header = Text(parent, "Header", "MENÚ PRINCIPAL", 25f,
            FontStyles.Bold);
        AddLayout(header.gameObject, 58f);
        continueButton = ButtonObject(parent, "ContinueButton", "CONTINUAR",
            ContinueGame);
        continueButton.interactable = PlayerPrefs.GetInt("HasStartedGame", 0) == 1;
        ButtonObject(parent, "NewGameButton", "NUEVO JUEGO", OpenCharacterCreation);
        ButtonObject(parent, "OptionsButton", "OPCIONES", OpenOptions);
        ButtonObject(parent, "ExitButton", "SALIR", ExitGame);
        TextMeshProUGUI version = Text(parent, "Version",
            continueButton.interactable
                ? "Hay una aventura iniciada"
                : "Todavía no hay una aventura iniciada",
            14f, FontStyles.Italic);
        version.color = new Color(.68f, .62f, .56f);
        AddLayout(version.gameObject, 38f);
    }

    void BuildCharacterPanel(Transform parent)
    {
        TextMeshProUGUI header = Text(parent, "Header",
            "CREAR PERSONAJE", 25f, FontStyles.Bold);
        AddLayout(header.gameObject, 50f);
        TextMeshProUGUI prompt = Text(parent, "NamePrompt", "Nombre", 16f);
        prompt.alignment = TextAlignmentOptions.Left;
        AddLayout(prompt.gameObject, 27f);
        nameInput = InputFieldObject(parent, "PlayerName",
            PlayerPrefs.GetString("PlayerName", "Frank"));
        AddLayout(nameInput.gameObject, 48f);

        TextMeshProUGUI origin = Text(parent, "ArchetypePrompt",
            "Arquetipo inicial", 16f);
        origin.alignment = TextAlignmentOptions.Left;
        AddLayout(origin.gameObject, 34f);

        GameObject row = new GameObject("ArchetypeButtons",
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        row.GetComponent<LayoutElement>().preferredHeight = 50f;
        ButtonObject(row.transform, "Warrior", "GUERRERO", () => SelectArchetype(0));
        ButtonObject(row.transform, "Mage", "MAGO", () => SelectArchetype(1));
        ButtonObject(row.transform, "Explorer", "EXPLORADOR", () => SelectArchetype(2));

        archetypeText = Text(parent, "ArchetypeDescription", "", 15f);
        archetypeText.textWrappingMode = TextWrappingModes.Normal;
        archetypeText.alignment = TextAlignmentOptions.TopLeft;
        archetypeText.color = Cream;
        AddLayout(archetypeText.gameObject, 102f);
        selectedArchetype = PlayerPrefs.GetInt("PlayerArchetype", 0);
        SelectArchetype(selectedArchetype);

        ButtonObject(parent, "StartAdventure", "COMENZAR AVENTURA", StartNewGame);
        ButtonObject(parent, "Back", "VOLVER", ShowMain);
    }

    void BuildOptionsPanel(Transform parent)
    {
        TextMeshProUGUI header = Text(parent, "Header", "OPCIONES", 25f,
            FontStyles.Bold);
        AddLayout(header.gameObject, 45f);

        graphicsText = Text(parent, "GraphicsValue", "", 15f);
        graphicsText.alignment = TextAlignmentOptions.Left;
        AddLayout(graphicsText.gameObject, 28f);
        GameObject graphicsRow = HorizontalRow(parent, "GraphicsButtons", 45f);
        ButtonObject(graphicsRow.transform, "Low", "BAJO", () => SetGraphics(0));
        ButtonObject(graphicsRow.transform, "Medium", "MEDIO", () => SetGraphics(1));
        ButtonObject(graphicsRow.transform, "High", "ALTO", () => SetGraphics(2));

        resolutionText = Text(parent, "ResolutionLabel", "Resolución", 15f);
        resolutionText.alignment = TextAlignmentOptions.Left;
        AddLayout(resolutionText.gameObject, 25f);
        resolutionDropdown = Dropdown(parent, "ResolutionDropdown");
        PopulateResolutions();
        AddLayout(resolutionDropdown.gameObject, 43f);

        fullscreenToggle = ToggleObject(parent, "Fullscreen", "Pantalla completa");
        fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        vsyncToggle = ToggleObject(parent, "VSync", "Sincronización vertical");
        vsyncToggle.isOn = PlayerPrefs.GetInt("VSync", 1) == 1;
        vsyncToggle.onValueChanged.AddListener(SetVSync);

        fpsDropdown = Dropdown(parent, "FpsLimit");
        fpsDropdown.AddOptions(new List<string> { "FPS: 30", "FPS: 60", "FPS: 120", "FPS: Sin límite" });
        fpsDropdown.value = PlayerPrefs.GetInt("FpsOption", 1);
        fpsDropdown.onValueChanged.AddListener(SetFps);
        AddLayout(fpsDropdown.gameObject, 42f);

        volumeText = Text(parent, "VolumeLabel", "", 15f);
        volumeText.alignment = TextAlignmentOptions.Left;
        AddLayout(volumeText.gameObject, 24f);
        volumeSlider = SliderObject(parent, "MasterVolume", 0f, 1f,
            PlayerPrefs.GetFloat("MasterVolume", .8f));
        volumeSlider.onValueChanged.AddListener(SetVolume);

        sensitivityText = Text(parent, "SensitivityLabel", "", 15f);
        sensitivityText.alignment = TextAlignmentOptions.Left;
        AddLayout(sensitivityText.gameObject, 24f);
        sensitivitySlider = SliderObject(parent, "MouseSensitivity", .25f, 2.5f,
            PlayerPrefs.GetFloat("MouseSensitivity", 1f));
        sensitivitySlider.onValueChanged.AddListener(SetSensitivity);

        selectedGraphics = Mathf.Clamp(PlayerPrefs.GetInt("GraphicsProfile", 1), 0, 2);
        SyncOptionLabels();
        ButtonObject(parent, "Back", "GUARDAR Y VOLVER", SaveOptionsAndBack);
    }

    void BuildLoadingPanel()
    {
        loadingPanel = Panel(canvas.transform, "LoadingPanel",
            new Vector2(.24f, .38f), new Vector2(.76f, .62f));
        Transform parent = loadingPanel.transform;
        TextMeshProUGUI label = Text(parent, "Header", "VIAJANDO A SPAWNVILLAGE",
            24f, FontStyles.Bold);
        AddLayout(label.gameObject, 50f);
        loadingBar = SliderObject(parent, "LoadingProgress", 0f, 1f, 0f);
        loadingBar.interactable = false;
        loadingText = Text(parent, "LoadingText", "Accediendo a Boat Stain...", 15f);
        AddLayout(loadingText.gameObject, 35f);
        loadingPanel.SetActive(false);
    }

    void ShowMain()
    {
        mainPanel.SetActive(true);
        characterPanel.SetActive(false);
        optionsPanel.SetActive(false);
    }

    void OpenCharacterCreation()
    {
        mainPanel.SetActive(false);
        optionsPanel.SetActive(false);
        characterPanel.SetActive(true);
    }

    void OpenOptions()
    {
        mainPanel.SetActive(false);
        characterPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    void SelectArchetype(int index)
    {
        selectedArchetype = Mathf.Clamp(index, 0, 2);
        archetypeText.text = selectedArchetype switch
        {
            1 => "<b>Mago</b>\nAfinidad con hechizos y energía elemental. Empieza orientado al combate a distancia.",
            2 => "<b>Explorador</b>\nMayor movilidad y resistencia. Ideal para descubrir el origen de la peste.",
            _ => "<b>Guerrero</b>\nAtaque físico y defensa equilibrados. Ideal para dominar espadas desde el comienzo."
        };
    }

    void StartNewGame()
    {
        if (isLoading) return;
        isLoading = true;
        string playerName = string.IsNullOrWhiteSpace(nameInput.text)
            ? "Frank"
            : nameInput.text.Trim();
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.SetInt("PlayerArchetype", selectedArchetype);
        PlayerPrefs.SetInt("HasStartedGame", 1);
        PlayerPrefs.SetInt("NewGameRequested", 1);
        PlayerPrefs.Save();
        StartCoroutine(LoadVillage());
    }

    void ContinueGame()
    {
        if (isLoading) return;
        isLoading = true;
        StartCoroutine(LoadVillage());
    }

    IEnumerator LoadVillage()
    {
        RestoreForeignCanvases();
        mainPanel.SetActive(false);
        characterPanel.SetActive(false);
        optionsPanel.SetActive(false);
        loadingPanel.SetActive(true);
        AsyncOperation operation = SceneManager.LoadSceneAsync(VillageScene);
        if (operation == null) yield break;
        operation.allowSceneActivation = false;
        while (operation.progress < .9f)
        {
            float progress = Mathf.Clamp01(operation.progress / .9f);
            loadingBar.value = progress;
            loadingText.text = "Accediendo a Boat Stain... " + Mathf.RoundToInt(progress * 100f) + "%";
            yield return null;
        }
        loadingBar.value = 1f;
        loadingText.text = "Boat Stain te espera";
        yield return new WaitForSecondsRealtime(.35f);
        operation.allowSceneActivation = true;
    }

    void ApplySavedOptions()
    {
        SetGraphics(PlayerPrefs.GetInt("GraphicsProfile", 1), false);
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", .8f);
        QualitySettings.vSyncCount = PlayerPrefs.GetInt("VSync", 1);
        ApplyFps(PlayerPrefs.GetInt("FpsOption", 1));
    }

    void SetGraphics(int profile) => SetGraphics(profile, true);

    void SetGraphics(int profile, bool save)
    {
        selectedGraphics = Mathf.Clamp(profile, 0, 2);
        switch (selectedGraphics)
        {
            case 0:
                ScalableBufferManager.ResizeBuffers(.70f, .70f);
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.shadowDistance = 0f;
                QualitySettings.lodBias = .65f;
                QualitySettings.antiAliasing = 0;
                break;
            case 2:
                ScalableBufferManager.ResizeBuffers(1f, 1f);
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowDistance = 75f;
                QualitySettings.lodBias = 1.35f;
                QualitySettings.antiAliasing = 4;
                break;
            default:
                ScalableBufferManager.ResizeBuffers(.85f, .85f);
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowDistance = 35f;
                QualitySettings.lodBias = .9f;
                QualitySettings.antiAliasing = 2;
                break;
        }
        if (save) PlayerPrefs.SetInt("GraphicsProfile", selectedGraphics);
        SyncOptionLabels();
    }

    void SetFullscreen(bool value)
    {
        Screen.fullScreen = value;
        PlayerPrefs.SetInt("Fullscreen", value ? 1 : 0);
    }

    void SetVSync(bool value)
    {
        QualitySettings.vSyncCount = value ? 1 : 0;
        PlayerPrefs.SetInt("VSync", value ? 1 : 0);
    }

    void SetFps(int option)
    {
        PlayerPrefs.SetInt("FpsOption", option);
        ApplyFps(option);
    }

    static void ApplyFps(int option)
    {
        Application.targetFrameRate = option switch
        {
            0 => 30,
            2 => 120,
            3 => -1,
            _ => 60
        };
    }

    void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
        SyncOptionLabels();
    }

    void SetSensitivity(float value)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        SyncOptionLabels();
    }

    void SaveOptionsAndBack()
    {
        PlayerPrefs.SetInt("GraphicsProfile", selectedGraphics);
        PlayerPrefs.Save();
        ShowMain();
    }

    void PopulateResolutions()
    {
        resolutionDropdown.ClearOptions();
        Resolution[] resolutions = Screen.resolutions;
        List<string> options = new List<string>();
        int selected = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            Resolution resolution = resolutions[i];
            options.Add(resolution.width + " x " + resolution.height +
                        "  " + Mathf.RoundToInt((float)resolution.refreshRateRatio.value) + " Hz");
            if (resolution.width == Screen.currentResolution.width &&
                resolution.height == Screen.currentResolution.height)
                selected = i;
        }
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = Mathf.Clamp(
            PlayerPrefs.GetInt("ResolutionIndex", selected), 0,
            Mathf.Max(0, resolutions.Length - 1));
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    void SetResolution(int index)
    {
        Resolution[] resolutions = Screen.resolutions;
        if (resolutions.Length == 0) return;
        index = Mathf.Clamp(index, 0, resolutions.Length - 1);
        Resolution selected = resolutions[index];
        FullScreenMode mode = fullscreenToggle != null && fullscreenToggle.isOn
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;
        Screen.SetResolution(selected.width, selected.height, mode,
            selected.refreshRateRatio);
        PlayerPrefs.SetInt("ResolutionIndex", index);
    }

    void SyncOptionLabels()
    {
        if (graphicsText != null)
            graphicsText.text = "Calidad gráfica: " +
                (selectedGraphics == 0 ? "Baja" : selectedGraphics == 2 ? "Alta" : "Media");
        if (volumeText != null && volumeSlider != null)
            volumeText.text = "Volumen general: " +
                Mathf.RoundToInt(volumeSlider.value * 100f) + "%";
        if (sensitivityText != null && sensitivitySlider != null)
            sensitivityText.text = "Sensibilidad del mouse: " +
                sensitivitySlider.value.ToString("0.00");
    }

    static void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static GameObject Panel(Transform parent, string name, Vector2 min, Vector2 max)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        SetRect(rect, min, max);
        Image image = panel.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(34, 34, 28, 28);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        return panel;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string value,
        float size, FontStyles style = FontStyles.Normal)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Cream;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.outlineWidth = 0f;
        text.characterSpacing = 0f;
        return text;
    }

    static Button ButtonObject(Transform parent, string name, string label,
        UnityEngine.Events.UnityAction action)
    {
        return SharpUIRuntimeFactory.CreateRectButton(parent, name, label, action, 52f);
    }

    static TMP_InputField InputFieldObject(Transform parent, string name, string value)
    {
        GameObject obj = new GameObject(name,
            typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/SharpUI/Input");
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        GameObject viewportObject = new GameObject("Text Area",
            typeof(RectTransform), typeof(RectMask2D));
        viewportObject.transform.SetParent(obj.transform, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        SetRect(viewport, Vector2.zero, Vector2.one, 12f);
        TextMeshProUGUI text = Text(viewport, "Text", value, 18f);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, 4f);
        text.alignment = TextAlignmentOptions.Left;
        TextMeshProUGUI placeholder = Text(viewport, "Placeholder",
            "Escribí el nombre...", 17f, FontStyles.Italic);
        SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, 4f);
        placeholder.alignment = TextAlignmentOptions.Left;
        placeholder.color = new Color(.55f, .5f, .46f);
        TMP_InputField input = obj.GetComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.text = value;
        input.characterLimit = 18;
        return input;
    }

    static TMP_Dropdown Dropdown(Transform parent, string name)
    {
        GameObject obj = new GameObject(name,
            typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/SharpUI/Button");
        image.type = Image.Type.Sliced;
        TextMeshProUGUI label = Text(obj.transform, "Label", "", 15f);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, 14f);
        label.alignment = TextAlignmentOptions.Left;
        GameObject templateObject = new GameObject("Template",
            typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        templateObject.transform.SetParent(obj.transform, false);
        RectTransform templateRect = templateObject.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -3f);
        templateRect.sizeDelta = new Vector2(0f, 190f);
        Image templateImage = templateObject.GetComponent<Image>();
        templateImage.sprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        templateImage.type = Image.Type.Sliced;

        GameObject viewportObject = new GameObject("Viewport",
            typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(templateObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        SetRect(viewportRect, Vector2.zero, Vector2.one, 7f);
        viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, .12f);
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 36f);

        GameObject itemObject = new GameObject("Item",
            typeof(RectTransform), typeof(Toggle));
        itemObject.transform.SetParent(contentObject.transform, false);
        RectTransform itemRect = itemObject.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, .5f);
        itemRect.anchorMax = new Vector2(1f, .5f);
        itemRect.sizeDelta = new Vector2(0f, 36f);
        Image itemBackground = ImageObject(itemObject.transform, "Item Background",
            new Color(.25f, .16f, .09f, .65f), Vector2.zero, Vector2.one);
        Image itemCheck = ImageObject(itemObject.transform, "Item Checkmark",
            Gold, new Vector2(.025f, .25f), new Vector2(.06f, .75f));
        TextMeshProUGUI item = Text(itemObject.transform, "Item Label", "", 15f);
        SetRect(item.rectTransform, new Vector2(.08f, 0f), new Vector2(.98f, 1f));
        item.alignment = TextAlignmentOptions.Left;
        Toggle itemToggle = itemObject.GetComponent<Toggle>();
        itemToggle.targetGraphic = itemBackground;
        itemToggle.graphic = itemCheck;

        ScrollRect scroll = templateObject.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        TMP_Dropdown dropdown = obj.GetComponent<TMP_Dropdown>();
        dropdown.captionText = label;
        dropdown.itemText = item;
        dropdown.template = templateRect;
        templateObject.SetActive(false);
        return dropdown;
    }

    static Toggle ToggleObject(Transform parent, string name, string value)
    {
        GameObject obj = new GameObject(name,
            typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<LayoutElement>().preferredHeight = 34f;
        GameObject box = new GameObject("Background",
            typeof(RectTransform), typeof(Image));
        box.transform.SetParent(obj.transform, false);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0f, .5f);
        boxRect.anchoredPosition = new Vector2(15f, 0f);
        boxRect.sizeDelta = new Vector2(28f, 28f);
        Image background = box.GetComponent<Image>();
        background.sprite = Resources.Load<Sprite>("UI/SharpUI/Slot");
        background.type = Image.Type.Sliced;
        GameObject check = new GameObject("Checkmark",
            typeof(RectTransform), typeof(Image));
        check.transform.SetParent(box.transform, false);
        RectTransform checkRect = check.GetComponent<RectTransform>();
        SetRect(checkRect, Vector2.zero, Vector2.one, 6f);
        check.GetComponent<Image>().color = Gold;
        TextMeshProUGUI label = Text(obj.transform, "Label", value, 15f);
        SetRect(label.rectTransform, new Vector2(.1f, 0f), Vector2.one);
        label.alignment = TextAlignmentOptions.Left;
        Toggle toggle = obj.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = check.GetComponent<Image>();
        return toggle;
    }

    static Slider SliderObject(Transform parent, string name, float min, float max,
        float value)
    {
        GameObject root = new GameObject(name,
            typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = 32f;
        Image background = ImageObject(root.transform, "Background",
            new Color(.22f, .18f, .16f), new Vector2(0f, .32f), new Vector2(1f, .68f));
        background.sprite = Resources.Load<Sprite>("UI/SharpUI/ResourceFrame");
        background.type = Image.Type.Sliced;
        Image fill = ImageObject(root.transform, "Fill", Gold,
            new Vector2(0f, .32f), new Vector2(1f, .68f));
        fill.sprite = Resources.Load<Sprite>("UI/SharpUI/ResourceFill");
        fill.type = Image.Type.Sliced;
        Image handle = ImageObject(root.transform, "Handle", Cream,
            new Vector2(.48f, .14f), new Vector2(.52f, .86f));
        handle.sprite = Resources.Load<Sprite>("UI/SharpUI/Slot");
        handle.type = Image.Type.Sliced;
        Slider slider = root.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        return slider;
    }

    static GameObject HorizontalRow(Transform parent, string name, float height)
    {
        GameObject row = new GameObject(name, typeof(RectTransform),
            typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        row.GetComponent<LayoutElement>().preferredHeight = height;
        return row;
    }

    static Image ImageObject(Transform parent, string name, Color color,
        Vector2 min, Vector2 max)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        SetRect(image.rectTransform, min, max);
        return image;
    }

    static void AddLayout(GameObject obj, float height)
    {
        LayoutElement layout = obj.GetComponent<LayoutElement>() ??
            obj.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
    }

    static void SetRect(RectTransform rect, Vector2 min, Vector2 max,
        float inset = 0f)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}

public sealed class DirectButtonTrigger : MonoBehaviour, IPointerDownHandler
{
    UnityEngine.Events.UnityAction action;
    public void SetAction(UnityEngine.Events.UnityAction value) => action = value;
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            action?.Invoke();
    }
}

public sealed class CharacterPreviewDrag : MonoBehaviour, IDragHandler
{
    Transform target;
    public void SetTarget(Transform value) => target = value;
    public void OnDrag(PointerEventData eventData)
    {
        if (target != null)
            target.Rotate(Vector3.up, -eventData.delta.x * .45f, Space.World);
    }
}

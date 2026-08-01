using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using System.Collections.Generic;

public class RuntimeChatConsole : MonoBehaviour
{
    const string DefaultSpeakerName = "player1";
    const int GodModeGold = 5000000;
    const int MaxVisibleMessages = 3;

    Canvas canvas;
    GameObject panel;
    GameObject logPanel;
    TextMeshProUGUI logText;
    TextMeshProUGUI inputText;
    TextMeshProUGUI placeholderText;
    string currentInput = "";
    readonly Queue<string> visibleLines = new Queue<string>();
    Keyboard subscribedKeyboard;
    bool isOpen;
    int openedFrame = -1;
    CanvasGroup logCanvasGroup;
    float lastActivityTime;
    const float IdleFadeDelay = 4f;
    const float IdleAlpha = 0.16f;
    public static bool IsTyping { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureExists()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "NewGame")
            return;
        if (FindAnyObjectByType<RuntimeChatConsole>(FindObjectsInactive.Include) != null)
            return;

        var go = new GameObject("RuntimeChatConsole");
        DontDestroyOnLoad(go);
        go.AddComponent<RuntimeChatConsole>();
    }

    void Awake()
    {
        BuildUI();
        Close();
    }

    void OnEnable()
    {
        SubscribeToKeyboard();
    }

    void OnDisable()
    {
        if (subscribedKeyboard != null)
            subscribedKeyboard.onTextInput -= OnTextInput;
        subscribedKeyboard = null;
    }

    void Update()
    {
        UpdateLogFade();
        var kb = Keyboard.current;
        if (kb == null)
            return;

        if (subscribedKeyboard != kb)
            SubscribeToKeyboard();

        bool enterPressed = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
        if (!isOpen && enterPressed)
        {
            Open();
            return;
        }

        if (!isOpen)
            return;

        if (kb.escapeKey.wasPressedThisFrame)
            Close();

        if (Time.frameCount != openedFrame && enterPressed)
        {
            Submit(currentInput);
            Close();
            return;
        }

        if (kb.backspaceKey.wasPressedThisFrame && currentInput.Length > 0)
        {
            currentInput = currentInput.Substring(0, currentInput.Length - 1);
            RefreshInputLine();
        }
    }

    void Open()
    {
        // The chat is also an input-owning window. Closing any inventory/journal/skill window
        // first prevents keyboard shortcuts and invisible raycast panels from competing with it.
        CentralUIWindowManager.Instance?.CloseAll();
        isOpen = true;
        openedFrame = Time.frameCount;
        IsTyping = true;
        panel.SetActive(true);
        currentInput = "";
        lastActivityTime = Time.unscaledTime;
        if (logCanvasGroup != null) logCanvasGroup.alpha = 1f;
        RefreshInputLine();
        GameManager.Instance?.PauseGameplay();
    }

    void Close()
    {
        isOpen = false;
        IsTyping = false;
        if (panel != null)
            panel.SetActive(false);
        GameManager.Instance?.ResumeGameplay();
    }

    void Submit(string rawMessage)
    {
        string message = (rawMessage ?? "").Trim();
        currentInput = "";
        RefreshInputLine();

        if (string.IsNullOrEmpty(message))
            return;

        AppendLine(DefaultSpeakerName, message);

        if (message.Equals("/dios", System.StringComparison.OrdinalIgnoreCase))
        {
            ActivateGodMode();
            AppendLine("Sistema", "Modo dios activado. Inmortalidad y 5.000.000 monedas agregadas.");
        }
        else if (message.Equals("/maxwindresistance", System.StringComparison.OrdinalIgnoreCase))
        {
            ToggleWindResistance();
        }
        else if (message.Equals("/staminfull", System.StringComparison.OrdinalIgnoreCase))
        {
            ToggleInfiniteStamina();
        }
        else if (message.Equals("/notbreak", System.StringComparison.OrdinalIgnoreCase))
        {
            ToggleNoBreak();
        }
        else if (message.Equals("/rafaga", System.StringComparison.OrdinalIgnoreCase))
        {
            ToggleExtremeGust();
        }
        else if (message.Equals("/lunadesangre", System.StringComparison.OrdinalIgnoreCase))
        {
            ToggleBloodMoon();
        }
        else if (message.Equals("/dia", System.StringComparison.OrdinalIgnoreCase))
        {
            SetGodTime(false);
        }
        else if (message.Equals("/noche", System.StringComparison.OrdinalIgnoreCase))
        {
            SetGodTime(true);
        }
        else if (message.StartsWith("/givexpe", System.StringComparison.OrdinalIgnoreCase))
        {
            HandleGiveXpCommand(message);
        }
        else if (message.StartsWith("/mision", System.StringComparison.OrdinalIgnoreCase))
        {
            HandleMissionCommand(message);
        }
        else if (message.StartsWith("/give", System.StringComparison.OrdinalIgnoreCase))
        {
            HandleGiveCommand(message);
        }
    }

    void HandleMissionCommand(string message)
    {
        PlayerStats stats = ResolvePlayerStats();
        if (stats == null || !stats.GodModeEnabled)
        {
            AppendLine("Sistema", "El comando /mision solo funciona en modo dios. Escribi /dios primero.");
            return;
        }

        string[] parts = message.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out int missionNumber))
        {
            AppendLine("Sistema", "Uso: /mision <numero>. Disponibles: 1, 2, 3, 4, 5, 6 y 7.");
            return;
        }

        QuestManager quests = QuestManager.Instance;
        string missionName = null;
        if (quests == null || !quests.TryGodJumpToPrimaryMission(missionNumber, out missionName))
        {
            AppendLine("Sistema", missionName ??
                "Mision principal inexistente. Disponibles: /mision 1, 2, 3, 4, 5, 6 y 7.");
            return;
        }

        AppendLine("Sistema", "Salto realizado: mision " + missionNumber + " - " + missionName + ".");
    }

    void HandleGiveXpCommand(string message)
    {
        PlayerStats stats = ResolvePlayerStats();
        if (stats == null || !stats.GodModeEnabled)
        {
            AppendLine("Sistema", "El comando /givexpe solo funciona en modo dios. Escribi /dios primero.");
            return;
        }

        string[] parts = message.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float amount) || amount <= 0f)
        {
            AppendLine("Sistema", "Uso: /givexpe <cantidad>");
            return;
        }

        stats.AddExperience(amount);
        AppendLine("Sistema", amount.ToString("0") + " de experiencia agregada.");
    }

    void ToggleWindResistance()
    {
        PlayerStats stats = ResolvePlayerStats();
        if (stats == null || !stats.GodModeEnabled)
        {
            AppendLine("Sistema", "/maxwindresistance solo funciona en modo dios.");
            return;
        }

        bool enabled = !stats.MaxWindResistanceEnabled;
        stats.SetMaxWindResistance(enabled);
        AppendLine("Sistema", enabled
            ? "Resistencia máxima al viento activada."
            : "Resistencia máxima al viento desactivada.");
    }

    void ToggleInfiniteStamina()
    {
        PlayerStats stats = ResolvePlayerStats();
        if (stats == null || !stats.GodModeEnabled)
        {
            AppendLine("Sistema", "/staminfull solo funciona en modo dios.");
            return;
        }

        bool enabled = !stats.InfiniteStaminaEnabled;
        stats.SetInfiniteStamina(enabled);
        AppendLine("Sistema", enabled
            ? "Estamina infinita activada."
            : "Estamina infinita desactivada.");
    }

    void ToggleNoBreak()
    {
        PlayerStats stats = ResolvePlayerStats();
        if (stats == null || !stats.GodModeEnabled)
        {
            AppendLine("Sistema", "/notbreak solo funciona en modo dios. Escribi /dios primero.");
            return;
        }

        bool enabled = !ItemUpgradeSystem.NoBreakModeEnabled;
        ItemUpgradeSystem.SetNoBreakMode(enabled);
        AppendLine("Sistema", enabled
            ? "Proteccion de mejoras activada: 100% de exito y el arma no puede romperse."
            : "Proteccion de mejoras desactivada: vuelve el riesgo normal de destruccion.");
    }

    void ToggleExtremeGust()
    {
        PlayerStats stats = ResolvePlayerStats();
        if (stats == null || !stats.GodModeEnabled)
        {
            AppendLine("Sistema",
                "/rafaga solo funciona en modo dios. Escribi /dios primero.");
            return;
        }

        ExtremeWindEventController controller =
            ExtremeWindEventController.Instance ??
            FindAnyObjectByType<ExtremeWindEventController>(
                FindObjectsInactive.Include);
        if (controller == null)
        {
            AppendLine("Sistema",
                "No se encontro el controlador de rafagas en esta escena.");
            return;
        }

        bool enabled = controller.ToggleExtremeGust();
        AppendLine("Sistema", enabled
            ? "Rafaga extrema activada. Comienza la alerta de 10 segundos."
            : "Rafaga extrema desactivada. El viento vuelve a la normalidad.");
    }

    void ToggleBloodMoon()
    {
        PlayerStats stats = ResolvePlayerStats();
        if (stats == null || !stats.GodModeEnabled)
        {
            AppendLine("Sistema",
                "/lunadesangre solo funciona en modo dios. Escribi /dios primero.");
            return;
        }

        BloodMoonEventManager controller =
            BloodMoonEventManager.Instance ??
            FindAnyObjectByType<BloodMoonEventManager>(
                FindObjectsInactive.Include);
        if (controller == null)
        {
            AppendLine("Sistema",
                "No se encontro el controlador de Luna de Sangre.");
            return;
        }

        bool enabled = controller.ToggleManualOverride();
        AppendLine("Sistema", enabled
            ? "Luna de Sangre activada manualmente."
            : BloodMoonEventManager.IsActive
                ? "Activacion manual deshabilitada; continua por corresponder al septimo dia."
                : "Luna de Sangre desactivada.");
    }

    void SetGodTime(bool night)
    {
        PlayerStats stats = ResolvePlayerStats();
        if (stats == null || !stats.GodModeEnabled)
        {
            AppendLine("Sistema",
                (night ? "/noche" : "/dia") +
                " solo funciona en modo dios. Escribi /dios primero.");
            return;
        }

        TenkokuDayNightCycle cycle =
            FindAnyObjectByType<TenkokuDayNightCycle>(
                FindObjectsInactive.Include);
        if (cycle == null)
        {
            AppendLine("Sistema",
                "No se encontro el ciclo de dia y noche en esta escena.");
            return;
        }

        float targetHour = night ? 22f : 10f;
        cycle.SetGodModeHour(targetHour);
        AppendLine("Sistema", night
            ? "Noche establecida: 10:00 PM."
            : "Dia establecido: 10:00 AM.");
    }

    void HandleGiveCommand(string message)
    {
        PlayerStats stats = ResolvePlayerStats();
        if (stats == null || !stats.GodModeEnabled)
        {
            AppendLine("Sistema", "El comando /give solo funciona en modo dios. Escribi /dios primero.");
            return;
        }

        string[] parts = message.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && parts[1].Equals("sableoscuro", System.StringComparison.OrdinalIgnoreCase))
        {
            GiveDarkSaber();
            return;
        }

        if (parts.Length < 3 || !parts[1].Equals("diamond", System.StringComparison.OrdinalIgnoreCase) || !int.TryParse(parts[2], out int amount))
        {
            AppendLine("Sistema", "Uso: /give sableoscuro o /give diamond <cantidad 1-100>");
            return;
        }

        amount = Mathf.Clamp(amount, 1, 100);
        ItemData diamond = ResolveItem("diamond_excellence", "Diamond");
        if (diamond == null)
        {
            AppendLine("Sistema", "No encontre el item Diamond.");
            return;
        }

        int remaining = amount;
        while (remaining > 0)
        {
            int stack = Mathf.Min(remaining, diamond.maxStack > 0 ? diamond.maxStack : 100);
            if (!(InventoryManager.Instance?.AddItem(diamond, stack) ?? false))
            {
                AppendLine("Sistema", "Inventario lleno. Se agregaron " + (amount - remaining) + " diamonds.");
                return;
            }
            remaining -= stack;
        }

        AppendLine("Sistema", amount + " diamond agregados.");
    }

    void GiveDarkSaber()
    {
        ItemData darkSaber = ResolveItem("dark_saber_reward", "Sable Oscuro");
        if (darkSaber == null)
        {
            AppendLine("Sistema", "No encontre el item Sable Oscuro.");
            return;
        }

        ItemInstance reward = new ItemInstance(darkSaber)
        {
            excellenceLevel = 7,
            extraAttackPercent = 70f
        };

        if (!(InventoryManager.Instance?.AddItemInstance(reward) ?? false))
        {
            AppendLine("Sistema", "Inventario lleno. No se entrego el Sable Oscuro.");
            return;
        }

        AppendLine("Sistema", "Sable Oscuro exe +7 agregado al inventario.");
    }

    static ItemData ResolveItem(string itemId, string itemName)
    {
        ItemData[] items = Resources.LoadAll<ItemData>("Items");
        foreach (ItemData item in items)
        {
            if (item == null)
                continue;
            if (item.itemID == itemId || item.itemName.Equals(itemName, System.StringComparison.OrdinalIgnoreCase))
                return item;
        }
        return null;
    }

    static PlayerStats ResolvePlayerStats()
    {
        GameObject player = GameManager.Instance != null && GameManager.Instance.PlayerObject != null
            ? GameManager.Instance.PlayerObject
            : GameObject.FindWithTag("Player");
        return player != null ? player.GetComponent<PlayerStats>() : null;
    }

    void OnTextInput(char character)
    {
        if (!isOpen)
            return;

        if (character == '\n' || character == '\r' || character == '\b')
            return;

        currentInput += character;
        RefreshInputLine();
    }

    void SubscribeToKeyboard()
    {
        if (subscribedKeyboard != null)
            subscribedKeyboard.onTextInput -= OnTextInput;

        subscribedKeyboard = Keyboard.current;
        if (subscribedKeyboard != null)
            subscribedKeyboard.onTextInput += OnTextInput;
    }

    void RefreshInputLine()
    {
        if (inputText != null)
            inputText.text = currentInput;
        if (placeholderText != null)
            placeholderText.enabled = string.IsNullOrEmpty(currentInput);
    }

    void ActivateGodMode()
    {
        ResolvePlayerStats()?.SetGodMode(true);
        InventoryManager.Instance?.AddGold(GodModeGold);
    }

    void AppendLine(string speaker, string message)
    {
        if (logText == null) return;

        string line = $"<color=#F8D46A>{speaker}</color>: {message}";
        visibleLines.Enqueue(line);
        while (visibleLines.Count > MaxVisibleMessages)
            visibleLines.Dequeue();
        logText.text = string.Join("\n", visibleLines);
        lastActivityTime = Time.unscaledTime;
        if (logCanvasGroup != null) logCanvasGroup.alpha = 1f;
    }

    void BuildUI()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        // The log is a lightweight always-visible overlay (no backdrop, never toggled) so
        // messages linger on screen like a real game chat instead of disappearing with the
        // input box the moment you hit Enter.
        logPanel = new GameObject("ChatLogPanel", typeof(RectTransform));
        logPanel.transform.SetParent(transform, false);
        var logPanelRect = logPanel.GetComponent<RectTransform>();
        logPanelRect.anchorMin = Vector2.zero;
        logPanelRect.anchorMax = Vector2.zero;
        logPanelRect.pivot = Vector2.zero;
        logPanelRect.anchoredPosition = new Vector2(18f, 88f);
        logPanelRect.sizeDelta = new Vector2(620f, 118f);
        logCanvasGroup = logPanel.AddComponent<CanvasGroup>();
        logCanvasGroup.interactable = false;
        logCanvasGroup.blocksRaycasts = false;
        lastActivityTime = Time.unscaledTime;

        var logRect = CreateRect(logPanel.transform, "ChatLog", new Vector2(0f, 0f), new Vector2(1f, 1f));
        logText = logRect.gameObject.AddComponent<TextMeshProUGUI>();
        logText.fontSize = 15;
        logText.color = Color.white;
        logText.alignment = TextAlignmentOptions.BottomLeft;
        logText.textWrappingMode = TextWrappingModes.Normal;
        logText.lineSpacing = 12f;
        logText.paragraphSpacing = 4f;
        logText.overflowMode = TextOverflowModes.Truncate;
        AddOutline(logText);

        panel = new GameObject("ChatInputPanel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        panelRect.anchoredPosition = new Vector2(18f, 18f);
        panelRect.sizeDelta = new Vector2(620f, 58f);

        var inputRoot = CreateRect(panel.transform, "ChatInput", new Vector2(0f, 0f), new Vector2(1f, 1f));
        AddImage(inputRoot.gameObject, new Color(0.08f, 0.065f, 0.052f, 1f));

        var textRect = CreateRect(inputRoot, "Text", new Vector2(0.03f, 0f), new Vector2(0.98f, 1f));
        inputText = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 17;
        inputText.enableAutoSizing = true;
        inputText.fontSizeMin = 11f;
        inputText.fontSizeMax = 17f;
        inputText.textWrappingMode = TextWrappingModes.NoWrap;
        inputText.color = Color.white;
        inputText.alignment = TextAlignmentOptions.MidlineLeft;

        var placeholderRect = CreateRect(inputRoot, "Placeholder", new Vector2(0.03f, 0f), new Vector2(0.98f, 1f));
        placeholderText = placeholderRect.gameObject.AddComponent<TextMeshProUGUI>();
        placeholderText.text =
            "Comando: /dios, /dia, /noche, /rafaga, /lunadesangre, /mision N";
        placeholderText.fontSize = 17;
        placeholderText.enableAutoSizing = true;
        placeholderText.fontSizeMin = 9f;
        placeholderText.fontSizeMax = 17f;
        placeholderText.textWrappingMode = TextWrappingModes.NoWrap;
        placeholderText.color = new Color(1f, 1f, 1f, 0.38f);
        placeholderText.alignment = TextAlignmentOptions.MidlineLeft;
    }

    void UpdateLogFade()
    {
        if (logCanvasGroup == null) return;
        bool active = isOpen || Time.unscaledTime - lastActivityTime < IdleFadeDelay;
        float targetAlpha = active ? 1f : IdleAlpha;
        logCanvasGroup.alpha = Mathf.MoveTowards(
            logCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 0.75f);
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    static Image AddImage(GameObject go, Color color)
    {
        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    static void AddOutline(TextMeshProUGUI text)
    {
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
    }
}

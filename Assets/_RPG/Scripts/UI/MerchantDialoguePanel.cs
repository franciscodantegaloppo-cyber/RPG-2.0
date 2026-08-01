using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class MerchantDialoguePanel : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI bodyText;
    [SerializeField] Button acceptButton;
    [SerializeField] TextMeshProUGUI acceptLabel;
    [SerializeField] Button secondaryButton;
    [SerializeField] TextMeshProUGUI secondaryLabel;
    [SerializeField] float charDelay = 0.03f;

    Coroutine typing;
    Action onAccept;
    Action onSecondary;
    string fullMessage;
    bool presentingChoices;
    int openedFrame;

    public static MerchantDialoguePanel Instance { get; private set; }
    public bool IsOpen => panel != null && panel.activeInHierarchy;

    void Awake()
    {
        Instance = this;
        ApplyReadableTypography();
        if (panel != null) panel.SetActive(false);
    }

    void ApplyReadableTypography()
    {
        if (panel != null && panel.transform is RectTransform panelRect)
        {
            panelRect.anchorMin = new Vector2(.08f, .055f);
            panelRect.anchorMax = new Vector2(.92f, .44f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }
        if (nameText != null)
        {
            nameText.fontSize = 20f;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 17f;
            nameText.fontSizeMax = 20f;
            nameText.characterSpacing = .3f;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
        }
        if (bodyText != null)
        {
            bodyText.fontSize = 18f;
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 14f;
            bodyText.fontSizeMax = 18f;
            bodyText.characterSpacing = .15f;
            bodyText.lineSpacing = 6f;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Overflow;
            bodyText.margin = new Vector4(10f, 5f, 10f, 5f);
        }
        ConfigureButtonLabel(acceptLabel);
        ConfigureButtonLabel(secondaryLabel);
    }

    static void ConfigureButtonLabel(TextMeshProUGUI text)
    {
        if (text == null) return;
        text.fontSize = 16f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 16f;
        text.characterSpacing = .25f;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    void Update()
    {
        if (!IsOpen || Time.frameCount <= openedFrame) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame) return;

        // Choice panels cannot safely guess which answer E should select. E only reveals their
        // complete text; linear dialogue advances immediately to its next box/action.
        if (presentingChoices)
        {
            if (typing != null) FinishTyping();
            return;
        }

        if (typing != null) FinishTyping();
        OnAcceptClicked();
    }

    // Self-heals like NPCHerrero's EnsureRuntimeShopPanel: NPCs that need a dialogue box
    // shouldn't depend on the editor-only "Setup Merchant" menu having been run first.
    public static MerchantDialoguePanel EnsureRuntime()
    {
        if (Instance != null)
            return Instance;

        // Deliberately excludes inactive canvases (unlike the EventSystem lookup below) - an
        // inactive canvas (e.g. another panel's, toggled off) would make this dialogue's
        // panel.SetActive(true) insufficient, since activeInHierarchy still depends on every
        // ancestor being active, silently breaking StartCoroutine and all button clicks.
        // FindAnyObjectByType<Canvas>() grabs whichever canvas Unity happens to enumerate first,
        // which since EnemyHealthBar started creating one small WorldSpace canvas per enemy is no
        // longer reliably the HUD - see InventoryUI.FindReusableCanvas() for the full story.
        GameObject canvasGo = new GameObject("DialogueCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2500;
        CanvasScaler dialogueScaler = canvasGo.GetComponent<CanvasScaler>();
        dialogueScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        dialogueScaler.referenceResolution = new Vector2(1920f, 1080f);
        dialogueScaler.matchWidthOrHeight = .5f;

        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include) == null)
        {
            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        GameObject panelGo = new GameObject("MerchantDialoguePanel", typeof(RectTransform));
        panelGo.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = (RectTransform)panelGo.transform;
        panelRect.anchorMin = new Vector2(0.1f, 0.05f);
        panelRect.anchorMax = new Vector2(0.9f, 0.4f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = panelGo.AddComponent<Image>();
        Sprite panelFrame = Resources.Load<Sprite>("UI/SharpUI/Panel");
        if (panelFrame != null)
        {
            panelImage.sprite = panelFrame;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;
        }
        else panelImage.color = new Color(0.1f, 0.08f, 0.06f, 1f);

        // Own sub-canvas forced to the top of the sort order: the shared canvas found above may
        // not be the one other runtime-built UI (tooltips, drag icons) overrides to render on
        // top of, so without this the accept button could end up visually on top but still
        // losing the raycast to another canvas's graphic occupying the same screen space.
        Canvas topCanvas = panelGo.AddComponent<Canvas>();
        topCanvas.overrideSorting = true;
        topCanvas.sortingOrder = 1000;
        panelGo.AddComponent<GraphicRaycaster>();

        GameObject nameGo = new GameObject("SpeakerName", typeof(RectTransform));
        nameGo.transform.SetParent(panelGo.transform, false);
        RectTransform nameRect = (RectTransform)nameGo.transform;
        nameRect.anchorMin = new Vector2(0.02f, 0.75f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        TextMeshProUGUI nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
        nameTmp.fontSize = 20;
        nameTmp.enableAutoSizing = true;
        nameTmp.fontSizeMin = 17f;
        nameTmp.fontSizeMax = 20f;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color = new Color(1f, 0.85f, 0.4f);

        GameObject bodyGo = new GameObject("DialogueBody", typeof(RectTransform));
        bodyGo.transform.SetParent(panelGo.transform, false);
        RectTransform bodyRect = (RectTransform)bodyGo.transform;
        bodyRect.anchorMin = new Vector2(0.02f, 0.3f);
        bodyRect.anchorMax = new Vector2(0.98f, 0.75f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;
        TextMeshProUGUI bodyTmp = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyTmp.fontSize = 18;
        bodyTmp.enableAutoSizing = true;
        bodyTmp.fontSizeMin = 14f;
        bodyTmp.fontSizeMax = 18f;
        bodyTmp.color = Color.white;
        bodyTmp.textWrappingMode = TextWrappingModes.Normal;
        bodyTmp.lineSpacing = 6f;
        bodyTmp.margin = new Vector4(10f, 5f, 10f, 5f);

        Button btn = CreateMuButton(panelGo.transform, "AcceptButton", new Vector2(0.10f, 0.15f), new Vector2(0.90f, 0.29f), out TextMeshProUGUI lblTmp);
        Button secondBtn = CreateMuButton(panelGo.transform, "SecondaryButton", new Vector2(0.10f, 0.02f), new Vector2(0.90f, 0.14f), out TextMeshProUGUI secondLblTmp);

        MerchantDialoguePanel dialoguePanel = panelGo.AddComponent<MerchantDialoguePanel>();
        dialoguePanel.panel = panelGo;
        dialoguePanel.nameText = nameTmp;
        dialoguePanel.bodyText = bodyTmp;
        dialoguePanel.acceptButton = btn;
        dialoguePanel.acceptLabel = lblTmp;
        dialoguePanel.secondaryButton = secondBtn;
        dialoguePanel.secondaryLabel = secondLblTmp;

        panelGo.SetActive(false);
        return dialoguePanel;
    }

    static Button CreateMuButton(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, out TextMeshProUGUI label)
    {
        Button button = SharpUIRuntimeFactory.CreateRectButton(
            parent, objectName, string.Empty, null, 44f);
        GameObject buttonGo = button.gameObject;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        label = buttonGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null)
        {
            GameObject labelGo = new GameObject("ButtonLabel", typeof(RectTransform));
            labelGo.transform.SetParent(buttonGo.transform, false);
            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 6f);
            labelRect.offsetMax = new Vector2(-12f, -6f);
            label = labelGo.AddComponent<TextMeshProUGUI>();
        }
        label.fontSize = 16;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 16f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        return button;
    }

    public void Show(string speakerName, string message, string buttonLabel, Action onAcceptCallback)
    {
        ApplyReadableTypography();
        onAccept = onAcceptCallback;
        fullMessage = message ?? "";
        presentingChoices = false;
        openedFrame = Time.frameCount;
        if (nameText != null) nameText.text = speakerName;
        if (acceptLabel != null) acceptLabel.text = buttonLabel;

        acceptButton.onClick.RemoveAllListeners();
        acceptButton.onClick.AddListener(OnAcceptClicked);
        secondaryButton.gameObject.SetActive(false);
        acceptButton.gameObject.SetActive(false);

        panel.SetActive(true);
        // Same InMenu/Exploration pattern InventoryUI and HelpPanelUI use to unlock the cursor -
        // without this the cursor stays locked/invisible for Exploration and "Aceptar armadura"
        // can't be clicked at all.
        GameManager.Instance?.SetState(GameState.InMenu);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (typing != null) StopCoroutine(typing);
        typing = StartCoroutine(TypeText(message));
    }

    public void ShowChoices(string speakerName, string message, string primaryLabel, Action primaryCallback, string secondaryText, Action secondaryCallback)
    {
        ApplyReadableTypography();
        onAccept = primaryCallback;
        onSecondary = secondaryCallback;
        fullMessage = message ?? "";
        presentingChoices = true;
        openedFrame = Time.frameCount;
        if (nameText != null) nameText.text = speakerName;
        acceptLabel.text = primaryLabel;
        secondaryLabel.text = secondaryText;
        acceptButton.onClick.RemoveAllListeners();
        acceptButton.onClick.AddListener(OnChoiceClicked);
        secondaryButton.onClick.RemoveAllListeners();
        secondaryButton.onClick.AddListener(OnSecondaryClicked);
        acceptButton.gameObject.SetActive(false);
        secondaryButton.gameObject.SetActive(false);
        panel.SetActive(true);
        GameManager.Instance?.SetState(GameState.InMenu);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (typing != null) StopCoroutine(typing);
        typing = StartCoroutine(TypeChoices(message));
    }

    IEnumerator TypeText(string text)
    {
        bodyText.text = "";
        foreach (char c in text)
        {
            bodyText.text += c;
            yield return new WaitForSeconds(charDelay);
        }
        typing = null;
        acceptButton.gameObject.SetActive(true);
    }

    IEnumerator TypeChoices(string text)
    {
        bodyText.text = "";
        foreach (char c in text)
        {
            bodyText.text += c;
            yield return new WaitForSeconds(charDelay);
        }
        typing = null;
        acceptButton.gameObject.SetActive(true);
        secondaryButton.gameObject.SetActive(true);
    }

    void FinishTyping()
    {
        if (typing != null) StopCoroutine(typing);
        typing = null;
        if (bodyText != null) bodyText.text = fullMessage;
        if (acceptButton != null) acceptButton.gameObject.SetActive(true);
        if (presentingChoices && secondaryButton != null) secondaryButton.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (typing != null) StopCoroutine(typing);
        typing = null;
        onAccept = null;
        onSecondary = null;
        panel.SetActive(false);
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        GameManager.Instance?.SetState(GameState.Exploration);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnAcceptClicked()
    {
        Action callback = onAccept;
        Hide();
        callback?.Invoke();
    }

    void OnChoiceClicked() => onAccept?.Invoke();
    void OnSecondaryClicked() => onSecondary?.Invoke();
}

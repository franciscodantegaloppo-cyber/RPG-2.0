using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared SharpUI title bar for runtime windows. It stays outside layout groups,
/// brings its window to the front, supports mouse dragging and exposes a close button.
/// </summary>
public sealed class SharpUIWindowChrome : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
{
    RectTransform window;
    RectTransform parentRect;
    Canvas canvas;
    Vector2 dragStartPointer;
    Vector2 dragStartPosition;

    public static SharpUIWindowChrome Attach(GameObject panel, string title, UnityAction closeAction)
    {
        if (panel == null)
            return null;

        Transform existing = panel.transform.Find("SharpUI_WindowHeader");
        GameObject header = existing != null
            ? existing.gameObject
            : new GameObject("SharpUI_WindowHeader", typeof(RectTransform), typeof(Image),
                typeof(LayoutElement), typeof(SharpUIWindowChrome));
        header.transform.SetParent(panel.transform, false);
        header.transform.SetAsLastSibling();

        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = Vector2.one;
        headerRect.pivot = new Vector2(.5f, 1f);
        headerRect.offsetMin = new Vector2(10f, -52f);
        headerRect.offsetMax = new Vector2(-10f, -6f);

        LayoutElement layout = header.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;

        Image headerImage = header.GetComponent<Image>();
        headerImage.sprite = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        headerImage.type = Image.Type.Sliced;
        headerImage.color = Color.white;
        headerImage.raycastTarget = true;

        TextMeshProUGUI titleText = EnsureTitle(header.transform);
        titleText.text = title;

        Button close = EnsureCloseButton(header.transform);
        close.onClick.RemoveAllListeners();
        if (closeAction != null)
            close.onClick.AddListener(closeAction);

        SharpUIWindowChrome chrome = header.GetComponent<SharpUIWindowChrome>();
        chrome.window = panel.GetComponent<RectTransform>();
        chrome.parentRect = panel.transform.parent as RectTransform;
        chrome.canvas = panel.GetComponentInParent<Canvas>();
        CentralUIWindowManager.Register(panel, title, closeAction, true);
        return chrome;
    }

    static TextMeshProUGUI EnsureTitle(Transform header)
    {
        Transform found = header.Find("Title");
        GameObject go = found != null
            ? found.gameObject
            : new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(header, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(22f, 4f);
        rect.offsetMax = new Vector2(-62f, -4f);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = 21f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 13f;
        text.fontSizeMax = 21f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, .82f, .42f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    static Button EnsureCloseButton(Transform header)
    {
        Transform found = header.Find("Close");
        GameObject go = found != null
            ? found.gameObject
            : new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(header, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
        rect.pivot = new Vector2(1f, .5f);
        rect.sizeDelta = new Vector2(38f, 34f);
        rect.anchoredPosition = new Vector2(-8f, 0f);

        Image image = go.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/SharpUI/Button");
        image.type = Image.Type.Sliced;
        image.color = new Color(.72f, .24f, .16f, 1f);

        Transform labelTransform = go.transform.Find("Label");
        GameObject label = labelTransform != null
            ? labelTransform.gameObject
            : new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        text.text = "×";
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, .88f, .62f);
        text.raycastTarget = false;
        return go.GetComponent<Button>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        window?.SetAsLastSibling();
        transform.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (window == null)
            return;
        window.SetAsLastSibling();
        dragStartPointer = eventData.position;
        dragStartPosition = window.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (window == null)
            return;

        float scale = canvas != null ? Mathf.Max(.01f, canvas.scaleFactor) : 1f;
        window.anchoredPosition = dragStartPosition + (eventData.position - dragStartPointer) / scale;
        KeepTitleBarVisible();
    }

    void KeepTitleBarVisible()
    {
        if (parentRect == null || window == null)
            return;

        var headerRect = transform as RectTransform;
        if (headerRect == null)
            return;

        var corners = new Vector3[4];
        headerRect.GetWorldCorners(corners);
        Vector3 bottomLeft = parentRect.InverseTransformPoint(corners[0]);
        Vector3 topRight = parentRect.InverseTransformPoint(corners[2]);
        Rect bounds = parentRect.rect;
        const float visibleWidth = 90f;
        Vector2 correction = Vector2.zero;

        if (topRight.x < bounds.xMin + visibleWidth)
            correction.x = bounds.xMin + visibleWidth - topRight.x;
        else if (bottomLeft.x > bounds.xMax - visibleWidth)
            correction.x = bounds.xMax - visibleWidth - bottomLeft.x;

        if (topRight.y > bounds.yMax)
            correction.y = bounds.yMax - topRight.y;
        else if (bottomLeft.y < bounds.yMin)
            correction.y = bounds.yMin - bottomLeft.y;

        window.anchoredPosition += correction;
    }
}

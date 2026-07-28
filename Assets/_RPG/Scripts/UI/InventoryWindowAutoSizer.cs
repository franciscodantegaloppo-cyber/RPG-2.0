using UnityEngine;
using UnityEngine.UI;

// Makes the generated inventory window follow the preferred size of its three framed columns.
// It also clamps that size to the current Canvas, reserving the bottom quickbar strip, so the
// inventory expands when content needs room without disappearing off-screen on smaller displays.
[DisallowMultipleComponent]
public sealed class InventoryWindowAutoSizer : MonoBehaviour
{
    float minWidth = 900f;
    float minHeight = 650f;
    float horizontalMargin = 32f;
    float bottomReserve = 136f;
    RectTransform window;
    RectTransform canvasRect;
    Vector2 lastCanvasSize = new Vector2(-1f, -1f);
    bool dirty = true;

    public void Configure(float minimumWidth, float minimumHeight,
        float screenMargin, float quickbarReserve)
    {
        minWidth = Mathf.Max(1f, minimumWidth);
        minHeight = Mathf.Max(1f, minimumHeight);
        horizontalMargin = Mathf.Max(0f, screenMargin);
        bottomReserve = Mathf.Max(0f, quickbarReserve);
        dirty = true;
    }

    void Awake()
    {
        window = transform as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.transform as RectTransform : null;
    }

    void OnEnable()
    {
        dirty = true;
        Recalculate(true);
    }

    void OnTransformChildrenChanged() => dirty = true;
    void OnRectTransformDimensionsChange() => dirty = true;

    void LateUpdate()
    {
        if (canvasRect == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        }

        Vector2 canvasSize = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
        if (dirty || canvasSize != lastCanvasSize)
            Recalculate(false);
    }

    public void Recalculate(bool forceLayout)
    {
        if (window == null) window = transform as RectTransform;
        if (window == null) return;

        if (forceLayout)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(window);
        }

        Vector2 available = canvasRect != null
            ? canvasRect.rect.size
            : new Vector2(Screen.width, Screen.height);
        float maxWidth = Mathf.Max(1f, available.x - horizontalMargin);
        float maxHeight = Mathf.Max(1f, available.y - bottomReserve);

        float preferredWidth = Mathf.Max(minWidth,
            LayoutUtility.GetPreferredWidth(window));
        float preferredHeight = Mathf.Max(minHeight,
            LayoutUtility.GetPreferredHeight(window));
        Vector2 target = new Vector2(
            Mathf.Min(preferredWidth, maxWidth),
            Mathf.Min(preferredHeight, maxHeight));

        if ((window.sizeDelta - target).sqrMagnitude > .25f)
        {
            window.sizeDelta = target;
            LayoutRebuilder.MarkLayoutForRebuild(window);
        }

        lastCanvasSize = available;
        dirty = false;
    }
}

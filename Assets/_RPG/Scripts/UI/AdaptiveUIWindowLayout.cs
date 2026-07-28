using UnityEngine;

// Keeps draggable windows inside the safe screen area and above the permanent quickbar.
[DisallowMultipleComponent]
public sealed class AdaptiveUIWindowLayout : MonoBehaviour
{
    const float SideMargin = 24f;
    const float TopMargin = 18f;
    const float QuickbarReserve = 128f;
    RectTransform window;
    RectTransform canvasRect;
    Vector2 lastScreen;

    void Awake()
    {
        window = transform as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.transform as RectTransform : null;
    }

    void OnEnable() => FitNow();

    void LateUpdate()
    {
        Vector2 screen = new Vector2(Screen.width, Screen.height);
        if (screen != lastScreen) FitNow();
    }

    public void FitNow()
    {
        if (window == null) window = transform as RectTransform;
        if (window == null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvasRect == null && canvas != null)
            canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) return;

        Rect parent = canvasRect.rect;
        Rect safe = Screen.safeArea;
        float scaleX = parent.width / Mathf.Max(1f, Screen.width);
        float scaleY = parent.height / Mathf.Max(1f, Screen.height);
        float safeWidth = safe.width * scaleX - SideMargin * 2f;
        float safeHeight = safe.height * scaleY - TopMargin - QuickbarReserve;
        float fit = Mathf.Min(1f,
            safeWidth / Mathf.Max(1f, window.rect.width),
            safeHeight / Mathf.Max(1f, window.rect.height));
        fit = Mathf.Clamp(fit, .58f, 1f);
        window.localScale = new Vector3(fit, fit, 1f);

        Canvas.ForceUpdateCanvases();
        ClampInside(parent, safe, scaleX, scaleY);
        lastScreen = new Vector2(Screen.width, Screen.height);
    }

    void ClampInside(Rect parent, Rect safe, float scaleX, float scaleY)
    {
        Vector3[] corners = new Vector3[4];
        window.GetWorldCorners(corners);
        Vector3 min = canvasRect.InverseTransformPoint(corners[0]);
        Vector3 max = canvasRect.InverseTransformPoint(corners[2]);

        float left = parent.xMin + safe.xMin * scaleX + SideMargin;
        float right = parent.xMin + safe.xMax * scaleX - SideMargin;
        float bottom = parent.yMin + safe.yMin * scaleY + QuickbarReserve;
        float top = parent.yMin + safe.yMax * scaleY - TopMargin;
        Vector2 correction = Vector2.zero;
        if (min.x < left) correction.x += left - min.x;
        if (max.x > right) correction.x -= max.x - right;
        if (min.y < bottom) correction.y += bottom - min.y;
        if (max.y > top) correction.y -= max.y - top;
        window.anchoredPosition += correction;
    }
}

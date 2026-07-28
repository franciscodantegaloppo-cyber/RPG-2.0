using UnityEngine;
using UnityEngine.UI;

// Normalizes every gameplay screen-space canvas so HUD proportions remain stable from
// 16:9 through ultrawide and smaller windowed resolutions.
[DisallowMultipleComponent]
public sealed class AdaptiveUICanvas : MonoBehaviour
{
    CanvasScaler scaler;
    Vector2 lastScreen;

    void Awake() => Apply();
    void OnRectTransformDimensionsChange() => Apply();

    public void Apply()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return;
        if (scaler == null) scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        float aspect = Screen.width / Mathf.Max(1f, (float)Screen.height);
        scaler.matchWidthOrHeight = aspect < 1.55f ? .78f : aspect > 2.05f ? .24f : .5f;
        scaler.referencePixelsPerUnit = 100f;
        lastScreen = new Vector2(Screen.width, Screen.height);
    }

    void LateUpdate()
    {
        Vector2 screen = new Vector2(Screen.width, Screen.height);
        if (screen != lastScreen) Apply();
    }
}

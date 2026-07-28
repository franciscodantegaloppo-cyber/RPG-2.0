using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// World-space health bar above the Crab Demon's head, styled with the same mu_1/mu_2 ornate
// frame art used throughout the rest of the UI (see HUDController/InventoryUI's PanelFrame_Clean
// usage) instead of KingGoblinHealthBar's imported 3D model - this is what the user meant by
// "el marco de recuadro estilo mu".
[RequireComponent(typeof(EnemyStats))]
public class CrabDemonHealthBar : MonoBehaviour
{
    [SerializeField] Vector3 localOffset = new Vector3(0f, 3.4f, 0f);
    [SerializeField] Vector2 barSize = new Vector2(2.2f, 0.32f);

    EnemyStats stats;
    Transform root;
    RectTransform fillRect;
    Canvas canvas;
    Camera mainCamera;
    float fillFullWidth;

    void Awake()
    {
        stats = GetComponent<EnemyStats>();
        Build();
    }

    void OnEnable()
    {
        stats.OnHealthChanged += HandleHealthChanged;
        stats.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        stats.OnHealthChanged -= HandleHealthChanged;
        stats.OnDeath -= HandleDeath;
    }

    void Build()
    {
        GameObject canvasGo = new GameObject("CrabDemonHealthBar");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = localOffset;
        root = canvasGo.transform;

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<CanvasScaler>();
        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = barSize * 100f;
        canvasRect.localScale = Vector3.one * (1f / 100f);

        Sprite frame = Resources.Load<Sprite>("UI/PanelFrame_Banner_Clean");

        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        RectTransform bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = bgGo.AddComponent<Image>();
        if (frame != null)
        {
            bgImage.sprite = frame;
            bgImage.type = Image.Type.Sliced;
        }
        bgImage.color = new Color(0.045f, 0.018f, 0.08f, 0.96f);

        GameObject fillGo = new GameObject("VioletBossFill");
        fillGo.transform.SetParent(canvasGo.transform, false);
        fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.18f);
        fillRect.anchorMax = new Vector2(1f, 0.82f);
        fillRect.offsetMin = new Vector2(10f, 0f);
        fillRect.offsetMax = new Vector2(-10f, 0f);
        Image fillImage = fillGo.AddComponent<Image>();
        fillImage.sprite = CreateVioletGradient();
        fillImage.type = Image.Type.Simple;
        fillImage.color = Color.white;
        Outline outline = fillGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.06f, 0.005f, 0.12f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);
        AddVolumeLayer(fillRect, "LowerShade", new Color(0.08f, 0.005f, 0.20f, 0.48f), 0f, 0.22f);
        AddVolumeLayer(fillRect, "MiddleViolet", new Color(0.48f, 0.12f, 0.82f, 0.22f), 0.22f, 0.62f);
        AddVolumeLayer(fillRect, "LavenderGlow", new Color(0.88f, 0.62f, 1f, 0.25f), 0.62f, 0.82f);
        AddVolumeLayer(fillRect, "TopGloss", new Color(1f, 1f, 1f, 0.30f), 0.83f, 0.93f);

        fillFullWidth = fillRect.rect.width;

        RefreshFill(stats.CurrentHealth, stats.MaxHealth);
    }

    void HandleHealthChanged(float current, float max) => RefreshFill(current, max);

    void HandleDeath()
    {
        if (root != null) root.gameObject.SetActive(false);
    }

    void RefreshFill(float current, float max)
    {
        if (fillRect == null) return;
        float percent = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        fillRect.anchorMax = new Vector2(percent, fillRect.anchorMax.y);
    }

    static void AddVolumeLayer(RectTransform parent, string layerName, Color color, float bottom, float top)
    {
        GameObject layer = new GameObject(layerName, typeof(RectTransform), typeof(Image));
        layer.transform.SetParent(parent, false);
        RectTransform rect = layer.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, bottom);
        rect.anchorMax = new Vector2(1f, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = layer.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    static Sprite CreateVioletGradient()
    {
        const int width = 48, height = 128;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Boss_Violet_MultiGradient", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        Color deep = new Color(0.10f, 0.008f, 0.24f);
        Color middle = new Color(0.48f, 0.10f, 0.82f);
        Color top = new Color(0.86f, 0.52f, 1f);
        for (int y = 0; y < height; y++)
        {
            float t = y / (height - 1f);
            Color vertical = t < 0.62f ? Color.Lerp(deep, middle, t / 0.62f) : Color.Lerp(middle, top, (t - 0.62f) / 0.38f);
            float gloss = Mathf.Exp(-Mathf.Pow((t - 0.78f) / 0.12f, 2f));
            vertical = Color.Lerp(vertical, Color.white, gloss * 0.18f);
            for (int x = 0; x < width; x++)
            {
                float edgeLight = Mathf.Pow(Mathf.Sin(x / (width - 1f) * Mathf.PI), 0.45f);
                texture.SetPixel(x, y, Color.Lerp(vertical * 0.72f, vertical, edgeLight));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(.5f, .5f), 100f);
    }

    void LateUpdate()
    {
        if (root == null) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
            root.rotation = Quaternion.LookRotation(root.position - mainCamera.transform.position, Vector3.up);
    }
}

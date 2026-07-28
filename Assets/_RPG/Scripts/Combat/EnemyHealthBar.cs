using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Auto-added by EnemyStats.Awake() so every enemy (skeleton, goblin, any future type) gets a
// health bar for free with no per-enemy setup wiring. World-space bar above the enemy's head,
// hidden until the enemy takes damage, then fades back out after a few seconds of no new hits.
[RequireComponent(typeof(EnemyStats))]
public class EnemyHealthBar : MonoBehaviour
{
    const float VisibleDuration = 4f;
    const float FadeDuration = 0.6f;
    const float HeightAboveRoot = 2.1f;
    const float WorldWidth = 1f;
    const float WorldHeight = 0.12f;

    EnemyStats stats;
    Canvas canvas;
    CanvasGroup canvasGroup;
    RectTransform fillRect;
    TextMeshProUGUI valueText;
    Camera mainCamera;
    float visibleUntil;

    void Awake()
    {
        stats = GetComponent<EnemyStats>();
        BuildBar();
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

    void BuildBar()
    {
        GameObject canvasGo = new GameObject("HealthBarCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = Vector3.up * HeightAboveRoot;
        canvasGo.transform.localScale = Vector3.one * 0.005f;

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(220f, 42f);

        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        Image bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);
        RectTransform bgRect = bg.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(canvasGo.transform, false);
        Image fillImage = fillGo.AddComponent<Image>();
        fillImage.color = GetCreatureColor();
        fillImage.type = Image.Type.Simple;
        fillRect = fillImage.rectTransform;
        fillRect.anchorMin = new Vector2(0.03f, 0.12f);
        fillRect.anchorMax = new Vector2(0.97f, 0.58f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.pivot = new Vector2(0f, 0.5f);

        GameObject textGo = new GameObject("EnemyInfo", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(canvasGo.transform, false);
        valueText = textGo.GetComponent<TextMeshProUGUI>();
        valueText.fontSize = 12f;
        valueText.fontStyle = FontStyles.Bold;
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.color = new Color(1f, .92f, .72f);
        valueText.outlineWidth = .18f;
        valueText.raycastTarget = false;
        RectTransform textRect = valueText.rectTransform;
        textRect.anchorMin = new Vector2(0f, .55f);
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    void HandleHealthChanged(float current, float max)
    {
        if (fillRect != null)
        {
            Vector3 scale = fillRect.localScale;
            scale.x = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            fillRect.localScale = scale;
        }
        if (valueText != null)
            valueText.text = CleanEnemyName() + "  " +
                             Mathf.CeilToInt(current) + "/" + Mathf.CeilToInt(max);

        visibleUntil = Time.time + VisibleDuration;
        canvasGroup.alpha = 1f;
    }

    Color GetCreatureColor() => stats.Type switch
    {
        CreatureType.Undead => new Color(.58f, .32f, .92f),
        CreatureType.Beast => new Color(.94f, .48f, .16f),
        CreatureType.Boss => new Color(.72f, .2f, .92f),
        _ => new Color(.82f, .14f, .14f)
    };

    string CleanEnemyName()
    {
        string value = gameObject.name.Replace("(Clone)", "").Replace("_", " ").Trim();
        return string.IsNullOrEmpty(value) ? "ENEMIGO" : value.ToUpperInvariant();
    }

    void HandleDeath()
    {
        canvasGroup.alpha = 0f;
    }

    void Update()
    {
        if (canvasGroup.alpha <= 0f)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera != null)
            canvas.transform.rotation = mainCamera.transform.rotation;

        if (Time.time >= visibleUntil)
            canvasGroup.alpha = Mathf.Max(0f, canvasGroup.alpha - Time.deltaTime / FadeDuration);
    }
}

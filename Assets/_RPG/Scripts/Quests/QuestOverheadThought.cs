using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small MU-style thought box that follows a world target and fades without interrupting gameplay.
public class QuestOverheadThought : MonoBehaviour
{
    Transform target;
    CanvasGroup group;
    float duration;

    public static void Show(Transform target, string message, float duration = 6f)
    {
        if (target == null || string.IsNullOrWhiteSpace(message)) return;
        QuestOverheadThought existing = target.GetComponentInChildren<QuestOverheadThought>(true);
        if (existing != null) Destroy(existing.gameObject);

        GameObject root = new GameObject("QuestThought", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(CanvasGroup), typeof(QuestOverheadThought));
        root.transform.SetParent(target, false);
        QuestOverheadThought thought = root.GetComponent<QuestOverheadThought>();
        thought.Initialize(target, message, duration);
    }

    void Initialize(Transform followTarget, string message, float visibleDuration)
    {
        target = followTarget;
        duration = Mathf.Max(2f, visibleDuration);
        transform.localPosition = Vector3.up * 2.62f;
        transform.localScale = Vector3.one * .0038f;

        RectTransform rootRect = GetComponent<RectTransform>();
        // Long internal monologues need real vertical room. Keeping the world-space scale small
        // preserves the compact MU look while avoiding a cramped single-strip paragraph.
        rootRect.sizeDelta = new Vector2(800f, 218f);
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 850;
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        GameObject frame = new GameObject("MUFrame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(transform, false);
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
        Image image = frame.GetComponent<Image>();
        Sprite sprite = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        if (sprite != null) { image.sprite = sprite; image.type = Image.Type.Sliced; image.color = new Color(1f, 1f, 1f, .86f); }
        else image.color = new Color(.035f, .02f, .055f, .86f);

        GameObject textObject = new GameObject("ThoughtText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(frame.transform, false);
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = message;
        label.fontSize = 24f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 15f;
        label.fontSizeMax = 24f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, .88f, .55f);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        RectTransform textRect = label.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(58f, 34f);
        textRect.offsetMax = new Vector2(-58f, -34f);
        StartCoroutine(Lifetime());
    }

    void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera != null) transform.rotation = camera.transform.rotation;
    }

    IEnumerator Lifetime()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (group != null)
                group.alpha = elapsed > duration - 1.25f ? Mathf.Clamp01((duration - elapsed) / 1.25f) : 1f;
            yield return null;
        }
        Destroy(gameObject);
    }
}

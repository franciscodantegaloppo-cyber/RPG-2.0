using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// One-shot reveal played immediately after Tonio assigns the first passage objective.
public sealed class GoblinPassageIntroCinematic : MonoBehaviour
{
    static bool running;

    Camera gameplayCamera;
    Camera cinematicCamera;
    ThirdPersonCamera gameplayController;
    GameObject overlay;
    Image fade;
    TextMeshProUGUI caption;
    bool skip;

    public static void PlayEntranceReveal()
    {
        if (running || FindAnyObjectByType<GoblinPassageIntroCinematic>() != null)
            return;
        new GameObject("GoblinPassageIntroCinematic")
            .AddComponent<GoblinPassageIntroCinematic>();
    }

    IEnumerator Start()
    {
        running = true;

        // Tonio's final button invokes the quest callback before its dialogue closes.
        // Wait for that window to leave the screen before taking camera ownership.
        float waitStarted = Time.unscaledTime;
        while (MerchantDialoguePanel.Instance != null &&
               MerchantDialoguePanel.Instance.IsOpen &&
               Time.unscaledTime - waitStarted < 2f)
            yield return null;
        yield return new WaitForSecondsRealtime(.18f);

        Transform entrance = FindPassageEntrance();
        if (entrance == null)
        {
            Debug.LogWarning("[GoblinPassageCinematic] No se encontro Pasajegoblin_cartel.");
            Finish();
            yield break;
        }

        gameplayCamera = Camera.main;
        if (gameplayCamera == null)
        {
            Finish();
            yield break;
        }

        gameplayController = gameplayCamera.GetComponent<ThirdPersonCamera>();
        BuildCamera();
        BuildOverlay();
        if (cinematicCamera == null)
        {
            Finish();
            yield break;
        }

        GameManager.Instance?.SetState(GameState.InMenu);
        if (gameplayController != null) gameplayController.enabled = false;
        yield return FadeTo(1f, .45f);
        gameplayCamera.enabled = false;
        cinematicCamera.enabled = true;

        Bounds bounds = GetBounds(entrance);
        Vector3 center = bounds.center + Vector3.up * Mathf.Max(.55f, bounds.extents.y * .15f);
        Transform player = GameManager.Instance != null
            ? GameManager.Instance.PlayerTransform
            : FindAnyObjectByType<PlayerStats>()?.transform;
        Vector3 approach = player != null
            ? Vector3.ProjectOnPlane(player.position - center, Vector3.up).normalized
            : Vector3.back;
        if (approach.sqrMagnitude < .01f) approach = Vector3.back;
        Vector3 side = Vector3.Cross(Vector3.up, approach).normalized;

        cinematicCamera.transform.SetPositionAndRotation(
            center + approach * 10f + Vector3.up * 5f,
            Quaternion.LookRotation(center - (center + approach * 10f + Vector3.up * 5f)));
        SetCaption("La entrada al Pasaje Goblin");
        yield return FadeTo(0f, .55f);
        yield return MoveShot(center + approach * 6.2f + side * 2f + Vector3.up * 2.8f,
            center, 2.6f);
        SetCaption("Tonio cree que las runas y los diamantes se encuentran al final.");
        yield return MoveShot(center + approach * 3.8f - side * 2.4f + Vector3.up * 1.9f,
            center + Vector3.up * .45f, 2.5f);
        yield return WaitSkippable(1.2f);
        yield return FadeTo(1f, .4f);
        Finish();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.escapeKey.wasPressedThisFrame ||
             keyboard.eKey.wasPressedThisFrame))
            skip = true;
    }

    IEnumerator MoveShot(Vector3 destination, Vector3 lookAt, float duration)
    {
        Vector3 start = cinematicCamera.transform.position;
        Quaternion startRotation = cinematicCamera.transform.rotation;
        Quaternion endRotation = Quaternion.LookRotation(lookAt - destination, Vector3.up);
        for (float elapsed = 0f; elapsed < duration && !skip;
             elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            cinematicCamera.transform.position = Vector3.Lerp(start, destination, t);
            cinematicCamera.transform.rotation =
                Quaternion.Slerp(startRotation, endRotation, t);
            yield return null;
        }
    }

    IEnumerator WaitSkippable(float duration)
    {
        for (float elapsed = 0f; elapsed < duration && !skip;
             elapsed += Time.unscaledDeltaTime)
            yield return null;
    }

    IEnumerator FadeTo(float target, float duration)
    {
        if (fade == null) yield break;
        float start = fade.color.a;
        for (float elapsed = 0f; elapsed < duration;
             elapsed += Time.unscaledDeltaTime)
        {
            Color color = fade.color;
            color.a = Mathf.Lerp(start, target, elapsed / duration);
            fade.color = color;
            yield return null;
        }
        Color final = fade.color;
        final.a = target;
        fade.color = final;
    }

    static Transform FindPassageEntrance()
    {
        Transform fallback = null;
        foreach (Transform candidate in
                 FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (candidate == null) continue;
            if (candidate.name.Equals("Pasajegoblin_cartel",
                    System.StringComparison.OrdinalIgnoreCase))
                return candidate;
            if (fallback == null &&
                candidate.name.Contains("pasajegoblin",
                    System.StringComparison.OrdinalIgnoreCase))
                fallback = candidate;
        }
        return fallback;
    }

    static Bounds GetBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers.Length > 0
            ? renderers[0].bounds
            : new Bounds(target.position + Vector3.up, new Vector3(3f, 2f, 1f));
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    void BuildCamera()
    {
        GameObject cameraObject = new GameObject("GoblinPassageCinematicCamera");
        cinematicCamera = cameraObject.AddComponent<Camera>();
        cinematicCamera.CopyFrom(gameplayCamera);
        cinematicCamera.enabled = false;
    }

    void BuildOverlay()
    {
        Canvas canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null) return;
        overlay = new GameObject("GoblinPassageCinematicOverlay",
            typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform root = overlay.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        Canvas overlayCanvas = overlay.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 2500;

        GameObject fadeObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeObject.transform.SetParent(overlay.transform, false);
        fade = fadeObject.GetComponent<Image>();
        fade.color = Color.clear;
        RectTransform fadeRect = fade.rectTransform;
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = fadeRect.offsetMax = Vector2.zero;

        GameObject box = new GameObject("CaptionBox", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(overlay.transform, false);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(.2f, .055f);
        boxRect.anchorMax = new Vector2(.8f, .17f);
        boxRect.offsetMin = boxRect.offsetMax = Vector2.zero;
        Image boxImage = box.GetComponent<Image>();
        Sprite frame = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        if (frame != null)
        {
            boxImage.sprite = frame;
            boxImage.type = Image.Type.Sliced;
            boxImage.color = new Color(1f, 1f, 1f, .9f);
        }
        else boxImage.color = new Color(.08f, .045f, .025f, .9f);

        GameObject textObject = new GameObject("Caption",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(box.transform, false);
        caption = textObject.GetComponent<TextMeshProUGUI>();
        caption.alignment = TextAlignmentOptions.Center;
        caption.fontSize = 24f;
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 14f;
        caption.fontSizeMax = 24f;
        caption.color = new Color(1f, .84f, .45f);
        RectTransform captionRect = caption.rectTransform;
        captionRect.anchorMin = Vector2.zero;
        captionRect.anchorMax = Vector2.one;
        captionRect.offsetMin = new Vector2(34f, 12f);
        captionRect.offsetMax = new Vector2(-34f, -12f);
    }

    void SetCaption(string text)
    {
        if (caption != null) caption.text = text;
    }

    void Finish()
    {
        if (gameplayCamera != null) gameplayCamera.enabled = true;
        if (gameplayController != null) gameplayController.enabled = true;
        if (cinematicCamera != null) Destroy(cinematicCamera.gameObject);
        if (overlay != null) Destroy(overlay);
        GameManager.Instance?.SetState(GameState.Exploration);
        running = false;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (running && gameObject != null)
        {
            if (gameplayCamera != null) gameplayCamera.enabled = true;
            if (gameplayController != null) gameplayController.enabled = true;
            running = false;
        }
    }
}

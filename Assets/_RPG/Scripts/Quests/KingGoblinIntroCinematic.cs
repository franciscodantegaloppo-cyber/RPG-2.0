using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class KingGoblinIntroCinematic : MonoBehaviour
{
    [SerializeField] float triggerDistance = 15f;
    bool played;
    bool running;
    Transform player;
    Camera gameplayCamera;
    Camera cinematicCamera;
    ThirdPersonCamera gameplayController;
    GameObject overlayRoot;
    Image fade;
    TextMeshProUGUI subtitle;
    bool skip;

    void Update()
    {
        if (running)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame))
                skip = true;
            return;
        }
        if (played || QuestManager.Instance == null ||
            QuestManager.Instance.MerchantIntroductionState != PrimaryQuestState.GoToKingGoblinStatue)
            return;
        if (player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null) player = found.transform;
        }
        if (player != null && (player.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance)
        {
            played = true;
            StartCoroutine(Play());
        }
    }

    IEnumerator Play()
    {
        running = true;
        skip = false;
        GameManager.Instance?.SetState(GameState.InMenu);
        gameplayCamera = Camera.main;
        if (gameplayCamera != null)
        {
            gameplayController = gameplayCamera.GetComponent<ThirdPersonCamera>();
            if (gameplayController != null) gameplayController.enabled = false;
        }
        BuildOverlay();
        BuildCamera();
        if (cinematicCamera == null)
        {
            Finish();
            yield break;
        }
        yield return FadeTo(1f, .55f);
        if (skip) { Finish(); yield break; }

        if (gameplayCamera != null) gameplayCamera.enabled = false;
        cinematicCamera.enabled = true;
        yield return FadeTo(0f, .7f);

        Bounds bounds = GetBounds();
        Vector3 center = bounds.center + Vector3.up * bounds.extents.y * .2f;
        Vector3 fromPlayer = player != null ? Vector3.ProjectOnPlane(player.position - center, Vector3.up).normalized : Vector3.back;
        if (fromPlayer.sqrMagnitude < .01f) fromPlayer = Vector3.back;
        Vector3 side = Vector3.Cross(Vector3.up, fromPlayer).normalized;

        yield return MoveShot(center + fromPlayer * 8f + Vector3.up * 3.2f, center, 2.6f);
        SetSubtitle("La estatua del Rey Goblin...");
        yield return WaitSkippable(1.8f);
        yield return MoveShot(center + side * 4.2f + Vector3.up * 2.2f, center + Vector3.up * 1.1f, 2.4f);
        SetSubtitle("No entiendo lo que est\u00e1 sucediendo...");
        yield return WaitSkippable(2.1f);
        yield return MoveShot(center - fromPlayer * 5.3f + Vector3.up * 2.7f, center, 2.1f);
        SetSubtitle("Hay algo oscuro encerrado en esa corona.");
        yield return WaitSkippable(2.2f);
        Finish();
    }

    IEnumerator MoveShot(Vector3 destination, Vector3 lookAt, float duration)
    {
        if (cinematicCamera == null) yield break;
        Vector3 start = cinematicCamera.transform.position;
        Quaternion startRotation = cinematicCamera.transform.rotation;
        Quaternion destinationRotation = Quaternion.LookRotation(lookAt - destination, Vector3.up);
        float elapsed = 0f;
        while (elapsed < duration && !skip)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            cinematicCamera.transform.position = Vector3.Lerp(start, destination, t);
            cinematicCamera.transform.rotation = Quaternion.Slerp(startRotation, destinationRotation, t);
            yield return null;
        }
    }

    IEnumerator WaitSkippable(float duration)
    {
        for (float elapsed = 0f; elapsed < duration && !skip; elapsed += Time.unscaledDeltaTime)
            yield return null;
    }

    IEnumerator FadeTo(float alpha, float duration)
    {
        if (fade == null) yield break;
        float start = fade.color.a;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            Color color = fade.color;
            color.a = Mathf.Lerp(start, alpha, elapsed / duration);
            fade.color = color;
            yield return null;
        }
        Color final = fade.color; final.a = alpha; fade.color = final;
    }

    void BuildCamera()
    {
        if (gameplayCamera == null)
            return;

        GameObject go = new GameObject("KingGoblinCinematicCamera");
        cinematicCamera = go.AddComponent<Camera>();
        cinematicCamera.CopyFrom(gameplayCamera);
        cinematicCamera.enabled = false;
        go.tag = "MainCamera";
        AudioListener listener = go.GetComponent<AudioListener>();
        if (listener != null) Destroy(listener);
    }

    void BuildOverlay()
    {
        Canvas canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null) return;
        overlayRoot = new GameObject("KingGoblinCinematicOverlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        overlayRoot.transform.SetParent(canvas.transform, false);
        RectTransform root = overlayRoot.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        Canvas top = overlayRoot.GetComponent<Canvas>(); top.overrideSorting = true; top.sortingOrder = 2400;

        GameObject fadeObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeObject.transform.SetParent(overlayRoot.transform, false);
        fade = fadeObject.GetComponent<Image>();
        fade.color = new Color(0f, 0f, 0f, 0f);
        RectTransform fr = fade.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = fr.offsetMax = Vector2.zero;

        GameObject box = new GameObject("SubtitleBox", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(overlayRoot.transform, false);
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(.22f, .055f); br.anchorMax = new Vector2(.78f, .17f);
        br.offsetMin = br.offsetMax = Vector2.zero;
        Image image = box.GetComponent<Image>();
        Sprite frame = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        if (frame != null) { image.sprite = frame; image.type = Image.Type.Sliced; }
        image.color = new Color(1f, 1f, 1f, .82f);

        GameObject text = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(box.transform, false);
        subtitle = text.GetComponent<TextMeshProUGUI>();
        subtitle.fontSize = 24f; subtitle.enableAutoSizing = true;
        subtitle.fontSizeMin = 15f; subtitle.fontSizeMax = 24f;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.color = new Color(1f, .86f, .5f);
        RectTransform tr = subtitle.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(40f, 15f); tr.offsetMax = new Vector2(-40f, -15f);
    }

    void SetSubtitle(string value)
    {
        if (subtitle != null) subtitle.text = value;
    }

    Bounds GetBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(transform.position, Vector3.one * 4f);
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    void Finish()
    {
        if (!running) return;
        if (cinematicCamera != null) Destroy(cinematicCamera.gameObject);
        cinematicCamera = null;
        if (gameplayCamera != null)
        {
            gameplayCamera.enabled = true;
            if (gameplayController != null) gameplayController.enabled = true;
        }
        if (overlayRoot != null) Destroy(overlayRoot);
        overlayRoot = null;
        GameManager.Instance?.SetState(GameState.Exploration);
        running = false;
    }

    void OnDisable()
    {
        if (running) Finish();
    }
}

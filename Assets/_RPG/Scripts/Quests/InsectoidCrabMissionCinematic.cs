using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InsectoidCrabMissionCinematic : MonoBehaviour
{
    const string ChestName = "FifthMission_ForgottenSwordChest";
    [SerializeField] float triggerDistance = 20f;

    Transform player;
    Camera gameplayCamera;
    Camera cinematicCamera;
    ThirdPersonCamera gameplayController;
    GameObject overlayRoot;
    Image fade;
    TextMeshProUGUI subtitle;
    bool played;
    bool running;
    bool skip;

    void Update()
    {
        if (running)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                skip = true;
            return;
        }

        QuestManager quests = QuestManager.Instance;
        if (played || quests == null ||
            quests.MerchantIntroductionState != PrimaryQuestState.DefeatInsectoidCrabBoss)
            return;

        if (player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null) player = found.transform;
        }

        if (player != null &&
            (player.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance)
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

        yield return FadeTo(1f, .45f);
        if (gameplayCamera != null) gameplayCamera.enabled = false;
        cinematicCamera.enabled = true;
        yield return FadeTo(0f, .55f);

        Bounds bossBounds = GetBounds(transform);
        Vector3 bossCenter = bossBounds.center + Vector3.up * bossBounds.extents.y * .08f;
        Vector3 approach = player != null
            ? Vector3.ProjectOnPlane(player.position - bossCenter, Vector3.up).normalized
            : Vector3.back;
        if (approach.sqrMagnitude < .01f) approach = Vector3.back;
        Vector3 side = Vector3.Cross(Vector3.up, approach).normalized;

        SetCamera(bossCenter + approach * 8.5f + Vector3.up * 3.4f, bossCenter);
        SetSubtitle("Algo duerme al final del pasaje...");
        yield return WaitSkippable(2.1f);
        yield return MoveShot(bossCenter + side * 6.1f + Vector3.up * 2.6f,
            bossCenter + Vector3.up * .5f, 2.4f);
        SetSubtitle("El Insectoid Crab Boss protege la recompensa.");
        yield return WaitSkippable(1.9f);

        GameObject chest = GameObject.Find(ChestName);
        if (!skip && chest != null)
        {
            Bounds chestBounds = GetBounds(chest.transform);
            Vector3 chestCenter = chestBounds.center;
            Vector3 chestDirection = Vector3.ProjectOnPlane(bossCenter - chestCenter, Vector3.up).normalized;
            if (chestDirection.sqrMagnitude < .01f) chestDirection = Vector3.back;
            SetCamera(chestCenter + chestDirection * 4.2f + Vector3.up * 2.1f,
                chestCenter + Vector3.up * .35f);
            SetSubtitle("El cofre del Sable Oscuro s\u00f3lo se abrir\u00e1 cuando la criatura muera.");
            yield return WaitSkippable(2.7f);
        }

        yield return FadeTo(1f, .42f);
        Finish();
    }

    IEnumerator MoveShot(Vector3 destination, Vector3 lookAt, float duration)
    {
        Vector3 start = cinematicCamera.transform.position;
        Quaternion startRotation = cinematicCamera.transform.rotation;
        Quaternion endRotation = Quaternion.LookRotation(lookAt - destination, Vector3.up);
        for (float elapsed = 0f; elapsed < duration && !skip; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            cinematicCamera.transform.position = Vector3.Lerp(start, destination, t);
            cinematicCamera.transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
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
        Color final = fade.color;
        final.a = alpha;
        fade.color = final;
    }

    void BuildCamera()
    {
        if (gameplayCamera == null) return;
        GameObject cameraObject = new GameObject("InsectoidCrabMissionCamera");
        cinematicCamera = cameraObject.AddComponent<Camera>();
        cinematicCamera.CopyFrom(gameplayCamera);
        cinematicCamera.enabled = false;
    }

    void BuildOverlay()
    {
        Canvas canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null) return;

        overlayRoot = new GameObject("InsectoidCrabCinematicOverlay",
            typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        overlayRoot.transform.SetParent(canvas.transform, false);
        RectTransform root = overlayRoot.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        Canvas top = overlayRoot.GetComponent<Canvas>();
        top.overrideSorting = true;
        top.sortingOrder = 2500;

        GameObject fadeObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeObject.transform.SetParent(overlayRoot.transform, false);
        fade = fadeObject.GetComponent<Image>();
        fade.color = new Color(0f, 0f, 0f, 0f);
        fade.raycastTarget = false;
        RectTransform fadeRect = fade.rectTransform;
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = fadeRect.offsetMax = Vector2.zero;

        GameObject box = new GameObject("SubtitleBox", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(overlayRoot.transform, false);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(.18f, .045f);
        boxRect.anchorMax = new Vector2(.82f, .17f);
        boxRect.offsetMin = boxRect.offsetMax = Vector2.zero;
        Image frameImage = box.GetComponent<Image>();
        Sprite frame = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        if (frame != null)
        {
            frameImage.sprite = frame;
            frameImage.type = Image.Type.Sliced;
        }
        frameImage.color = new Color(1f, 1f, 1f, .86f);

        GameObject textObject = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(box.transform, false);
        subtitle = textObject.GetComponent<TextMeshProUGUI>();
        subtitle.fontSize = 24f;
        subtitle.enableAutoSizing = true;
        subtitle.fontSizeMin = 15f;
        subtitle.fontSizeMax = 24f;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.color = new Color(.82f, .5f, 1f);
        subtitle.outlineColor = Color.black;
        subtitle.outlineWidth = .18f;
        RectTransform textRect = subtitle.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(38f, 12f);
        textRect.offsetMax = new Vector2(-38f, -12f);
    }

    void SetCamera(Vector3 position, Vector3 lookAt)
    {
        if (cinematicCamera == null) return;
        cinematicCamera.transform.position = position;
        cinematicCamera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
    }

    void SetSubtitle(string text)
    {
        if (subtitle != null) subtitle.text = text;
    }

    static Bounds GetBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers.Length > 0
            ? renderers[0].bounds
            : new Bounds(root.position, Vector3.one * 3f);
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    void Finish()
    {
        if (!running) return;
        if (cinematicCamera != null) Destroy(cinematicCamera.gameObject);
        if (gameplayCamera != null)
        {
            gameplayCamera.enabled = true;
            if (gameplayController != null) gameplayController.enabled = true;
        }
        if (overlayRoot != null) Destroy(overlayRoot);
        cinematicCamera = null;
        overlayRoot = null;
        GameManager.Instance?.SetState(GameState.Exploration);
        running = false;
    }

    void OnDisable()
    {
        if (running) Finish();
    }
}

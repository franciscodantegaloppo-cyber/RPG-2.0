using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runtime presentation for mission seven: Tonio's disappearance, blood trail,
// Nahue's transformation/death, the extreme gust and the player's awakening.
public sealed class SeventhMissionSequence : MonoBehaviour
{
    public static SeventhMissionSequence Instance { get; private set; }

    GameObject bloodRoot;
    TonioQuestGiver tonio;
    NahueQuestGiver nahue;
    bool transformationRunning;
    CanvasGroup blackout;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<SeventhMissionSequence>(
                FindObjectsInactive.Include) != null) return;
        new GameObject("SeventhMissionSequence")
            .AddComponent<SeventhMissionSequence>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildBlackout();
    }

    void Start()
    {
        if (QuestManager.Instance != null &&
            QuestManager.Instance.MerchantIntroductionState >=
                PrimaryQuestState.SeventhUseRedDiamond)
            PrepareMissionSeven();
    }

    public void PrepareMissionSeven()
    {
        FindCharacters();
        if (QuestManager.Instance != null &&
            QuestManager.Instance.MerchantIntroductionState >=
                PrimaryQuestState.SeventhFindTonio)
            BeginTonioSearch();
    }

    public void BeginTonioSearch()
    {
        FindCharacters();
        if (tonio == null || nahue == null) return;
        Vector3 tonioPosition = tonio.transform.position;
        tonio.gameObject.SetActive(false);
        // Keep the end of the trail aligned with the character who must be confronted.
        nahue.GetComponent<NPCWander>()?.PauseForInteraction();
        BuildBloodTrail(tonioPosition, nahue.transform.position);
        QuestOverheadThought.Show(
            FindAnyObjectByType<PlayerStats>()?.transform,
            "Tonio desapareci\u00f3... \u00bfDe qui\u00e9n es toda esta sangre?", 5f);
    }

    void FindCharacters()
    {
        if (tonio == null)
            tonio = FindAnyObjectByType<TonioQuestGiver>(
                FindObjectsInactive.Include);
        if (nahue == null)
            nahue = FindAnyObjectByType<NahueQuestGiver>(
                FindObjectsInactive.Include);
    }

    void BuildBloodTrail(Vector3 from, Vector3 to)
    {
        if (bloodRoot != null) Destroy(bloodRoot);
        bloodRoot = new GameObject("Mission7_BloodTrail");
        Vector3 flatDelta = to - from;
        flatDelta.y = 0f;
        float distance = flatDelta.magnitude;
        int count = Mathf.Clamp(Mathf.CeilToInt(distance / 1.45f), 5, 45);
        Material blood = CreateBloodMaterial();
        for (int i = 0; i <= count; i++)
        {
            float t = i / (float)count;
            Vector3 point = Vector3.Lerp(from, to, t);
            point += Vector3.Cross(flatDelta.normalized, Vector3.up) *
                     Mathf.Sin(i * 2.13f) * .36f;
            point = SnapToGround(point);
            GameObject stain = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stain.name = i == 0 ? "Charco_de_sangre_Tonio" : "Rastro_de_sangre";
            stain.transform.SetParent(bloodRoot.transform, true);
            stain.transform.position = point + Vector3.up * .018f;
            stain.transform.localScale = i == 0
                ? new Vector3(1.7f, .008f, 1.3f)
                : new Vector3(Random.Range(.18f, .42f), .006f,
                    Random.Range(.3f, .72f));
            stain.transform.rotation = Quaternion.Euler(0f,
                Random.Range(0f, 360f), 0f);
            stain.GetComponent<Renderer>().sharedMaterial = blood;
            Destroy(stain.GetComponent<Collider>());
        }

        GameObject startTrigger = new GameObject("DiscoverBloodTrail");
        startTrigger.transform.SetParent(bloodRoot.transform, false);
        startTrigger.transform.position = from;
        QuestBloodTrailMarker start = startTrigger.AddComponent<QuestBloodTrailMarker>();
        start.Configure(false);

        GameObject endTrigger = new GameObject("ReachNahueBloodTrail");
        endTrigger.transform.SetParent(bloodRoot.transform, false);
        endTrigger.transform.position = to;
        QuestBloodTrailMarker end = endTrigger.AddComponent<QuestBloodTrailMarker>();
        end.Configure(true, nahue != null ? nahue.transform : null);
    }

    static Vector3 SnapToGround(Vector3 point)
    {
        if (Physics.Raycast(point + Vector3.up * 30f, Vector3.down,
                out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        return point;
    }

    static Material CreateBloodMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("Sprites/Default");
        Material material = new Material(shader)
        {
            name = "Mission7_Blood_Runtime",
            color = new Color(.23f, .002f, .008f, .78f),
            renderQueue = 3000
        };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", material.color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        return material;
    }

    public void BeginNahueTransformation(NahueQuestGiver target)
    {
        if (transformationRunning || target == null) return;
        transformationRunning = true;
        StartCoroutine(PlayNahueTransformation(target));
    }

    IEnumerator PlayNahueTransformation(NahueQuestGiver target)
    {
        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null) playerController.enabled = false;
        NPCWander wander = target.GetComponent<NPCWander>();
        if (wander != null) wander.enabled = false;

        Camera main = Camera.main;
        Camera cinematic = null;
        if (main != null)
        {
            GameObject cameraGo = new GameObject("NahueTransformationCamera");
            cinematic = cameraGo.AddComponent<Camera>();
            cinematic.CopyFrom(main);
            cameraGo.transform.position = target.transform.position +
                target.transform.forward * 4.5f + Vector3.up * 1.8f;
            cameraGo.transform.LookAt(target.transform.position + Vector3.up);
            main.enabled = false;
        }

        Animator animator = target.GetComponentInChildren<Animator>();
        Renderer[] nahueRenderers = target.GetComponentsInChildren<Renderer>(true);
        Vector3 originalScale = target.transform.localScale;
        GameObject goblinVisual = CreateGoblinVisual(target.transform);

        float duration = 7f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float panic = Mathf.Sin(elapsed * 10f) * Mathf.SmoothStep(0f, 1f, t);
            target.transform.localScale = Vector3.Lerp(originalScale,
                new Vector3(originalScale.x * .78f, originalScale.y * .84f,
                    originalScale.z * 1.08f), t);
            target.transform.rotation *= Quaternion.Euler(0f, panic * .35f, 0f);
            if (animator != null && elapsed > 1f && elapsed < 4.5f)
                animator.SetFloat("Speed", Mathf.Abs(panic) * .25f);

            TintNahue(nahueRenderers, t);
            if (goblinVisual != null)
                SetVisualAlpha(goblinVisual, Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(.35f, .9f, t)));
            yield return null;
        }

        if (animator != null) animator.speed = 0f;
        foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        target.enabled = false;
        yield return new WaitForSeconds(1.2f);

        if (main != null) main.enabled = true;
        if (cinematic != null) Destroy(cinematic.gameObject);
        if (playerController != null) playerController.enabled = true;

        yield return new WaitForSeconds(5f);
        ExtremeWindEventController wind = ExtremeWindEventController.Instance;
        bool windFinished = false;
        System.Action handler = () => windFinished = true;
        if (wind != null)
        {
            wind.ExtremeGustEnded += handler;
            wind.StartExtremeGust(10f, 300f);
            while (!windFinished) yield return null;
            wind.ExtremeGustEnded -= handler;
        }

        yield return StartCoroutine(BlackoutAndWake(playerController));
        QuestManager.Instance?.CompleteSeventhMission();
        transformationRunning = false;
    }

    static GameObject CreateGoblinVisual(Transform nahueTransform)
    {
        foreach (EnemyStats enemy in FindObjectsByType<EnemyStats>(
                     FindObjectsInactive.Include))
        {
            if (!enemy.name.ToLowerInvariant().Contains("goblin")) continue;
            Animator source = enemy.GetComponentInChildren<Animator>();
            if (source == null) continue;
            GameObject visual = Instantiate(source.gameObject,
                nahueTransform.position, nahueTransform.rotation);
            visual.name = "Nahue_GoblinTransformation";
            foreach (MonoBehaviour behaviour in
                     visual.GetComponentsInChildren<MonoBehaviour>(true))
                Destroy(behaviour);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                Destroy(collider);
            SetVisualAlpha(visual, 0f);
            return visual;
        }
        return null;
    }

    static void TintNahue(Renderer[] renderers, float amount)
    {
        foreach (Renderer renderer in renderers)
            foreach (Material material in renderer.materials)
            {
                Color baseColor = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material.color;
                Color changed = Color.Lerp(baseColor,
                    new Color(.22f, .43f, .12f, baseColor.a), amount * .72f);
                material.color = changed;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", changed);
            }
    }

    static void SetVisualAlpha(GameObject root, float alpha)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.materials)
            {
                Color color = material.color;
                color.a = alpha;
                material.color = color;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
            }
    }

    IEnumerator BlackoutAndWake(PlayerController controller)
    {
        if (controller != null) controller.enabled = false;
        PlayerAnimatorBridge bridge = FindAnyObjectByType<PlayerAnimatorBridge>();
        blackout.gameObject.SetActive(true);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
        {
            blackout.alpha = t;
            yield return null;
        }
        blackout.alpha = 1f;
        yield return new WaitForSecondsRealtime(5f);

        OldManDialogue oldMan = FindAnyObjectByType<OldManDialogue>(
            FindObjectsInactive.Include);
        if (controller != null && oldMan != null)
        {
            CharacterController cc = controller.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            controller.transform.position = oldMan.transform.position +
                oldMan.transform.forward * 2.2f + Vector3.up * .2f;
            if (cc != null) cc.enabled = true;
        }
        // Start the recovery while the black screen is lifting so the player is visibly
        // getting up beside El Viejo instead of completing the animation off-camera.
        bridge?.TriggerKnockdown();
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime * .55f)
        {
            blackout.alpha = t;
            yield return null;
        }
        blackout.gameObject.SetActive(false);
        if (controller != null) controller.enabled = true;
    }

    void BuildBlackout()
    {
        GameObject canvasGo = new GameObject("Mission7BlackoutCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        GameObject imageGo = new GameObject("Black", typeof(RectTransform),
            typeof(Image), typeof(CanvasGroup));
        imageGo.transform.SetParent(canvasGo.transform, false);
        RectTransform rect = imageGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        imageGo.GetComponent<Image>().color = Color.black;
        blackout = imageGo.GetComponent<CanvasGroup>();
        blackout.alpha = 0f;
        imageGo.SetActive(false);
    }
}

public sealed class QuestBloodTrailMarker : MonoBehaviour
{
    bool end;
    bool consumed;
    Transform player;
    Transform followTarget;

    public void Configure(bool isEnd, Transform target = null)
    {
        end = isEnd;
        followTarget = target;
    }

    void Update()
    {
        if (consumed) return;
        if (end && followTarget != null)
            transform.position = followTarget.position;
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }
        if (player == null || Vector3.Distance(player.position, transform.position) > 3f)
            return;
        consumed = true;
        if (end)
            QuestManager.Instance?.ReachNahueThroughBloodTrail();
        else
            QuestManager.Instance?.DiscoverBloodTrail();
    }
}

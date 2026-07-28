using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SixthMissionSequence : MonoBehaviour
{
    public static SixthMissionSequence Instance { get; private set; }

    [SerializeField] Transform redDiamondTarget;
    [SerializeField] Sprite darkAuraIndicatorSprite;
    [SerializeField] Material stoneMaterial;

    readonly List<GameObject> darkStains = new List<GameObject>();
    readonly List<GameObject> npcGuideStatues = new List<GameObject>();
    readonly List<float> npcGuideFootOffsets = new List<float>();
    readonly Dictionary<Renderer, Material[]> originalMaterials =
        new Dictionary<Renderer, Material[]>();
    readonly Dictionary<Behaviour, bool> originalBehaviourStates =
        new Dictionary<Behaviour, bool>();
    readonly Dictionary<Rigidbody, bool> originalRigidbodyKinematicStates =
        new Dictionary<Rigidbody, bool>();

    CanvasGroup auraIndicator;
    Transform player;
    bool warningRunning;
    bool cinematicRunning;
    bool villagePetrified;
    float nextPetrifyRefresh;
    Material stainMaterial;
    Material guideStatueMaterial;
    GameObject redDiamondCrabBoss;
    EnemyStats redDiamondCrabStats;
    bool redDiamondEncounterActive;
    bool savedFog;
    FogMode savedFogMode;
    float savedFogStart;
    float savedFogEnd;
    float savedFogDensity;
    Color savedFogColor;
    float nextRedDiamondRepair;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!SceneManager.GetActiveScene().name.Equals("SpawnVillage",
                System.StringComparison.OrdinalIgnoreCase) ||
            FindAnyObjectByType<SixthMissionSequence>(
                FindObjectsInactive.Include) != null)
            return;
        new GameObject("SixthMissionSequence_Runtime")
            .AddComponent<SixthMissionSequence>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        FindExistingRedDiamondTarget();
        BuildAuraIndicator();
        BuildDarkStains();
        PrepareRedDiamondCrabBoss();
    }

    void Update()
    {
        QuestManager quests = QuestManager.Instance;
        if (quests == null)
        {
            SetGuidanceVisible(false);
            return;
        }

        if (quests.MerchantIntroductionState == PrimaryQuestState.SixthMissionWarningPending &&
            !warningRunning)
            StartCoroutine(PlaySixthMissionOpening());

        if (quests.IsSixthMissionActive &&
            Time.time >= nextRedDiamondRepair)
        {
            nextRedDiamondRepair = Time.time + 1f;
            EnsureRedDiamondAvailable();
        }

        bool guidance = quests.IsSixthMissionActive && quests.IsDarkSaberEquipped();
        SetGuidanceVisible(guidance);
        if (guidance)
            UpdateDarkStainPath();

        if (villagePetrified && Time.time >= nextPetrifyRefresh)
        {
            nextPetrifyRefresh = Time.time + 1f;
            PetrifyNewVillageLife();
        }
    }

    IEnumerator PlaySixthMissionOpening()
    {
        warningRunning = true;
        yield return new WaitForSeconds(3f);
        if (QuestManager.Instance == null ||
            QuestManager.Instance.MerchantIntroductionState !=
                PrimaryQuestState.SixthMissionWarningPending)
        {
            warningRunning = false;
            yield break;
        }

        yield return ShowWarning(
            "La oscuridad atrae m\u00e1s oscuridad.\n" +
            "Cada latido del Diamante Rojo convoca una sombra nueva.", 5.5f);

        MerchantDialoguePanel panel = MerchantDialoguePanel.EnsureRuntime();
        if (panel == null)
        {
            QuestManager.Instance.BeginSixthTonioObjective();
            warningRunning = false;
            yield break;
        }

        panel.Show("Frank",
            "\u00bfDebo seguir la oscuridad?",
            "Siguiente", () => panel.Show("Frank",
                "\u00bfEl Diamante Rojo?",
                "Siguiente", () => panel.Show("Frank",
                    "Quiz\u00e1 hace referencia a la estatua del Rey Goblin. Puede servirme...",
                    "Hablar con Tonio", () =>
                    {
                        QuestManager.Instance?.BeginSixthTonioObjective();
                        warningRunning = false;
                    })));
    }

    IEnumerator ShowWarning(string message, float duration)
    {
        Canvas canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null) yield break;

        GameObject root = new GameObject("SixthMissionDarkWarning",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(.25f, .68f);
        rootRect.anchorMax = new Vector2(.75f, .86f);
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
        Canvas topCanvas = root.GetComponent<Canvas>();
        topCanvas.overrideSorting = true;
        topCanvas.sortingOrder = 2300;
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        Image frame = root.AddComponent<Image>();
        Sprite panelFrame = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        if (panelFrame != null)
        {
            frame.sprite = panelFrame;
            frame.type = Image.Type.Sliced;
        }
        frame.color = new Color(.32f, .2f, .42f, .96f);
        frame.raycastTarget = false;

        GameObject textObject = new GameObject("WarningText",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = message;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 25f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = 25f;
        text.color = new Color(.92f, .76f, 1f);
        text.outlineColor = new Color(.04f, 0f, .08f, 1f);
        text.outlineWidth = .19f;
        text.raycastTarget = false;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(52f, 25f);
        textRect.offsetMax = new Vector2(-52f, -25f);

        for (float t = 0f; t < .45f; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.SmoothStep(0f, 1f, t / .45f);
            yield return null;
        }
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(duration);
        for (float t = 0f; t < .55f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / .55f);
            yield return null;
        }
        Destroy(root);
    }

    public void DiscoverPetrifiedVillage(Transform tonio, Transform interactingPlayer)
    {
        if (cinematicRunning || tonio == null || QuestManager.Instance == null ||
            QuestManager.Instance.MerchantIntroductionState !=
                PrimaryQuestState.SixthTalkToPetrifiedTonio)
            return;

        player = interactingPlayer;
        PetrifyVillage();
        StartCoroutine(PlayTonioStoneCinematic(tonio));
    }

    IEnumerator PlayTonioStoneCinematic(Transform tonio)
    {
        cinematicRunning = true;
        GameManager.Instance?.SetState(GameState.InMenu);
        MerchantDialoguePanel.Instance?.Hide();

        Camera gameplayCamera = Camera.main;
        ThirdPersonCamera gameplayController =
            gameplayCamera != null ? gameplayCamera.GetComponent<ThirdPersonCamera>() : null;
        if (gameplayController != null) gameplayController.enabled = false;

        Camera cinematicCamera = null;
        if (gameplayCamera != null)
        {
            GameObject cameraObject = new GameObject("TonioPetrifiedCinematicCamera");
            cinematicCamera = cameraObject.AddComponent<Camera>();
            cinematicCamera.CopyFrom(gameplayCamera);
            cinematicCamera.enabled = false;
        }

        GameObject overlay;
        Image fade;
        TextMeshProUGUI subtitle;
        BuildCinematicOverlay(out overlay, out fade, out subtitle);
        yield return Fade(fade, 0f, 1f, .4f);

        if (gameplayCamera != null) gameplayCamera.enabled = false;
        if (cinematicCamera != null) cinematicCamera.enabled = true;
        yield return Fade(fade, 1f, 0f, .55f);

        Bounds bounds = GetBounds(tonio);
        Vector3 center = bounds.center + Vector3.up * bounds.extents.y * .05f;
        Vector3 direction = player != null
            ? Vector3.ProjectOnPlane(player.position - center, Vector3.up).normalized
            : Vector3.back;
        if (direction.sqrMagnitude < .01f) direction = Vector3.back;
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 start = center + direction * 4.2f + Vector3.up * 1.55f;
        Vector3 end = center + side * 3.1f + direction * 2.2f + Vector3.up * 1.25f;
        if (cinematicCamera != null)
        {
            cinematicCamera.transform.position = start;
            cinematicCamera.transform.rotation =
                Quaternion.LookRotation(center - start, Vector3.up);
        }
        if (subtitle != null)
            subtitle.text =
                "Parece ser que el Diamante Rojo es el coraz\u00f3n que mantiene vivo ese llamado.";

        bool skip = false;
        for (float elapsed = 0f; elapsed < 6.5f && !skip; elapsed += Time.unscaledDeltaTime)
        {
            Keyboard keyboard = Keyboard.current;
            skip = keyboard != null &&
                (keyboard.eKey.wasPressedThisFrame ||
                 keyboard.escapeKey.wasPressedThisFrame);
            if (cinematicCamera != null)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / 6.5f);
                cinematicCamera.transform.position = Vector3.Lerp(start, end, t);
                cinematicCamera.transform.rotation = Quaternion.LookRotation(
                    center - cinematicCamera.transform.position, Vector3.up);
            }
            yield return null;
        }

        yield return Fade(fade, fade != null ? fade.color.a : 0f, 1f, .35f);
        if (cinematicCamera != null) Destroy(cinematicCamera.gameObject);
        if (gameplayCamera != null)
        {
            gameplayCamera.enabled = true;
            if (gameplayController != null) gameplayController.enabled = true;
        }
        if (overlay != null) Destroy(overlay);
        GameManager.Instance?.SetState(GameState.Exploration);
        QuestManager.Instance?.BeginRedDiamondSearch();
        cinematicRunning = false;
    }

    void PetrifyVillage()
    {
        villagePetrified = true;
        PetrifyNewVillageLife();
    }

    void PetrifyNewVillageLife()
    {
        HashSet<GameObject> roots = new HashSet<GameObject>();
        AddRoots(roots, FindObjectsByType<TonioQuestGiver>(FindObjectsInactive.Include));
        AddRoots(roots, FindObjectsByType<NahueQuestGiver>(FindObjectsInactive.Include));
        AddRoots(roots, FindObjectsByType<NPCMerchant>(FindObjectsInactive.Include));
        AddRoots(roots, FindObjectsByType<NPCHerrero>(FindObjectsInactive.Include));
        AddRoots(roots, FindObjectsByType<OldManDialogue>(FindObjectsInactive.Include));
        AddRoots(roots, FindObjectsByType<NPCMeleeDefender>(FindObjectsInactive.Include));
        AddRoots(roots, FindObjectsByType<AnimalAI>(FindObjectsInactive.Include));

        foreach (GameObject root in roots)
            PetrifyRoot(root);

        DeerSpawner[] deerSpawners =
            FindObjectsByType<DeerSpawner>(FindObjectsInactive.Include);
        foreach (DeerSpawner spawner in deerSpawners)
            SaveAndDisable(spawner);
    }

    static void AddRoots<T>(HashSet<GameObject> roots, T[] components) where T : Component
    {
        foreach (T component in components)
            if (component != null) roots.Add(component.gameObject);
    }

    void PetrifyRoot(GameObject root)
    {
        if (root == null) return;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer is ParticleSystemRenderer ||
                renderer is LineRenderer || originalMaterials.ContainsKey(renderer))
                continue;
            originalMaterials.Add(renderer, renderer.sharedMaterials);
            if (stoneMaterial == null) continue;
            Material[] stones = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < stones.Length; i++) stones[i] = stoneMaterial;
            renderer.sharedMaterials = stones;
        }

        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            SaveAndDisable(animator);
        foreach (AnimalAI ai in root.GetComponentsInChildren<AnimalAI>(true))
            SaveAndDisable(ai);
        foreach (NPCWander ai in root.GetComponentsInChildren<NPCWander>(true))
            SaveAndDisable(ai);
        foreach (NPCMeleeDefender ai in root.GetComponentsInChildren<NPCMeleeDefender>(true))
            SaveAndDisable(ai);
        foreach (NPCAmbientSpeech speech in root.GetComponentsInChildren<NPCAmbientSpeech>(true))
            SaveAndDisable(speech);
        foreach (NavMeshAgent agent in root.GetComponentsInChildren<NavMeshAgent>(true))
            SaveAndDisable(agent);

        // A petrified character is scenery: dialogue/merchant/quest components must not still
        // answer PlayerInteraction, even if their own movement component was disabled.
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is IInteractable)
                SaveAndDisable(behaviour);

        // Some NPC/animal prefabs are physics driven instead of using a NavMeshAgent. Freezing
        // their bodies prevents accumulated forces from sliding the stone statue.
        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
        {
            if (body == null || originalRigidbodyKinematicStates.ContainsKey(body)) continue;
            originalRigidbodyKinematicStates.Add(body, body.isKinematic);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }

    void SaveAndDisable(Behaviour behaviour)
    {
        if (behaviour == null || originalBehaviourStates.ContainsKey(behaviour)) return;
        originalBehaviourStates.Add(behaviour, behaviour.enabled);
        behaviour.enabled = false;
    }

    public void RestoreVillageLife()
    {
        villagePetrified = false;
        foreach (KeyValuePair<Renderer, Material[]> entry in originalMaterials)
            if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
        foreach (KeyValuePair<Behaviour, bool> entry in originalBehaviourStates)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        foreach (KeyValuePair<Rigidbody, bool> entry in originalRigidbodyKinematicStates)
            if (entry.Key != null) entry.Key.isKinematic = entry.Value;
        originalMaterials.Clear();
        originalBehaviourStates.Clear();
        originalRigidbodyKinematicStates.Clear();
        SetGuidanceVisible(false);
    }

    void PrepareRedDiamondCrabBoss()
    {
        foreach (CrabDemonBossAI candidate in
                 FindObjectsByType<CrabDemonBossAI>(FindObjectsInactive.Include))
        {
            if (candidate == null ||
                !candidate.gameObject.name.Contains("CrabDemonBoss",
                    System.StringComparison.OrdinalIgnoreCase))
                continue;
            redDiamondCrabBoss = candidate.gameObject;
            redDiamondCrabBoss.SetActive(false);
            return;
        }
    }

    void FindExistingRedDiamondTarget()
    {
        if (redDiamondTarget != null) return;
        RedDiamondPickup pickup =
            FindAnyObjectByType<RedDiamondPickup>(FindObjectsInactive.Include);
        if (pickup != null)
            redDiamondTarget = pickup.transform;
    }

    void EnsureRedDiamondAvailable()
    {
        FindExistingRedDiamondTarget();
        RedDiamondPickup pickup = redDiamondTarget != null
            ? redDiamondTarget.GetComponent<RedDiamondPickup>()
            : null;

        if (pickup == null)
        {
            // The ItemData is in Resources and retains its prefab dependency even
            // though the quest prefab itself lives outside a Resources directory.
            ItemData item = Resources.Load<ItemData>("Items/RedDiamond");
            GameObject prefab = item != null ? item.equipmentPrefab : null;
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab);
                instance.name = "Red_Diamond_Quest_Runtime";
                instance.transform.SetPositionAndRotation(
                    FindRuntimeDiamondPosition(), Quaternion.identity);
                pickup = instance.GetComponent<RedDiamondPickup>();
                redDiamondTarget = instance.transform;
            }
        }

        if (pickup == null) return;
        if (!pickup.gameObject.activeSelf)
            pickup.gameObject.SetActive(true);
        pickup.EnsureQuestPresentation();
    }

    static Vector3 FindRuntimeDiamondPosition()
    {
        KingGoblinPlagueStatue statue =
            FindAnyObjectByType<KingGoblinPlagueStatue>();
        GameObject spawnObject = GameObject.Find("SpawnPoint") ??
                                 GameObject.Find("PlayerSpawnPoint");
        Vector3 statuePosition = statue != null
            ? statue.transform.position
            : new Vector3(-35f, 0f, -35f);
        Vector3 spawnPosition = spawnObject != null
            ? spawnObject.transform.position
            : Vector3.zero;
        Vector3 outward = Vector3.ProjectOnPlane(
            statuePosition - spawnPosition, Vector3.up).normalized;
        if (outward.sqrMagnitude < .01f) outward = Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, outward);
        Vector3 position = statuePosition + outward * 34f + side * 7f;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            position.y = terrain.SampleHeight(position) +
                         terrain.transform.position.y + .58f;
        return position;
    }

    public void BeginRedDiamondEncounter(Transform interactingPlayer,
        Vector3? diamondWorldPosition = null)
    {
        if (redDiamondEncounterActive) return;
        redDiamondEncounterActive = true;
        player = interactingPlayer;

        TenkokuDayNightCycle cycle = FindAnyObjectByType<TenkokuDayNightCycle>();
        cycle?.SetCurrentHour(0f);
        WindManager.Instance?.SetEncounterWind(1f);

        savedFog = RenderSettings.fog;
        savedFogMode = RenderSettings.fogMode;
        savedFogStart = RenderSettings.fogStartDistance;
        savedFogEnd = RenderSettings.fogEndDistance;
        savedFogDensity = RenderSettings.fogDensity;
        savedFogColor = RenderSettings.fogColor;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 2.25f;
        RenderSettings.fogEndDistance = 10f;
        RenderSettings.fogColor = new Color(.08f, .012f, .018f);

        if (redDiamondCrabBoss == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Enemies/CrabDemonBoss");
            if (prefab != null)
                redDiamondCrabBoss = Instantiate(prefab);
        }

        if (redDiamondCrabBoss == null)
        {
            Debug.LogError("[SixthMissionSequence] Falta Resources/Enemies/CrabDemonBoss.");
            return;
        }

        // The boss belongs to the diamond encounter: its X/Z now come from the visible quest
        // object itself, never from "player + 10 metres", which could land across a slope.
        Vector3 spawn = diamondWorldPosition ??
                        (redDiamondTarget != null
                            ? redDiamondTarget.position
                            : interactingPlayer != null
                                ? interactingPlayer.position
                                : Vector3.zero);
        float supportY = GroundUtility.GetGroundY(spawn,
            redDiamondCrabBoss.transform, 24f, 40f);
        Terrain terrain = Terrain.activeTerrain;
        if (!float.IsNegativeInfinity(supportY))
            spawn.y = supportY;
        else if (terrain != null)
            spawn.y = terrain.SampleHeight(spawn) + terrain.transform.position.y;
        redDiamondCrabBoss.SetActive(true);
        CrabDemonSummonEmergence emergence =
            redDiamondCrabBoss.GetComponent<CrabDemonSummonEmergence>() ??
            redDiamondCrabBoss.AddComponent<CrabDemonSummonEmergence>();
        emergence.Begin(spawn, interactingPlayer);

        redDiamondCrabStats = redDiamondCrabBoss.GetComponent<EnemyStats>();
        if (redDiamondCrabStats != null)
            redDiamondCrabStats.OnDeath += FinishRedDiamondEncounter;
    }

    void FinishRedDiamondEncounter()
    {
        if (redDiamondCrabStats != null)
            redDiamondCrabStats.OnDeath -= FinishRedDiamondEncounter;
        redDiamondCrabStats = null;
        redDiamondEncounterActive = false;
        WindManager.Instance?.SetEncounterWind(0f);
        RenderSettings.fog = savedFog;
        RenderSettings.fogMode = savedFogMode;
        RenderSettings.fogStartDistance = savedFogStart;
        RenderSettings.fogEndDistance = savedFogEnd;
        RenderSettings.fogDensity = savedFogDensity;
        RenderSettings.fogColor = savedFogColor;
        RestoreVillageLife();
    }

    // God-mode mission navigation uses this instead of inflicting artificial damage.
    // It leaves the world in exactly the post-boss state without granting combat drops twice.
    public void CompleteRedDiamondEncounterForMissionJump()
    {
        if (redDiamondCrabStats != null)
            redDiamondCrabStats.OnDeath -= FinishRedDiamondEncounter;
        redDiamondCrabStats = null;

        // Disabling the boss also lets CrabDemonNightEchoController release its night
        // override and remove any surviving blue echoes.
        if (redDiamondCrabBoss != null && redDiamondCrabBoss.activeSelf)
            redDiamondCrabBoss.SetActive(false);

        if (redDiamondEncounterActive)
        {
            WindManager.Instance?.SetEncounterWind(0f);
            RenderSettings.fog = savedFog;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogStartDistance = savedFogStart;
            RenderSettings.fogEndDistance = savedFogEnd;
            RenderSettings.fogDensity = savedFogDensity;
            RenderSettings.fogColor = savedFogColor;
        }

        redDiamondEncounterActive = false;
        if (redDiamondTarget != null)
            redDiamondTarget.gameObject.SetActive(false);
        SetGuidanceVisible(false);
        RestoreVillageLife();
    }

    void BuildAuraIndicator()
    {
        Canvas canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null) return;
        GameObject root = new GameObject("DarkAuraActiveIndicator",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.66f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -18f);
        rect.sizeDelta = new Vector2(58f, 58f);
        Canvas top = root.GetComponent<Canvas>();
        top.overrideSorting = true;
        top.sortingOrder = 840;
        Image frame = root.GetComponent<Image>();
        frame.sprite = Resources.Load<Sprite>("UI/SharpUI/StatusSlot");
        frame.type = frame.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        frame.color = frame.sprite != null
            ? new Color(.36f, .2f, .48f, .96f)
            : new Color(.035f, .012f, .055f, .94f);
        frame.raycastTarget = false;

        GameObject iconObject = new GameObject("DarkEnchantmentSkillIcon",
            typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(root.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(6f, 6f);
        iconRect.offsetMax = new Vector2(-6f, -6f);
        Image icon = iconObject.GetComponent<Image>();
        SkillIconCatalog catalog =
            Resources.Load<SkillIconCatalog>("UI/SkillIconCatalog");
        icon.sprite = catalog != null ? catalog.ForNode("dark_4") : null;
        if (icon.sprite == null)
            icon.sprite = Resources.Load<Sprite>("StatusIcons/DarkAura") ??
                          darkAuraIndicatorSprite;
        icon.preserveAspect = true;
        icon.color = new Color(.9f, .82f, 1f, 1f);
        icon.raycastTarget = false;
        auraIndicator = root.GetComponent<CanvasGroup>();
        auraIndicator.blocksRaycasts = false;
        auraIndicator.interactable = false;
        root.SetActive(false);
    }

    void BuildDarkStains()
    {
        stainMaterial = CreateStainMaterial();
        for (int i = 0; i < 8; i++)
        {
            GameObject stain = GameObject.CreatePrimitive(PrimitiveType.Quad);
            stain.name = "DarkGuidanceStain_" + (i + 1);
            Destroy(stain.GetComponent<Collider>());
            stain.transform.SetParent(transform, true);
            stain.transform.rotation = Quaternion.Euler(90f, i * 37f, 0f);
            float size = Mathf.Lerp(.7f, 1.35f, (i % 4) / 3f);
            stain.transform.localScale = new Vector3(size * 1.45f, size, 1f);
            stain.GetComponent<MeshRenderer>().sharedMaterial = stainMaterial;
            stain.SetActive(false);
            darkStains.Add(stain);
        }
    }

    void BuildNpcGuideStatues()
    {
        if (npcGuideStatues.Count > 0) return;
        TonioQuestGiver tonio =
            FindAnyObjectByType<TonioQuestGiver>(FindObjectsInactive.Include);
        if (tonio == null) return;

        // Force the source NPC to fully evaluate its current animator pose before baking.
        // Otherwise SkinnedMeshRenderer.BakeMesh can capture the bind pose (T-pose) instead
        // of the actual relaxed idle pose, e.g. when the NPC hasn't rendered a frame yet.
        Animator sourceAnimator = tonio.GetComponentInChildren<Animator>();
        if (sourceAnimator != null) sourceAnimator.Update(0f);

        guideStatueMaterial = CreateGuideStatueMaterial();
        npcGuideFootOffsets.Clear();
        for (int i = 0; i < 5; i++)
        {
            GameObject statue = BakeNpcStatue(tonio.transform, i + 1, out float footOffset);
            if (statue == null) continue;
            statue.transform.SetParent(transform, true);
            statue.SetActive(false);
            npcGuideStatues.Add(statue);
            npcGuideFootOffsets.Add(footOffset);
        }
    }

    GameObject BakeNpcStatue(Transform source, int index, out float footOffset)
    {
        GameObject root = new GameObject("DarkNpcGuideStatue_" + index);
        root.transform.SetPositionAndRotation(source.position, source.rotation);
        root.transform.localScale = source.lossyScale;
        int rendererCount = 0;
        Bounds bounds = default;
        bool hasBounds = false;

        foreach (Renderer sourceRenderer in
                 source.GetComponentsInChildren<Renderer>(true))
        {
            if (sourceRenderer is ParticleSystemRenderer ||
                sourceRenderer is TrailRenderer ||
                sourceRenderer is LineRenderer)
                continue;

            Mesh mesh = null;
            if (sourceRenderer is SkinnedMeshRenderer skinned &&
                skinned.sharedMesh != null)
            {
                bool prevUpdateOffscreen = skinned.updateWhenOffscreen;
                skinned.updateWhenOffscreen = true;
                mesh = new Mesh
                {
                    name = "BakedDarkGuide_" + skinned.sharedMesh.name,
                    hideFlags = HideFlags.DontSave
                };
                skinned.BakeMesh(mesh);
                skinned.updateWhenOffscreen = prevUpdateOffscreen;
            }
            else
            {
                MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                if (sourceFilter != null)
                    mesh = sourceFilter.sharedMesh;
            }
            if (mesh == null) continue;

            GameObject part = new GameObject("Ghost_" + sourceRenderer.gameObject.name);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition =
                source.InverseTransformPoint(sourceRenderer.transform.position);
            part.transform.localRotation =
                Quaternion.Inverse(source.rotation) * sourceRenderer.transform.rotation;
            part.transform.localScale = DivideScale(
                sourceRenderer.transform.lossyScale, source.lossyScale);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer ghostRenderer = part.AddComponent<MeshRenderer>();
            ghostRenderer.sharedMaterial = guideStatueMaterial;
            ghostRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            ghostRenderer.receiveShadows = false;
            rendererCount++;

            if (!hasBounds) { bounds = ghostRenderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(ghostRenderer.bounds);
        }

        // Ground statues by their actual baked geometry rather than assuming the source
        // transform's pivot sits exactly at the feet — keeps them from sinking into or
        // floating above uneven terrain regardless of the NPC rig's pivot placement.
        footOffset = hasBounds ? root.transform.position.y - bounds.min.y : 0f;

        if (rendererCount > 0) return root;
        Destroy(root);
        return null;
    }

    static Vector3 DivideScale(Vector3 value, Vector3 divisor)
    {
        return new Vector3(
            Mathf.Abs(divisor.x) > .0001f ? value.x / divisor.x : value.x,
            Mathf.Abs(divisor.y) > .0001f ? value.y / divisor.y : value.y,
            Mathf.Abs(divisor.z) > .0001f ? value.z / divisor.z : value.z);
    }

    void SetGuidanceVisible(bool visible)
    {
        if (visible && npcGuideStatues.Count == 0)
            BuildNpcGuideStatues();
        if (auraIndicator != null)
        {
            if (auraIndicator.gameObject.activeSelf != visible)
                auraIndicator.gameObject.SetActive(visible);
            if (visible)
                auraIndicator.alpha = .84f +
                    (Mathf.Sin(Time.unscaledTime * 3f) + 1f) * .08f;
        }
        foreach (GameObject stain in darkStains)
            if (stain != null && stain.activeSelf != visible) stain.SetActive(visible);
        foreach (GameObject statue in npcGuideStatues)
            if (statue != null && statue.activeSelf != visible) statue.SetActive(visible);
    }

    void UpdateDarkStainPath()
    {
        if (redDiamondTarget == null) return;
        if (player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null) player = found.transform;
        }
        if (player == null) return;

        Vector3 delta = redDiamondTarget.position - player.position;
        float distance = delta.magnitude;
        if (distance < .1f) return;
        Vector3 direction = delta / distance;
        float spacing = Mathf.Clamp(distance / (darkStains.Count + 1), 2.5f, 7f);
        Terrain terrain = Terrain.activeTerrain;
        for (int i = 0; i < darkStains.Count; i++)
        {
            GameObject stain = darkStains[i];
            float along = Mathf.Min(distance - .8f, spacing * (i + 1));
            Vector3 position = player.position + direction * along;
            Vector3 lateral = Vector3.Cross(Vector3.up, direction) *
                Mathf.Sin((i + 1) * 2.17f) * .75f;
            position += lateral;
            if (terrain != null)
                position.y = terrain.SampleHeight(position) +
                    terrain.transform.position.y + .035f;
            else if (Physics.Raycast(position + Vector3.up * 30f, Vector3.down,
                         out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y + .035f;
            stain.transform.position = position;
        }

        if (npcGuideStatues.Count == 0) return;
        float statueSpacing = Mathf.Clamp(
            distance / (npcGuideStatues.Count + 1), 4.5f, 12f);
        Vector3 flatDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        Quaternion facing = flatDirection.sqrMagnitude > .001f
            ? Quaternion.LookRotation(flatDirection.normalized, Vector3.up)
            : Quaternion.identity;
        for (int i = 0; i < npcGuideStatues.Count; i++)
        {
            GameObject statue = npcGuideStatues[i];
            float along = Mathf.Min(distance - 1.2f, statueSpacing * (i + 1));
            Vector3 position = player.position + direction * along;
            float footOffset = i < npcGuideFootOffsets.Count ? npcGuideFootOffsets[i] : 0f;
            if (terrain != null)
                position.y = terrain.SampleHeight(position) +
                    terrain.transform.position.y + footOffset + .025f;
            else if (Physics.Raycast(position + Vector3.up * 30f, Vector3.down,
                         out RaycastHit statueHit, 80f, ~0,
                         QueryTriggerInteraction.Ignore))
                position.y = statueHit.point.y + footOffset + .025f;
            statue.transform.SetPositionAndRotation(position, facing);
        }
    }

    static Material CreateStainMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        Material material = new Material(shader)
        {
            name = "Runtime_DarkGuidanceStain",
            hideFlags = HideFlags.DontSave,
            renderQueue = 3000
        };
        Color color = new Color(.055f, .005f, .085f, .68f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    static Material CreateGuideStatueMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        Material material = new Material(shader)
        {
            name = "Runtime_SemitransparentNpcGuide",
            hideFlags = HideFlags.DontSave,
            renderQueue = 3000
        };
        Color color = new Color(.24f, .045f, .48f, .32f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    static void BuildCinematicOverlay(out GameObject root, out Image fade,
        out TextMeshProUGUI subtitle)
    {
        root = null;
        fade = null;
        subtitle = null;
        Canvas canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null) return;

        root = new GameObject("TonioStoneCinematicOverlay",
            typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
        Canvas top = root.GetComponent<Canvas>();
        top.overrideSorting = true;
        top.sortingOrder = 2500;

        GameObject fadeObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeObject.transform.SetParent(root.transform, false);
        fade = fadeObject.GetComponent<Image>();
        fade.color = Color.clear;
        fade.raycastTarget = false;
        fade.rectTransform.anchorMin = Vector2.zero;
        fade.rectTransform.anchorMax = Vector2.one;
        fade.rectTransform.offsetMin = fade.rectTransform.offsetMax = Vector2.zero;

        GameObject box = new GameObject("SubtitleBox", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(root.transform, false);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(.15f, .045f);
        boxRect.anchorMax = new Vector2(.85f, .18f);
        boxRect.offsetMin = boxRect.offsetMax = Vector2.zero;
        Image frame = box.GetComponent<Image>();
        Sprite frameSprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        if (frameSprite != null)
        {
            frame.sprite = frameSprite;
            frame.type = Image.Type.Sliced;
        }
        frame.color = new Color(.38f, .3f, .45f, .94f);

        GameObject textObject = new GameObject("Subtitle",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(box.transform, false);
        subtitle = textObject.GetComponent<TextMeshProUGUI>();
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.fontSize = 23f;
        subtitle.enableAutoSizing = true;
        subtitle.fontSizeMin = 15f;
        subtitle.fontSizeMax = 23f;
        subtitle.color = new Color(.9f, .82f, .97f);
        subtitle.outlineColor = Color.black;
        subtitle.outlineWidth = .18f;
        subtitle.rectTransform.anchorMin = Vector2.zero;
        subtitle.rectTransform.anchorMax = Vector2.one;
        subtitle.rectTransform.offsetMin = new Vector2(42f, 14f);
        subtitle.rectTransform.offsetMax = new Vector2(-42f, -14f);
    }

    static IEnumerator Fade(Image image, float from, float to, float duration)
    {
        if (image == null) yield break;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            Color color = image.color;
            color.a = Mathf.Lerp(from, to, elapsed / duration);
            image.color = color;
            yield return null;
        }
        Color final = image.color;
        final.a = to;
        image.color = final;
    }

    static Bounds GetBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers.Length > 0
            ? renderers[0].bounds
            : new Bounds(root.position, Vector3.one * 2f);
        for (int i = 1; i < renderers.Length; i++)
            if (!(renderers[i] is ParticleSystemRenderer))
                bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    void OnDestroy()
    {
        if (redDiamondCrabStats != null)
            redDiamondCrabStats.OnDeath -= FinishRedDiamondEncounter;
        if (Instance == this) Instance = null;
        if (villagePetrified) RestoreVillageLife();
    }
}

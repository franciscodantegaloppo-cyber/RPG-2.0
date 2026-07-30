using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
    Vector3 nahueExitDirection = Vector3.forward;

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
        if (QuestManager.Instance == null) return;
        PrimaryQuestState state =
            QuestManager.Instance.MerchantIntroductionState;
        if (state == PrimaryQuestState.SeventhQuestCompleted)
        {
            MissionSevenAnomalyPathController
                .EnsureCityConstructionBlockade();
            return;
        }
        if (state == PrimaryQuestState.SeventhHuntDemonAnomaly)
        {
            PlayerController controller =
                FindAnyObjectByType<PlayerController>();
            MissionSevenAnomalyPathController.Begin(
                controller != null ? controller.transform : null);
            return;
        }
        if (state >= PrimaryQuestState.SeventhFindTonio &&
            state <= PrimaryQuestState.SeventhWindAftermath)
            BeginTonioSearch();
    }

    public void BeginTonioSearch()
    {
        // Mission six petrifies and disables every village character. Mission seven needs
        // Nahue alive and animated before Tonio disappears.
        SixthMissionSequence.Instance?.RestoreVillageLife();
        FindCharacters();
        if (tonio == null || nahue == null) return;
        Vector3 tonioPosition = tonio.transform.position;
        tonio.gameObject.SetActive(false);
        EnsureNahueVisible(nahue);
        // Keep the end of the trail aligned with the character who must be confronted.
        nahue.GetComponent<NPCWander>()?.PauseForInteraction();
        NahueCinematicProximityTrigger proximity =
            nahue.GetComponent<NahueCinematicProximityTrigger>();
        if (proximity == null)
            proximity = nahue.gameObject
                .AddComponent<NahueCinematicProximityTrigger>();
        proximity.Configure(this, nahue);
        BuildBloodTrail(tonioPosition, nahue.transform.position);
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
        if (flatDelta.sqrMagnitude > .01f)
            nahueExitDirection = flatDelta.normalized;
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
        if (target == null)
        {
            FindCharacters();
            target = nahue;
        }
        if (transformationRunning || target == null) return;
        EnsureNahueVisible(target);
        transformationRunning = true;
        StartCoroutine(PlayNahueTransformation(target));
    }

    static void EnsureNahueVisible(NahueQuestGiver target)
    {
        if (target == null) return;
        target.gameObject.SetActive(true);
        target.enabled = true;
        GanzPlayerVisual modular =
            target.GetComponent<GanzPlayerVisual>();
        bool hasModularVisual =
            modular != null && modular.EnsureInitialized();
        if (hasModularVisual && modular.VisualAnimator != null)
            modular.VisualAnimator.gameObject.SetActive(true);
        else
            foreach (Renderer renderer in
                     target.GetComponentsInChildren<Renderer>(true))
                if (renderer != null)
                    renderer.enabled = true;
        foreach (Animator animator in
                 target.GetComponentsInChildren<Animator>(true))
            if (animator != null)
            {
                animator.enabled = true;
                animator.speed = 1f;
                animator.Rebind();
                animator.Update(0f);
            }
    }

    static GameObject CreateNahueCinematicActor(
        NahueQuestGiver source)
    {
        if (source == null) return null;

        // Clone the complete visual hierarchy before hiding the mission NPC. The
        // clone has no gameplay scripts or collision, so petrification, wandering,
        // combat and quest state can no longer make the actor disappear.
        Transform visualSource = null;
        foreach (Animator candidate in
                 source.GetComponentsInChildren<Animator>(true))
            if (candidate != null &&
                candidate.name.IndexOf("Ganz_Player_Visual",
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                visualSource = candidate.transform;
                break;
            }

        GameObject actor = Instantiate(
            visualSource != null
                ? visualSource.gameObject
                : source.gameObject);
        actor.name = "Nahue_CinematicActor";
        actor.transform.SetParent(null, true);
        Transform placement = visualSource != null
            ? visualSource
            : source.transform;
        actor.transform.SetPositionAndRotation(placement.position,
            placement.rotation);
        actor.transform.localScale = placement.lossyScale;

        foreach (MonoBehaviour behaviour in
                 actor.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (Collider collider in
                 actor.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Rigidbody body in
                 actor.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        Renderer[] actorRenderers =
            actor.GetComponentsInChildren<Renderer>(true);
        bool hasVisibleRenderer = false;
        foreach (Renderer renderer in actorRenderers)
        {
            renderer.forceRenderingOff = false;
            hasVisibleRenderer |= renderer.enabled &&
                                  renderer.gameObject.activeInHierarchy;
        }
        // Non-modular legacy Nahue models occasionally arrive with their only
        // renderer disabled. Modular models preserve their selected pieces so
        // unused armor variants are not all stacked on top of one another.
        if (!hasVisibleRenderer && visualSource == null)
            foreach (Renderer renderer in actorRenderers)
                renderer.enabled = true;
        foreach (Animator animator in
                 actor.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = true;
            animator.speed = 1f;
            if (animator.runtimeAnimatorController != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        foreach (Renderer renderer in
                 source.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
        return actor;
    }

    IEnumerator PlayNahueTransformation(NahueQuestGiver target)
    {
        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null) playerController.enabled = false;
        NPCWander wander = target.GetComponent<NPCWander>();
        if (wander != null) wander.enabled = false;
        GameObject cinematicActor = CreateNahueCinematicActor(target);
        Transform actor = cinematicActor != null
            ? cinematicActor.transform
            : target.transform;

        Camera main = Camera.main;
        Camera cinematic = null;
        if (main != null)
        {
            GameObject cameraGo = new GameObject("NahueTransformationCamera");
            cinematic = cameraGo.AddComponent<Camera>();
            cinematic.CopyFrom(main);
            cinematic.enabled = true;
            cinematic.depth = main.depth + 20f;
            cameraGo.transform.position = actor.position +
                actor.forward * 4.5f + Vector3.up * 1.8f;
            cameraGo.transform.LookAt(actor.position + Vector3.up);
            main.enabled = false;
        }

        Animator[] nahueAnimators =
            actor.GetComponentsInChildren<Animator>(true);
        Animator animator = nahueAnimators.Length > 0
            ? nahueAnimators[0]
            : null;
        Renderer[] nahueRenderers = actor.GetComponentsInChildren<Renderer>(true);
        Vector3 originalScale = actor.localScale;
        Vector3 exitDirection = nahueExitDirection;
        if (exitDirection.sqrMagnitude < .01f && playerController != null)
        {
            exitDirection = actor.position -
                            playerController.transform.position;
            exitDirection.y = 0f;
        }
        exitDirection = exitDirection.sqrMagnitude > .01f
            ? exitDirection.normalized
            : actor.forward;
        actor.rotation = Quaternion.LookRotation(exitDirection);

        // Nahue first walks away from the village while the camera travels beside
        // him. Movement is authored here so wandering/navigation cannot interrupt
        // the cinematic path.
        const float walkDuration = 4.2f;
        Vector3 walkStart = actor.position;
        Vector3 walkEnd = SnapCharacterToGround(
            walkStart + exitDirection * 4.6f, actor);
        for (float elapsed = 0f; elapsed < walkDuration;
             elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / walkDuration));
            Vector3 wanted = Vector3.Lerp(walkStart, walkEnd, t);
            wanted = SnapCharacterToGround(wanted, actor);
            actor.position = wanted;
            SetCinematicMovement(nahueAnimators, true, .75f);
            UpdateNahueCamera(cinematic, actor, exitDirection,
                4.8f, 1.65f);
            yield return null;
        }
        SetCinematicMovement(nahueAnimators, false, 0f);

        // Keep the existing story beat, now inside the cinematic instead of
        // requiring a second approach after the blood trail.
        bool dialogueFinished = false;
        MerchantDialoguePanel panel = MerchantDialoguePanel.EnsureRuntime();
        if (panel != null)
        {
            panel.Show("Nahue",
                "Creo que estoy alucinando... Vi un monstruo gigante que robó el alma de Tonio. Desde entonces siento que algo dentro de mí intenta cambiar.",
                "Continuar", () =>
                {
                    panel.Hide();
                    QuestManager.Instance?.BeginSeventhWindAftermath();
                    dialogueFinished = true;
                });
            while (!dialogueFinished)
            {
                UpdateNahueCamera(cinematic, actor, exitDirection,
                    4.15f, 1.72f);
                yield return null;
            }
        }
        else
        {
            QuestManager.Instance?.BeginSeventhWindAftermath();
            dialogueFinished = true;
        }

        GameObject goblinVisual = CreateGoblinVisual(actor);
        GoblinBodyReveal bodyReveal = goblinVisual != null
            ? new GoblinBodyReveal(goblinVisual)
            : null;
        NahueHandsOnHeadPose panicPose =
            actor.gameObject.AddComponent<NahueHandsOnHeadPose>();
        panicPose.Configure(nahueAnimators);

        const float panicLeadIn = 3.2f;
        for (float elapsed = 0f; elapsed < panicLeadIn;
             elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / panicLeadIn));
            panicPose.Weight = t;
            panicPose.Shake = Mathf.Sin(elapsed * Mathf.Lerp(3f, 8f, t)) *
                              t;
            TintNahue(nahueRenderers, t * .35f);
            UpdateNahueCamera(cinematic, actor, exitDirection,
                Mathf.Lerp(4.1f, 3.35f, t), 1.72f);
            yield return null;
        }

        // Torso and arms emerge first, then legs, and finally the head.
        const float transformationDuration = 7.5f;
        for (float elapsed = 0f; elapsed < transformationDuration;
             elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / transformationDuration);
            float panic = Mathf.Sin(elapsed * 11f) *
                          Mathf.SmoothStep(0f, 1f, t);
            panicPose.Weight = 1f;
            panicPose.Shake = panic;
            actor.localScale = Vector3.Lerp(originalScale,
                new Vector3(originalScale.x * .78f,
                    originalScale.y * .84f,
                    originalScale.z * 1.08f), t);
            TintNahue(nahueRenderers, t);
            bodyReveal?.SetProgress(t);
            UpdateNahueCamera(cinematic, actor, exitDirection,
                Mathf.Lerp(3.35f, 2.9f, t), 1.7f);
            yield return null;
        }

        panicPose.Weight = 0f;
        Destroy(panicPose);
        foreach (Renderer renderer in nahueRenderers)
            if (renderer != null) renderer.enabled = false;
        bodyReveal?.Complete();

        Animator goblinAnimator = goblinVisual != null
            ? goblinVisual.GetComponentInChildren<Animator>(true)
            : null;
        TriggerGoblinDeath(goblinAnimator);
        Quaternion goblinStartRotation = goblinVisual != null
            ? goblinVisual.transform.rotation
            : Quaternion.identity;
        Quaternion goblinFallRotation = goblinStartRotation *
            Quaternion.Euler(78f, 0f, 7f);
        const float deathDuration = 2.2f;
        for (float elapsed = 0f; elapsed < deathDuration;
             elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / deathDuration));
            // Animator death is preferred; the gradual fall guarantees a readable
            // death even if the chosen goblin controller has no Death trigger.
            if (goblinVisual != null)
                goblinVisual.transform.rotation =
                    Quaternion.Slerp(goblinStartRotation,
                        goblinFallRotation, t);
            UpdateNahueCamera(cinematic,
                goblinVisual != null ? goblinVisual.transform : actor,
                exitDirection, Mathf.Lerp(3.1f, 3.8f, t),
                Mathf.Lerp(1.55f, .9f, t));
            yield return null;
        }

        if (animator != null) animator.speed = 0f;
        foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        target.enabled = false;
        if (cinematicActor != null)
            Destroy(cinematicActor);
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
            // Mission seven uses a shorter, authored catastrophe. The general /rafaga
            // command keeps its normal duration, but this story gust ends after one minute.
            wind.StartExtremeGust(10f, 60f);
            while (!windFinished) yield return null;
            wind.ExtremeGustEnded -= handler;
        }

        yield return StartCoroutine(BlackoutAndWake(playerController));
        QuestManager.Instance?.BeginSeventhAnomalyHunt();
        MissionSevenAnomalyPathController.Begin(
            playerController != null ? playerController.transform : null);
        transformationRunning = false;
    }

    static void SetCinematicMovement(Animator[] animators, bool moving,
        float speed)
    {
        foreach (Animator candidate in animators)
        {
            if (candidate == null || candidate.runtimeAnimatorController == null)
                continue;
            SetAnimatorBool(candidate, "Moving", moving);
            SetAnimatorBool(candidate, "IsMoving", moving);
            SetAnimatorFloat(candidate, "Speed", speed);
            SetAnimatorFloat(candidate, "MoveSpeed", speed);
            SetAnimatorFloat(candidate, "Velocity Z", moving ? 1f : 0f);
            int state = Animator.StringToHash(moving ? "Walk" : "Idle");
            int fullState = Animator.StringToHash(
                moving ? "Base Layer.Walk" : "Base Layer.Idle");
            AnimatorStateInfo current = candidate.GetCurrentAnimatorStateInfo(0);
            if (!current.IsName(moving ? "Walk" : "Idle") &&
                !current.IsName(moving
                    ? "Base Layer.Walk"
                    : "Base Layer.Idle"))
            {
                if (candidate.HasState(0, state))
                    candidate.CrossFade(state, .18f);
                else if (candidate.HasState(0, fullState))
                    candidate.CrossFade(fullState, .18f);
            }
            candidate.speed = moving ? 1.05f : 1f;
        }
    }

    static Vector3 SnapCharacterToGround(Vector3 point,
        Transform ignoredCharacter)
    {
        RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 3f,
            Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        Vector3 best = point;
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == null ||
                (ignoredCharacter != null &&
                 hit.transform.IsChildOf(ignoredCharacter)))
                continue;
            if (hit.distance >= nearest) continue;
            nearest = hit.distance;
            best = hit.point;
        }
        if (!float.IsPositiveInfinity(nearest))
            return best;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            point.y = terrain.SampleHeight(point) +
                      terrain.transform.position.y;
        return point;
    }

    static void SetAnimatorBool(Animator animator, string name, bool value)
    {
        if (HasAnimatorParameter(animator, name,
                AnimatorControllerParameterType.Bool))
            animator.SetBool(name, value);
    }

    static void SetAnimatorFloat(Animator animator, string name, float value)
    {
        if (HasAnimatorParameter(animator, name,
                AnimatorControllerParameterType.Float))
            animator.SetFloat(name, value);
    }

    static void UpdateNahueCamera(Camera camera, Transform focus,
        Vector3 travelDirection, float distance, float height)
    {
        if (camera == null || focus == null) return;
        Vector3 side = Vector3.Cross(Vector3.up, travelDirection).normalized;
        Vector3 desired = focus.position - travelDirection * distance +
                          side * 1.65f + Vector3.up * height;
        float follow = 1f - Mathf.Exp(-3.7f * Time.deltaTime);
        camera.transform.position = Vector3.Lerp(
            camera.transform.position, desired, follow);
        Vector3 lookAt = focus.position + Vector3.up *
                         Mathf.Max(.65f, height * .68f);
        Quaternion rotation = Quaternion.LookRotation(
            lookAt - camera.transform.position, Vector3.up);
        camera.transform.rotation = Quaternion.Slerp(
            camera.transform.rotation, rotation, follow);
    }

    static void TriggerGoblinDeath(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;
        if (HasAnimatorParameter(animator, "Death",
                AnimatorControllerParameterType.Trigger))
            animator.SetTrigger("Death");
        else if (HasAnimatorParameter(animator, "Die",
                     AnimatorControllerParameterType.Trigger))
            animator.SetTrigger("Die");
        SetAnimatorBool(animator, "Moving", false);
        SetAnimatorBool(animator, "IsMoving", false);
        SetAnimatorFloat(animator, "Speed", 0f);
    }

    static bool HasAnimatorParameter(Animator animator, string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;
        int hash = Animator.StringToHash(parameterName);
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == hash && parameter.type == parameterType)
                return true;
        return false;
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

            // This is only a cinematic visual. Removing EnemyStats directly is invalid because
            // separation and health-bar components require it, so keep the copied component
            // graph intact and disable all gameplay behaviour instead.
            visual.SetActive(false);
            foreach (MonoBehaviour behaviour in
                     visual.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            foreach (Canvas canvas in visual.GetComponentsInChildren<Canvas>(true))
                canvas.enabled = false;
            foreach (ParticleSystem particles in
                     visual.GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (Renderer renderer in
                     visual.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            visual.SetActive(true);
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
        TenkokuDayNightCycle cycle = FindAnyObjectByType<TenkokuDayNightCycle>();
        if (cycle != null)
            cycle.SetCurrentHour(0f);
        MissionSevenWakeDarkness darkness =
            GetComponent<MissionSevenWakeDarkness>();
        if (darkness == null)
            darkness = gameObject.AddComponent<MissionSevenWakeDarkness>();
        darkness.Begin(cycle);

        if (oldMan != null)
            EnsureTentTorch(oldMan.transform);

        Transform bedroll = FindClosestBedroll(
            oldMan != null ? oldMan.transform.position :
            controller != null ? controller.transform.position : Vector3.zero);
        Bounds bedBounds = bedroll != null
            ? CalculateBounds(bedroll)
            : new Bounds(oldMan != null
                ? oldMan.transform.position : Vector3.zero,
                new Vector3(1.5f, .2f, 2.2f));

        if (controller != null)
        {
            CharacterController cc = controller.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            Vector3 bedForward = bedroll != null
                ? Vector3.ProjectOnPlane(bedroll.forward, Vector3.up)
                : oldMan != null
                    ? Vector3.ProjectOnPlane(oldMan.transform.forward,
                        Vector3.up)
                    : Vector3.forward;
            if (bedForward.sqrMagnitude < .01f)
                bedForward = Vector3.forward;
            bedForward.Normalize();
            controller.transform.position = bedBounds.center -
                bedForward * Mathf.Min(.55f, bedBounds.extents.z * .45f) +
                Vector3.up * .08f;
            controller.transform.rotation =
                Quaternion.LookRotation(bedForward, Vector3.up);
        }
        // Stay in the downed pose until the player explicitly wakes up with L.
        bridge?.TriggerKnockdown(false);
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime * .55f)
        {
            blackout.alpha = t;
            yield return null;
        }
        blackout.gameObject.SetActive(false);

        GameObject wakePrompt = BuildWakePrompt();
        Keyboard keyboard = Keyboard.current;
        while (keyboard == null || !keyboard.lKey.wasPressedThisFrame)
        {
            keyboard = Keyboard.current;
            yield return null;
        }
        if (wakePrompt != null) Destroy(wakePrompt);

        bridge?.TriggerGetup();
        yield return new WaitForSecondsRealtime(2.15f);

        if (controller != null)
        {
            CharacterController cc =
                controller.GetComponent<CharacterController>();
            Vector3 standPosition = bedroll != null
                ? bedBounds.center + bedroll.right *
                  (Mathf.Max(.65f, bedBounds.extents.x) + .7f)
                : oldMan != null
                    ? oldMan.transform.position +
                      oldMan.transform.right * 1.15f
                    : controller.transform.position;
            standPosition = SnapCharacterToGround(
                standPosition, controller.transform) + Vector3.up * .08f;
            controller.transform.position = standPosition;
            if (oldMan != null)
            {
                Vector3 faceOldMan = oldMan.transform.position -
                                     controller.transform.position;
                faceOldMan.y = 0f;
                if (faceOldMan.sqrMagnitude > .01f)
                    controller.transform.rotation =
                        Quaternion.LookRotation(faceOldMan);
            }
            if (cc != null) cc.enabled = true;
            controller.enabled = true;
        }

        bool storyFinished = oldMan == null;
        if (oldMan != null)
            oldMan.BeginMissionSevenStory(
                controller != null ? controller.transform : null,
                () => storyFinished = true);
        while (!storyFinished)
            yield return null;

        if (controller != null)
        {
            MissionSevenPlayerDarkAura.Ensure(controller.transform);
            QuestOverheadThought.Show(controller.transform,
                "Debo terminar con esta oscuridad, atravesar\u00e9 el sendero...",
                7.5f);
        }
    }

    static Transform FindClosestBedroll(Vector3 near)
    {
        Transform closest = null;
        float closestSqr = float.PositiveInfinity;
        foreach (Transform candidate in FindObjectsByType<Transform>(
                     FindObjectsInactive.Include))
        {
            if (candidate == null ||
                candidate.name.IndexOf("Bedroll_Type1_Color1",
                    System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            float sqr = (candidate.position - near).sqrMagnitude;
            if (sqr >= closestSqr) continue;
            closest = candidate;
            closestSqr = sqr;
        }
        return closest;
    }

    static Bounds CalculateBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.position, new Vector3(1.5f, .2f, 2.2f));
        Bounds result = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            if (!(renderers[i] is ParticleSystemRenderer))
                result.Encapsulate(renderers[i].bounds);
        return result;
    }

    static GameObject BuildWakePrompt()
    {
        GameObject root = new GameObject("Mission7_WakePrompt",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(CanvasGroup));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2600;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("SharpUI_WakePanel",
            typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .18f);
        panelRect.pivot = new Vector2(.5f, .5f);
        panelRect.sizeDelta = new Vector2(310f, 58f);
        Image background = panel.GetComponent<Image>();
        background.sprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        background.type = background.sprite != null
            ? Image.Type.Sliced : Image.Type.Simple;
        background.color = background.sprite != null
            ? Color.white : new Color(.055f, .025f, .025f, .94f);

        GameObject textObject = new GameObject("WakeText",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(12f, 7f);
        text.rectTransform.offsetMax = new Vector2(-12f, -7f);
        text.text = "<color=#EAB264>[ L ]</color>  LEVANTARSE";
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = 21f;
        text.color = new Color(.94f, .88f, .76f);
        text.raycastTarget = false;
        return root;
    }

    static void EnsureTentTorch(Transform oldMan)
    {
        GameObject existing = GameObject.Find("Mission7_TentTorch");
        if (existing != null)
        {
            existing.SetActive(true);
            return;
        }

        GameObject torch = new GameObject("Mission7_TentTorch");
        torch.transform.position = oldMan.position -
            oldMan.right * .78f + oldMan.forward * .28f;

        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = "Torch_Post";
        post.transform.SetParent(torch.transform, false);
        post.transform.localPosition = Vector3.up * .58f;
        post.transform.localScale = new Vector3(.055f, .58f, .055f);
        Collider postCollider = post.GetComponent<Collider>();
        if (postCollider != null) Destroy(postCollider);
        Renderer postRenderer = post.GetComponent<Renderer>();
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ??
                     Shader.Find("Standard");
        if (postRenderer != null && lit != null)
        {
            Material wood = new Material(lit)
            {
                name = "Mission7_TorchWood",
                color = new Color(.13f, .055f, .018f)
            };
            if (wood.HasProperty("_BaseColor"))
                wood.SetColor("_BaseColor", wood.color);
            postRenderer.sharedMaterial = wood;
        }

        GameObject flame = new GameObject("Torch_Flame");
        flame.transform.SetParent(torch.transform, false);
        flame.transform.localPosition = Vector3.up * 1.22f;
        ParticleSystem particles = flame.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.28f, .62f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.18f, .5f);
        main.startSize = new ParticleSystem.MinMaxCurve(.07f, .17f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, .18f, .015f, .72f),
            new Color(1f, .72f, .12f, .9f));
        main.maxParticles = 45;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 24f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = .045f;
        shape.angle = 10f;
        ParticleSystem.ColorOverLifetimeModule colour =
            particles.colorOverLifetime;
        colour.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, .2f, .015f), 0f),
                new GradientColorKey(new Color(1f, .8f, .18f), .45f),
                new GradientColorKey(new Color(.35f, .02f, .005f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(.9f, .18f),
                new GradientAlphaKey(0f, 1f)
            });
        colour.color = fade;
        ParticleSystemRenderer particleRenderer =
            flame.GetComponent<ParticleSystemRenderer>();
        Shader particleShader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Particles/Standard Unlit") ??
            Shader.Find("Sprites/Default");
        if (particleShader != null)
        {
            Material fire = new Material(particleShader)
            {
                name = "Mission7_TorchFire",
                color = Color.white,
                renderQueue = 3000
            };
            if (fire.HasProperty("_BaseColor"))
                fire.SetColor("_BaseColor", Color.white);
            particleRenderer.sharedMaterial = fire;
        }

        Light warmLight = flame.AddComponent<Light>();
        warmLight.type = LightType.Point;
        warmLight.color = new Color(1f, .34f, .08f);
        warmLight.range = 7.5f;
        warmLight.intensity = 3.4f;
        warmLight.shadows = LightShadows.None;
        flame.AddComponent<PlaceableFireVFX>();
        particles.Play();
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

// Drives the staged body replacement without requiring a bespoke mesh for the
// cinematic. Humanoid limb bones begin collapsed and recover in the requested
// order: arms, legs, then head.
sealed class GoblinBodyReveal
{
    readonly Renderer[] renderers;
    readonly List<BoneScale> arms = new List<BoneScale>();
    readonly List<BoneScale> legs = new List<BoneScale>();
    readonly List<BoneScale> head = new List<BoneScale>();
    bool visible;

    public GoblinBodyReveal(GameObject root)
    {
        renderers = root.GetComponentsInChildren<Renderer>(true);
        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator == null || !animator.isHuman)
            return;

        Add(animator, arms, HumanBodyBones.LeftUpperArm);
        Add(animator, arms, HumanBodyBones.LeftLowerArm);
        Add(animator, arms, HumanBodyBones.LeftHand);
        Add(animator, arms, HumanBodyBones.RightUpperArm);
        Add(animator, arms, HumanBodyBones.RightLowerArm);
        Add(animator, arms, HumanBodyBones.RightHand);
        Add(animator, legs, HumanBodyBones.LeftUpperLeg);
        Add(animator, legs, HumanBodyBones.LeftLowerLeg);
        Add(animator, legs, HumanBodyBones.LeftFoot);
        Add(animator, legs, HumanBodyBones.RightUpperLeg);
        Add(animator, legs, HumanBodyBones.RightLowerLeg);
        Add(animator, legs, HumanBodyBones.RightFoot);
        Add(animator, head, HumanBodyBones.Neck);
        Add(animator, head, HumanBodyBones.Head);
        Apply(arms, .001f);
        Apply(legs, .001f);
        Apply(head, .001f);
    }

    public void SetProgress(float progress)
    {
        if (!visible)
        {
            foreach (Renderer renderer in renderers)
                if (renderer != null) renderer.enabled = true;
            visible = true;
        }

        Apply(arms, Mathf.SmoothStep(.001f, 1f,
            Mathf.InverseLerp(0f, .34f, progress)));
        Apply(legs, Mathf.SmoothStep(.001f, 1f,
            Mathf.InverseLerp(.32f, .69f, progress)));
        Apply(head, Mathf.SmoothStep(.001f, 1f,
            Mathf.InverseLerp(.67f, 1f, progress)));
    }

    public void Complete()
    {
        foreach (Renderer renderer in renderers)
            if (renderer != null) renderer.enabled = true;
        Apply(arms, 1f);
        Apply(legs, 1f);
        Apply(head, 1f);
    }

    static void Add(Animator animator, List<BoneScale> collection,
        HumanBodyBones bone)
    {
        Transform found = animator.GetBoneTransform(bone);
        if (found != null)
            collection.Add(new BoneScale(found, found.localScale));
    }

    static void Apply(List<BoneScale> bones, float amount)
    {
        foreach (BoneScale bone in bones)
            if (bone.transform != null)
                bone.transform.localScale = bone.original * amount;
    }

    readonly struct BoneScale
    {
        public readonly Transform transform;
        public readonly Vector3 original;

        public BoneScale(Transform transform, Vector3 original)
        {
            this.transform = transform;
            this.original = original;
        }
    }
}

// Procedural upper-body pose used only by Nahue's cinematic. It works after the
// currently assigned animation, placing both hands against the temples and adding
// an increasingly desperate head shake.
sealed class NahueHandsOnHeadPose : MonoBehaviour
{
    readonly List<Rig> rigs = new List<Rig>();
    public float Weight { get; set; }
    public float Shake { get; set; }

    public void Configure(Animator[] animators)
    {
        rigs.Clear();
        foreach (Animator animator in animators)
        {
            if (animator == null || !animator.isHuman) continue;
            Rig rig = new Rig(animator);
            if (rig.IsValid) rigs.Add(rig);
        }
    }

    void LateUpdate()
    {
        float weight = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(Weight));
        if (weight <= .001f) return;

        foreach (Rig rig in rigs)
        {
            if (!rig.IsValid) continue;
            Vector3 right = rig.head.right;
            Vector3 up = rig.head.up;
            Vector3 forward = rig.head.forward;
            Vector3 leftTemple = rig.head.position - right * .13f +
                                 up * .015f + forward * .055f;
            Vector3 rightTemple = rig.head.position + right * .13f +
                                  up * .015f + forward * .055f;
            AimLimb(rig.leftUpperArm, rig.leftLowerArm, leftTemple, weight);
            AimLimb(rig.leftLowerArm, rig.leftHand, leftTemple, weight);
            AimLimb(rig.rightUpperArm, rig.rightLowerArm, rightTemple, weight);
            AimLimb(rig.rightLowerArm, rig.rightHand, rightTemple, weight);

            Quaternion leftPalm = Quaternion.LookRotation(
                -right, up) * Quaternion.Euler(0f, 0f, -20f);
            Quaternion rightPalm = Quaternion.LookRotation(
                right, up) * Quaternion.Euler(0f, 0f, 20f);
            rig.leftHand.rotation = Quaternion.Slerp(
                rig.leftHand.rotation, leftPalm, weight);
            rig.rightHand.rotation = Quaternion.Slerp(
                rig.rightHand.rotation, rightPalm, weight);
            rig.head.rotation *= Quaternion.Euler(
                Shake * 4.8f * weight,
                Shake * 10.5f * weight,
                -Shake * 3.2f * weight);
        }
    }

    static void AimLimb(Transform from, Transform child,
        Vector3 target, float weight)
    {
        Vector3 current = child.position - from.position;
        Vector3 desired = target - from.position;
        if (current.sqrMagnitude < .0001f ||
            desired.sqrMagnitude < .0001f) return;
        Quaternion solved = Quaternion.FromToRotation(current, desired) *
                            from.rotation;
        from.rotation = Quaternion.Slerp(from.rotation, solved, weight);
    }

    readonly struct Rig
    {
        public readonly Transform head;
        public readonly Transform leftUpperArm;
        public readonly Transform leftLowerArm;
        public readonly Transform leftHand;
        public readonly Transform rightUpperArm;
        public readonly Transform rightLowerArm;
        public readonly Transform rightHand;

        public bool IsValid => head != null && leftUpperArm != null &&
            leftLowerArm != null && leftHand != null &&
            rightUpperArm != null && rightLowerArm != null &&
            rightHand != null;

        public Rig(Animator animator)
        {
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            leftUpperArm = animator.GetBoneTransform(
                HumanBodyBones.LeftUpperArm);
            leftLowerArm = animator.GetBoneTransform(
                HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpperArm = animator.GetBoneTransform(
                HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(
                HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
    }
}

// Keeps the awakening genuinely dark even though the dynamic sky updates RenderSettings every
// frame. Point lights remain untouched, making the tent torch the visual focus of the scene.
public sealed class MissionSevenWakeDarkness : MonoBehaviour
{
    TenkokuDayNightCycle cycle;
    Light[] directionalLights;
    readonly List<float> directionalIntensities = new List<float>();
    bool active;
    bool previousFog;
    FogMode previousFogMode;
    float previousFogDensity;
    float previousFogStart;
    float previousFogEnd;
    Color previousFogColor;
    Color previousAmbient;
    float previousAmbientIntensity;
    UnityEngine.Rendering.AmbientMode previousAmbientMode;

    public void Begin(TenkokuDayNightCycle dayNightCycle)
    {
        if (!active)
            CaptureOriginalSettings();
        cycle = dayNightCycle;
        active = true;
        enabled = true;
        ApplyDarkness();
    }

    void LateUpdate()
    {
        if (!active)
            return;
        if (cycle == null)
            cycle = FindAnyObjectByType<TenkokuDayNightCycle>();

        float hour = cycle != null ? cycle.CurrentHour : 0f;
        if (hour >= 6f && hour < 18f)
        {
            RestoreOriginalSettings();
            active = false;
            enabled = false;
            return;
        }
        ApplyDarkness();
    }

    void CaptureOriginalSettings()
    {
        previousFog = RenderSettings.fog;
        previousFogMode = RenderSettings.fogMode;
        previousFogDensity = RenderSettings.fogDensity;
        previousFogStart = RenderSettings.fogStartDistance;
        previousFogEnd = RenderSettings.fogEndDistance;
        previousFogColor = RenderSettings.fogColor;
        previousAmbient = RenderSettings.ambientLight;
        previousAmbientIntensity = RenderSettings.ambientIntensity;
        previousAmbientMode = RenderSettings.ambientMode;

        directionalLights = FindObjectsByType<Light>(
            FindObjectsInactive.Include);
        directionalIntensities.Clear();
        foreach (Light light in directionalLights)
            directionalIntensities.Add(light != null &&
                light.type == LightType.Directional
                    ? light.intensity
                    : -1f);
    }

    void ApplyDarkness()
    {
        RenderSettings.ambientMode =
            UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight =
            new Color(.0007f, .00025f, .0014f);
        RenderSettings.ambientIntensity = .025f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor =
            new Color(.0015f, .0003f, .003f);
        RenderSettings.fogStartDistance = 1.8f;
        RenderSettings.fogEndDistance = 14f;

        if (directionalLights != null)
            foreach (Light light in directionalLights)
                if (light != null &&
                    light.type == LightType.Directional)
                    light.intensity = Mathf.Min(light.intensity, .006f);

        Material sky = RenderSettings.skybox;
        if (sky != null && sky.HasProperty("_Exposure"))
            sky.SetFloat("_Exposure", .055f);
    }

    void RestoreOriginalSettings()
    {
        RenderSettings.fog = previousFog;
        RenderSettings.fogMode = previousFogMode;
        RenderSettings.fogDensity = previousFogDensity;
        RenderSettings.fogStartDistance = previousFogStart;
        RenderSettings.fogEndDistance = previousFogEnd;
        RenderSettings.fogColor = previousFogColor;
        RenderSettings.ambientLight = previousAmbient;
        RenderSettings.ambientIntensity = previousAmbientIntensity;
        RenderSettings.ambientMode = previousAmbientMode;

        if (directionalLights == null)
            return;
        for (int i = 0; i < directionalLights.Length &&
                        i < directionalIntensities.Count; i++)
            if (directionalLights[i] != null &&
                directionalIntensities[i] >= 0f)
                directionalLights[i].intensity =
                    directionalIntensities[i];
    }

    void OnDestroy()
    {
        if (active)
            RestoreOriginalSettings();
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
        float activationRadius = end ? 10f : 3f;
        if (player == null ||
            Vector3.Distance(player.position, transform.position) >
                activationRadius)
            return;
        if (end)
        {
            QuestManager quests = QuestManager.Instance;
            bool shouldStartEncounter = quests != null &&
                (quests.MerchantIntroductionState ==
                     PrimaryQuestState.SeventhFollowBloodTrail ||
                 quests.MerchantIntroductionState ==
                     PrimaryQuestState.SeventhTalkToNahue);
            // Do not consume this marker while the player is merely passing through the
            // location in an earlier quest state. It must remain available once the blood
            // trail objective becomes active.
            if (!shouldStartEncounter)
                return;
            consumed = true;
            quests?.ReachNahueThroughBloodTrail();
            NahueQuestGiver target =
                followTarget != null
                    ? followTarget.GetComponent<NahueQuestGiver>()
                    : null;
            if (shouldStartEncounter)
                SeventhMissionSequence.Instance
                    ?.BeginNahueTransformation(target);
        }
        else
        {
            consumed = true;
            QuestManager.Instance?.DiscoverBloodTrail();
            QuestOverheadThought.Show(player,
                "Tonio desapareci\u00f3... \u00bfDe qui\u00e9n es toda esta sangre?", 5f);
        }
    }
}

// Independent of dialogue and of individual blood stains: as soon as the mission asks the
// player to follow the trail, reaching ten metres from the real Nahue starts the cinematic.
public sealed class NahueCinematicProximityTrigger : MonoBehaviour
{
    SeventhMissionSequence sequence;
    NahueQuestGiver nahue;
    Transform player;
    bool started;

    public void Configure(SeventhMissionSequence owner,
        NahueQuestGiver target)
    {
        sequence = owner;
        nahue = target;
        started = false;
        enabled = true;
    }

    void Update()
    {
        if (started || sequence == null || nahue == null)
            return;
        QuestManager quests = QuestManager.Instance;
        if (quests == null ||
            (quests.MerchantIntroductionState !=
                 PrimaryQuestState.SeventhFollowBloodTrail &&
             quests.MerchantIntroductionState !=
                 PrimaryQuestState.SeventhTalkToNahue))
            return;

        if (player == null)
        {
            PlayerController controller =
                FindAnyObjectByType<PlayerController>();
            if (controller != null)
                player = controller.transform;
        }
        if (player == null)
            return;

        Vector3 offset = player.position - nahue.transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > 100f)
            return;

        started = true;
        quests.ReachNahueThroughBloodTrail();
        sequence.BeginNahueTransformation(nahue);
        enabled = false;
    }
}

// Permanent story aura granted after El Viejo finishes his mission-seven account.
// It uses the imported Travis status-effect prefab copied into Resources for builds.
public sealed class MissionSevenPlayerDarkAura : MonoBehaviour
{
    const string AuraResource = "VFX/StatusEffects/DarkAura";
    const string AuraObjectName = "Mission7_PermanentDarkAura";
    const string StatusIconKey = "mission7_darkness";
    GameObject aura;

    public static MissionSevenPlayerDarkAura Ensure(Transform player)
    {
        if (player == null) return null;
        MissionSevenPlayerDarkAura effect =
            player.GetComponent<MissionSevenPlayerDarkAura>();
        if (effect == null)
            effect = player.gameObject
                .AddComponent<MissionSevenPlayerDarkAura>();
        effect.enabled = true;
        effect.Build();
        effect.SetStatusIcon(true);
        return effect;
    }

    public static void RemoveFrom(Transform player)
    {
        if (player == null) return;
        MissionSevenPlayerDarkAura effect =
            player.GetComponent<MissionSevenPlayerDarkAura>();
        if (effect != null)
        {
            effect.RemoveVisuals();
            Destroy(effect);
            return;
        }

        Transform orphan = player.Find(AuraObjectName);
        if (orphan != null)
            Destroy(orphan.gameObject);
        ActiveStatusIconHUD.SetPersistent(StatusIconKey, "DarkAura",
            false);
    }

    void Awake()
    {
        Build();
    }

    void OnEnable()
    {
        Build();
        SetStatusIcon(true);
    }

    void Build()
    {
        if (aura != null) return;
        Transform existing = transform.Find(AuraObjectName);
        if (existing != null)
        {
            aura = existing.gameObject;
            aura.SetActive(true);
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(AuraResource);
        if (prefab == null)
        {
            // The editor copies the imported aura automatically. This fallback keeps the
            // story effect visible during the very first import/domain-reload frame.
            InsectoidCrabDarkAura fallback =
                GetComponent<InsectoidCrabDarkAura>();
            if (fallback == null)
                fallback = gameObject.AddComponent<InsectoidCrabDarkAura>();
            fallback.SetAuraActive(true);
            fallback.SetOpacityMultiplier(1.25f);
            return;
        }

        aura = Instantiate(prefab, transform, false);
        aura.name = AuraObjectName;
        aura.transform.localPosition = new Vector3(0f, .08f, 0f);
        aura.transform.localRotation = Quaternion.identity;
        aura.transform.localScale = Vector3.one * .92f;

        foreach (Collider collider in
                 aura.GetComponentsInChildren<Collider>(true))
            Destroy(collider);
        foreach (Rigidbody body in
                 aura.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        foreach (Light light in aura.GetComponentsInChildren<Light>(true))
            light.enabled = false;

        Color deep = new Color(.055f, .006f, .11f, .48f);
        Color violet = new Color(.34f, .045f, .52f, .64f);
        foreach (ParticleSystem particles in
                 aura.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startColor =
                new ParticleSystem.MinMaxGradient(deep, violet);
            foreach (ParticleSystemRenderer renderer in
                     particles.GetComponentsInChildren<
                         ParticleSystemRenderer>(true))
            {
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            particles.Play(true);
        }

        SetStatusIcon(true);
    }

    void OnDisable()
    {
        SetStatusIcon(false);
    }

    void OnDestroy()
    {
        SetStatusIcon(false);
    }

    void SetStatusIcon(bool active)
    {
        ActiveStatusIconHUD.SetPersistent(StatusIconKey, "DarkAura",
            active, active ? "Aura oscura" : null);
    }

    void RemoveVisuals()
    {
        if (aura != null)
            Destroy(aura);
        Transform orphan = transform.Find(AuraObjectName);
        if (orphan != null)
            Destroy(orphan.gameObject);
        InsectoidCrabDarkAura fallback =
            GetComponent<InsectoidCrabDarkAura>();
        fallback?.SetAuraActive(false);
        SetStatusIcon(false);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

// Additive access to DoubleL's RPG Animations Pack. The existing locomotion,
// melee, spell and equipment controllers remain the base layer and are never replaced.
[DisallowMultipleComponent]
public sealed class PlayerRpgAnimationPackController : MonoBehaviour
{
    const string CatalogResource = "Animation/RpgAnimationPackCatalog";
    const string BlockIdle = "OneHand_Up_Shield_Block_Idle";
    const string BlockHit = "OneHand_Up_Shield_Block_Hit_1_InPlace";
    const string CrouchIdle = "1Hand_Up_Crouch_Idle_1";
    const string CrouchMove = "1Hand_Up_Crouch_F_InPlace";
    const string RelaxedIdle = "HumanM@Idle01";
    const string RelaxedIdleVariationA = "HumanM@Idle02";
    const string RelaxedIdleVariationB = "HumanM@Idle03";
    // Walking and running intentionally use different leg cycles. The slow sword
    // walk supplies a real heel-to-toe step, while a relaxed idle upper-body layer
    // replaces its armed arms. Sprint keeps HumanM's natural full-body run.
    const string RelaxedWalk = "2Hand-Sword-Walk-Slow";
    const string RelaxedRun = "HumanM@Run01_Forward";

    readonly List<Animator> animators = new List<Animator>(2);
    readonly List<Animator> controlledAnimators = new List<Animator>(2);
    readonly List<AnimatorControllerPlayable> controllerPlayables =
        new List<AnimatorControllerPlayable>(2);
    readonly List<AnimationClipPlayable> clipPlayables =
        new List<AnimationClipPlayable>(2);
    readonly List<AnimationLayerMixerPlayable> layerMixers =
        new List<AnimationLayerMixerPlayable>(2);
    readonly List<AnimationClipPlayable> movingAttackLocomotionPlayables =
        new List<AnimationClipPlayable>(2);

    RpgAnimationPackCatalog catalog;
    PlayerDefenseController defense;
    PlayerController movement;
    PlayerSpellAnimationPlayer spellAnimations;
    PlayerStats stats;
    CombatSystem combat;
    WeaponSocket weaponSocket;
    WeaponDrawSystem weaponDrawSystem;
    PlayableGraph graph;
    AvatarMask upperBodyMask;
    GameObject shieldVisual;
    bool wasGuarding;
    bool looping;
    int selectedClip;
    int actionCycle;
    int dialogueCycle;
    string currentClipName;
    float nextRelaxedVariationAt;
    float nextThreatScan;
    float lastThreatSeenAt = -10f;
    float overlayWeight = 1f;
    float overlayTargetWeight = 1f;
    float overlayFadeSpeed = 20f;
    bool destroyAfterFade;
    bool threatNearby;
    bool movingAttackUsesRun;

    public bool IsCrouching { get; private set; }
    public bool IsPlaying => graph.IsValid() && graph.IsPlaying();
    public float MovementMultiplier => IsCrouching ? .48f : 1f;
    public bool ThreatNearby => threatNearby;
    public bool UsePeacefulLocomotion
    {
        get
        {
            bool gameplay = GameManager.Instance == null ||
                            GameManager.Instance.IsGameplayActive();
            bool actionBusy = (combat != null && combat.IsAttacking) ||
                              (spellAnimations != null && spellAnimations.IsPlaying) ||
                              (stats != null &&
                               (stats.IsDead || stats.IsRolling || stats.IsKnockedDown));
            bool weaponInHands = weaponSocket != null &&
                                 weaponSocket.HasWeaponEquipped() &&
                                 !weaponSocket.IsCarriedOnBack;
            bool combatProfile = weaponDrawSystem != null
                ? weaponDrawSystem.IsCombatProfileActive
                : weaponInHands;
            return gameplay && !threatNearby && !combatProfile &&
                   !weaponInHands &&
                   !IsCrouching &&
                   (defense == null || !defense.IsGuarding) && !actionBusy &&
                   movement != null && movement.IsGrounded;
        }
    }

    void Awake()
    {
        catalog = Resources.Load<RpgAnimationPackCatalog>(CatalogResource);
        defense = GetComponent<PlayerDefenseController>();
        movement = GetComponent<PlayerController>();
        spellAnimations = GetComponent<PlayerSpellAnimationPlayer>();
        stats = GetComponent<PlayerStats>();
        combat = GetComponent<CombatSystem>();
        weaponSocket = GetComponent<WeaponSocket>();
        weaponDrawSystem = GetComponent<WeaponDrawSystem>();
        EnsureShield();
    }

    void Update()
    {
        bool gameplay = GameManager.Instance == null ||
                        GameManager.Instance.IsGameplayActive();
        if (!gameplay || RuntimeChatConsole.IsTyping)
            return;

        UpdateThreatAwareness();

        if (spellAnimations == null)
            spellAnimations = GetComponent<PlayerSpellAnimationPlayer>();
        if (spellAnimations != null && spellAnimations.IsPlaying)
        {
            StopPlayback();
            SetShieldVisible(false);
            return;
        }

        bool guarding = defense != null && defense.IsGuarding;
        if (guarding != wasGuarding)
        {
            wasGuarding = guarding;
            SetShieldVisible(guarding);
            if (guarding) PlayNamed(BlockIdle, true, true);
            else RefreshPersistentPose();
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.zKey.wasPressedThisFrame)
        {
            IsCrouching = !IsCrouching;
            RefreshPersistentPose();
            ActiveStatusIconHUD.ShowTimed("rpg_crouch", "Utility", 1.1f,
                IsCrouching ? "Agachado" : "De pie");
        }

        HandleDirectControls(kb);
        HandleCatalogBrowser(kb);

        if (IsCrouching && !guarding)
        {
            string wanted = movement != null && movement.IsMoving
                ? CrouchMove
                : CrouchIdle;
            if (currentClipName != wanted)
                PlayNamed(wanted, true, false);
            return;
        }

        UpdateRelaxedIdle(guarding);
    }

    void LateUpdate()
    {
        UpdateRelaxedLocomotionSpeed();
        UpdateMovingAttackLocomotion();

        if (graph.IsValid() &&
            !Mathf.Approximately(overlayWeight, overlayTargetWeight))
        {
            overlayWeight = Mathf.MoveTowards(overlayWeight,
                overlayTargetWeight, overlayFadeSpeed * Time.deltaTime);
            foreach (AnimationLayerMixerPlayable mixer in layerMixers)
                if (mixer.IsValid())
                {
                    mixer.SetInputWeight(1, overlayWeight);
                    if (mixer.GetInputCount() > 2)
                        mixer.SetInputWeight(2, overlayWeight);
                }

            if (destroyAfterFade && overlayWeight <= .001f)
            {
                StopPlayback();
                return;
            }
        }

        int count = Mathf.Min(controlledAnimators.Count,
            controllerPlayables.Count);
        for (int i = 0; i < count; i++)
            if (controlledAnimators[i] != null &&
                controllerPlayables[i].IsValid())
                CopyParameters(controlledAnimators[i],
                    controllerPlayables[i]);

        foreach (AnimationClipPlayable playable in
                 movingAttackLocomotionPlayables)
        {
            if (!playable.IsValid()) continue;
            AnimationClip clip = playable.GetAnimationClip();
            if (clip != null && clip.length > .01f &&
                playable.GetTime() >= clip.length)
                playable.SetTime(playable.GetTime() % clip.length);
        }

        if (!looping) return;
        foreach (AnimationClipPlayable playable in clipPlayables)
        {
            if (!playable.IsValid()) continue;
            AnimationClip clip = playable.GetAnimationClip();
            if (clip != null && clip.length > .01f &&
                playable.GetTime() >= clip.length)
                playable.SetTime(playable.GetTime() % clip.length);
        }
    }

    void HandleDirectControls(Keyboard kb)
    {
        bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        if (shift)
        {
            if (kb.f1Key.wasPressedThisFrame) PlayNamed(CrouchIdle, true, false);
            if (kb.f2Key.wasPressedThisFrame) PlayNamed(CrouchMove, true, false);
            if (kb.f3Key.wasPressedThisFrame) PlayNamed("Ladder_Up_Play_InPlace", true, false);
            if (kb.f4Key.wasPressedThisFrame) PlayNamed("Ladder_Down_Play_InPlace", true, false);
            if (kb.f5Key.wasPressedThisFrame) PlayNamed("Bow_Attack_A_1_All", false, false);
            if (kb.f6Key.wasPressedThisFrame) PlayNamed("Bow_Attack_B_1_All", false, false);
            if (kb.f7Key.wasPressedThisFrame) PlayNamed("Enemy_Attack_1_InPlace", false, false);
            if (kb.f8Key.wasPressedThisFrame) PlayNamed(BlockIdle, true, true);
            if (kb.f9Key.wasPressedThisFrame) PlayNamed("OneHand_Up_Idle", true, false);
            if (kb.f10Key.wasPressedThisFrame) PlayNamed("OneHand_Up_Sprint_InPlace", true, false);
            if (kb.f11Key.wasPressedThisFrame) PlayNamed("OneHand_Up_Run_F_InPlace", true, false);
            if (kb.f12Key.wasPressedThisFrame) PlayNamed("OneHand_Up_Walk_F_InPlace", true, false);
            return;
        }

        if (kb.f1Key.wasPressedThisFrame) PlayAttack(1, false);
        if (kb.f2Key.wasPressedThisFrame) PlayAttack(2, false);
        if (kb.f3Key.wasPressedThisFrame) PlayAttack(3, false);
        if (kb.f4Key.wasPressedThisFrame) PlayAttack(1, true);
        if (kb.f5Key.wasPressedThisFrame) PlayAttack(2, true);
        if (kb.f6Key.wasPressedThisFrame) PlayAttack(3, true);
        if (kb.f7Key.wasPressedThisFrame) PlayBlockImpact();
        if (kb.f8Key.wasPressedThisFrame) PlayNamed("Hit_F_1_InPlace", false, false);
        if (kb.f9Key.wasPressedThisFrame) PlayNamed("Hit_F_2_InPlace", false, false);
        if (kb.f10Key.wasPressedThisFrame)
        {
            actionCycle = actionCycle % 3 + 1;
            PlayNamed(actionCycle == 1 ? "Action_A_4_1" :
                actionCycle == 2 ? "Action_A_5_1" : "Action_A_5_2",
                false, false);
        }
        if (kb.f11Key.wasPressedThisFrame)
        {
            dialogueCycle = dialogueCycle % 5 + 1;
            PlayNamed("Dialogue_" + dialogueCycle, false, true);
        }
        if (kb.f12Key.wasPressedThisFrame)
            PlayNamed("OneHand_Up_Jump_B_InPlace", false, false);
    }

    void HandleCatalogBrowser(Keyboard kb)
    {
        if (catalog == null || catalog.clips == null ||
            catalog.clips.Length == 0) return;
        if (kb.pageUpKey.wasPressedThisFrame)
        {
            selectedClip = (selectedClip - 1 + catalog.clips.Length) %
                           catalog.clips.Length;
            ShowSelected();
        }
        if (kb.pageDownKey.wasPressedThisFrame)
        {
            selectedClip = (selectedClip + 1) % catalog.clips.Length;
            ShowSelected();
        }
        if (kb.homeKey.wasPressedThisFrame)
        {
            AnimationClip clip = catalog.clips[selectedClip];
            if (clip != null) PlayClip(clip, false,
                clip.name.Contains("Idle") || clip.name.Contains("Play"));
        }
        if (kb.endKey.wasPressedThisFrame)
        {
            IsCrouching = false;
            StopPlayback();
            SetShieldVisible(false);
        }
    }

    void ShowSelected()
    {
        AnimationClip clip = catalog.clips[selectedClip];
        if (clip != null)
            ActiveStatusIconHUD.ShowTimed("rpg_anim_selected", "Skill", 1.5f,
                "Animación: " + clip.name);
    }

    void PlayAttack(int index, bool alternate)
    {
        PlayNamed("OneHand_Up_Attack_" + (alternate ? "B_" : "") +
                  Mathf.Clamp(index, 1, 3) + "_InPlace", false, true);
    }

    public void PlayBlockImpact()
    {
        SetShieldVisible(true);
        PlayNamed(BlockHit, false, true);
        CancelInvoke(nameof(ReturnToGuardPose));
        Invoke(nameof(ReturnToGuardPose), .42f);
    }

    void ReturnToGuardPose()
    {
        if (defense != null && defense.IsGuarding)
            PlayNamed(BlockIdle, true, true);
        else
            RefreshPersistentPose();
    }

    void RefreshPersistentPose()
    {
        CancelInvoke(nameof(ReturnToGuardPose));
        bool guarding = defense != null && defense.IsGuarding;
        SetShieldVisible(guarding);
        if (guarding)
            PlayNamed(BlockIdle, true, true);
        else if (IsCrouching)
            PlayNamed(movement != null && movement.IsMoving
                ? CrouchMove : CrouchIdle, true, false);
        else
            UpdateRelaxedIdle(false);
    }

    void UpdateRelaxedIdle(bool guarding)
    {
        bool shouldRelax = !guarding && UsePeacefulLocomotion;

        if (!shouldRelax)
        {
            FadeOutRelaxedPose(.32f);
            return;
        }

        if (movement.IsMoving)
        {
            string wanted = movement.IsSprinting ? RelaxedRun : RelaxedWalk;
            if (currentClipName != wanted)
                PlayNamed(wanted, true, false);
        }
        else if (currentClipName != RelaxedIdle &&
                 currentClipName != RelaxedIdleVariationA &&
                 currentClipName != RelaxedIdleVariationB)
        {
            PlayNamed(RelaxedIdle, true, false);
            ScheduleRelaxedVariation();
        }

        if (IsRelaxedClip(currentClipName) && graph.IsValid())
        {
            // If the player stopped again while the relaxed pose was fading
            // away, reverse that same blend instead of destroying/recreating
            // the graph. This prevents a visible snap at very short stops.
            overlayTargetWeight = 1f;
            overlayFadeSpeed = 1f / .28f;
            destroyAfterFade = false;
        }

        if (!IsRelaxedClip(currentClipName))
        {
            if (graph.IsValid())
                return;
            PlayNamed(RelaxedIdle, true, false);
            ScheduleRelaxedVariation();
            return;
        }

        if (!movement.IsMoving && currentClipName == RelaxedIdle &&
            Time.time >= nextRelaxedVariationAt)
        {
            PlayNamed(Random.value < .5f
                ? RelaxedIdleVariationA
                : RelaxedIdleVariationB, false, false);
            ScheduleRelaxedVariation();
        }
    }

    void ScheduleRelaxedVariation()
    {
        nextRelaxedVariationAt = Time.time + Random.Range(9f, 16f);
    }

    static bool IsRelaxedClip(string clipName)
    {
        return clipName == RelaxedIdle ||
               clipName == RelaxedIdleVariationA ||
               clipName == RelaxedIdleVariationB ||
               clipName == RelaxedWalk ||
               clipName == RelaxedRun;
    }

    // Called by the regular Animator bridge immediately before attacks, jumps,
    // impacts and other base-controller actions. This prevents the full-body
    // relaxed playable from visually covering the requested action.
    public void ReleaseRelaxedPoseForAction()
    {
        if (IsRelaxedClip(currentClipName))
        {
            // Animator triggers are stored on the real Animator, not on the temporary
            // AnimatorControllerPlayable below this relaxed layer. Leaving the graph alive
            // for a few more frames made attacks/draw transitions arrive late and allowed the
            // underlying controller to resume an old Jump state. Restore the live controller
            // state first, then let Mecanim blend normally into the requested action.
            StopPlayback();
        }
    }

    void FadeOutRelaxedPose(float duration)
    {
        if (!IsRelaxedClip(currentClipName) || !graph.IsValid())
            return;
        overlayTargetWeight = 0f;
        overlayFadeSpeed = 1f / Mathf.Max(.02f, duration);
        destroyAfterFade = true;
    }

    void UpdateThreatAwareness()
    {
        if (Time.time < nextThreatScan)
            return;
        nextThreatScan = Time.time + .22f;

        const float enterRadius = 12f;
        const float exitRadius = 15f;
        float radius = threatNearby ? exitRadius : enterRadius;
        float radiusSqr = radius * radius;
        bool found = false;

        foreach (EnemyStats enemy in
                 FindObjectsByType<EnemyStats>(FindObjectsInactive.Exclude))
        {
            if (!IsHostileThreat(enemy))
                continue;
            Vector3 delta = enemy.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > radiusSqr)
                continue;
            found = true;
            lastThreatSeenAt = Time.time;
            break;
        }

        if (found)
            threatNearby = true;
        else if (Time.time - lastThreatSeenAt > 1.15f)
            threatNearby = false;
    }

    static bool IsHostileThreat(EnemyStats enemy)
    {
        if (enemy == null || enemy.IsDead)
            return false;
        GameObject candidate = enemy.gameObject;
        AnimalAI animal = candidate.GetComponent<AnimalAI>();
        if (animal != null && !animal.IsCombatHostile)
            return false;
        return candidate.GetComponent<NPCWander>() == null &&
               candidate.GetComponent<TonioQuestGiver>() == null &&
               candidate.GetComponent<NahueQuestGiver>() == null &&
               candidate.GetComponent<NPCHerrero>() == null &&
               candidate.GetComponent<NPCMerchant>() == null;
    }

    void PlayNamed(string clipName, bool loop, bool upperBody)
    {
        if (catalog == null)
            catalog = Resources.Load<RpgAnimationPackCatalog>(CatalogResource);
        AnimationClip clip = catalog != null ? catalog.Find(clipName) : null;
        if (clip == null) return;
        PlayClip(clip, loop, upperBody);
    }

    void PlayClip(AnimationClip clip, bool loop, bool upperBody)
    {
        RefreshAnimators();
        if (clip == null || animators.Count == 0) return;
        bool changingBetweenRelaxedClips =
            IsRelaxedClip(currentClipName) && graph.IsValid() &&
            overlayWeight > .5f && IsRelaxedClip(clip.name);
        StopPlayback();
        currentClipName = clip.name;
        looping = loop;
        bool relaxedClip = IsRelaxedClip(currentClipName);
        bool compositeRelaxedWalk = currentClipName == RelaxedWalk;
        // Never reveal the combat controller between peaceful idle/walk clips.
        // That brief zero-weight frame was the visible "hands holding a sword"
        // flash while the actual weapon remained on the back.
        overlayWeight = relaxedClip && !changingBetweenRelaxedClips ? 0f : 1f;
        overlayTargetWeight = 1f;
        overlayFadeSpeed = relaxedClip ? 1f / .18f : 20f;
        destroyAfterFade = false;
        graph = PlayableGraph.Create("Player_DoubleL_RPG_Animations");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        upperBodyMask = upperBody || compositeRelaxedWalk
            ? BuildUpperBodyMask()
            : null;

        foreach (Animator target in animators)
        {
            if (target == null || !target.isActiveAndEnabled ||
                target.runtimeAnimatorController == null) continue;
            AnimatorControllerPlayable baseController =
                AnimatorControllerPlayable.Create(graph,
                    target.runtimeAnimatorController);
            CopyParameters(target, baseController);
            for (int layer = 0; layer < target.layerCount; layer++)
            {
                AnimatorStateInfo state =
                    target.GetCurrentAnimatorStateInfo(layer);
                if (state.fullPathHash != 0)
                    baseController.Play(state.fullPathHash, layer,
                        state.normalizedTime);
            }

            AnimationLayerMixerPlayable mixer =
                AnimationLayerMixerPlayable.Create(graph,
                    compositeRelaxedWalk ? 3 : 2);
            graph.Connect(baseController, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);
            AnimationClipPlayable clipPlayable =
                AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(!upperBody);
            clipPlayable.SetApplyPlayableIK(false);
            if (currentClipName == RelaxedWalk)
                clipPlayable.SetSpeed(movement != null
                    ? movement.PeacefulWalkPlaybackSpeed
                    : 1d);
            else if (currentClipName == RelaxedRun)
                clipPlayable.SetSpeed(movement != null
                    ? movement.PeacefulRunPlaybackSpeed
                    : 1d);
            graph.Connect(clipPlayable, 0, mixer, 1);
            mixer.SetInputWeight(1, overlayWeight);
            if (upperBodyMask != null && !compositeRelaxedWalk)
                mixer.SetLayerMaskFromAvatarMask(1, upperBodyMask);

            if (compositeRelaxedWalk)
            {
                AnimationClip relaxedUpperClip =
                    catalog != null ? catalog.Find(RelaxedIdle) : null;
                if (relaxedUpperClip != null)
                {
                    AnimationClipPlayable upperPlayable =
                        AnimationClipPlayable.Create(graph, relaxedUpperClip);
                    upperPlayable.SetApplyFootIK(false);
                    graph.Connect(upperPlayable, 0, mixer, 2);
                    mixer.SetInputWeight(2, overlayWeight);
                    mixer.SetLayerMaskFromAvatarMask(2, upperBodyMask);
                    clipPlayables.Add(upperPlayable);
                }
            }
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(
                graph, "DoubleL_" + target.name, target);
            output.SetSourcePlayable(mixer);
            controlledAnimators.Add(target);
            controllerPlayables.Add(baseController);
            clipPlayables.Add(clipPlayable);
            layerMixers.Add(mixer);
        }
        graph.Play();
        if (!loop)
            Invoke(nameof(FinishOneShot), Mathf.Max(.15f, clip.length));
    }

    void UpdateRelaxedLocomotionSpeed()
    {
        if ((currentClipName != RelaxedWalk &&
             currentClipName != RelaxedRun) || movement == null)
            return;

        double speed = currentClipName == RelaxedRun
            ? movement.PeacefulRunPlaybackSpeed
            : movement.PeacefulWalkPlaybackSpeed;
        foreach (AnimationClipPlayable playable in clipPlayables)
            if (playable.IsValid() &&
                playable.GetAnimationClip() != null &&
                playable.GetAnimationClip().name == currentClipName)
                playable.SetSpeed(speed);
    }

    public bool PlayMovingSwordAttack(int actionIndex, float speedMultiplier)
    {
        if (catalog == null)
            catalog = Resources.Load<RpgAnimationPackCatalog>(CatalogResource);
        string attackClipName = "2Hand-Sword-Attack" +
                          Mathf.Clamp(actionIndex, 1, 11);
        movingAttackUsesRun = movement != null && movement.IsSprinting;
        string locomotionClipName = movingAttackUsesRun
            ? "2Hand-Sword-Run-Forward"
            : "2Hand-Sword-Walk";
        AnimationClip attackClip =
            catalog != null ? catalog.Find(attackClipName) : null;
        AnimationClip locomotionClip =
            catalog != null ? catalog.Find(locomotionClipName) : null;
        if (attackClip == null || locomotionClip == null)
            return false;

        RefreshAnimators();
        if (animators.Count == 0)
            return false;
        StopPlayback();
        currentClipName = attackClip.name;
        looping = false;
        overlayWeight = 1f;
        overlayTargetWeight = 1f;
        destroyAfterFade = false;
        graph = PlayableGraph.Create("Player_Moving_Sword_Attack");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        upperBodyMask = BuildUpperBodyMask();

        double attackSpeed = Mathf.Max(.1f, speedMultiplier);
        foreach (Animator target in animators)
        {
            if (target == null || !target.isActiveAndEnabled ||
                target.runtimeAnimatorController == null)
                continue;

            AnimatorControllerPlayable baseController =
                AnimatorControllerPlayable.Create(graph,
                    target.runtimeAnimatorController);
            CopyParameters(target, baseController);
            for (int layer = 0; layer < target.layerCount; layer++)
            {
                AnimatorStateInfo state =
                    target.GetCurrentAnimatorStateInfo(layer);
                if (state.fullPathHash != 0)
                    baseController.Play(state.fullPathHash, layer,
                        state.normalizedTime);
            }

            AnimationLayerMixerPlayable mixer =
                AnimationLayerMixerPlayable.Create(graph, 3);
            graph.Connect(baseController, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);

            AnimationClipPlayable locomotionPlayable =
                AnimationClipPlayable.Create(graph, locomotionClip);
            locomotionPlayable.SetApplyFootIK(true);
            locomotionPlayable.SetSpeed(CurrentMovingAttackLocomotionSpeed());
            graph.Connect(locomotionPlayable, 0, mixer, 1);
            mixer.SetInputWeight(1, 1f);

            AnimationClipPlayable attackPlayable =
                AnimationClipPlayable.Create(graph, attackClip);
            attackPlayable.SetApplyFootIK(false);
            attackPlayable.SetSpeed(attackSpeed);
            graph.Connect(attackPlayable, 0, mixer, 2);
            mixer.SetInputWeight(2, 1f);
            mixer.SetLayerMaskFromAvatarMask(2, upperBodyMask);

            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(graph,
                    "MovingSwordAttack_" + target.name, target);
            output.SetSourcePlayable(mixer);
            controlledAnimators.Add(target);
            controllerPlayables.Add(baseController);
            clipPlayables.Add(attackPlayable);
            movingAttackLocomotionPlayables.Add(locomotionPlayable);
            layerMixers.Add(mixer);
        }
        graph.Play();

        CancelInvoke(nameof(FinishOneShot));
        Invoke(nameof(FinishOneShot),
            Mathf.Max(.15f, attackClip.length / (float)attackSpeed));
        return true;
    }

    double CurrentMovingAttackLocomotionSpeed()
    {
        if (movement == null)
            return 1d;
        return movingAttackUsesRun
            ? movement.PeacefulRunPlaybackSpeed
            : movement.PeacefulWalkPlaybackSpeed;
    }

    void UpdateMovingAttackLocomotion()
    {
        if (movingAttackLocomotionPlayables.Count == 0)
            return;
        double speed = CurrentMovingAttackLocomotionSpeed();
        foreach (AnimationClipPlayable playable in
                 movingAttackLocomotionPlayables)
            if (playable.IsValid())
                playable.SetSpeed(speed);
    }

    void FinishOneShot()
    {
        StopPlayback();
        RefreshPersistentPose();
    }

    void RefreshAnimators()
    {
        animators.Clear();
        Animator rootAnimator = GetComponent<Animator>();
        if (rootAnimator != null) animators.Add(rootAnimator);
        GanzPlayerVisual ganz = GetComponent<GanzPlayerVisual>();
        Animator visual = ganz != null ? ganz.VisualAnimator : null;
        if (visual != null && !animators.Contains(visual))
            animators.Add(visual);
    }

    static void CopyParameters(Animator source,
        AnimatorControllerPlayable destination)
    {
        foreach (AnimatorControllerParameter parameter in source.parameters)
        {
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Bool:
                    destination.SetBool(parameter.nameHash,
                        source.GetBool(parameter.nameHash));
                    break;
                case AnimatorControllerParameterType.Float:
                    destination.SetFloat(parameter.nameHash,
                        source.GetFloat(parameter.nameHash));
                    break;
                case AnimatorControllerParameterType.Int:
                    destination.SetInteger(parameter.nameHash,
                        source.GetInteger(parameter.nameHash));
                    break;
            }
        }
    }

    static AvatarMask BuildUpperBodyMask()
    {
        AvatarMask mask = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        return mask;
    }

    void EnsureShield()
    {
        if (shieldVisual != null || catalog == null ||
            catalog.shieldPrefab == null) return;
        RefreshAnimators();
        Animator target = animators.Count > 1 ? animators[animators.Count - 1] :
            animators.Count > 0 ? animators[0] : null;
        Transform hand = target != null
            ? target.GetBoneTransform(HumanBodyBones.LeftHand)
            : null;
        if (hand == null) return;
        shieldVisual = Instantiate(catalog.shieldPrefab, hand);
        shieldVisual.name = "Player_DoubleL_Shield";
        foreach (Animator childAnimator in
                 shieldVisual.GetComponentsInChildren<Animator>(true))
            childAnimator.enabled = false;
        shieldVisual.transform.localPosition = new Vector3(.02f, .02f, .02f);
        shieldVisual.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
        NormalizeShieldSize(shieldVisual, .72f);
        shieldVisual.SetActive(false);
    }

    static void NormalizeShieldSize(GameObject shield, float targetHeight)
    {
        Renderer[] renderers = shield.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largest > .001f)
            shield.transform.localScale *= targetHeight / largest;
    }

    void SetShieldVisible(bool visible)
    {
        if (shieldVisual == null) EnsureShield();
        if (shieldVisual != null && shieldVisual.activeSelf != visible)
            shieldVisual.SetActive(visible);
    }

    void StopPlayback()
    {
        CancelInvoke(nameof(FinishOneShot));
        if (graph.IsValid())
        {
            RestoreControllerStateToAnimators();
            graph.Destroy();
            foreach (Animator animator in controlledAnimators)
                if (animator != null && animator.isActiveAndEnabled)
                {
                    if (movement != null && movement.IsGrounded)
                        ForceGroundedParameters(animator);
                    animator.Update(0f);
                }
        }
        controlledAnimators.Clear();
        controllerPlayables.Clear();
        clipPlayables.Clear();
        layerMixers.Clear();
        movingAttackLocomotionPlayables.Clear();
        if (upperBodyMask != null) Destroy(upperBodyMask);
        upperBodyMask = null;
        looping = false;
        currentClipName = null;
        overlayWeight = 1f;
        overlayTargetWeight = 1f;
        destroyAfterFade = false;
    }

    void RestoreControllerStateToAnimators()
    {
        int count = Mathf.Min(controlledAnimators.Count,
            controllerPlayables.Count);
        for (int i = 0; i < count; i++)
        {
            Animator animator = controlledAnimators[i];
            AnimatorControllerPlayable controller = controllerPlayables[i];
            if (animator == null || !controller.IsValid())
                continue;

            foreach (AnimatorControllerParameter parameter in
                     animator.parameters)
            {
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        animator.SetBool(parameter.nameHash,
                            controller.GetBool(parameter.nameHash));
                        break;
                    case AnimatorControllerParameterType.Float:
                        animator.SetFloat(parameter.nameHash,
                            controller.GetFloat(parameter.nameHash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        animator.SetInteger(parameter.nameHash,
                            controller.GetInteger(parameter.nameHash));
                        break;
                }
            }

            int layers = Mathf.Min(animator.layerCount,
                controller.GetLayerCount());
            for (int layer = 0; layer < layers; layer++)
            {
                AnimatorStateInfo state =
                    controller.GetCurrentAnimatorStateInfo(layer);
                if (state.fullPathHash != 0)
                    animator.Play(state.fullPathHash, layer,
                        Mathf.Repeat(state.normalizedTime, 1f));
            }
        }
    }

    static void ForceGroundedParameters(Animator animator)
    {
        int jumping = Animator.StringToHash("Jumping");
        int triggerNumber = Animator.StringToHash("TriggerNumber");
        int actionTrigger = Animator.StringToHash("Trigger");
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == jumping &&
                parameter.type == AnimatorControllerParameterType.Int)
                animator.SetInteger(jumping, 0);
            else if (parameter.nameHash == triggerNumber &&
                     parameter.type == AnimatorControllerParameterType.Int &&
                     animator.GetInteger(triggerNumber) == 18)
                animator.SetInteger(triggerNumber, 0);
            else if (parameter.nameHash == actionTrigger &&
                     parameter.type == AnimatorControllerParameterType.Trigger)
                animator.ResetTrigger(actionTrigger);
        }
    }

    void OnDisable()
    {
        StopPlayback();
        SetShieldVisible(false);
    }
}

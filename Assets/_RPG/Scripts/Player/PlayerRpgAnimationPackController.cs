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

    readonly List<Animator> animators = new List<Animator>(2);
    readonly List<Animator> controlledAnimators = new List<Animator>(2);
    readonly List<AnimatorControllerPlayable> controllerPlayables =
        new List<AnimatorControllerPlayable>(2);
    readonly List<AnimationClipPlayable> clipPlayables =
        new List<AnimationClipPlayable>(2);

    RpgAnimationPackCatalog catalog;
    PlayerDefenseController defense;
    PlayerController movement;
    PlayerSpellAnimationPlayer spellAnimations;
    PlayableGraph graph;
    AvatarMask upperBodyMask;
    GameObject shieldVisual;
    bool wasGuarding;
    bool looping;
    int selectedClip;
    int actionCycle;
    int dialogueCycle;
    string currentClipName;

    public bool IsCrouching { get; private set; }
    public bool IsPlaying => graph.IsValid() && graph.IsPlaying();
    public float MovementMultiplier => IsCrouching ? .48f : 1f;

    void Awake()
    {
        catalog = Resources.Load<RpgAnimationPackCatalog>(CatalogResource);
        defense = GetComponent<PlayerDefenseController>();
        movement = GetComponent<PlayerController>();
        spellAnimations = GetComponent<PlayerSpellAnimationPlayer>();
        EnsureShield();
    }

    void Update()
    {
        bool gameplay = GameManager.Instance == null ||
                        GameManager.Instance.IsGameplayActive();
        if (!gameplay || RuntimeChatConsole.IsTyping)
            return;

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
        }
    }

    void LateUpdate()
    {
        int count = Mathf.Min(controlledAnimators.Count,
            controllerPlayables.Count);
        for (int i = 0; i < count; i++)
            if (controlledAnimators[i] != null &&
                controllerPlayables[i].IsValid())
                CopyParameters(controlledAnimators[i],
                    controllerPlayables[i]);

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
            StopPlayback();
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
        StopPlayback();
        currentClipName = clip.name;
        looping = loop;
        graph = PlayableGraph.Create("Player_DoubleL_RPG_Animations");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        upperBodyMask = upperBody ? BuildUpperBodyMask() : null;

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
                AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(baseController, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);
            AnimationClipPlayable clipPlayable =
                AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(!upperBody);
            clipPlayable.SetApplyPlayableIK(false);
            graph.Connect(clipPlayable, 0, mixer, 1);
            mixer.SetInputWeight(1, 1f);
            if (upperBodyMask != null)
                mixer.SetLayerMaskFromAvatarMask(1, upperBodyMask);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(
                graph, "DoubleL_" + target.name, target);
            output.SetSourcePlayable(mixer);
            controlledAnimators.Add(target);
            controllerPlayables.Add(baseController);
            clipPlayables.Add(clipPlayable);
        }
        graph.Play();
        if (!loop)
            Invoke(nameof(FinishOneShot), Mathf.Max(.15f, clip.length));
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
        if (graph.IsValid()) graph.Destroy();
        controlledAnimators.Clear();
        controllerPlayables.Clear();
        clipPlayables.Clear();
        if (upperBodyMask != null) Destroy(upperBodyMask);
        upperBodyMask = null;
        looping = false;
        currentClipName = null;
    }

    void OnDisable()
    {
        StopPlayback();
        SetShieldVisible(false);
    }
}

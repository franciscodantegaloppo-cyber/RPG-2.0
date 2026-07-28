using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Plays the imported Human Spellcasting clips directly on the player. It is independent from
// the existing melee controller, so all spell animations work without replacing sword/walk
// state machines or authoring fragile Animator transitions by hand.
public class PlayerSpellAnimationPlayer : MonoBehaviour
{
    static readonly string[] ClipNames =
    {
        "HumanM@MagicAttackDirect1H01_R - Cast", // fireball
        "HumanM@MagicAttackDirect2H01 - Cast",   // ice
        "HumanM@MagicAttackDirect1H01_L - Cast", // lightning
        "HumanM@MagicAttackOmni01 - Cast",       // heal
        "HumanM@MagicAttackOmni01"               // shockwave: complete omnidirectional action
    };
    readonly List<Animator> animators = new List<Animator>(2);
    readonly List<Animator> controlledAnimators = new List<Animator>(2);
    readonly List<AnimatorControllerPlayable> controllerPlayables = new List<AnimatorControllerPlayable>(2);
    AnimationClip[] clips;
    PlayableGraph graph;
    AvatarMask upperBodyMask;
    public bool IsPlaying => graph.IsValid() && graph.IsPlaying();

    void Awake()
    {
        RefreshAnimators();
        clips = Resources.LoadAll<AnimationClip>("SpellAnimations");
    }

    public void Play(int index)
    {
        RefreshAnimators();
        if (animators.Count == 0 || clips == null || clips.Length == 0) return;
        CancelInvoke(nameof(StopPlayback));
        if (graph.IsValid()) graph.Destroy();
        AnimationClip clip = FindClip(index);
        if (clip == null) return;
        graph = PlayableGraph.Create("PlayerSpellCast");
        controlledAnimators.Clear();
        controllerPlayables.Clear();
        upperBodyMask = BuildUpperBodyMask();
        for (int i = 0; i < animators.Count; i++)
        {
            Animator target = animators[i];
            if (target == null || !target.isActiveAndEnabled || target.runtimeAnimatorController == null) continue;

            // Keep the current locomotion controller running underneath the casting clip. Only
            // torso, head, arms and fingers come from the spell animation, so the legs can keep
            // walking or running normally during the cast.
            AnimatorControllerPlayable locomotion = AnimatorControllerPlayable.Create(graph, target.runtimeAnimatorController);
            CopyParameters(target, locomotion);
            for (int layer = 0; layer < target.layerCount; layer++)
            {
                AnimatorStateInfo state = target.GetCurrentAnimatorStateInfo(layer);
                if (state.fullPathHash != 0) locomotion.Play(state.fullPathHash, layer, state.normalizedTime);
            }

            AnimationLayerMixerPlayable mixer = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(locomotion, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "SpellCast_" + i, target);
            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            graph.Connect(playable, 0, mixer, 1);
            mixer.SetInputWeight(1, 1f);
            mixer.SetLayerMaskFromAvatarMask(1, upperBodyMask);
            output.SetSourcePlayable(mixer);
            controlledAnimators.Add(target);
            controllerPlayables.Add(locomotion);
        }
        graph.Play();
        Invoke(nameof(StopPlayback), Mathf.Max(.2f, clip.length));
    }

    AvatarMask BuildUpperBodyMask()
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

    void LateUpdate()
    {
        // PlayerAnimatorBridge continues updating movement on the Animators while this graph is
        // active. Mirror those values into each controller playable so its lower-body locomotion
        // responds immediately instead of freezing on the frame where casting began.
        int count = Mathf.Min(controlledAnimators.Count, controllerPlayables.Count);
        for (int i = 0; i < count; i++)
            if (controlledAnimators[i] != null && controllerPlayables[i].IsValid())
                CopyParameters(controlledAnimators[i], controllerPlayables[i]);
    }

    static void CopyParameters(Animator source, AnimatorControllerPlayable destination)
    {
        foreach (AnimatorControllerParameter parameter in source.parameters)
        {
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Bool:
                    destination.SetBool(parameter.nameHash, source.GetBool(parameter.nameHash));
                    break;
                case AnimatorControllerParameterType.Float:
                    destination.SetFloat(parameter.nameHash, source.GetFloat(parameter.nameHash));
                    break;
                case AnimatorControllerParameterType.Int:
                    destination.SetInteger(parameter.nameHash, source.GetInteger(parameter.nameHash));
                    break;
            }
        }
    }

    void RefreshAnimators()
    {
        animators.Clear();
        foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
            AddAnimator(candidate);

        // Equipment swaps create a separate visible Ganz model at runtime. It is not guaranteed
        // to exist when this component's Awake runs, so request it again for every cast.
        GanzPlayerVisual ganz = GetComponent<GanzPlayerVisual>();
        if (ganz != null) AddAnimator(ganz.VisualAnimator);
    }

    void AddAnimator(Animator candidate)
    {
        if (candidate != null && !animators.Contains(candidate)) animators.Add(candidate);
    }

    AnimationClip FindClip(int index)
    {
        string exactName = ClipNames[Mathf.Abs(index) % ClipNames.Length];
        foreach (AnimationClip candidate in clips)
            if (candidate != null && candidate.name == exactName)
                return candidate;
        string hint = index == 4 ? "Omni01" : "Cast";
        foreach (AnimationClip candidate in clips)
            if (candidate != null && candidate.name.Contains(hint))
                return candidate;
        return null;
    }

    void StopPlayback()
    {
        CancelInvoke(nameof(StopPlayback));
        if (graph.IsValid()) graph.Destroy();
        controlledAnimators.Clear();
        controllerPlayables.Clear();
        if (upperBodyMask != null) Destroy(upperBodyMask);
        upperBodyMask = null;
    }
    void OnDisable() => StopPlayback();
}

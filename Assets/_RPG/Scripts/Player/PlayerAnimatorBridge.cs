using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TriggerNumber values from RPGCharacterAnims.Lookups.AnimatorTrigger:
//   AttackTrigger=4  GetHitTrigger=12  KnockbackTrigger=26  KnockdownTrigger=27
//   DiveRollTrigger=28  WeaponSheathTrigger=15  WeaponUnsheathTrigger=16
//   JumpTrigger=18  DeathTrigger=20  ReviveTrigger=21

[RequireComponent(typeof(Animator))]
public class PlayerAnimatorBridge : MonoBehaviour
{
    static readonly int HashMoving         = Animator.StringToHash("Moving");
    static readonly int HashVelocityX      = Animator.StringToHash("Velocity X");
    static readonly int HashVelocityZ      = Animator.StringToHash("Velocity Z");
    static readonly int HashJumping        = Animator.StringToHash("Jumping");
    static readonly int HashTrigger        = Animator.StringToHash("Trigger");
    static readonly int HashTriggerNumber  = Animator.StringToHash("TriggerNumber");
    static readonly int HashWeapon         = Animator.StringToHash("Weapon");
    static readonly int HashRightWeapon    = Animator.StringToHash("RightWeapon");
    static readonly int HashAction         = Animator.StringToHash("Action");
    static readonly int HashSide           = Animator.StringToHash("Side");
    static readonly int HashAnimSpeed      = Animator.StringToHash("AnimationSpeed");

    Animator anim;
    CombatSystem combat;
    PlayerController controller;
    PlayerWaterBreathing waterBreathing;
    PlayerStats stats;
    WeaponSocket weaponSocket;
    GanzPlayerVisual ganzVisual;
    PlayerSpellAnimationPlayer spellAnimations;
    PlayerRpgAnimationPackController rpgAnimationPack;
    WeaponDrawSystem weaponDrawSystem;

    // GanzPlayerVisual (when equipment is worn) puts up a second, fully separate Animator
    // instance on the visible skinned character. Sharing the same RuntimeAnimatorController
    // asset does NOT sync parameter values between the two - each Animator has its own
    // independent state machine. Every Set*/Trigger call below is mirrored to both so
    // whichever one is actually being rendered still animates.
    readonly List<Animator> targets = new List<Animator>(2);
    float attackAnimationSpeed = 1f;
    Coroutine resetAttackSpeed;
    int equippedWeaponType;
    float relaxedIdleEligibleSince = -1f;
    const float RelaxedIdleDelay = .12f;

    void Awake()
    {
        anim         = GetComponent<Animator>();
        combat       = GetComponent<CombatSystem>();
        controller   = GetComponent<PlayerController>();
        waterBreathing = GetComponent<PlayerWaterBreathing>();
        stats         = GetComponent<PlayerStats>();
        weaponSocket = GetComponent<WeaponSocket>();
        ganzVisual   = GetComponent<GanzPlayerVisual>();
        spellAnimations = GetComponent<PlayerSpellAnimationPlayer>();
        rpgAnimationPack = GetComponent<PlayerRpgAnimationPackController>();
        weaponDrawSystem = GetComponent<WeaponDrawSystem>();
        // An equipped sword can physically be on the back. Locomotion must follow
        // where the weapon really is, not merely whether the inventory slot is occupied.
        equippedWeaponType = weaponSocket != null &&
                             weaponSocket.HasWeaponEquipped() &&
                             !weaponSocket.IsCarriedOnBack
            ? 1
            : 0;
        anim.SetFloat(HashAnimSpeed, 1f);
    }

    List<Animator> Targets()
    {
        targets.Clear();
        if (anim != null) targets.Add(anim);

        if (ganzVisual == null) ganzVisual = GetComponent<GanzPlayerVisual>();
        Animator visual = ganzVisual != null ? ganzVisual.VisualAnimator : null;
        if (visual != null && visual != anim) targets.Add(visual);

        return targets;
    }

    void SetBool(int hash, bool value)
    {
        foreach (var a in Targets()) a.SetBool(hash, value);
    }

    void SetFloat(int hash, float value)
    {
        foreach (var a in Targets()) a.SetFloat(hash, value);
    }

    void SetFloat(int hash, float value, float dampTime, float deltaTime)
    {
        foreach (var a in Targets()) a.SetFloat(hash, value, dampTime, deltaTime);
    }

    void SetInteger(int hash, int value)
    {
        foreach (var a in Targets()) a.SetInteger(hash, value);
    }

    void SetTrigger(int hash)
    {
        foreach (var a in Targets()) a.SetTrigger(hash);
    }

    void LateUpdate()
    {
        if (controller == null) return;

        // PlayerController.Update() (which computes IsMoving/MoveDirection every frame) bails out
        // as soon as a menu/dialogue opens (GameManager.IsGameplayActive() false) - but this
        // LateUpdate had no such check, so it kept feeding whatever IsMoving was frozen at (e.g.
        // "true" if the player was mid-stride) into the Animator for as long as the menu stayed
        // open. That's why the character's walk animation looked stuck the instant a dialogue
        // opened. Force idle here instead of trusting the now-stale controller state.
        bool gameplayActive = GameManager.Instance == null ||
                              GameManager.Instance.IsGameplayActive();
        bool moving = gameplayActive && controller.IsMoving;
        if (waterBreathing == null) waterBreathing = GetComponent<PlayerWaterBreathing>();
        float playback = waterBreathing != null ? waterBreathing.ActionSpeedMultiplier : 1f;
        if (moving)
            playback *= controller.MecanimLocomotionPlaybackScale;
        playback = Mathf.Clamp(playback, .45f, 3f);
        foreach (Animator target in Targets()) target.speed = playback;
        SetBool(HashMoving, moving);

        // Physical ground contact is authoritative. TriggerLand supplies the
        // animation pack's Jumping=0 / TriggerNumber=18 transition; keep that
        // value stable here without firing a second trigger that can overwrite it.
        if (controller.IsGrounded)
            SetInteger(HashJumping, 0);

        float speed = controller.IsSprinting ? 1f : 0.5f;
        if (moving)
        {
            Vector3 visualDirection = controller.MoveDirection.sqrMagnitude > .01f
                ? controller.MoveDirection.normalized
                : controller.CurrentPlanarMotion.normalized;
            Vector3 localVel = transform.InverseTransformDirection(visualDirection);
            SetFloat(HashVelocityX, localVel.x * speed, 0.1f, Time.deltaTime);
            SetFloat(HashVelocityZ, localVel.z * speed, 0.1f, Time.deltaTime);
        }
        else
        {
            SetFloat(HashVelocityX, 0f, 0.1f, Time.deltaTime);
            SetFloat(HashVelocityZ, 0f, 0.1f, Time.deltaTime);
        }

        // A sword being equipped must not force the player to spend every quiet moment in the
        // pack's rigid 2H combat stance. Keep the real equipment state in equippedWeaponType,
        // but select the relaxed Unarmed-Idle pose while standing still. The real sword
        // parameter is restored as soon as locomotion, an attack or a spell begins.
        if (spellAnimations == null) spellAnimations = GetComponent<PlayerSpellAnimationPlayer>();
        bool actionActive = (combat != null && combat.IsAttacking) ||
                            (spellAnimations != null && spellAnimations.IsPlaying);
        bool weaponInHands = weaponSocket != null &&
                             weaponSocket.HasWeaponEquipped() &&
                             !weaponSocket.IsCarriedOnBack;
        bool combatProfile = weaponDrawSystem != null
            ? weaponDrawSystem.IsCombatProfileActive
            : weaponInHands;
        if (rpgAnimationPack == null)
            rpgAnimationPack = GetComponent<PlayerRpgAnimationPackController>();
        bool canRelax = rpgAnimationPack != null &&
                        rpgAnimationPack.UsePeacefulLocomotion &&
                        !combatProfile &&
                        !weaponInHands &&
                        (stats == null || !stats.IsDead) &&
                        controller.IsGrounded && !actionActive;
        if (canRelax)
        {
            if (relaxedIdleEligibleSince < 0f)
                relaxedIdleEligibleSince = Time.time;
            if (Time.time - relaxedIdleEligibleSince >= RelaxedIdleDelay)
                // The unarmed locomotion set has loose shoulders and arms down.
                // Keep the equipped sword visual untouched; only select the
                // peaceful body language while no hostile is nearby.
                ApplyWeaponAnimatorType(0);
        }
        else
        {
            relaxedIdleEligibleSince = -1f;
            if (stats == null || !stats.IsDead)
                ApplyWeaponAnimatorType(equippedWeaponType);
        }
    }

    // ── Attack ───────────────────────────────────────────────────────
    // unarmed: Action 1-6  (L1,L2,L3,R1,R2,R3)
    // sword:   Action 1-11
    public void TriggerAttack(int actionIndex, float speedMultiplier = 1f,
        bool preserveLowerBody = false)
    {
        ReleaseRelaxedPose();
        // Restore the actual weapon state before firing the attack transition. Standing idle may
        // intentionally be using the relaxed unarmed pose, but a sword attack still needs the
        // controller's complete 2H animation set.
        relaxedIdleEligibleSince = -1f;
        ApplyWeaponAnimatorType(equippedWeaponType);
        if (preserveLowerBody)
        {
            if (rpgAnimationPack == null)
                rpgAnimationPack =
                    GetComponent<PlayerRpgAnimationPackController>();
            if (rpgAnimationPack != null &&
                rpgAnimationPack.PlayMovingSwordAttack(
                    actionIndex, speedMultiplier))
                return;
        }
        attackAnimationSpeed = Mathf.Max(.1f, speedMultiplier);
        SetFloat(HashAnimSpeed, attackAnimationSpeed);
        if (resetAttackSpeed != null) StopCoroutine(resetAttackSpeed);
        resetAttackSpeed = StartCoroutine(ResetAttackAnimationSpeed(.95f / attackAnimationSpeed));
        SetInteger(HashTriggerNumber, 4); // AttackTrigger
        SetInteger(HashAction, actionIndex);
        // The pack's Upperbody layer (the one that lets you swing while walking/running - legs
        // keep animating on the Base Layer underneath) has separate Left/Right run-attack states
        // for UNARMED combos, gated on a "Side" int (1=Left, 2=Right) we never used to set. The
        // 2H-sword run-attack state has no such condition, which is exactly why attacking while
        // moving worked with a sword equipped but silently did nothing while unarmed - Side stayed
        // at its default value, which matched neither the Left(1) nor Right(2) condition, so the
        // AnyState transition into either state never fired. unarmedActions is L1,L2,L3,R1,R2,R3
        // (see CombatSystem), so action 1-3 is Left, 4-6 is Right.
        SetInteger(HashSide, actionIndex <= 3 ? 1 : 2);
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
    }

    IEnumerator ResetAttackAnimationSpeed(float delay)
    {
        yield return new WaitForSeconds(delay);
        attackAnimationSpeed = 1f;
        SetFloat(HashAnimSpeed, 1f);
        resetAttackSpeed = null;
    }

    // ── Hit reactions ─────────────────────────────────────────────────
    // hitType: 1=Front1 2=Front2 3=Back 4=Left 5=Right  (HitType enum)
    public void TriggerGetHit(int hitType = 1)
    {
        ReleaseRelaxedPose();
        SetInteger(HashTriggerNumber, 12); // GetHitTrigger
        SetInteger(HashAction, hitType);
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
    }

    // Computes hitType from attacker world position
    public void TriggerGetHitDirectional(Vector3 attackerWorldPos)
    {
        Vector3 dir = (attackerWorldPos - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, dir);
        float cross = transform.forward.x * dir.z - transform.forward.z * dir.x;

        int hitType;
        if (dot >= 0.5f)       hitType = 1; // Front1
        else if (dot >= 0f)    hitType = 2; // Front2
        else if (cross > 0f)   hitType = 5; // Right
        else if (cross < 0f)   hitType = 4; // Left
        else                   hitType = 3; // Back

        TriggerGetHit(hitType);
    }

    // ── Knockback ─────────────────────────────────────────────────────
    // knockbackType: 1 or 2
    public void TriggerKnockback(int knockbackType = 1)
    {
        ReleaseRelaxedPose();
        SetInteger(HashTriggerNumber, 26); // KnockbackTrigger
        SetInteger(HashAction, knockbackType);
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
    }

    // ── Knockdown + auto Getup ────────────────────────────────────────
    public void TriggerKnockdown(bool autoGetup = true)
    {
        ReleaseRelaxedPose();
        SetInteger(HashTriggerNumber, 27); // KnockdownTrigger
        SetInteger(HashAction, 1);
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
        if (autoGetup)
            StartCoroutine(AutoGetup(2.5f));
    }

    IEnumerator AutoGetup(float delay)
    {
        yield return new WaitForSeconds(delay);
        TriggerGetup();
    }

    public void TriggerGetup()
    {
        ReleaseRelaxedPose();
        // Getup: trigger ActionTrigger (2) with Action=0.
        SetInteger(HashTriggerNumber, 2); // ActionTrigger (returns to idle)
        SetInteger(HashAction, 0);
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
    }

    // ── Dodge/DiveRoll ────────────────────────────────────────────────
    public void TriggerDiveRoll()
    {
        ReleaseRelaxedPose();
        SetInteger(HashTriggerNumber, 28); // DiveRollTrigger
        SetInteger(HashAction, 1);         // DiveRoll1
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
    }

    // ── Weapon Sheath/Draw ────────────────────────────────────────────
    public void TriggerWeaponSheath()
    {
        ReleaseRelaxedPose();
        SetInteger(HashTriggerNumber, 15); // WeaponSheathTrigger
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
    }

    public void TriggerWeaponUnsheath()
    {
        ReleaseRelaxedPose();
        SetInteger(HashTriggerNumber, 16); // WeaponUnsheathTrigger
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
    }

    // ── Jump / Fall / Land ────────────────────────────────────────────
    public void TriggerJump()
    {
        ReleaseRelaxedPose();
        SetInteger(HashTriggerNumber, 18);
        SetInteger(HashJumping, 1);
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
    }

    public void TriggerFall()
    {
        ReleaseRelaxedPose();
        SetInteger(HashTriggerNumber, 18);
        SetInteger(HashJumping, 2);
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
    }

    public void TriggerLand()
    {
        ReleaseRelaxedPose();
        SetInteger(HashTriggerNumber, 18);
        SetInteger(HashJumping, 0);
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
    }

    // Kept for callers that need an immediate grounded correction. Do not fire
    // ActionTrigger here: doing so in the landing frame replaces TriggerLand's
    // TriggerNumber=18 before the Animator can consume it.
    public void ForceGroundedState()
    {
        SetInteger(HashJumping, 0);
    }

    // Keep the animator out of its jump/fall branch while the controller is
    // physically standing on any collider (bridges included).
    public void SetGroundedState() => SetInteger(HashJumping, 0);

    // ── Death / Revive ────────────────────────────────────────────────
    public void TriggerDeath()
    {
        ReleaseRelaxedPose();
        SetInteger(HashTriggerNumber, 20); // DeathTrigger
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
    }

    public void TriggerRevive()
    {
        ReleaseRelaxedPose();
        SetInteger(HashTriggerNumber, 21); // ReviveTrigger
        SetTrigger(HashTrigger);
        StartCoroutine(ResetTriggerNumber());
    }

    // ── Weapon type ───────────────────────────────────────────────────
    // 0=Unarmed  1=2H Sword
    public void SetWeaponType(int type)
    {
        equippedWeaponType = type == 0 ? 0 : 1;
        ApplyWeaponAnimatorType(equippedWeaponType);
    }

    void ApplyWeaponAnimatorType(int type)
    {
        int normalizedType = type == 0 ? 0 : 1;
        SetInteger(HashWeapon, normalizedType);
        SetInteger(HashRightWeapon, normalizedType);
    }

    public void SetMoveParams(float speed) { }

    void ReleaseRelaxedPose()
    {
        if (rpgAnimationPack == null)
            rpgAnimationPack = GetComponent<PlayerRpgAnimationPackController>();
        rpgAnimationPack?.ReleaseRelaxedPoseForAction();
    }

    // ── Animation event receivers ─────────────────────────────────────
    void Hit()          => combat?.ProcessAttackHit();
    void FootL()        { }
    void FootR()        { }
    void Land()         { }
    void Shoot()        { }
    void WeaponSwitch() => weaponSocket?.ToggleWeaponVisibility();
    void OnAttackHit()  => combat?.ProcessAttackHit();
    void OnWeaponDrawn() => weaponSocket?.ShowWeapon();

    IEnumerator ResetTriggerNumber()
    {
        yield return null;
        SetInteger(HashTriggerNumber, 0);
    }
}

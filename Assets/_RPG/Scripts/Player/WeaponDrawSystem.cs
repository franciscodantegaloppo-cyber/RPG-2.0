using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Keeps the equipped weapon visible on the back while the area is safe and
// automatically draws it when PlayerRpgAnimationPackController detects danger.
// G remains available as a manual request, but nearby danger always wins.
public class WeaponDrawSystem : MonoBehaviour
{
    public enum WeaponProfile
    {
        Relaxed,
        Combat
    }

    PlayerAnimatorBridge animBridge;
    WeaponSocket weaponSocket;
    PlayerStats stats;
    PlayerRpgAnimationPackController awareness;
    CombatSystem combat;

    bool weaponDrawn;
    float sheathCooldown;
    float lastWeaponUseTime = -100f;
    Coroutine transitionRoutine;
    const float SafeSheathDelay = 5f;
    WeaponProfile currentProfile = WeaponProfile.Relaxed;

    public WeaponProfile CurrentProfile => currentProfile;
    public bool IsCombatProfileActive =>
        currentProfile == WeaponProfile.Combat;

    void Awake()
    {
        animBridge   = GetComponent<PlayerAnimatorBridge>();
        weaponSocket = GetComponent<WeaponSocket>();
        stats        = GetComponent<PlayerStats>();
        awareness    = GetComponent<PlayerRpgAnimationPackController>();
        combat       = GetComponent<CombatSystem>();
        if (weaponSocket != null && weaponSocket.HasWeaponEquipped() &&
            !weaponSocket.IsCarriedOnBack)
        {
            currentProfile = WeaponProfile.Combat;
            weaponDrawn = true;
            lastWeaponUseTime = Time.time;
        }
    }

    void Update()
    {
        if (sheathCooldown > 0f) sheathCooldown -= Time.deltaTime;
        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive()) return;
        if (stats.IsDead) return;

        if (awareness == null)
            awareness = GetComponent<PlayerRpgAnimationPackController>();

        bool danger = awareness != null && awareness.ThreatNearby;
        if (danger)
            ActivateCombatProfile(SafeSheathDelay);

        if (weaponSocket != null && weaponSocket.HasWeaponEquipped())
        {
            if (currentProfile == WeaponProfile.Combat &&
                !weaponDrawn && transitionRoutine == null)
                BeginAutomaticTransition(true);
            else if (!danger &&
                     currentProfile == WeaponProfile.Combat &&
                     weaponDrawn && transitionRoutine == null &&
                     sheathCooldown <= 0f &&
                     Time.time - lastWeaponUseTime >= SafeSheathDelay &&
                     (combat == null || !combat.IsAttacking))
                BeginAutomaticTransition(false);
            else if (!danger &&
                     currentProfile == WeaponProfile.Relaxed &&
                     !weaponDrawn && transitionRoutine == null &&
                     !weaponSocket.IsCarriedOnBack)
                weaponSocket.HideWeapon();
        }
        else if (!danger &&
                 currentProfile == WeaponProfile.Combat &&
                 Time.time - lastWeaponUseTime >= SafeSheathDelay &&
                 (combat == null || !combat.IsAttacking))
        {
            // Unarmed attacks also use the combat body language, but there is
            // no weapon transition coroutine available to return us to the
            // relaxed profile once the five-second combat window expires.
            currentProfile = WeaponProfile.Relaxed;
            animBridge?.SetWeaponType(0);
            weaponDrawn = false;
        }

        var kb = Keyboard.current;
        if (kb != null && kb.gKey.wasPressedThisFrame &&
            sheathCooldown <= 0f && transitionRoutine == null)
        {
            if (danger)
                BeginAutomaticTransition(true);
            else
                BeginAutomaticTransition(!weaponDrawn);
        }
    }

    void BeginAutomaticTransition(bool draw)
    {
        if (weaponSocket == null || !weaponSocket.HasWeaponEquipped())
            return;
        transitionRoutine = StartCoroutine(
            draw ? DrawRoutine() : SheathRoutine());
    }

    IEnumerator DrawRoutine()
    {
        sheathCooldown = .75f;
        // Manual draw and automatic threat draw both deserve a stable armed
        // window. Otherwise a safe-area draw can finish and immediately start
        // sheathing before the player has time to run or attack.
        ActivateCombatProfile(SafeSheathDelay);
        animBridge?.TriggerWeaponUnsheath();
        weaponSocket?.AnimateWeaponToHand(.44f);

        // The socket keeps the blade on the back until the reaching hand makes
        // contact (58% of its transition). Do not select armed locomotion before
        // that instant, otherwise the hands hold an invisible sword while running.
        yield return new WaitForSeconds(.26f);
        animBridge?.SetWeaponType(1);
        weaponDrawn = true;

        yield return new WaitForSeconds(.30f);
        transitionRoutine = null;
    }

    IEnumerator SheathRoutine()
    {
        sheathCooldown = .75f;
        animBridge?.TriggerWeaponSheath();
        weaponSocket?.AnimateWeaponToBack(.48f);

        // Keep the armed pose while the sword is still attached to the hand.
        // The socket reaches the back at 78% of this transition.
        yield return new WaitForSeconds(.38f);
        animBridge?.SetWeaponType(0);
        weaponDrawn = false;
        yield return new WaitForSeconds(.20f);
        currentProfile = WeaponProfile.Relaxed;
        transitionRoutine = null;
    }

    public bool IsWeaponDrawn() => weaponDrawn;

    // Called when a weapon is equipped from inventory to auto-draw it
    public void OnWeaponEquipped()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
        weaponSocket?.CancelCarryTransition();
        weaponDrawn = false;
        currentProfile = WeaponProfile.Relaxed;
        animBridge?.SetWeaponType(0);
        weaponSocket?.HideWeapon();
    }

    // Air swings and scripted attacks must never leave the sword on the back.
    public void EnsureWeaponInHandImmediate()
    {
        if (weaponSocket == null || !weaponSocket.HasWeaponEquipped())
            return;
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
        weaponSocket.CancelCarryTransition();
        ActivateCombatProfile(SafeSheathDelay);
        weaponSocket.ShowWeapon();
        animBridge?.SetWeaponType(1);
        weaponDrawn = true;
        sheathCooldown = .65f;
    }

    public void NotifyWeaponUsed()
    {
        ActivateCombatProfile(SafeSheathDelay);
        sheathCooldown = Mathf.Max(sheathCooldown, .65f);
    }

    public void ActivateCombatProfile(float minimumDuration = SafeSheathDelay)
    {
        currentProfile = WeaponProfile.Combat;
        // This timestamp is refreshed by every attack and every frame in which
        // danger remains nearby. The five-second calm period therefore begins
        // only after the final hit or after the final hostile leaves.
        lastWeaponUseTime = Mathf.Max(lastWeaponUseTime,
            Time.time - SafeSheathDelay + Mathf.Max(0f, minimumDuration));
    }

    // Called by EquipmentManager.Unequip() when the weapon slot is cleared. Setting the Weapon
    // animator int to 0 alone doesn't move the character out of the 2H-sword idle/walk pose in
    // this pack's controller - only the WeaponSheath trigger actually plays that transition, the
    // int just picks which locomotion set a *subsequent* transition lands in. Also resyncs this
    // component's own weaponDrawn flag, which EquipmentManager has no other way to reach - without
    // this, unequipping while drawn left the animator stuck showing a held sword forever after.
    public void ForceSheath()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
        weaponSocket?.CancelCarryTransition();
        if (weaponDrawn)
            animBridge?.TriggerWeaponSheath();
        weaponSocket?.HideWeapon();
        animBridge?.SetWeaponType(0);
        weaponDrawn = false;
        currentProfile = WeaponProfile.Relaxed;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

// G key: draws or sheaths the equipped weapon. F is reserved for the fire spell.
// Plays the correct animation and changes the animator Weapon parameter.
public class WeaponDrawSystem : MonoBehaviour
{
    PlayerAnimatorBridge animBridge;
    WeaponSocket weaponSocket;
    PlayerStats stats;

    bool weaponDrawn;
    float sheathCooldown;

    void Awake()
    {
        animBridge   = GetComponent<PlayerAnimatorBridge>();
        weaponSocket = GetComponent<WeaponSocket>();
        stats        = GetComponent<PlayerStats>();
    }

    void Update()
    {
        if (sheathCooldown > 0f) sheathCooldown -= Time.deltaTime;
        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive()) return;
        if (stats.IsDead) return;

        var kb = Keyboard.current;
        if (kb != null && kb.gKey.wasPressedThisFrame && sheathCooldown <= 0f)
            ToggleWeapon();
    }

    void ToggleWeapon()
    {
        sheathCooldown = 1.2f;

        if (!weaponDrawn)
        {
            // Draw weapon
            if (weaponSocket == null || !weaponSocket.HasWeaponEquipped()) return;
            weaponSocket.ShowWeapon();
            animBridge?.TriggerWeaponUnsheath();
            animBridge?.SetWeaponType(1); // 2H sword
            weaponDrawn = true;
        }
        else
        {
            // Sheath weapon
            animBridge?.TriggerWeaponSheath();
            animBridge?.SetWeaponType(0); // unarmed
            weaponDrawn = false;
            // Hide weapon mesh at end of animation (handled by WeaponSwitch anim event)
        }
    }

    public bool IsWeaponDrawn() => weaponDrawn;

    // Called when a weapon is equipped from inventory to auto-draw it
    public void OnWeaponEquipped()
    {
        if (weaponDrawn) return;
        ToggleWeapon();
    }

    // Called by EquipmentManager.Unequip() when the weapon slot is cleared. Setting the Weapon
    // animator int to 0 alone doesn't move the character out of the 2H-sword idle/walk pose in
    // this pack's controller - only the WeaponSheath trigger actually plays that transition, the
    // int just picks which locomotion set a *subsequent* transition lands in. Also resyncs this
    // component's own weaponDrawn flag, which EquipmentManager has no other way to reach - without
    // this, unequipping while drawn left the animator stuck showing a held sword forever after.
    public void ForceSheath()
    {
        if (weaponDrawn)
            animBridge?.TriggerWeaponSheath();
        animBridge?.SetWeaponType(0);
        weaponDrawn = false;
    }
}

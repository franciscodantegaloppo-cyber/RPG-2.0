using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CombatSystem : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] float hitRadius = 1.5f;
    [SerializeField] float hitOffset = 1f;
    [SerializeField] LayerMask enemyMask;
    [SerializeField] float comboWindow = 1.0f;    // time to chain next hit
    [SerializeField] float attackCooldown = 0.35f; // min time between clicks
    [SerializeField] float attackStaminaCost = 15f;
    [Tooltip("Approximate length of the swing + recovery animation. WeaponSocket lets the sword follow the hand bone's real rotation for this long after a swing starts, instead of forcing it back to its rest pose - should be a bit longer than attackCooldown or the blade snaps upright mid-recovery.")]
    [SerializeField] float attackAnimationDuration = 0.9f;

    [Header("HitStop")]
    [SerializeField] float hitStopDuration = 0.05f;

    PlayerStats stats;
    PlayerAnimatorBridge animBridge;
    WeaponSocket weaponSocket;
    PlayerWaterBreathing waterBreathing;
    PlayerSpellCaster spellCaster;

    // Unarmed attacks cycle through all 6 actions (L1,L2,L3,R1,R2,R3)
    // Sword cycles through up to 11 attacks
    static readonly int[] unarmedActions = { 1, 2, 3, 4, 5, 6 };
    static readonly int[] swordActions   = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

    int comboStep;
    int activeSwordAction;
    float lastAttackTime;
    bool attackLocked;
    bool activeAttackHasSword;
    bool slashPlayedForAttack;

    // Lets WeaponSocket know when a swing animation is playing so it can stop forcing the
    // weapon's rest orientation and let it follow the hand bone's real animated rotation.
    // Uses attackAnimationDuration rather than attackLocked/attackCooldown: the cooldown only
    // gates how soon the next click can chain a combo hit, which is shorter than how long the
    // swing's recovery actually keeps animating the hand - using the cooldown here made the
    // blade snap back to its rest pose while the arm was still mid-recovery.
    float ActionSpeedMultiplier
    {
        get
        {
            if (waterBreathing == null) waterBreathing = GetComponent<PlayerWaterBreathing>();
            return waterBreathing != null ? waterBreathing.ActionSpeedMultiplier : 1f;
        }
    }
    float WeaponAttackSpeedMultiplier
    {
        get
        {
            WeaponData weapon = weaponSocket?.GetCurrentWeapon()?.Data;
            return weapon != null ? Mathf.Max(.1f, weapon.attackSpeedMultiplier) : 1f;
        }
    }
    public bool IsAttacking => Time.time - lastAttackTime <
        attackAnimationDuration / Mathf.Max(0.01f, ActionSpeedMultiplier * WeaponAttackSpeedMultiplier);

    void Awake()
    {
        stats       = GetComponent<PlayerStats>();
        animBridge  = GetComponent<PlayerAnimatorBridge>();
        weaponSocket = GetComponent<WeaponSocket>();
        waterBreathing = GetComponent<PlayerWaterBreathing>();
        spellCaster = GetComponent<PlayerSpellCaster>();

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            enemyMask.value |= 1 << enemyLayer;
    }

    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive()) return;
        if (stats.IsDead) return;
        if (stats.IsKnockedDown) return;
        if (stats.IsRolling) return;

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !attackLocked)
        {
            if (spellCaster == null) spellCaster = GetComponent<PlayerSpellCaster>();
            if (spellCaster != null && spellCaster.HasEquippedSpell)
            {
                spellCaster.CastEquipped();
                return;
            }
            if (stats.HasStamina(attackStaminaCost))
                PerformAttack();
        }
    }

    void PerformAttack()
    {
        // Reset combo if too much time passed since last hit
        if (Time.time - lastAttackTime >
            comboWindow / Mathf.Max(0.01f, ActionSpeedMultiplier * WeaponAttackSpeedMultiplier))
            comboStep = 0;

        bool hasSword = weaponSocket?.HasWeaponEquipped() ?? false;
        int[] actions = hasSword ? swordActions : unarmedActions;
        int action = actions[comboStep % actions.Length];
        activeSwordAction = action;
        activeAttackHasSword = hasSword;
        slashPlayedForAttack = false;

        stats.DrainStamina(attackStaminaCost);
        animBridge?.TriggerAttack(action, WeaponAttackSpeedMultiplier);

        var weapon = weaponSocket?.GetCurrentWeapon();
        AudioManager.Instance?.PlaySFX(weapon?.GetSwingSound());

        comboStep++;
        lastAttackTime = Time.time;
        attackLocked = true;

        if (hasSword && weaponSocket != null)
        {
            float visualAttackSpeed =
                ActionSpeedMultiplier * WeaponAttackSpeedMultiplier;
            float visualAttackDuration = attackAnimationDuration /
                Mathf.Max(.01f, visualAttackSpeed);
            weaponSocket.BeginMeasuredSlash(visualAttackDuration, visualAttackSpeed);
            // The slash is now the complete trajectory painted by the real sword tip.
            // Prevent the old single prefab from being spawned again at the hit event.
            slashPlayedForAttack = true;
        }

        ItemInstance weaponInstance = EquipmentManager.Instance?.GetEquippedInstance(ItemType.Weapon);
        float atkSpeedPct = weaponInstance?.GetAffixValue(AffixType.AttackSpeedPercent) ?? 0f;
        float effectiveCooldown = attackCooldown / (1f + atkSpeedPct / 100f) /
            Mathf.Max(0.01f, ActionSpeedMultiplier * WeaponAttackSpeedMultiplier);
        StartCoroutine(UnlockAttack(effectiveCooldown));
    }

    IEnumerator UnlockAttack(float cooldown)
    {
        yield return new WaitForSeconds(cooldown);
        attackLocked = false;
    }

    public void ProcessAttackHit()
    {
        ItemInstance weaponInstance = EquipmentManager.Instance?.GetEquippedInstance(ItemType.Weapon);
        float rangePct = weaponInstance?.GetAffixValue(AffixType.WeaponRangePercent) ?? 0f;
        float rangeMultiplier = 1f + rangePct / 100f;

        if (activeAttackHasSword && !slashPlayedForAttack &&
            weaponSocket?.GetCurrentWeapon() != null)
        {
            slashPlayedForAttack = true;
            StylizedSwordSlashVfx.Play(transform, weaponSocket, activeSwordAction,
                rangeMultiplier,
                ActionSpeedMultiplier * WeaponAttackSpeedMultiplier);
        }

        Vector3 origin = transform.position + transform.forward * (hitOffset * rangeMultiplier) + Vector3.up * 1f;
        Collider[] hits = Physics.OverlapSphere(origin, hitRadius * rangeMultiplier, enemyMask);

        var weapon = weaponSocket?.GetCurrentWeapon();
        float baseDamage = (stats != null ? stats.TotalAttack : 0f) + (weapon?.GetDamage() ?? 0f);

        float physPct = weaponInstance?.GetAffixValue(AffixType.PhysicalDamagePercent) ?? 0f;
        float magicPct = weaponInstance?.GetAffixValue(AffixType.MagicDamagePercent) ?? 0f;
        float damage = baseDamage * (1f + (physPct + magicPct) / 100f);

        float critChance = stats != null ? stats.CriticalChancePercent : 5f;
        float critDamage = stats != null ? stats.CriticalDamagePercent : 50f;
        bool isCrit = Random.value * 100f <= critChance;
        if (isCrit)
            damage *= 1f + critDamage / 100f;

        // Filo increases the weapon's own penetration on top of any flat penetration affix.
        float totalPenetration = stats != null
            ? stats.ArmorPenetration
            : (weaponInstance?.GetAffixValue(AffixType.ArmorPenetrationFlat) ?? 0f) +
              (weaponInstance?.GetAffixValue(AffixType.Filo) ?? 0f);

        bool hitSomething = false;
        HashSet<EnemyStats> damagedEnemies = new HashSet<EnemyStats>();
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponentInParent<EnemyStats>();
            if (enemy == null || !damagedEnemies.Add(enemy)) continue;

            float vsTypeBonus = 0f;
            if (weaponInstance != null)
            {
                switch (enemy.Type)
                {
                    case CreatureType.Undead: vsTypeBonus = weaponInstance.GetAffixValue(AffixType.DamageVsUndeadPercent); break;
                    case CreatureType.Beast: vsTypeBonus = weaponInstance.GetAffixValue(AffixType.DamageVsBeastPercent); break;
                    case CreatureType.Boss: vsTypeBonus = weaponInstance.GetAffixValue(AffixType.DamageVsBossPercent); break;
                }
            }

            float typedDamage = damage * (1f + vsTypeBonus / 100f);

            // Diminishing-returns mitigation (100 armor halves damage); penetration/Filo reduce
            // the armor value used in the formula before mitigation is applied.
            float effectiveArmor = Mathf.Max(0f, enemy.Armor - totalPenetration);
            float mitigation = 100f / (100f + effectiveArmor);
            float finalDamage = typedDamage * mitigation;

            enemy.TakeDamage(finalDamage, transform.position);
            Vector3 impactPoint = ResolveVisibleImpactPoint(hit, enemy, origin);
            if (weapon != null)
                DarkEnergyVfx.PlaySwordBloodImpact(impactPoint, transform.forward);
            if (weaponInstance != null && weaponInstance.darkEnergyEnchanted)
            {
                DarkEnergyBurn.Apply(enemy, 20f, 5f);
                DarkEnergyVfx.PlayImpact(impactPoint);
            }
            DamageNumberPool.Instance?.ShowDamage(hit.transform.position, finalDamage, isCrit);
            AudioManager.Instance?.PlaySFXAtPoint(weapon?.GetHitSound(), hit.transform.position);
            hitSomething = true;
        }

        if (hitSomething)
        {
            Camera.main?.GetComponent<ThirdPersonCamera>()?.AddImpulse(
                isCrit ? .16f : .085f, isCrit ? .13f : .085f);
            StartCoroutine(HitStop());
        }
    }

    static Vector3 ResolveVisibleImpactPoint(Collider hit, EnemyStats enemy, Vector3 attackOrigin)
    {
        Renderer[] renderers = enemy != null
            ? enemy.GetComponentsInChildren<Renderer>(true)
            : System.Array.Empty<Renderer>();

        bool hasVisualBounds = false;
        Bounds visualBounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;
            if (renderer.GetComponentInParent<SkeletonWeaponMarker>() != null)
                continue;
            if (!hasVisualBounds)
            {
                visualBounds = renderer.bounds;
                hasVisualBounds = true;
            }
            else visualBounds.Encapsulate(renderer.bounds);
        }

        Vector3 closest = hit != null ? hit.ClosestPoint(attackOrigin) : attackOrigin;
        // ClosestPoint returns the query point unchanged when it starts inside a collider.
        // That placed blood on the player or beside the target. Only accept it when it is also
        // inside the enemy's rendered body; otherwise intersect the visible bounds and finally
        // fall back to their center, which is always preferable to an off-body splash.
        if (hasVisualBounds && (closest - attackOrigin).sqrMagnitude > .0004f &&
            visualBounds.Contains(closest))
            return closest;

        if (hasVisualBounds)
        {
            Vector3 direction = visualBounds.center - attackOrigin;
            if (direction.sqrMagnitude > .0001f)
            {
                Ray ray = new Ray(attackOrigin, direction.normalized);
                if (visualBounds.IntersectRay(ray, out float distance))
                {
                    Vector3 point = ray.GetPoint(distance);
                    if (visualBounds.Contains(point))
                        return point;
                }
            }
            return visualBounds.center;
        }

        return hit != null ? hit.bounds.center : (enemy != null ? enemy.transform.position : attackOrigin);
    }

    IEnumerator HitStop()
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = 1f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 origin = transform.position + transform.forward * hitOffset + Vector3.up * 1f;
        Gizmos.DrawWireSphere(origin, hitRadius);
    }
}

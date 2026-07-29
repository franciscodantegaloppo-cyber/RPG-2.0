using System.Collections;
using UnityEngine;

public class NPCMeleeDefender : MonoBehaviour
{
    [SerializeField] float detectionRadius = 10f;
    [SerializeField] float attackRange = 2.5f;
    [SerializeField] float attackDamage = 40f;
    [SerializeField] float attackCooldown = 1.45f;
    [SerializeField] float combatMoveSpeed = .85f;
    [SerializeField] RuntimeAnimatorController combatController;
    [SerializeField] WeaponData weaponDataTemplate;
    [SerializeField] GameObject weaponPrefab;
    [SerializeField] WeaponType weaponType = WeaponType.Sword1H;
    [SerializeField, Range(1, 11)] int swordAttackAction = 7;

    static readonly int HashTrigger = Animator.StringToHash("Trigger");
    static readonly int HashTriggerNumber = Animator.StringToHash("TriggerNumber");
    static readonly int HashAction = Animator.StringToHash("Action");
    static readonly int HashSide = Animator.StringToHash("Side");
    static readonly int HashWeapon = Animator.StringToHash("Weapon");
    static readonly int HashRightWeapon = Animator.StringToHash("RightWeapon");
    static readonly int HashVelocityZ = Animator.StringToHash("Velocity Z");

    NPCWander wander;
    Animator animator;
    WeaponSocket socket;
    EnemyStats target;
    RuntimeAnimatorController peacefulController;
    WeaponData runtimeWeapon;
    TonioHouseRoutine tonioHouseRoutine;
    float nextScan;
    float nextAttack;
    bool fighting;
    bool attacking;

    void Awake()
    {
#if UNITY_EDITOR
        EnsureEditorBlacksmithDefaults();
#endif
        wander = GetComponent<NPCWander>();
        animator = GetComponentInChildren<Animator>(true);
        socket = GetComponent<WeaponSocket>();
        if (socket == null) socket = gameObject.AddComponent<WeaponSocket>();
        tonioHouseRoutine = GetComponent<TonioHouseRoutine>();
        if (animator != null) peacefulController = animator.runtimeAnimatorController;
        BuildRuntimeWeapon();
    }

    void Start()
    {
        if (tonioHouseRoutine == null)
            tonioHouseRoutine = GetComponent<TonioHouseRoutine>();

        // NPCWander selects the relaxed unarmed controller in Awake. Cache it after every
        // Awake has completed so a previously serialized combat pose cannot become permanent.
        if (animator != null)
            peacefulController = animator.runtimeAnimatorController;
        EnsureNpcWeapon();
        RestoreRelaxedPose();
    }

    void OnDisable() => LeaveCombat();

    void Update()
    {
        // Tonio must remain a peaceful quest giver while sheltered inside his house.
        // Do this before scanning so nearby enemies cannot interrupt his indoor route,
        // equip his weapon or leave him stuck in a combat pose.
        if (tonioHouseRoutine != null &&
            (tonioHouseRoutine.IsInsideHouse || tonioHouseRoutine.IsRouteActive))
        {
            target = null;
            LeaveCombat();
            return;
        }

        if (Time.time >= nextScan)
        {
            nextScan = Time.time + .28f;
            if (!IsValidTarget(target)) target = FindClosestEnemy();
        }

        if (!IsValidTarget(target))
        {
            LeaveCombat();
            return;
        }

        EnterCombat();
        Vector3 delta = target.transform.position - transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (delta.sqrMagnitude > .01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(delta), 540f * Time.deltaTime);

        if (attacking)
            return;

        if (distance > attackRange)
        {
            wander?.MoveForCombat(delta.normalized, combatMoveSpeed);
            return;
        }

        wander?.StopCombatMove();
        if (Time.time >= nextAttack)
            StartCoroutine(AttackRoutine(target));
    }

    EnemyStats FindClosestEnemy()
    {
        EnemyStats best = null;
        float bestDistance = detectionRadius * detectionRadius;
        foreach (EnemyStats enemy in FindObjectsByType<EnemyStats>(FindObjectsInactive.Exclude))
        {
            if (!IsHostile(enemy)) continue;
            float distance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; best = enemy; }
        }
        return best;
    }

    bool IsValidTarget(EnemyStats enemy) =>
        IsHostile(enemy) && (enemy.transform.position - transform.position).sqrMagnitude <=
        detectionRadius * detectionRadius;

    static bool IsHostile(EnemyStats enemy)
    {
        if (enemy == null || enemy.IsDead) return false;
        GameObject go = enemy.gameObject;
        AnimalAI animal = go.GetComponent<AnimalAI>();
        if (animal != null && !animal.IsCombatHostile)
            return false;
        return go.GetComponent<NPCWander>() == null &&
               go.GetComponent<TonioQuestGiver>() == null &&
               go.GetComponent<NahueQuestGiver>() == null &&
               go.GetComponent<NPCHerrero>() == null &&
               go.GetComponent<NPCMerchant>() == null;
    }

    void EnterCombat()
    {
        if (fighting) return;
        fighting = true;
        wander?.PauseForInteraction();
        EnsureNpcWeapon();
        if (animator != null && combatController != null)
            animator.runtimeAnimatorController = combatController;
        SetInt(HashWeapon, weaponType == WeaponType.Sword2H ? 2 : 1);
        SetInt(HashRightWeapon, 1);
        SetInt(HashTriggerNumber, 16); // WeaponUnsheathTrigger
        if (HasParameter(HashTrigger, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(HashTrigger);
        socket?.AnimateWeaponToHand(.44f);
    }

    void LeaveCombat()
    {
        if (!fighting) return;
        fighting = false;
        attacking = false;
        target = null;
        StopAllCoroutines();
        wander?.StopCombatMove();
        RestoreRelaxedPose();
        wander?.ResumeWander();
    }

    IEnumerator AttackRoutine(EnemyStats attacked)
    {
        attacking = true;
        nextAttack = Time.time + attackCooldown;

        wander?.StopCombatMove();
        if (animator != null && combatController != null)
            animator.runtimeAnimatorController = combatController;

        EnsureNpcWeapon();
        if (socket != null &&
            (socket.IsCarriedOnBack || socket.IsCarryTransitioning))
        {
            socket.AnimateWeaponToHand(.38f);
            yield return new WaitForSeconds(.4f);
        }
        socket?.ShowWeapon();
        socket?.SetAnimationDrivenGrip(true);

        SetInt(HashWeapon, weaponType == WeaponType.Sword2H ? 2 : 1);
        SetInt(HashRightWeapon, 1);
        SetInt(HashAction, swordAttackAction);
        SetInt(HashSide, 2);
        SetInt(HashTriggerNumber, 4);
        bool triggered =
            HasParameter(HashTrigger, AnimatorControllerParameterType.Trigger);
        if (triggered)
            animator.SetTrigger(HashTrigger);
        // The controller normally reaches this state through TriggerNumber=4.
        // A direct cross-fade makes the blacksmith's two-handed swing reliable
        // even when its previous peaceful controller was swapped this frame.
        if (weaponType == WeaponType.Sword2H)
            CrossFadeIfPresent("2Hand-Sword-Attack" +
                               swordAttackAction, .08f);
        yield return new WaitForSeconds(.42f);
        if (IsValidTarget(attacked) &&
            Vector3.Distance(transform.position, attacked.transform.position) <= attackRange + .35f)
            attacked.TakeDamage(attackDamage, transform.position);

        yield return new WaitForSeconds(.48f);
        socket?.SetAnimationDrivenGrip(false);
        SetInt(HashTriggerNumber, 2); // Return to the armed idle.
        if (HasParameter(HashTrigger, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(HashTrigger);
        attacking = false;
    }

    void RestoreRelaxedPose()
    {
        EnsureNpcWeapon();
        socket?.SetAnimationDrivenGrip(false);
        socket?.AnimateWeaponToBack(.46f);
        if (animator == null)
            return;

        if (peacefulController != null && animator.runtimeAnimatorController != peacefulController)
            animator.runtimeAnimatorController = peacefulController;
        SetInt(HashWeapon, 0);
        SetInt(HashRightWeapon, 0);
        SetFloat(HashVelocityZ, 0f);
        int idle = Animator.StringToHash("Idle");
        if (animator.layerCount > 0 && animator.HasState(0, idle))
            animator.CrossFade(idle, .16f);
    }

    void EnsureNpcWeapon()
    {
        if (socket == null || socket.HasWeaponEquipped())
            return;
        // Prefer the NPC-specific prefab. This prevents a stale shared
        // WeaponData reference from showing another NPC's sword.
        WeaponData weapon = weaponPrefab != null ? runtimeWeapon : weaponDataTemplate;
        if (weapon != null && weapon.weaponPrefab != null)
            socket.AttachWeapon(weapon.weaponPrefab, weapon);
    }

    void BuildRuntimeWeapon()
    {
        if (weaponDataTemplate != null || weaponPrefab == null) return;
        runtimeWeapon = ScriptableObject.CreateInstance<WeaponData>();
        runtimeWeapon.weaponID = name + "_npc_weapon";
        runtimeWeapon.weaponName = name + " Weapon";
        runtimeWeapon.weaponType = weaponType;
        runtimeWeapon.weaponPrefab = weaponPrefab;
        runtimeWeapon.baseDamage = attackDamage;
        runtimeWeapon.attackRange = attackRange;
        runtimeWeapon.attackCooldown = attackCooldown;
    }

    bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == hash && parameter.type == type) return true;
        return false;
    }

    void SetInt(int hash, int value)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Int)) animator.SetInteger(hash, value);
    }

    void SetFloat(int hash, float value)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Float)) animator.SetFloat(hash, value);
    }

    void CrossFadeIfPresent(string stateName, float duration)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;
        int shortHash = Animator.StringToHash(stateName);
        if (animator.HasState(0, shortHash))
            animator.CrossFade(shortHash, duration, 0, 0f);
    }

#if UNITY_EDITOR
    void EnsureEditorBlacksmithDefaults()
    {
        if (GetComponent<NPCHerrero>() == null)
            return;
        if (combatController == null)
            combatController =
                UnityEditor.AssetDatabase.LoadAssetAtPath
                    <RuntimeAnimatorController>(
                    "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animation Controller/RPG-Character-Animation-Controller.controller");
        if (weaponPrefab == null)
            weaponPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/URP GanzSe Free Modular Character Pack/Prefabs/GREAT SWORDS/FREE GREAT SWORD 4 COLOR 1.prefab");
        weaponType = WeaponType.Sword2H;
        swordAttackAction = 7;
    }
#endif
}

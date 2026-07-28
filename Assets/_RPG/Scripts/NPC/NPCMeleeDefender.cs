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
    float nextScan;
    float nextAttack;
    bool fighting;
    bool attacking;

    void Awake()
    {
        wander = GetComponent<NPCWander>();
        animator = GetComponentInChildren<Animator>(true);
        socket = GetComponent<WeaponSocket>();
        if (socket == null) socket = gameObject.AddComponent<WeaponSocket>();
        if (animator != null) peacefulController = animator.runtimeAnimatorController;
        BuildRuntimeWeapon();
    }

    void Start()
    {
        // NPCWander selects the relaxed unarmed controller in Awake. Cache it after every
        // Awake has completed so a previously serialized combat pose cannot become permanent.
        if (animator != null)
            peacefulController = animator.runtimeAnimatorController;
        RestoreRelaxedPose();
    }

    void OnDisable() => LeaveCombat();

    void Update()
    {
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
        socket?.DetachWeapon();
        SetInt(HashWeapon, 0);
        SetInt(HashRightWeapon, 0);
    }

    void LeaveCombat()
    {
        if (!fighting) return;
        fighting = false;
        attacking = false;
        target = null;
        StopAllCoroutines();
        wander?.StopCombatMove();
        socket?.DetachWeapon();
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

        // Prefer the NPC-specific prefab. This prevents a stale shared WeaponData reference
        // from making the blacksmith or Nahue display Tonio's/King Goblin's sword.
        WeaponData weapon = weaponPrefab != null ? runtimeWeapon : weaponDataTemplate;
        if (weapon != null && weapon.weaponPrefab != null)
        {
            socket.AttachWeapon(weapon.weaponPrefab, weapon);
            socket.SetAnimationDrivenGrip(true);
        }

        SetInt(HashWeapon, weaponType == WeaponType.Sword2H ? 2 : 1);
        SetInt(HashRightWeapon, 1);
        SetInt(HashAction, 7);
        SetInt(HashSide, 2);
        SetInt(HashTriggerNumber, 4);
        if (HasParameter(HashTrigger, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(HashTrigger);
        yield return new WaitForSeconds(.42f);
        if (IsValidTarget(attacked) &&
            Vector3.Distance(transform.position, attacked.transform.position) <= attackRange + .35f)
            attacked.TakeDamage(attackDamage, transform.position);

        yield return new WaitForSeconds(.48f);
        socket?.DetachWeapon();
        RestoreRelaxedPose();
        attacking = false;
    }

    void RestoreRelaxedPose()
    {
        socket?.DetachWeapon();
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
}

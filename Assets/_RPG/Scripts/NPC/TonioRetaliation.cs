using System.Collections;
using UnityEngine;

// Tonio is a peaceful quest-giver, but attacking him triggers a single retaliatory swing with
// the King Goblin Sword equipped just for that swing, then reverts to unarmed. He can never
// actually die - EnemyStats only exists so CombatSystem's hit detection has something to call
// TakeDamage on (it requires that component specifically); every hit is immediately healed back
// to full so the quest chain can't break.
//
// Tonio's own idle/walk controller (MerchantWalk2) has no attack states - his rig uses the
// GanzSe avatar, not the pack's own, but Humanoid retargeting already lets the same
// RPG-Character-Animation-Controller drive a GanzSe-skinned model elsewhere in this project
// (see GanzPlayerVisual), so swapping to it just for the swing and back afterward works the
// same way here.
[RequireComponent(typeof(EnemyStats))]
public class TonioRetaliation : MonoBehaviour
{
    [SerializeField] float attackDamage = 40f;
    [SerializeField] float attackRange = 2.6f;
    [SerializeField] float weaponHeldSeconds = 1.3f;
    [SerializeField] float retaliationCooldown = 2f;
    [SerializeField] RuntimeAnimatorController combatController;
    [SerializeField] WeaponData swordWeaponData;

    static readonly int HashTrigger = Animator.StringToHash("Trigger");
    static readonly int HashTriggerNumber = Animator.StringToHash("TriggerNumber");
    static readonly int HashAction = Animator.StringToHash("Action");
    static readonly int HashSide = Animator.StringToHash("Side");
    static readonly int HashWeapon = Animator.StringToHash("Weapon");
    static readonly int HashRightWeapon = Animator.StringToHash("RightWeapon");

    EnemyStats stats;
    Animator anim;
    WeaponSocket weaponSocket;
    NPCWander wander;
    RuntimeAnimatorController idleWalkController;
    float cooldownTimer;
    bool retaliating;

    void Awake()
    {
        stats = GetComponent<EnemyStats>();
        anim = GetComponentInChildren<Animator>();
        wander = GetComponent<NPCWander>();
        weaponSocket = GetComponent<WeaponSocket>();
        if (weaponSocket == null)
            weaponSocket = gameObject.AddComponent<WeaponSocket>();
        if (anim != null)
            idleWalkController = anim.runtimeAnimatorController;
    }

    void OnEnable() => stats.OnHealthChanged += HandleHealthChanged;
    void OnDisable() => stats.OnHealthChanged -= HandleHealthChanged;

    void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    void HandleHealthChanged(float current, float max)
    {
        if (current < max)
            stats.Heal(max - current);

        if (retaliating || cooldownTimer > 0f)
            return;

        StartCoroutine(RetaliateRoutine());
    }

    IEnumerator RetaliateRoutine()
    {
        retaliating = true;
        cooldownTimer = retaliationCooldown;
        wander?.PauseForInteraction();

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Vector3 dir = player.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        // Must go through the WeaponData overload, not just the raw prefab - WeaponSocket only
        // uses the vertex-based grip anchor (needed for this diagonally-authored mesh) when
        // currentWeaponData.useVertexGripAnchor is set, which lives on this asset.
        if (swordWeaponData != null)
        {
            weaponSocket.AttachWeapon(swordWeaponData.weaponPrefab, swordWeaponData);
            weaponSocket.SetAnimationDrivenGrip(true);
        }

        if (anim != null && combatController != null)
        {
            anim.runtimeAnimatorController = combatController;
            anim.SetInteger(HashWeapon, 1);
            anim.SetInteger(HashRightWeapon, 1);
            anim.SetInteger(HashAction, 7); // one of the sword combo swings (sword: Action 1-11)
            anim.SetInteger(HashSide, 2);
            anim.SetInteger(HashTriggerNumber, 4); // AttackTrigger
            anim.SetTrigger(HashTrigger);
        }

        yield return new WaitForSeconds(0.4f);

        if (player != null && Vector3.Distance(transform.position, player.transform.position) <= attackRange)
            player.GetComponent<PlayerStats>()?.TakeDamage(attackDamage, transform.position);

        float remaining = weaponHeldSeconds - 0.4f;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        weaponSocket.DetachWeapon();
        if (anim != null)
        {
            anim.SetInteger(HashWeapon, 0);
            anim.SetInteger(HashRightWeapon, 0);
            anim.SetInteger(HashTriggerNumber, 2); // ActionTrigger - back to idle
            anim.SetTrigger(HashTrigger);
            anim.runtimeAnimatorController = idleWalkController;
        }

        retaliating = false;
        wander?.ResumeWander();
    }
}

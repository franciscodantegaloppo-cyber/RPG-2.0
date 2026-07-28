using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Dodge/DiveRoll — press Left Alt (or C) to roll in move direction.
// Grants 0.4s of invincibility. Costs stamina.
[RequireComponent(typeof(CharacterController))]
public class PlayerDodge : MonoBehaviour
{
    [SerializeField] float staminaCost = 25f;
    [SerializeField] float cooldown    = 1.5f;
    [SerializeField] float rollSpeed   = 6f;
    [SerializeField] float rollDuration = 0.5f;

    CharacterController cc;
    PlayerStats stats;
    PlayerAnimatorBridge animBridge;
    PlayerController controller;
    PlayerWaterBreathing waterBreathing;

    float cooldownTimer;

    void Awake()
    {
        cc         = GetComponent<CharacterController>();
        stats      = GetComponent<PlayerStats>();
        animBridge = GetComponent<PlayerAnimatorBridge>();
        controller = GetComponent<PlayerController>();
        waterBreathing = GetComponent<PlayerWaterBreathing>();
    }

    void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive()) return;
        if (stats.IsDead || stats.IsKnockedDown || stats.IsRolling) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // C is reserved for the healing spell.
        if ((kb.leftAltKey.wasPressedThisFrame || kb.vKey.wasPressedThisFrame)
            && cooldownTimer <= 0f
            && stats.HasStamina(staminaCost))
        {
            StartCoroutine(DoDodge());
        }
    }

    IEnumerator DoDodge()
    {
        stats.DrainStamina(staminaCost);
        if (waterBreathing == null) waterBreathing = GetComponent<PlayerWaterBreathing>();
        float actionSpeed = waterBreathing != null ? waterBreathing.ActionSpeedMultiplier : 1f;
        cooldownTimer = cooldown / Mathf.Max(0.01f, actionSpeed);

        Vector3 rollDir = controller.MoveDirection.sqrMagnitude > .01f
            ? controller.MoveDirection.normalized
            : -transform.forward; // roll backward if standing still

        stats.SetRolling(true);
        animBridge?.TriggerDiveRoll();

        float elapsed = 0f;
        float waterDuration = rollDuration / Mathf.Max(0.01f, actionSpeed);
        float waterRollSpeed = rollSpeed * actionSpeed;
        while (elapsed < waterDuration)
        {
            cc.Move(rollDir * waterRollSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        stats.SetRolling(false);
    }
}

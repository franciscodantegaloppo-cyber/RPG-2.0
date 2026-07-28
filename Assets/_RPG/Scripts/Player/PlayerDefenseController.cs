using UnityEngine;
using UnityEngine.InputSystem;

// Right mouse: hold to block, tap just before impact to parry. Damage is still ultimately
// processed by PlayerStats; this component only supplies a bounded mitigation decision.
[DisallowMultipleComponent]
public sealed class PlayerDefenseController : MonoBehaviour
{
    [SerializeField, Range(.1f, .9f)] float blockedDamageMultiplier = .45f;
    [SerializeField] float parryWindow = .18f;
    [SerializeField] float blockStaminaCost = 7f;
    [SerializeField] float parryStaminaCost = 4f;
    [SerializeField, Range(-1f, 1f)] float frontalDot = -.05f;

    PlayerStats stats;
    float guardPressedAt = -10f;
    bool guarding;

    public bool IsGuarding => guarding;

    void Awake() => stats = GetComponent<PlayerStats>();

    void Update()
    {
        bool gameplay = GameManager.Instance == null || GameManager.Instance.IsGameplayActive();
        Mouse mouse = Mouse.current;
        guarding = gameplay && mouse != null && mouse.rightButton.isPressed &&
                   stats != null && !stats.IsDead && !stats.IsRolling &&
                   !stats.IsKnockedDown;
        if (gameplay && mouse != null && mouse.rightButton.wasPressedThisFrame)
            guardPressedAt = Time.unscaledTime;
    }

    public bool TryMitigate(ref float amount, Vector3 attackerWorldPosition)
    {
        if (!guarding || stats == null || amount <= 0f)
            return false;

        Vector3 toAttacker = Vector3.ProjectOnPlane(
            attackerWorldPosition - transform.position, Vector3.up);
        if (toAttacker.sqrMagnitude > .01f &&
            Vector3.Dot(transform.forward, toAttacker.normalized) < frontalDot)
            return false;

        bool parry = Time.unscaledTime - guardPressedAt <= parryWindow &&
                     stats.HasStamina(parryStaminaCost);
        if (parry)
        {
            stats.DrainStamina(parryStaminaCost);
            amount = 0f;
            ActiveStatusIconHUD.ShowTimed("player_parry", "Shield", .45f, "Parada perfecta");
            PlayGuardFlash(new Color(1f, .82f, .28f, .9f), 13);
            Camera.main?.GetComponent<ThirdPersonCamera>()?.AddImpulse(.12f, .11f);
            GetComponent<PlayerRpgAnimationPackController>()?.PlayBlockImpact();
            return true;
        }

        if (!stats.HasStamina(blockStaminaCost))
            return false;
        stats.DrainStamina(blockStaminaCost);
        amount *= blockedDamageMultiplier;
        ActiveStatusIconHUD.ShowTimed("player_block", "Shield", .32f, "Bloqueo");
        PlayGuardFlash(new Color(.36f, .66f, 1f, .65f), 7);
        GetComponent<PlayerRpgAnimationPackController>()?.PlayBlockImpact();
        return true;
    }

    void PlayGuardFlash(Color color, int particles)
    {
        GameObject go = new GameObject("PlayerGuardImpact");
        go.transform.position = transform.position + Vector3.up * 1.15f +
                                transform.forward * .38f;
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        var main = system.main;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.08f, .2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(.018f, .055f);
        main.startColor = color;
        main.maxParticles = 24;
        var emission = system.emission;
        emission.rateOverTime = 0f;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = .12f;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.4f;
        renderer.velocityScale = .16f;
        renderer.sharedMaterial = WindVisualEffect.CreateWindStreakMaterial();
        system.Emit(particles);
        Destroy(go, .5f);
    }
}

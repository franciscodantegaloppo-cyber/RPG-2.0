using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// Adds combat readability and physical contact to the existing Crab Demon AI without replacing
// its proven locomotion/obstacle code. All hit decisions originate from the animated limb.
[DisallowMultipleComponent]
public sealed class CrabDemonCombatRealism : MonoBehaviour
{
    [SerializeField] float maxPosture = 260f;
    [SerializeField] float postureRecoveryPerSecond = 22f;
    [SerializeField] float postureRecoveryDelay = 2.2f;
    [SerializeField] float staggerDuration = 2.15f;
    [SerializeField] float blockReactionCooldown = 1.65f;

    EnemyStats stats;
    CrabDemonBossAI ai;
    Animator animator;
    float posture;
    float lastPostureDamageTime;
    float lastBlockTime = -20f;
    int phase = 1;
    int announcedPhase = 1;
    static AudioClip impactClip;

    public int Phase => phase;
    public float MovementMultiplier => phase == 1 ? 1f : phase == 2 ? 1.12f : 1.25f;
    public float ActionMultiplier => phase == 1 ? 1f : phase == 2 ? 1.16f : 1.32f;

    void Awake()
    {
        stats = GetComponent<EnemyStats>();
        ai = GetComponent<CrabDemonBossAI>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (stats == null || stats.IsDead)
            return;
        if (Time.time - lastPostureDamageTime > postureRecoveryDelay)
            posture = Mathf.MoveTowards(posture, 0f,
                postureRecoveryPerSecond * Time.deltaTime);
    }

    public void RefreshPhase()
    {
        if (stats == null || stats.MaxHealth <= .01f)
            return;
        float health = stats.CurrentHealth / stats.MaxHealth;
        phase = health > .6f ? 1 : health > .3f ? 2 : 3;
        if (phase == announcedPhase)
            return;
        announcedPhase = phase;
        Color color = phase == 2
            ? new Color(.26f, .48f, 1f, .75f)
            : new Color(.62f, .12f, 1f, .88f);
        EmitBurst(transform.position + Vector3.up * 1.15f, color,
            phase == 2 ? 28 : 52, phase == 2 ? 1.4f : 2.1f);
        Camera.main?.GetComponent<ThirdPersonCamera>()?.AddImpulse(
            phase == 2 ? .09f : .16f, .22f);
    }

    public void PlayTelegraph(int attackIndex, float duration, bool heavy)
    {
        Transform limb = ResolveAttackLimb(attackIndex);
        Vector3 origin = limb != null
            ? limb.position
            : transform.position + transform.forward * .7f + Vector3.up;
        Color color = heavy
            ? new Color(.72f, .16f, 1f, .82f)
            : new Color(.16f, .48f, 1f, .62f);
        StartCoroutine(TelegraphPulse(origin, limb, color, duration, heavy));
    }

    IEnumerator TelegraphPulse(Vector3 fallbackOrigin, Transform limb, Color color,
        float duration, bool heavy)
    {
        GameObject marker = new GameObject("CrabDemon_AttackTelegraph");
        ParticleSystem particles = marker.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.08f, .22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.15f, .55f);
        main.startSize = new ParticleSystem.MinMaxCurve(.018f, heavy ? .085f : .055f);
        main.startColor = color;
        main.maxParticles = 48;
        var emission = particles.emission;
        emission.rateOverTime = heavy ? 75f : 42f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = heavy ? .32f : .2f;
        ParticleSystemRenderer renderer = marker.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.6f;
        renderer.velocityScale = .14f;
        renderer.sharedMaterial = WindVisualEffect.CreateWindStreakMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        float end = Time.time + duration;
        while (Time.time < end && stats != null && !stats.IsDead)
        {
            marker.transform.position = limb != null ? limb.position : fallbackOrigin;
            yield return null;
        }
        particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(marker, .28f);
    }

    public bool TryDealAnimatedHit(Transform target, int attackIndex, float damage,
        bool heavy)
    {
        if (target == null)
            return false;
        Transform limb = ResolveAttackLimb(attackIndex);
        Vector3 contact = limb != null
            ? limb.position
            : transform.position + transform.forward * .85f + Vector3.up;
        Collider targetCollider = target.GetComponent<Collider>() ??
                                  target.GetComponentInChildren<Collider>();
        Vector3 closest = targetCollider != null
            ? targetCollider.ClosestPoint(contact)
            : target.position + Vector3.up;
        float radius = heavy ? 1.05f : .78f;
        bool physicalContact = Vector3.Distance(contact, closest) <= radius;

        // Retargeted clips can differ by a few centimetres. A narrow forward fallback prevents
        // visually intersecting claws from missing while still rejecting side/back phantom hits.
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        bool frontalFallback = toTarget.magnitude <= (heavy ? 1.75f : 1.45f) &&
                               Vector3.Dot(transform.forward, toTarget.normalized) > .62f;
        if (!physicalContact && !frontalFallback)
        {
            EmitBurst(contact, new Color(.25f, .55f, 1f, .22f), 5, .45f);
            if (heavy)
                StartCoroutine(Shockwave(contact, target, damage * .35f, false));
            return false;
        }

        PlayerStats playerStats = target.GetComponent<PlayerStats>();
        playerStats?.TakeDamage(damage * (phase == 3 ? 1.18f : 1f), transform.position);
        PlayImpact(closest, heavy);
        if (heavy)
            StartCoroutine(Shockwave(closest, target, damage * .28f, true));
        return true;
    }

    public bool CanBlock(Vector3 attackerPosition, float incomingDamage)
    {
        if (Time.time - lastBlockTime < blockReactionCooldown / ActionMultiplier)
            return false;
        Vector3 towardAttacker = attackerPosition - transform.position;
        towardAttacker.y = 0f;
        if (towardAttacker.sqrMagnitude > .01f &&
            Vector3.Dot(transform.forward, towardAttacker.normalized) < .2f)
            return false;

        // Heavy repeated blows drain posture even when caught on the guard.
        posture += Mathf.Max(4f, incomingDamage * .32f);
        lastPostureDamageTime = Time.time;
        if (posture >= maxPosture)
        {
            BreakPosture();
            return false;
        }
        lastBlockTime = Time.time;
        EmitBurst(transform.position + transform.forward * .7f + Vector3.up * 1.15f,
            new Color(.26f, .62f, 1f, .8f), 18, .8f);
        return true;
    }

    public bool RegisterPostureDamage(float damage)
    {
        if (damage <= 0f)
            return false;
        posture += Mathf.Clamp(damage * .72f, 5f, 80f);
        lastPostureDamageTime = Time.time;
        if (posture < maxPosture)
            return false;
        BreakPosture();
        return true;
    }

    void BreakPosture()
    {
        posture = 0f;
        lastBlockTime = Time.time;
        EmitBurst(transform.position + Vector3.up * 1.25f,
            new Color(.7f, .2f, 1f, .9f), 46, 1.8f);
        ai?.Stagger(staggerDuration);
        Camera.main?.GetComponent<ThirdPersonCamera>()?.AddImpulse(.18f, .2f);
    }

    Transform ResolveAttackLimb(int attackIndex)
    {
        if (animator == null || !animator.isHuman)
            return null;
        HumanBodyBones bone = attackIndex switch
        {
            2 => HumanBodyBones.LeftHand,
            3 => HumanBodyBones.RightFoot,
            4 => HumanBodyBones.LeftFoot,
            5 => HumanBodyBones.RightFoot,
            _ => HumanBodyBones.RightHand
        };
        return animator.GetBoneTransform(bone);
    }

    void PlayImpact(Vector3 point, bool heavy)
    {
        EmitBurst(point, new Color(.22f, .5f, 1f, .85f),
            heavy ? 34 : 20, heavy ? 1.55f : .9f);
        Camera.main?.GetComponent<ThirdPersonCamera>()?.AddImpulse(
            heavy ? .17f : .09f, heavy ? .16f : .1f);
        AudioSource.PlayClipAtPoint(GetImpactClip(), point, heavy ? .65f : .42f);

        Collider[] nearby = Physics.OverlapSphere(point, heavy ? 2.6f : 1.35f,
            ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider collider in nearby)
        {
            Rigidbody body = collider.attachedRigidbody;
            if (body != null && !body.isKinematic)
                body.AddExplosionForce(heavy ? 260f : 90f, point, heavy ? 2.6f : 1.35f,
                    .15f, ForceMode.Impulse);
        }
    }

    IEnumerator Shockwave(Vector3 center, Transform target, float damage, bool canDamage)
    {
        center.y = Terrain.activeTerrain != null
            ? Terrain.activeTerrain.SampleHeight(center) +
              Terrain.activeTerrain.transform.position.y + .035f
            : center.y;
        GameObject ring = new GameObject("CrabDemon_GroundShockwave");
        LineRenderer line = ring.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = true;
        line.positionCount = 49;
        line.widthMultiplier = .065f;
        line.sharedMaterial = WindVisualEffect.CreateWindStreakMaterial();
        line.startColor = new Color(.35f, .14f, 1f, .72f);
        line.endColor = new Color(.08f, .42f, 1f, .15f);
        line.shadowCastingMode = ShadowCastingMode.Off;
        float duration = .28f;
        float elapsed = 0f;
        bool damaged = false;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float radius = Mathf.Lerp(.25f, 2.8f, Mathf.SmoothStep(0f, 1f, progress));
            for (int i = 0; i < 49; i++)
            {
                float angle = i / 48f * Mathf.PI * 2f;
                line.SetPosition(i, center +
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
            line.widthMultiplier = Mathf.Lerp(.1f, 0f, progress);
            if (canDamage && !damaged && target != null &&
                Vector3.Distance(new Vector3(target.position.x, center.y, target.position.z),
                    center) <= radius + .35f)
            {
                target.GetComponent<PlayerStats>()?.TakeDamage(damage, transform.position);
                damaged = true;
            }
            yield return null;
        }
        Destroy(ring);
    }

    static void EmitBurst(Vector3 position, Color color, int count, float speed)
    {
        GameObject root = new GameObject("CrabDemon_ImpactParticles");
        root.transform.position = position;
        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .38f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .35f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(.018f, .075f);
        main.startColor = color;
        main.maxParticles = 80;
        var emission = particles.emission;
        emission.rateOverTime = 0f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .12f;
        ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.8f;
        renderer.velocityScale = .12f;
        renderer.sharedMaterial = WindVisualEffect.CreateWindStreakMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        particles.Emit(count);
        Destroy(root, .7f);
    }

    static AudioClip GetImpactClip()
    {
        if (impactClip != null)
            return impactClip;
        const int rate = 22050;
        const int samples = 3307;
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float time = i / (float)rate;
            float envelope = Mathf.Exp(-time * 24f);
            float low = Mathf.Sin(time * 78f * Mathf.PI * 2f) * .5f;
            float noise = (Random.value * 2f - 1f) * .32f;
            data[i] = (low + noise) * envelope;
        }
        impactClip = AudioClip.Create("CrabDemon_HeavyImpact", samples, 1, rate, false);
        impactClip.SetData(data, 0);
        return impactClip;
    }
}

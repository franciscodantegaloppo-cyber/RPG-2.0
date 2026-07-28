using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyStats))]
public class InsectoidCrabBossAI : MonoBehaviour
{
    [Header("Awareness")]
    [SerializeField] float detectionRadius = 10f;
    [SerializeField] float loseRadius = 45f;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 3.8f;
    [SerializeField] float turnSpeed = 260f;
    [SerializeField] float terrainAlignSpeed = 7f;
    [SerializeField] float groundOffset;

    [Header("Combat")]
    [SerializeField] float attackRange = 2.35f;
    [SerializeField] float attackCooldown = 1.55f;
    [SerializeField] float damageMoment = .38f;
    [SerializeField] float attackRecovery = .72f;

    static readonly string SleepState = "Armature|Sleep";
    static readonly string FightIdleState = "Armature|Fight_Idle_1";
    static readonly string WalkState1 = "Armature|Walk_Cycle_1";
    static readonly string WalkState2 = "Armature|Walk_Cycle_2";
    static readonly string SneakState = "Armature|Sneak_Cycle_1";
    static readonly string DeathState = "Armature|Die";
    static readonly string[] AttackStates =
    {
        "Armature|Attack_1", "Armature|Attack_2", "Armature|Attack_3",
        "Armature|Attack_4", "Armature|Attack_5"
    };
    static readonly string[] HurtStates =
    {
        "Armature|Take_Damage_1", "Armature|Take_Damage_2", "Armature|Take_Damage_3"
    };
    static readonly string[] WakeStates =
    {
        "Armature|Intimidate_1", "Armature|Intimidate_2", "Armature|Intimidate_3"
    };

    EnemyStats stats;
    Animator animator;
    Transform player;
    Rigidbody body;
    Collider bodyCollider;
    Terrain terrain;
    float lastAttackTime = -99f;
    float previousHealth;
    float nextLocomotionVariation;
    int attackIndex;
    int walkVariation;
    bool engaged;
    bool waking;
    bool attacking;
    bool hurt;
    bool dead;
    float requestedMaxSpeed;
    string currentLoop;

    readonly int walkState1Hash = Animator.StringToHash(WalkState1);
    readonly int walkState2Hash = Animator.StringToHash(WalkState2);
    readonly int sneakStateHash = Animator.StringToHash(SneakState);

    void Awake()
    {
        stats = GetComponent<EnemyStats>();
        animator = GetComponentInChildren<Animator>(true);
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        terrain = Terrain.activeTerrain;
        if (GetComponent<InsectoidCrabMissionCinematic>() == null)
            gameObject.AddComponent<InsectoidCrabMissionCinematic>();
        if (GetComponent<InsectoidCrabDarkAura>() == null)
            gameObject.AddComponent<InsectoidCrabDarkAura>();
        if (animator != null)
        {
            // These clips are authored in place. Translation is gated by their visible
            // gait phase below so the body cannot skate while the legs are planted.
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 1f;
        }
    }

    void Start()
    {
        previousHealth = stats.CurrentHealth;
        SnapAndAlignToGround(true);
        PlayLoop(SleepState, 0f);
    }

    void OnEnable()
    {
        stats.OnHealthChanged += HandleHealthChanged;
        stats.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        stats.OnHealthChanged -= HandleHealthChanged;
        stats.OnDeath -= HandleDeath;
    }

    void Update()
    {
        if (dead)
            return;

        FindPlayer();
        SnapAndAlignToGround(false);

        if (!engaged)
        {
            if (player != null && PlanarDistance(player.position, transform.position) <= detectionRadius)
                StartCoroutine(WakeUp());
            else
                PlayLoop(SleepState);
            return;
        }

        if (waking || attacking || hurt)
            return;

        if (player == null || PlanarDistance(player.position, transform.position) > loseRadius)
        {
            engaged = false;
            PlayLoop(SleepState);
            return;
        }

        float distance = PlanarDistance(transform.position, player.position);
        FacePlayer();
        if (distance <= attackRange)
        {
            PlayLoop(FightIdleState);
            if (Time.time >= lastAttackTime + attackCooldown)
                StartCoroutine(Attack());
            return;
        }

        MoveTowardPlayer();
    }

    void FindPlayer()
    {
        if (player != null)
            return;
        GameObject found = GameObject.FindWithTag("Player");
        if (found != null)
            player = found.transform;
    }

    IEnumerator WakeUp()
    {
        if (waking || engaged)
            yield break;

        engaged = true;
        waking = true;
        PlayOneShot(WakeStates[Random.Range(0, WakeStates.Length)], .08f);
        yield return new WaitForSeconds(1.35f);
        waking = false;
        currentLoop = null;
        PlayLoop(FightIdleState, .12f);
    }

    void MoveTowardPlayer()
    {
        if (player == null)
            return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < .01f)
            return;

        if (Time.time >= nextLocomotionVariation)
        {
            nextLocomotionVariation = Time.time + Random.Range(2.8f, 5.2f);
            walkVariation = Random.Range(0, 5);
        }

        string movementState = walkVariation == 0 ? SneakState :
            walkVariation <= 2 ? WalkState2 : WalkState1;
        float speedFactor = walkVariation == 0 ? .58f : walkVariation <= 2 ? 1.12f : 1f;
        // Root motion supplies the distance. Animator speed only changes cadence slightly
        // between the pack's own slow/fast cycles; no external constant translation is used.
        if (animator != null)
            animator.speed = walkVariation == 0 ? .9f : walkVariation <= 2 ? 1.08f : 1f;
        PlayLoop(movementState, .14f);
        requestedMaxSpeed = moveSpeed * speedFactor;
        AdvanceDuringVisibleStep();
    }

    void AdvanceDuringVisibleStep()
    {
        if (animator == null || dead || waking || attacking || hurt || animator.IsInTransition(0))
            return;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (!IsLocomotionState(current))
            return;

        // Two firm plant instants per animation cycle. Motion eases to zero at both,
        // then advances only while the legs visibly push the body.
        float phase = Mathf.Repeat(current.normalizedTime, 1f);
        float gaitPush = Mathf.SmoothStep(0f, 1f,
            Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2f)));
        if (gaitPush < .2f)
            return;

        float distance = requestedMaxSpeed * Time.deltaTime * gaitPush * 1.35f;
        Vector3 next = transform.position + transform.forward * distance;
        if (!TryGround(next, out Vector3 groundPoint, out Vector3 normal))
            return;

        next.y = groundPoint.y + groundOffset;
        transform.position = next;
        AlignToSurface(normal, false);
    }

    bool IsLocomotionState(AnimatorStateInfo state) =>
        state.shortNameHash == walkState1Hash ||
        state.shortNameHash == walkState2Hash ||
        state.shortNameHash == sneakStateHash ||
        state.IsName(WalkState1) ||
        state.IsName(WalkState2) ||
        state.IsName(SneakState);

    void FacePlayer()
    {
        if (player == null)
            return;
        Vector3 forward = Vector3.ProjectOnPlane(player.position - transform.position, transform.up);
        if (forward.sqrMagnitude < .01f)
            return;
        Quaternion target = Quaternion.LookRotation(forward.normalized, transform.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    IEnumerator Attack()
    {
        attacking = true;
        lastAttackTime = Time.time;
        string state = AttackStates[attackIndex++ % AttackStates.Length];
        PlayOneShot(state, .06f);
        yield return new WaitForSeconds(damageMoment);

        if (!dead && player != null &&
            PlanarDistance(transform.position, player.position) <= attackRange + .55f)
        {
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            playerStats?.TakeDamage(stats.Attack, transform.position);
            DarkEnergyBurn.Apply(playerStats, 20f, 5f);
            DarkEnergyVfx.PlayImpact(player.position + Vector3.up, player);
        }

        yield return new WaitForSeconds(attackRecovery);
        attacking = false;
        currentLoop = null;
        if (!dead)
            PlayLoop(FightIdleState, .1f);
    }

    void HandleHealthChanged(float current, float max)
    {
        bool tookDamage = current < previousHealth - .01f;
        previousHealth = current;
        if (!tookDamage || current <= 0f || dead)
            return;

        if (!engaged)
        {
            engaged = true;
            waking = false;
        }
        if (!attacking && !hurt)
            StartCoroutine(HurtReaction());
    }

    IEnumerator HurtReaction()
    {
        hurt = true;
        PlayOneShot(HurtStates[Random.Range(0, HurtStates.Length)], .04f);
        yield return new WaitForSeconds(.48f);
        hurt = false;
        currentLoop = null;
    }

    void HandleDeath()
    {
        if (dead)
            return;
        dead = true;
        StopAllCoroutines();
        if (bodyCollider != null)
            bodyCollider.enabled = false;
        if (body != null)
            body.detectCollisions = false;
        PlayOneShot(DeathState, .08f);
        Destroy(gameObject, 7f);
    }

    void SnapAndAlignToGround(bool instant)
    {
        if (!TryGround(transform.position, out Vector3 groundPoint, out Vector3 normal))
            return;
        Vector3 position = transform.position;
        position.y = groundPoint.y + groundOffset;
        transform.position = position;
        AlignToSurface(normal, instant);
    }

    void AlignToSurface(Vector3 normal, bool instant)
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, normal);
        if (forward.sqrMagnitude < .001f)
            forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
        Quaternion target = Quaternion.LookRotation(forward.normalized, normal);
        transform.rotation = instant ? target :
            Quaternion.Slerp(transform.rotation, target, Time.deltaTime * terrainAlignSpeed);
    }

    bool TryGround(Vector3 position, out Vector3 point, out Vector3 normal)
    {
        if (terrain == null)
            terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            bool insideTerrain = position.x >= terrainPosition.x &&
                                 position.x <= terrainPosition.x + terrainSize.x &&
                                 position.z >= terrainPosition.z &&
                                 position.z <= terrainPosition.z + terrainSize.z;
            if (insideTerrain)
            {
                point = position;
                point.y = terrain.SampleHeight(position) + terrainPosition.y;
                Vector3 local = position - terrainPosition;
                normal = terrain.terrainData.GetInterpolatedNormal(
                    Mathf.Clamp01(local.x / terrainSize.x),
                    Mathf.Clamp01(local.z / terrainSize.z));
                return true;
            }
        }

        // Fallback for maps that use a mesh as their ground. The active Terrain is
        // deliberately preferred so props, roofs and vegetation cannot lift the boss.
        Vector3 origin = position + Vector3.up * 6f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 16f, ~0, QueryTriggerInteraction.Ignore);
        bool found = false;
        point = position;
        normal = Vector3.up;
        float bestY = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.normal.y < .16f)
                continue;
            if (hit.point.y <= bestY)
                continue;
            bestY = hit.point.y;
            point = hit.point;
            normal = hit.normal.normalized;
            found = true;
        }

        if (found)
            return true;
        return false;
    }

    void PlayLoop(string state, float fade = .18f)
    {
        if (animator == null || currentLoop == state)
            return;
        if (state != WalkState1 && state != WalkState2 && state != SneakState)
            animator.speed = 1f;
        currentLoop = state;
        int hash = Animator.StringToHash(state);
        if (animator.HasState(0, hash))
            animator.CrossFade(hash, fade, 0);
    }

    void PlayOneShot(string state, float fade)
    {
        if (animator == null)
            return;
        animator.speed = 1f;
        currentLoop = null;
        int hash = Animator.StringToHash(state);
        if (animator.HasState(0, hash))
            animator.CrossFade(hash, fade, 0);
    }

    static float PlanarDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Persistent melee enemy: idle/patrol until the player enters its awareness
// radius, then chase, stop in strike range, attack/react and resume pursuit.
[RequireComponent(typeof(EnemyStats))]
public sealed class OrcWarriorAI : MonoBehaviour
{
    enum TacticalState
    {
        Guard,
        Pursue,
        Attack,
        Hurt,
        Dead
    }

    [Header("Awareness")]
    [SerializeField] float detectionRadius = 20f;
    [SerializeField] float decisionInterval = .12f;

    [Header("Movement")]
    [SerializeField] float patrolRadius = 6f;
    [SerializeField] float walkSpeed = 1.75f;
    [SerializeField] float pursueSpeed = 4.25f;
    [SerializeField] float turnSpeed = 520f;
    [SerializeField] float preferredRange = 2.15f;
    [SerializeField] float attackRange = 2.35f;
    [SerializeField] float authoredWalkSpeed = 1.7f;
    [SerializeField] float authoredRunSpeed = 4.15f;
    [SerializeField] Vector2 locomotionPlaybackRange =
        new Vector2(.7f, 1.55f);
    [SerializeField] float locomotionStartSpeed = .16f;
    [SerializeField] float locomotionStopSpeed = .055f;
    [SerializeField] float runAnimationEnterSpeed = 3.25f;
    [SerializeField] float runAnimationExitSpeed = 2.45f;
    [SerializeField] float animationParameterResponse = 14f;

    [Header("Combat")]
    [SerializeField] float attackCooldown = 1.55f;
    [SerializeField] float primaryHitTime = .43f;
    [SerializeField] float alternateHitTime = .56f;
    [SerializeField] float recoveryTime = .48f;
    [SerializeField, Range(0f, 1f)] float blockChance = .22f;
    [SerializeField] float blockCooldown = 3.4f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int CombatHash = Animator.StringToHash("Combat");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int AttackAltHash = Animator.StringToHash("AttackAlt");
    static readonly int HitHash = Animator.StringToHash("Hit");
    static readonly int DieHash = Animator.StringToHash("Die");
    static readonly int BlockHash = Animator.StringToHash("Block");

    readonly Collider[] playerHits = new Collider[4];

    EnemyStats stats;
    Animator animator;
    NavMeshAgent agent;
    Transform player;
    Terrain terrain;
    TacticalState state;
    Vector3 homePosition;
    Vector3 destination;
    Vector3 previousPosition;
    float nextDecisionTime;
    float nextAttackTime;
    float nextBlockTime;
    float stateUntil;
    float nextPatrolTime;
    bool hasDestination;
    bool usingAgent;
    bool actionLocked;
    bool deathHandled;
    bool locomotionActive;
    bool usingRunAnimation;
    float stableAnimationSpeed;
    Transform collisionIgnoredPlayer;
    bool playerDetected;

    bool hasSpeed;
    bool hasCombat;
    bool hasAttack;
    bool hasAttackAlt;
    bool hasHit;
    bool hasDie;
    bool hasBlock;

    public bool IsDead => state == TacticalState.Dead;

    void Awake()
    {
        stats = GetComponent<EnemyStats>();
        animator = GetComponentInChildren<Animator>(true);
        agent = GetComponent<NavMeshAgent>();
        terrain = Terrain.activeTerrain;
        homePosition = ProjectToGround(transform.position);
        previousPosition = transform.position;

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            CacheAnimatorParameters();
        }

        if (agent != null)
        {
            agent.speed = pursueSpeed;
            agent.angularSpeed = turnSpeed;
            agent.acceleration = 16f;
            agent.stoppingDistance = preferredRange * .8f;
            agent.autoBraking = true;
            usingAgent = agent.enabled && agent.isOnNavMesh;
        }

        GameObject existingPlayer = GameObject.FindWithTag("Player");
        if (existingPlayer != null)
            IgnorePlayerBodyCollisions(existingPlayer.transform);

        ChangeState(TacticalState.Guard, Random.Range(.6f, 1.4f));
    }

    void OnEnable()
    {
        nextDecisionTime = Time.time + Random.Range(.05f, .18f);
        nextPatrolTime = Time.time + Random.Range(1f, 2.5f);
        PlayerStats.GlobalOnDeath += HandlePlayerDeath;
    }

    void OnDisable()
    {
        PlayerStats.GlobalOnDeath -= HandlePlayerDeath;
    }

    void Update()
    {
        if (state == TacticalState.Dead)
            return;

        UpdateAnimationSpeed();

        if (actionLocked)
        {
            StopMoving();
            if (player != null)
                Face(player.position);
            previousPosition = transform.position;
            return;
        }

        if (Time.time >= nextDecisionTime)
        {
            nextDecisionTime = Time.time + decisionInterval;
            Think();
        }

        if (!usingAgent)
            ManualMovement();

        previousPosition = transform.position;
    }

    void Think()
    {
        if (player == null)
        {
            TryAcquirePlayer();
            if (player == null)
            {
                playerDetected = false;
                GuardArea();
                return;
            }
        }

        if (!playerDetected)
        {
            float initialDistance =
                FlatDistance(transform.position, player.position);
            if (initialDistance > detectionRadius)
            {
                player = null;
                GuardArea();
                return;
            }
            playerDetected = true;
        }

        // Once the encounter starts the orc behaves like the Crab Demon melee
        // core: it keeps the same target and closes the gap until one of them
        // dies. There are no random investigate/circle/retreat decisions.
        SetCombat(true);
        float distance = FlatDistance(transform.position, player.position);
        if (distance > attackRange)
        {
            ChangeState(TacticalState.Pursue);
            MoveTo(player.position, pursueSpeed);
            return;
        }

        StopMoving();
        Face(player.position);
        ChangeState(TacticalState.Guard);
        if (distance <= attackRange && Time.time >= nextAttackTime)
            StartCoroutine(AttackRoutine(Random.value > .58f));
    }

    void GuardArea()
    {
        SetCombat(false);
        if (Time.time < stateUntil)
        {
            StopMoving();
            return;
        }

        if (hasDestination && !ReachedDestination())
            return;
        if (Time.time < nextPatrolTime)
            return;

        Vector2 offset = Random.insideUnitCircle * patrolRadius;
        MoveTo(homePosition + new Vector3(offset.x, 0f, offset.y),
            walkSpeed);
        ChangeState(TacticalState.Guard,
            Random.Range(1.3f, 2.8f));
        nextPatrolTime = Time.time + Random.Range(3.5f, 6.5f);
    }

    void TryAcquirePlayer()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, detectionRadius, playerHits,
            LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);
        if (count <= 0)
            return;

        Transform candidate = playerHits[0].transform;
        PlayerStats candidateStats =
            candidate.GetComponentInParent<PlayerStats>();
        player = candidateStats != null
            ? candidateStats.transform
            : candidate.root;
        IgnorePlayerBodyCollisions(player);
        playerDetected = true;
        ChangeState(TacticalState.Pursue);
    }

    void HandlePlayerDeath()
    {
        if (state == TacticalState.Dead)
            return;

        StopAllCoroutines();
        actionLocked = false;
        playerDetected = false;
        player = null;
        StopMoving();
        StopLocomotionAnimation();
        SetCombat(false);
        ChangeState(TacticalState.Guard, 1.2f);
    }

    void IgnorePlayerBodyCollisions(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        PlayerStats playerStats =
            playerTransform.GetComponentInParent<PlayerStats>();
        Transform playerRoot =
            playerStats != null ? playerStats.transform : playerTransform.root;
        if (collisionIgnoredPlayer == playerRoot)
            return;

        Collider[] enemyColliders =
            GetComponentsInChildren<Collider>(true);
        Collider[] playerColliders =
            playerRoot.GetComponentsInChildren<Collider>(true);

        foreach (Collider enemyCollider in enemyColliders)
        {
            if (enemyCollider == null)
                continue;
            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider != null)
                    Physics.IgnoreCollision(
                        enemyCollider, playerCollider, true);
            }
        }

        collisionIgnoredPlayer = playerRoot;
    }

    IEnumerator AttackRoutine(bool alternate)
    {
        actionLocked = true;
        ChangeState(TacticalState.Attack);
        StopMoving();
        StopLocomotionAnimation();
        Face(player != null ? player.position :
            transform.position + transform.forward);

        Trigger(alternate && hasAttackAlt ? AttackAltHash : AttackHash);
        float hitTime = alternate ? alternateHitTime : primaryHitTime;
        yield return new WaitForSeconds(hitTime);

        if (state != TacticalState.Dead && player != null &&
            FlatDistance(transform.position, player.position) <=
            attackRange * 1.12f)
        {
            Vector3 direction =
                FlatDirection(transform.position, player.position);
            if (Vector3.Dot(transform.forward, direction) >= .25f)
            {
                PlayerStats playerStats =
                    player.GetComponentInParent<PlayerStats>();
                playerStats?.TakeDamage(
                    stats != null ? stats.Attack : 18f,
                    transform.position);
            }
        }

        yield return new WaitForSeconds(recoveryTime);
        if (state == TacticalState.Dead)
            yield break;

        nextAttackTime = Time.time +
                         attackCooldown * Random.Range(.88f, 1.18f);
        actionLocked = false;
        ChangeState(player != null
            ? TacticalState.Pursue
            : TacticalState.Guard);
    }

    public bool TryBlock(Vector3 attackerPosition, float incomingDamage)
    {
        if (state == TacticalState.Dead || actionLocked ||
            Time.time < nextBlockTime || player == null)
            return false;

        Vector3 towardAttacker =
            FlatDirection(transform.position, attackerPosition);
        if (towardAttacker.sqrMagnitude < .01f)
            towardAttacker = transform.forward;
        if (Vector3.Dot(transform.forward, towardAttacker) < -.15f)
            return false;
        if (Random.value > blockChance)
            return false;

        nextBlockTime = Time.time + blockCooldown;
        StartCoroutine(BlockRoutine());
        return true;
    }

    IEnumerator BlockRoutine()
    {
        actionLocked = true;
        StopMoving();
        StopLocomotionAnimation();
        if (hasBlock)
            Trigger(BlockHash);
        yield return new WaitForSeconds(.52f);
        if (state != TacticalState.Dead)
        {
            actionLocked = false;
            ChangeState(player != null
                ? TacticalState.Pursue
                : TacticalState.Guard);
        }
    }

    public void OnHurt(Vector3 attackerPosition, float damage)
    {
        if (state == TacticalState.Dead)
            return;

        StopAllCoroutines();
        StartCoroutine(HurtRoutine());
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + .45f);

        if (player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }
        if (player != null)
        {
            playerDetected = true;
            IgnorePlayerBodyCollisions(player);
        }
    }

    IEnumerator HurtRoutine()
    {
        actionLocked = true;
        StopMoving();
        StopLocomotionAnimation();
        ChangeState(TacticalState.Hurt, .38f);
        Trigger(HitHash);
        yield return new WaitForSeconds(.38f);
        if (state == TacticalState.Dead)
            yield break;
        actionLocked = false;
        ChangeState(player != null
            ? TacticalState.Pursue
            : TacticalState.Guard);
    }

    public void OnDeath()
    {
        if (deathHandled)
            return;
        deathHandled = true;
        state = TacticalState.Dead;
        StopAllCoroutines();
        StopMoving();
        StopLocomotionAnimation();
        SetCombat(false);
        Trigger(DieHash);

        if (agent != null && agent.enabled)
            agent.enabled = false;
        foreach (Collider collider in
                 GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        Destroy(gameObject, 4.5f);
    }

    void ManualMovement()
    {
        if (!hasDestination || actionLocked)
            return;

        Vector3 direction = FlatDirection(transform.position, destination);
        if (direction.sqrMagnitude < .01f)
        {
            hasDestination = false;
            return;
        }

        float speed = CurrentMovementSpeed();
        Vector3 candidate =
            transform.position + direction * speed * Time.deltaTime;
        candidate = ProjectToGround(candidate);
        Face(transform.position + direction);
        transform.position = candidate;
    }

    float CurrentMovementSpeed()
    {
        return state == TacticalState.Pursue
            ? pursueSpeed
            : walkSpeed;
    }

    void MoveTo(Vector3 target, float speed)
    {
        destination = ProjectToGround(target);
        hasDestination = true;
        if (!usingAgent || agent == null || !agent.enabled)
            return;

        agent.isStopped = false;
        agent.speed = speed;
        agent.SetDestination(destination);
    }

    void StopMoving()
    {
        hasDestination = false;
        if (usingAgent && agent != null && agent.enabled &&
            agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    void StopLocomotionAnimation()
    {
        locomotionActive = false;
        usingRunAnimation = false;
        stableAnimationSpeed = 0f;
        if (animator != null && hasSpeed)
            animator.SetFloat(SpeedHash, 0f);
        if (animator != null)
            animator.speed = 1f;
    }

    bool ReachedDestination()
    {
        if (usingAgent && agent != null && agent.enabled &&
            agent.isOnNavMesh)
        {
            return !agent.pathPending &&
                   agent.remainingDistance <=
                   Mathf.Max(.4f, agent.stoppingDistance + .1f);
        }

        return FlatDistance(transform.position, destination) <= .45f;
    }

    void Face(Vector3 worldPoint)
    {
        Vector3 direction = FlatDirection(transform.position, worldPoint);
        if (direction.sqrMagnitude < .001f)
            return;
        Quaternion rotation =
            Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, rotation, turnSpeed * Time.deltaTime);
    }

    Vector3 ProjectToGround(Vector3 position)
    {
        if (GroundUtility.TryProjectToGround(
                position, transform, out Vector3 grounded, 4f, 8f))
            return grounded;

        if (terrain != null)
        {
            position.y = terrain.SampleHeight(position) +
                         terrain.transform.position.y;
        }
        return position;
    }

    void UpdateAnimationSpeed()
    {
        if (animator == null || !hasSpeed)
            return;

        float speed;
        if (usingAgent && agent != null && agent.enabled &&
            agent.isOnNavMesh)
        {
            speed = agent.velocity.magnitude;
        }
        else
        {
            Vector3 delta = transform.position - previousPosition;
            delta.y = 0f;
            speed = delta.magnitude /
                    Mathf.Max(Time.deltaTime, .001f);
        }

        // Do not feed raw NavMesh velocity directly to the controller. It
        // fluctuates continuously while turning and used to make Walk/Run
        // fight for control around the transition threshold.
        if (locomotionActive)
        {
            if (speed <= locomotionStopSpeed)
            {
                locomotionActive = false;
                usingRunAnimation = false;
            }
        }
        else if (speed >= locomotionStartSpeed)
        {
            locomotionActive = true;
        }

        if (locomotionActive)
        {
            if (usingRunAnimation)
            {
                if (speed <= runAnimationExitSpeed)
                    usingRunAnimation = false;
            }
            else if (speed >= runAnimationEnterSpeed)
            {
                usingRunAnimation = true;
            }
        }

        float targetAnimationSpeed = !locomotionActive
            ? 0f
            : usingRunAnimation
                ? authoredRunSpeed
                : authoredWalkSpeed;
        stableAnimationSpeed = Mathf.MoveTowards(
            stableAnimationSpeed,
            targetAnimationSpeed,
            animationParameterResponse * Time.deltaTime);
        animator.SetFloat(SpeedHash, stableAnimationSpeed);

        // Meshy locomotion is in-place. Match its foot cadence to the actual
        // NavMesh/manual displacement so the body never glides over planted
        // feet. Combat reactions keep their authored timing.
        bool locomoting = !actionLocked &&
                          state != TacticalState.Attack &&
                          state != TacticalState.Hurt &&
                          state != TacticalState.Dead &&
                          locomotionActive;
        if (!locomoting)
        {
            animator.speed = 1f;
            return;
        }

        float authoredSpeed = usingRunAnimation
            ? authoredRunSpeed
            : authoredWalkSpeed;
        animator.speed = Mathf.Clamp(
            speed / Mathf.Max(.1f, authoredSpeed),
            locomotionPlaybackRange.x,
            locomotionPlaybackRange.y);
    }

    void ChangeState(TacticalState next, float duration = 0f)
    {
        state = next;
        stateUntil = duration > 0f ? Time.time + duration : 0f;
    }

    void SetCombat(bool value)
    {
        if (animator != null && hasCombat)
            animator.SetBool(CombatHash, value);
    }

    void Trigger(int hash)
    {
        if (animator == null)
            return;
        if ((hash == AttackHash && !hasAttack) ||
            (hash == AttackAltHash && !hasAttackAlt) ||
            (hash == HitHash && !hasHit) ||
            (hash == DieHash && !hasDie) ||
            (hash == BlockHash && !hasBlock))
            return;
        animator.SetTrigger(hash);
    }

    void CacheAnimatorParameters()
    {
        foreach (AnimatorControllerParameter parameter in
                 animator.parameters)
        {
            if (parameter.nameHash == SpeedHash) hasSpeed = true;
            else if (parameter.nameHash == CombatHash) hasCombat = true;
            else if (parameter.nameHash == AttackHash) hasAttack = true;
            else if (parameter.nameHash == AttackAltHash)
                hasAttackAlt = true;
            else if (parameter.nameHash == HitHash) hasHit = true;
            else if (parameter.nameHash == DieHash) hasDie = true;
            else if (parameter.nameHash == BlockHash) hasBlock = true;
        }
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static Vector3 FlatDirection(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        direction.y = 0f;
        return direction.sqrMagnitude > .0001f
            ? direction.normalized
            : Vector3.zero;
    }
}

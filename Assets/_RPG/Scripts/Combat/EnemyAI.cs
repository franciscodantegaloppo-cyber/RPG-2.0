using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState { Idle, Patrol, Chase, Attack, Hurt, Dead }

public class EnemyAI : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] float detectionRadius = 12f;
    [SerializeField] float leashRadius = 20f;
    [SerializeField] float attackRadius = 1.55f;
    [SerializeField] LayerMask playerMask;

    [Header("Movement")]
    [SerializeField] Transform[] patrolPoints;
    [SerializeField] float idleWaitTime = 1.8f;
    [SerializeField] float wanderRadius = 7f;
    [SerializeField] float wanderSpeed = 1.35f;
    [SerializeField] float chaseWalkSpeed = 2.15f;
    [SerializeField] float chaseRunSpeed = 4.15f;
    [SerializeField] float runDistance = 7f;
    [SerializeField] float turnSpeed = 540f;
    [SerializeField] float obstacleProbeDistance = 0.85f;
    [SerializeField] float obstacleSideStep = 1.2f;
    [SerializeField] LayerMask obstacleMask = ~0;

    [Header("Combat")]
    [SerializeField] float attackCooldown = 1.45f;
    [SerializeField] float attackDamageDelay = 0.42f;

    [Header("Animation Sync")]
    // Some clips (e.g. the goblin pack) have zero baked root motion - Walk/Run are pure in-place
    // loops. Animator.speed is rescaled at runtime so the leg-cycle pace matches actual travel
    // speed instead of always playing at 1x; without this, wanderSpeed and chaseWalkSpeed (which
    // share the same Walk blend state) visibly slide since only one of them can match a fixed
    // playback rate. 0 = disabled (legacy behavior, anim.speed stays 1).
    [SerializeField] float walkAnimSpeed = 0f;
    [SerializeField] float runAnimSpeed = 0f;

    NavMeshAgent agent;
    Animator anim;
    EnemyStats stats;
    SkeletonEnemyAnimatorFX animatorFx;
    Transform player;
    Terrain terrain;
    WaterEnemyEffects waterEffects;

    EnemyState state = EnemyState.Idle;
    int patrolIndex;
    float lastAttackTime;
    float idleUntil;
    float nextWanderTime;
    float lastPlayerDistance;
    Vector3 homePosition;
    Vector3 manualDestination;
    Vector3 previousPosition;
    bool useAgent;
    bool hasManualDestination;
    bool nextRightAttack;
    float approachAngleOffset;
    bool bloodRainSpeedBuffed;

    public void SetBloodRainSpeedBuff(bool enabled)
    {
        if (bloodRainSpeedBuffed == enabled) return;
        float multiplier = enabled ? 2f : .5f;
        wanderSpeed *= multiplier;
        chaseWalkSpeed *= multiplier;
        chaseRunSpeed *= multiplier;
        turnSpeed *= multiplier;
        bloodRainSpeedBuffed = enabled;
    }

    // Monotonically-increasing spawn-order counter, not GetInstanceID() (which can collide modulo
    // a small slot count between just a couple of objects, as confirmed by two goblins landing on
    // the exact same approach point in testing) - guarantees no two simultaneously-alive enemies
    // ever share a slot.
    static int approachSlotCounter;

    static readonly int HashSpeed = Animator.StringToHash("Speed");
    static readonly int HashAttack = Animator.StringToHash("Attack");
    static readonly int HashHit = Animator.StringToHash("Hit");
    static readonly int HashDie = Animator.StringToHash("Die");
    static readonly int HashDamage = Animator.StringToHash("Damage");
    static readonly int HashDeath = Animator.StringToHash("Death");
    static readonly int HashLeftAttack = Animator.StringToHash("LeftAttack");
    static readonly int HashRightAttack = Animator.StringToHash("RightAttack");

    bool hasSpeedParam;
    bool hasAttackParam;
    bool hasHitParam;
    bool hasDieParam;
    bool hasDamageParam;
    bool hasDeathParam;
    bool hasLeftAttackParam;
    bool hasRightAttackParam;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        PrepareAnimator();
        stats = GetComponent<EnemyStats>();
        animatorFx = GetComponent<SkeletonEnemyAnimatorFX>();
        terrain = Terrain.activeTerrain;
        if (GetComponent<WaterEnemyEffects>() == null)
            gameObject.AddComponent<WaterEnemyEffects>();
        waterEffects = GetComponent<WaterEnemyEffects>();
        homePosition = ProjectToGround(transform.position);
        previousPosition = transform.position;

        // Match the player's own CharacterController.slopeLimit exactly, rather than a
        // separately-tuned constant - goblins should only ever climb what the player can climb,
        // no steeper.
        GameObject playerGO = GameObject.FindWithTag("Player");
        CharacterController playerController = playerGO != null ? playerGO.GetComponent<CharacterController>() : null;
        if (playerController != null)
            maxWalkableSlopeAngle = playerController.slopeLimit;

        if (playerMask.value == 0)
            playerMask = LayerMask.GetMask("Player");

        // obstacleMask deliberately excludes the Enemy layer (so they don't treat each other as
        // walls), which means nothing stops multiple attackers from converging on the exact same
        // point next to the player and visibly overlapping/clipping through each other. Giving
        // each instance a fixed compass-direction slot around the target spreads them into a fan
        // instead of a pile.
        approachAngleOffset = (approachSlotCounter % 8) * 45f;
        approachSlotCounter++;

        if (agent != null)
        {
            agent.speed = wanderSpeed;
            agent.angularSpeed = turnSpeed;
            agent.stoppingDistance = attackRadius * 0.75f;
            agent.updateRotation = true;
            useAgent = agent.isOnNavMesh;
        }

        CacheAnimatorParameters();
    }

    void OnEnable()
    {
        PrepareAnimator();
        idleUntil = Time.time + Random.Range(0.3f, 1.1f);
        nextWanderTime = Time.time;
        ChangeState(EnemyState.Patrol);
    }

    void Start()
    {
        if (useAgent && patrolPoints != null && patrolPoints.Length > 0)
            MoveTo(patrolPoints[patrolIndex].position, wanderSpeed);
    }

    void Update()
    {
        if (state == EnemyState.Dead)
            return;

        float speed = CurrentPlanarSpeed();
        SetAnimatorSpeed(speed);

        switch (state)
        {
            case EnemyState.Idle:
            case EnemyState.Patrol:
                PatrolBehavior();
                DetectPlayer();
                break;
            case EnemyState.Chase:
                ChaseBehavior();
                break;
            case EnemyState.Attack:
                AttackBehavior();
                break;
        }

        previousPosition = transform.position;
    }

    void LateUpdate()
    {
        if (state == EnemyState.Dead || useAgent)
            return;

        if ((state == EnemyState.Patrol || state == EnemyState.Idle) && hasManualDestination && Time.time >= idleUntil)
            ManualMoveTo(manualDestination, wanderSpeed);
        else if (state == EnemyState.Chase && player != null)
            ManualMoveTo(player.position, ChaseSpeed(Vector3.Distance(transform.position, player.position)));
    }

    void DetectPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, playerMask);
        if (hits.Length == 0)
            return;

        player = hits[0].transform;
        lastPlayerDistance = Vector3.Distance(transform.position, player.position);
        ChangeState(EnemyState.Chase);
    }

    void PatrolBehavior()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            if (ReachedDestination())
                StartCoroutine(WaitAndNextPatrol());
            return;
        }

        if (Time.time < idleUntil)
        {
            StopMoving();
            return;
        }

        if (!hasManualDestination || ReachedDestination() || Time.time >= nextWanderTime)
        {
            Vector2 random = Random.insideUnitCircle * wanderRadius;
            MoveTo(homePosition + new Vector3(random.x, 0f, random.y), wanderSpeed);
            nextWanderTime = Time.time + Random.Range(3f, 6f);
        }
    }

    IEnumerator WaitAndNextPatrol()
    {
        ChangeState(EnemyState.Idle);
        StopMoving();
        yield return new WaitForSeconds(idleWaitTime);

        if (state == EnemyState.Dead)
            yield break;

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            ChangeState(EnemyState.Patrol);
            yield break;
        }

        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        MoveTo(patrolPoints[patrolIndex].position, wanderSpeed);
        ChangeState(EnemyState.Patrol);
    }

    void ChaseBehavior()
    {
        if (player == null)
        {
            ResetToWander();
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > leashRadius)
        {
            ResetToWander();
            return;
        }

        if (distance <= attackRadius)
        {
            StopMoving();
            ChangeState(EnemyState.Attack);
        }
        else
        {
            MoveTo(ApproachPointAroundPlayer(attackRadius * 0.85f), ChaseSpeed(distance));
        }

        lastPlayerDistance = distance;
    }

    Vector3 ApproachPointAroundPlayer(float radius)
    {
        float rad = approachAngleOffset * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        return player.position + dir * radius;
    }

    void AttackBehavior()
    {
        if (player == null)
        {
            ResetToWander();
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > leashRadius)
        {
            ResetToWander();
            return;
        }

        if (distance > attackRadius * 1.25f)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        FacePlayer();
        float actionScale = waterEffects != null ? waterEffects.ActionScale : 1f;
        if (Time.time - lastAttackTime < attackCooldown / actionScale)
            return;

        lastAttackTime = Time.time;
        TriggerAttackAnimation();
        StartCoroutine(DealDamageDelay());
    }

    IEnumerator DealDamageDelay()
    {
        float actionScale = waterEffects != null ? waterEffects.ActionScale : 1f;
        yield return new WaitForSeconds(attackDamageDelay / actionScale);
        if (state == EnemyState.Dead || player == null)
            yield break;
        if (Vector3.Distance(transform.position, player.position) <= attackRadius * 1.18f)
            player.GetComponent<PlayerStats>()?.TakeDamage(
                stats != null ? stats.Attack : 8f, transform.position);
    }

    public void OnHurt(Vector3 attackerPosition)
    {
        if (state == EnemyState.Dead)
            return;

        animatorFx?.PlayHit(attackerPosition);
        TriggerHitAnimation();
    }

    public void OnDeath()
    {
        ChangeState(EnemyState.Dead);
        StopAllCoroutines();
        StopMoving();
        if (agent != null)
            agent.enabled = false;

        TriggerDeathAnimation();
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        animatorFx?.PlayDeath();
        Destroy(gameObject, 3f);
    }

    float ChaseSpeed(float distance)
    {
        float playerDelta = Mathf.Abs(distance - lastPlayerDistance) / Mathf.Max(Time.deltaTime, 0.001f);
        return distance >= runDistance || playerDelta > 4.5f ? chaseRunSpeed : chaseWalkSpeed;
    }

    bool ReachedDestination()
    {
        if (useAgent && agent != null && agent.enabled && agent.isOnNavMesh)
            return !agent.pathPending && agent.remainingDistance <= Mathf.Max(0.45f, agent.stoppingDistance + 0.1f);

        return !hasManualDestination || Vector3.Distance(transform.position, manualDestination) <= 0.45f;
    }

    void MoveTo(Vector3 destination, float speed)
    {
        if (waterEffects != null) speed *= waterEffects.MovementScale;
        destination = ProjectToGround(destination);
        manualDestination = destination;
        hasManualDestination = true;

        if (useAgent && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = speed;
            agent.SetDestination(destination);
        }
    }

    void StopMoving()
    {
        hasManualDestination = false;
        if (useAgent && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    void ManualMoveTo(Vector3 destination, float speed)
    {
        if (waterEffects != null) speed *= waterEffects.MovementScale;
        Vector3 flatTarget = new Vector3(destination.x, transform.position.y, destination.z);
        Vector3 direction = flatTarget - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.04f)
            return;

        Vector3 moveDirection = AvoidObstacles(direction.normalized);
        if (moveDirection.sqrMagnitude < 0.001f)
            return;

        // Blocked()'s horizontal capsule-cast is a wall check - it can miss a gradually rising
        // slope entirely (the cast just passes over ground that curves away beneath it), and
        // ProjectToGround below has no steepness gate of its own, so without this the goblin
        // could walk up an arbitrarily steep hillside one small step at a time. Check the actual
        // destination's slope directly instead, against the SAME limit the player's own
        // CharacterController enforces (see maxWalkableSlopeAngle) - not a separately-tuned value
        // that just happens to match today.
        Vector3 proposed = transform.position + moveDirection * speed * Time.deltaTime;
        if (IsGroundTooSteepAt(proposed))
            return;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        transform.position = ProjectToGround(proposed);
    }

    Vector3 AvoidObstacles(Vector3 desiredDirection)
    {
        if (!Blocked(desiredDirection))
            return desiredDirection;

        Vector3 right = Vector3.Cross(Vector3.up, desiredDirection).normalized;
        Vector3 leftCandidate = (desiredDirection + right * obstacleSideStep).normalized;
        Vector3 rightCandidate = (desiredDirection - right * obstacleSideStep).normalized;

        bool leftBlocked = Blocked(leftCandidate);
        bool rightBlocked = Blocked(rightCandidate);

        if (!leftBlocked && !rightBlocked)
            return Vector3.Distance(transform.position + leftCandidate, player != null ? player.position : manualDestination) <
                Vector3.Distance(transform.position + rightCandidate, player != null ? player.position : manualDestination)
                ? leftCandidate
                : rightCandidate;

        if (!leftBlocked)
            return leftCandidate;
        if (!rightBlocked)
            return rightCandidate;

        return Vector3.zero;
    }

    bool Blocked(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.001f)
            return true;

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        Vector3 center = transform.position + (capsule != null ? capsule.center : new Vector3(0f, 0.9f, 0f));
        float height = capsule != null ? Mathf.Max(capsule.height, capsule.radius * 2f) : 1.8f;
        float radius = capsule != null ? capsule.radius : 0.35f;
        Vector3 half = Vector3.up * Mathf.Max(0f, height * 0.5f - radius);
        Vector3 bottom = center - half;
        Vector3 top = center + half;

        if (!Physics.CapsuleCast(bottom, top, radius * 0.92f, direction, out RaycastHit hit, obstacleProbeDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        // A gentle hillside reads as a solid wall to this horizontal capsule cast, even though
        // it's walkable ground (ProjectToGround already keeps the enemy glued to its surface each
        // frame) - without this, enemies without a NavMeshAgent (e.g. the goblins) would try both
        // side-step candidates against the same slope, find both blocked too, and just stop dead
        // instead of climbing it, reading as "stuck" the moment the player led them onto a hill.
        // But treating EVERY terrain hit as passable regardless of steepness let them scale
        // near-vertical faces the same way - check the actual surface normal and only pass gentle
        // slopes through; steep ones still block like a wall.
        if (hit.collider is TerrainCollider || hit.normal.y > 0.01f)
            return IsGroundTooSteepAt(hit.point);

        return hit.collider != null && !hit.collider.transform.IsChildOf(transform);
    }

    // Fallback only - overwritten in Awake() from the player's actual CharacterController.slopeLimit
    // whenever the player exists, so this constant only matters if that lookup fails.
    float maxWalkableSlopeAngle = 45f;

    bool IsGroundTooSteepAt(Vector3 worldPoint)
    {
        if (TryGetGroundNormal(worldPoint, out Vector3 normal))
            return Vector3.Angle(normal, Vector3.up) > maxWalkableSlopeAngle;

        return false;
    }

    bool TryGetGroundNormal(Vector3 worldPoint, out Vector3 normal)
    {
        normal = Vector3.up;
        if (Physics.Raycast(worldPoint + Vector3.up * 2.5f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(transform))
            {
                normal = hit.normal;
                return true;
            }
        }

        if (terrain == null)
            return false;

        TerrainData data = terrain.terrainData;
        Vector3 local = worldPoint - terrain.transform.position;
        float normX = Mathf.Clamp01(local.x / data.size.x);
        float normZ = Mathf.Clamp01(local.z / data.size.z);
        normal = data.GetInterpolatedNormal(normX, normZ);
        return true;
    }

    Vector3 ProjectToGround(Vector3 position)
    {
        if (GroundUtility.TryProjectToGround(position, transform, out Vector3 grounded, 5f, 8f))
            return grounded;

        if (terrain != null)
        {
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position;
        }

        // No Terrain in this scene (e.g. the dungeon's flat floor meshes are plain colliders,
        // not a Terrain component) - fall back to a downward raycast against the floor so
        // enemies don't stay frozen at their spawn height and sink through sloped/uneven spots.
        Vector3 origin = position + Vector3.up * 3f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y;
        return position;
    }

    float CurrentPlanarSpeed()
    {
        if (useAgent && agent != null && agent.enabled && agent.isOnNavMesh)
            return agent.velocity.magnitude;

        Vector3 delta = transform.position - previousPosition;
        delta.y = 0f;
        return delta.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
    }

    void FacePlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    void ResetToWander()
    {
        player = null;
        idleUntil = Time.time + Random.Range(0.6f, 1.6f);
        nextWanderTime = Time.time;
        StopMoving();
        ChangeState(EnemyState.Patrol);
    }

    void ChangeState(EnemyState newState) => state = newState;

    void SetAnimatorSpeed(float speed)
    {
        if (anim == null || !hasSpeedParam)
            return;

        float value = speed <= 0.05f ? 0f : speed >= chaseRunSpeed * 0.75f ? 1f : 0.5f;
        anim.SetFloat(HashSpeed, value, 0.12f, Time.deltaTime);

        float reference = value >= 1f ? runAnimSpeed : walkAnimSpeed;
        float playback = speed <= 0.05f || reference <= 0.01f ? 1f : Mathf.Clamp(speed / reference, 0.5f, 2.5f);
        // Attack states have no locomotion speed to derive playback from, so
        // explicitly halve them while underwater.
        if (state == EnemyState.Attack && waterEffects != null && waterEffects.IsInWater)
            playback *= waterEffects.ActionScale;
        anim.speed = playback;
    }

    void TriggerAttackAnimation()
    {
        animatorFx?.PlayAttack();

        if (anim == null || anim.runtimeAnimatorController == null)
            return;

        if (hasLeftAttackParam || hasRightAttackParam)
        {
            nextRightAttack = !nextRightAttack;
            if (nextRightAttack && hasRightAttackParam) anim.SetTrigger(HashRightAttack);
            else if (hasLeftAttackParam) anim.SetTrigger(HashLeftAttack);
            else anim.SetTrigger(HashRightAttack);
            return;
        }

        if (hasAttackParam)
            anim.SetTrigger(HashAttack);
    }

    void TriggerHitAnimation()
    {
        if (anim == null)
            return;

        if (hasDamageParam) anim.SetTrigger(HashDamage);
        else if (hasHitParam) anim.SetTrigger(HashHit);
    }

    void TriggerDeathAnimation()
    {
        if (anim == null)
            return;

        if (hasDeathParam) anim.SetTrigger(HashDeath);
        else if (hasDieParam) anim.SetTrigger(HashDie);
    }

    void CacheAnimatorParameters()
    {
        hasSpeedParam = false;
        hasAttackParam = false;
        hasHitParam = false;
        hasDieParam = false;
        hasDamageParam = false;
        hasDeathParam = false;
        hasLeftAttackParam = false;
        hasRightAttackParam = false;

        if (anim == null || anim.runtimeAnimatorController == null)
            return;

        foreach (var parameter in anim.parameters)
        {
            if (parameter.nameHash == HashSpeed && parameter.type == AnimatorControllerParameterType.Float) hasSpeedParam = true;
            if (parameter.nameHash == HashAttack && parameter.type == AnimatorControllerParameterType.Trigger) hasAttackParam = true;
            if (parameter.nameHash == HashHit && parameter.type == AnimatorControllerParameterType.Trigger) hasHitParam = true;
            if (parameter.nameHash == HashDie && parameter.type == AnimatorControllerParameterType.Trigger) hasDieParam = true;
            if (parameter.nameHash == HashDamage && parameter.type == AnimatorControllerParameterType.Trigger) hasDamageParam = true;
            if (parameter.nameHash == HashDeath && parameter.type == AnimatorControllerParameterType.Trigger) hasDeathParam = true;
            if (parameter.nameHash == HashLeftAttack && parameter.type == AnimatorControllerParameterType.Trigger) hasLeftAttackParam = true;
            if (parameter.nameHash == HashRightAttack && parameter.type == AnimatorControllerParameterType.Trigger) hasRightAttackParam = true;
        }
    }

    void PrepareAnimator()
    {
        if (anim == null)
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            foreach (Animator candidate in animators)
            {
                if (candidate == null)
                    continue;

                if (anim == null || candidate.runtimeAnimatorController != null)
                    anim = candidate;

                if (candidate.runtimeAnimatorController != null)
                    break;
            }
        }

        if (anim == null)
            return;

        anim.enabled = true;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.updateMode = AnimatorUpdateMode.Normal;
        anim.speed = 1f;
        CacheAnimatorParameters();
    }

    void OnValidate()
    {
        detectionRadius = Mathf.Max(attackRadius + 0.5f, detectionRadius);
        leashRadius = Mathf.Max(detectionRadius, leashRadius);
        wanderRadius = Mathf.Max(1f, wanderRadius);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, leashRadius);
    }
}

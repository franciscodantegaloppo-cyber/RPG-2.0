using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// A deliberate melee opponent: it observes, circles, commits to short attack
// windows and disengages instead of continuously running into the player.
[RequireComponent(typeof(EnemyStats))]
public sealed class OrcWarriorAI : MonoBehaviour
{
    enum TacticalState
    {
        Guard,
        Investigate,
        Pursue,
        Circle,
        Attack,
        Retreat,
        Hurt,
        Dead
    }

    [Header("Awareness")]
    [SerializeField] float detectionRadius = 16f;
    [SerializeField] float loseInterestRadius = 28f;
    [SerializeField] float homeLeashRadius = 34f;
    [SerializeField] float eyeHeight = 1.65f;
    [SerializeField] float decisionInterval = .12f;
    [SerializeField] LayerMask sightMask = ~0;

    [Header("Movement")]
    [SerializeField] float patrolRadius = 6f;
    [SerializeField] float walkSpeed = 1.75f;
    [SerializeField] float pursueSpeed = 4.25f;
    [SerializeField] float circleSpeed = 2.05f;
    [SerializeField] float retreatSpeed = 3.15f;
    [SerializeField] float turnSpeed = 520f;
    [SerializeField] float preferredRange = 2.15f;
    [SerializeField] float attackRange = 2.35f;
    [SerializeField] float personalSpace = 1.25f;

    [Header("Combat")]
    [SerializeField] float attackCooldown = 1.55f;
    [SerializeField] float primaryHitTime = .43f;
    [SerializeField] float alternateHitTime = .56f;
    [SerializeField] float recoveryTime = .48f;
    [SerializeField, Range(0f, 1f)] float blockChance = .22f;
    [SerializeField] float blockCooldown = 3.4f;
    [SerializeField, Range(0f, 1f)] float retreatChance = .48f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int CombatHash = Animator.StringToHash("Combat");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int AttackAltHash = Animator.StringToHash("AttackAlt");
    static readonly int HitHash = Animator.StringToHash("Hit");
    static readonly int DieHash = Animator.StringToHash("Die");
    static readonly int BlockHash = Animator.StringToHash("Block");
    static readonly int DodgeHash = Animator.StringToHash("Dodge");

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
    int circleDirection;
    bool hasDestination;
    bool usingAgent;
    bool actionLocked;
    bool deathHandled;

    bool hasSpeed;
    bool hasCombat;
    bool hasAttack;
    bool hasAttackAlt;
    bool hasHit;
    bool hasDie;
    bool hasBlock;
    bool hasDodge;

    public bool IsDead => state == TacticalState.Dead;

    void Awake()
    {
        stats = GetComponent<EnemyStats>();
        animator = GetComponentInChildren<Animator>(true);
        agent = GetComponent<NavMeshAgent>();
        terrain = Terrain.activeTerrain;
        homePosition = ProjectToGround(transform.position);
        previousPosition = transform.position;
        circleDirection = Random.value < .5f ? -1 : 1;

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

        ChangeState(TacticalState.Guard, Random.Range(.6f, 1.4f));
    }

    void OnEnable()
    {
        nextDecisionTime = Time.time + Random.Range(.05f, .18f);
        nextPatrolTime = Time.time + Random.Range(1f, 2.5f);
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
            GuardArea();
            return;
        }

        float distance = FlatDistance(transform.position, player.position);
        float homeDistance = FlatDistance(transform.position, homePosition);
        if (distance > loseInterestRadius || homeDistance > homeLeashRadius)
        {
            player = null;
            ChangeState(TacticalState.Guard, Random.Range(.4f, 1f));
            MoveTo(homePosition, walkSpeed);
            return;
        }

        SetCombat(true);

        if (!HasLineOfSight(player))
        {
            ChangeState(TacticalState.Investigate, 1.4f);
            MoveTo(player.position, walkSpeed);
            return;
        }

        if (state == TacticalState.Retreat && Time.time < stateUntil)
        {
            Vector3 away = FlatDirection(player.position, transform.position);
            Vector3 side = Vector3.Cross(Vector3.up, away) *
                           circleDirection * .45f;
            MoveTo(transform.position + (away + side).normalized * 3.2f,
                retreatSpeed);
            return;
        }

        if (distance < personalSpace && Random.value < .72f)
        {
            ChangeState(TacticalState.Retreat,
                Random.Range(.4f, .8f));
            return;
        }

        if (distance > preferredRange * 1.55f)
        {
            ChangeState(TacticalState.Pursue);
            Vector3 offset = CircleDirectionFromPlayer() *
                             preferredRange * .82f;
            MoveTo(player.position + offset, pursueSpeed);
            return;
        }

        if (distance <= attackRange && Time.time >= nextAttackTime)
        {
            StartCoroutine(AttackRoutine(Random.value > .58f));
            return;
        }

        // At fighting distance the orc keeps changing angle so it cannot be
        // defeated by simply holding the attack button in one direction.
        ChangeState(TacticalState.Circle, Random.Range(.7f, 1.45f));
        Vector3 radial = FlatDirection(player.position, transform.position);
        Vector3 tangent = Vector3.Cross(Vector3.up, radial) * circleDirection;
        float rangeCorrection = Mathf.Clamp(
            preferredRange - distance, -.8f, .8f);
        Vector3 tacticalDirection =
            (tangent + radial * rangeCorrection * .7f).normalized;
        MoveTo(transform.position + tacticalDirection * 2.25f, circleSpeed);

        if (Random.value < .08f)
            circleDirection *= -1;
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
        if (!HasLineOfSight(candidate))
            return;

        player = candidate;
        circleDirection = Random.value < .5f ? -1 : 1;
        ChangeState(TacticalState.Investigate, .35f);
    }

    IEnumerator AttackRoutine(bool alternate)
    {
        actionLocked = true;
        ChangeState(TacticalState.Attack);
        StopMoving();
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
        if (Random.value < retreatChance)
        {
            if (hasDodge && Random.value < .45f)
                Trigger(DodgeHash);
            ChangeState(TacticalState.Retreat,
                Random.Range(.55f, 1.05f));
        }
        else
        {
            ChangeState(TacticalState.Circle,
                Random.Range(.55f, 1.15f));
        }
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
        if (hasBlock)
            Trigger(BlockHash);
        yield return new WaitForSeconds(.52f);
        if (state != TacticalState.Dead)
        {
            actionLocked = false;
            ChangeState(TacticalState.Circle, .65f);
        }
    }

    public void OnHurt(Vector3 attackerPosition, float damage)
    {
        if (state == TacticalState.Dead)
            return;

        StopAllCoroutines();
        actionLocked = false;
        Trigger(HitHash);
        ChangeState(TacticalState.Hurt, .28f);
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + .45f);

        if (player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }
    }

    public void OnDeath()
    {
        if (deathHandled)
            return;
        deathHandled = true;
        state = TacticalState.Dead;
        StopAllCoroutines();
        StopMoving();
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
        switch (state)
        {
            case TacticalState.Pursue:
                return pursueSpeed;
            case TacticalState.Circle:
                return circleSpeed;
            case TacticalState.Retreat:
                return retreatSpeed;
            default:
                return walkSpeed;
        }
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

    bool HasLineOfSight(Transform target)
    {
        if (target == null)
            return false;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 end = target.position + Vector3.up * 1.05f;
        Vector3 direction = end - origin;
        if (!Physics.Raycast(origin, direction.normalized,
                out RaycastHit hit, direction.magnitude, sightMask,
                QueryTriggerInteraction.Ignore))
            return true;

        return hit.transform == target ||
               hit.transform.IsChildOf(target) ||
               hit.transform.GetComponentInParent<PlayerStats>() != null;
    }

    Vector3 CircleDirectionFromPlayer()
    {
        if (player == null)
            return -transform.forward;
        float angle = circleDirection * Random.Range(32f, 58f);
        Vector3 radial =
            FlatDirection(player.position, transform.position);
        return Quaternion.Euler(0f, angle, 0f) * radial;
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

        animator.SetFloat(SpeedHash, speed, .12f, Time.deltaTime);
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
            (hash == BlockHash && !hasBlock) ||
            (hash == DodgeHash && !hasDodge))
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
            else if (parameter.nameHash == DodgeHash) hasDodge = true;
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

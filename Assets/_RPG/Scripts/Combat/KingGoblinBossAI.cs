using System.Collections;
using UnityEngine;

public class KingGoblinBossAI : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] float detectionRadius = 35f;
    [SerializeField] float attackRadius = 2.4f;
    [SerializeField] float superRunDistance = 20f;

    [Header("Movement")]
    [SerializeField] float walkSpeed = 1.35f;
    [SerializeField] float runSpeed = 4.6f;
    [SerializeField] float superRunSpeed = 7f;
    // Boss walks (not runs) once it's closed to within this range but is still outside attack
    // range - previously MoveTowardPlayer only ever picked run/superRun, so the Walk animator
    // state (and walkSpeed) was configured but never actually reached in gameplay.
    [SerializeField] float walkDistance = 6f;
    // Reference speed each clip's in-place leg-cycle naturally represents at animator.speed=1 -
    // this is the clip's OWN native pace, deliberately kept separate from runSpeed/superRunSpeed
    // above (unlike walkAnimSpeed, which still matches walkSpeed 1:1). Movement was sped up past
    // the clip's native pace on purpose so the boss chases faster; SetAnimatorPlayback rescales
    // animator.speed to actualSpeed/reference each frame, so the leg-cycle automatically speeds up
    // to stay in sync with the faster translation instead of the stride visually stretching (which
    // is what reads as sliding/surfing). Keep these at the clip's original authored pace - raise
    // runSpeed/superRunSpeed above to make the boss faster, not these.
    [SerializeField] float walkAnimSpeed = 1.35f;
    [SerializeField] float runAnimSpeed = 3.05f;
    [SerializeField] float superRunAnimSpeed = 4.65f;
    [SerializeField] float turnSpeed = 620f;
    [SerializeField] float gravity = 18f;
    [SerializeField] float obstacleProbeDistance = 1.05f;
    [SerializeField] float obstacleSideStep = 1.65f;
    [SerializeField] LayerMask obstacleMask = ~0;

    [Header("Combat")]
    [SerializeField] float attackCooldown = 1.8f;
    [SerializeField] float attackDamageDelay = 0.45f;
    [SerializeField] float jumpCooldown = 5f;
    [SerializeField] float jumpDistanceMin = 8f;
    [SerializeField] float jumpForce = 6.5f;
    [SerializeField] float jumpForwardSpeed = 2.1f;

    EnemyStats stats;
    Animator animator;
    Transform player;
    Terrain terrain;
    Vector3 verticalVelocity;
    float lastAttackTime;
    float lastJumpTime;
    bool dead;
    bool attacking;
    bool jumping;
    int nextAttack;
    CapsuleCollider capsule;
    CharacterController controller;
    WaterEnemyEffects waterEffects;

    static readonly int HashSpeed = Animator.StringToHash("Speed");
    static readonly int HashAttack1 = Animator.StringToHash("Attack1");
    static readonly int HashAttack2 = Animator.StringToHash("Attack2");
    static readonly int HashAttack3 = Animator.StringToHash("Attack3");
    static readonly int HashJump = Animator.StringToHash("Jump");
    static readonly int HashHit = Animator.StringToHash("Hit");
    static readonly int HashDie = Animator.StringToHash("Die");

    void Awake()
    {
        // Match the player's own CharacterController.slopeLimit exactly, rather than a
        // separately-tuned constant - the boss should only ever climb what the player can climb,
        // no steeper. Must run before ConfigureControllerFromCapsule() below, which consumes it.
        GameObject playerGO = GameObject.FindWithTag("Player");
        CharacterController playerController = playerGO != null ? playerGO.GetComponent<CharacterController>() : null;
        if (playerController != null)
            maxWalkableSlopeAngle = playerController.slopeLimit;

        stats = GetComponent<EnemyStats>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
            animator.applyRootMotion = false;
        capsule = GetComponent<CapsuleCollider>();
        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();
        ConfigureControllerFromCapsule();
        if (GetComponent<WaterEnemyEffects>() == null)
            gameObject.AddComponent<WaterEnemyEffects>();
        waterEffects = GetComponent<WaterEnemyEffects>();
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        terrain = Terrain.activeTerrain;

        if (obstacleMask.value == ~0)
            obstacleMask = ~LayerMask.GetMask("Enemy", "Player", "Interactable");

        EnemyHealthBar normalBar = GetComponent<EnemyHealthBar>();
        if (normalBar != null)
            Destroy(normalBar);

        if (GetComponent<KingGoblinHealthBar>() == null)
            gameObject.AddComponent<KingGoblinHealthBar>();

        KingGoblinVisualRootLock rootLock = GetComponent<KingGoblinVisualRootLock>();
        if (rootLock != null)
            rootLock.enabled = false;

        if (GetComponent<KingGoblinAnimationRootStabilizer>() == null)
            gameObject.AddComponent<KingGoblinAnimationRootStabilizer>();
    }

    void OnEnable()
    {
        if (stats != null)
            stats.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (stats != null)
            stats.OnDeath -= HandleDeath;
    }

    void Update()
    {
        if (dead)
            return;

        // Snapshot BEFORE this frame's movement happens, so the measured speed below reflects
        // what actually moved THIS frame. Comparing against a position captured at the end of the
        // PREVIOUS frame (the old approach) always measured zero, because nothing moves between
        // the end of one Update() and the start of the next - that permanently pinned
        // measuredSpeed near 0 and animator.speed at its 0.5 clamp floor regardless of how fast
        // the boss was actually translating, which is what read as sliding/surfing.
        Vector3 frameStartPosition = transform.position;

        AcquirePlayer();
        if (player == null)
        {
            SetSpeed(0f, 0f);
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (!jumping && distance >= jumpDistanceMin && Time.time - lastJumpTime >= jumpCooldown && Random.value < 0.35f)
        {
            StartCoroutine(JumpAttack());
            return;
        }

        if (distance <= attackRadius)
        {
            FacePlayer();
            SetSpeed(0f, 0f);
            TryAttack();
            return;
        }

        if (!attacking && !jumping)
            MoveTowardPlayer(distance, frameStartPosition);
    }

    void AcquirePlayer()
    {
        if (player != null)
            return;

        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null)
            return;

        if (Vector3.Distance(transform.position, found.transform.position) <= detectionRadius)
            player = found.transform;
    }

    void MoveTowardPlayer(float distance, Vector3 frameStartPosition)
    {
        // Three real movement tiers now (previously only run/superRun ever ran, so the Walk
        // animator state and walkSpeed were configured but unreachable in actual gameplay).
        float speed;
        float speedParam;
        if (distance <= walkDistance)
        {
            speed = walkSpeed;
            speedParam = 0.5f;
        }
        else if (distance > superRunDistance)
        {
            speed = superRunSpeed;
            speedParam = 2f;
        }
        else
        {
            speed = runSpeed;
            speedParam = 1f;
        }

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector3 moveDirection = AvoidObstacles(direction.normalized);
        if (moveDirection.sqrMagnitude < 0.001f)
        {
            SetSpeed(0f, 0f);
            return;
        }

        FaceDirection(moveDirection.normalized);
        MoveWithCollision(moveDirection.normalized, speed);

        // Measured AFTER the move above actually happened this frame, against the position from
        // the start of THIS frame - not a snapshot left over from the previous frame.
        float measuredSpeed = (transform.position - frameStartPosition).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
        SetSpeed(speedParam, measuredSpeed);
    }

    void TryAttack()
    {
        float actionScale = waterEffects != null ? waterEffects.ActionScale : 1f;
        if (attacking || Time.time - lastAttackTime < attackCooldown / actionScale)
            return;

        lastAttackTime = Time.time;
        attacking = true;
        nextAttack = (nextAttack + 1) % 3;
        if (animator != null)
        {
            animator.speed = actionScale;
            if (nextAttack == 0) animator.SetTrigger(HashAttack1);
            else if (nextAttack == 1) animator.SetTrigger(HashAttack2);
            else animator.SetTrigger(HashAttack3);
        }
        StartCoroutine(DealDamageAfterDelay(attackDamageDelay / actionScale));
        StartCoroutine(UnlockAttack(attackCooldown * 0.75f / actionScale));
    }

    IEnumerator JumpAttack()
    {
        jumping = true;
        lastJumpTime = Time.time;
        animator?.SetTrigger(HashJump);

        float actionScale = waterEffects != null ? waterEffects.ActionScale : 1f;
        float gravityScale = waterEffects != null ? waterEffects.GravityScale : 1f;
        float duration = 0.85f / actionScale;
        float elapsed = 0f;
        verticalVelocity.y = jumpForce * (waterEffects != null ? waterEffects.JumpScale : 1f);
        while (elapsed < duration && !dead)
        {
            elapsed += Time.deltaTime;
            Vector3 direction = player != null ? player.position - transform.position : transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                Vector3 jumpDirection = AvoidObstacles(direction.normalized);
                FaceDirection(jumpDirection);
                MoveWithCollision(jumpDirection, jumpForwardSpeed, false);
            }

            verticalVelocity.y -= gravity * gravityScale * Time.deltaTime;
            if (controller != null && controller.enabled)
                controller.Move(verticalVelocity * Time.deltaTime);
            else
                transform.position += verticalVelocity * Time.deltaTime;
            ClampToGround(false);
            SetSpeed(1.2f, jumpForwardSpeed);
            yield return null;
        }

        ApplyGrounding();
        jumping = false;
        if (player != null && Vector3.Distance(transform.position, player.position) <= attackRadius * 1.35f)
            player.GetComponent<PlayerStats>()?.TakeDamage(stats != null ? stats.Attack : 500f, transform.position);
    }

    IEnumerator DealDamageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!dead && player != null && Vector3.Distance(transform.position, player.position) <= attackRadius * 1.25f)
            player.GetComponent<PlayerStats>()?.TakeDamage(stats != null ? stats.Attack : 500f, transform.position);
    }

    IEnumerator UnlockAttack(float delay)
    {
        yield return new WaitForSeconds(delay);
        attacking = false;
    }

    public void OnHurt()
    {
        if (!dead && !attacking && !jumping)
            SetSpeed(0f, 0f);
    }

    void HandleDeath()
    {
        dead = true;
        StopAllCoroutines();
        SetSpeed(0f, 0f);
        animator?.SetTrigger(HashDie);
        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;
        if (controller != null)
            controller.enabled = false;
        Destroy(gameObject, 6f);
    }

    void FacePlayer()
    {
        if (player == null)
            return;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    void ApplyGrounding()
    {
        verticalVelocity = Vector3.zero;
        ClampToGround(true);
    }

    void ClampToGround(bool force)
    {
        Vector3 pos = transform.position;
        float groundY = pos.y;
        if (terrain != null)
            groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;
        else if (Physics.Raycast(pos + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            groundY = hit.point.y;

        if (force || pos.y < groundY)
        {
            pos.y = groundY;
            transform.position = pos;
            verticalVelocity.y = 0f;
        }
    }

    void MoveWithCollision(Vector3 direction, float speed, bool groundAfterMove = true)
    {
        if (direction.sqrMagnitude < 0.001f)
            return;

        if (waterEffects != null) speed *= waterEffects.MovementScale;

        float distance = speed * Time.deltaTime;
        if (CapsuleBlocked(direction, distance + 0.08f, out RaycastHit hit))
        {
            Vector3 slide = Vector3.ProjectOnPlane(direction, hit.normal);
            slide.y = 0f;
            if (slide.sqrMagnitude < 0.001f || CapsuleBlocked(slide.normalized, distance + 0.03f, out _))
                return;

            direction = slide.normalized;
        }

        if (controller != null && controller.enabled)
            controller.Move(direction * distance);
        else
            transform.position += direction * distance;

        if (groundAfterMove)
            ApplyGrounding();

        ResolveOverlaps();
    }

    Vector3 AvoidObstacles(Vector3 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude < 0.001f)
            return Vector3.zero;

        if (!CapsuleBlocked(desiredDirection, obstacleProbeDistance, out _))
            return desiredDirection;

        Vector3 right = Vector3.Cross(Vector3.up, desiredDirection).normalized;
        Vector3 leftCandidate = (desiredDirection + right * obstacleSideStep).normalized;
        Vector3 rightCandidate = (desiredDirection - right * obstacleSideStep).normalized;

        bool leftBlocked = CapsuleBlocked(leftCandidate, obstacleProbeDistance, out _);
        bool rightBlocked = CapsuleBlocked(rightCandidate, obstacleProbeDistance, out _);

        if (!leftBlocked && !rightBlocked && player != null)
        {
            float leftDistance = Vector3.Distance(transform.position + leftCandidate, player.position);
            float rightDistance = Vector3.Distance(transform.position + rightCandidate, player.position);
            return leftDistance < rightDistance ? leftCandidate : rightCandidate;
        }

        if (!leftBlocked)
            return leftCandidate;
        if (!rightBlocked)
            return rightCandidate;

        return Vector3.zero;
    }

    bool CapsuleBlocked(Vector3 direction, float distance, out RaycastHit hit)
    {
        hit = default;
        if (direction.sqrMagnitude < 0.001f)
            return true;

        GetCapsulePoints(out Vector3 bottom, out Vector3 top, out float radius);
        if (!Physics.CapsuleCast(bottom, top, radius, direction.normalized, out hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
            return false;

        // Same reasoning as EnemyAI.Blocked(): a gentle hillside reads as a wall to this
        // horizontal capsule cast even though it's walkable, but treating EVERY terrain hit as
        // passable let the boss scale near-vertical faces the same way as a gentle slope. Check
        // the real surface normal instead of blanket-exempting terrain.
        if (hit.collider is TerrainCollider)
            return IsTerrainTooSteepAt(hit.point);

        return true;
    }

    // Fallback only - overwritten in Awake() from the player's actual CharacterController.slopeLimit
    // whenever the player exists, so this constant only matters if that lookup fails.
    float maxWalkableSlopeAngle = 45f;

    bool IsTerrainTooSteepAt(Vector3 worldPoint)
    {
        if (terrain == null)
            return false;

        TerrainData data = terrain.terrainData;
        Vector3 local = worldPoint - terrain.transform.position;
        float normX = Mathf.Clamp01(local.x / data.size.x);
        float normZ = Mathf.Clamp01(local.z / data.size.z);
        Vector3 normal = data.GetInterpolatedNormal(normX, normZ);
        return Vector3.Angle(normal, Vector3.up) > maxWalkableSlopeAngle;
    }

    void GetCapsulePoints(out Vector3 bottom, out Vector3 top, out float radius)
    {
        Vector3 center = transform.position + (capsule != null ? capsule.center : new Vector3(0f, 1.55f, 0f));
        float height = capsule != null ? Mathf.Max(capsule.height, capsule.radius * 2f) : 3.1f;
        radius = capsule != null ? capsule.radius * 0.92f : 0.6f;
        Vector3 half = Vector3.up * Mathf.Max(0f, height * 0.5f - radius);
        bottom = center - half;
        top = center + half;
    }

    void ConfigureControllerFromCapsule()
    {
        if (controller == null)
            return;

        controller.center = capsule != null ? capsule.center : new Vector3(0f, 1.55f, 0f);
        controller.height = capsule != null ? capsule.height : 3.1f;
        controller.radius = capsule != null ? capsule.radius : 0.65f;
        controller.stepOffset = 0.45f;
        // Matches maxWalkableSlopeAngle (the player's own slopeLimit) used by CapsuleBlocked()'s
        // terrain-steepness check below - a taller/looser value here let the boss's own
        // CharacterController.Move() climb slopes the obstacle-avoidance probe alone wouldn't
        // have caught (that check only gates direction-picking, not the actual move).
        controller.slopeLimit = maxWalkableSlopeAngle;
        controller.skinWidth = 0.08f;

        if (capsule != null)
            capsule.isTrigger = true;
    }

    void ResolveOverlaps()
    {
        GetCapsulePoints(out Vector3 bottom, out Vector3 top, out float radius);
        Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, obstacleMask, QueryTriggerInteraction.Ignore);
        foreach (Collider other in overlaps)
        {
            if (other == null || other.transform.IsChildOf(transform) || other is TerrainCollider)
                continue;

            Collider own = controller != null ? controller : capsule;
            if (own == null)
                continue;

            if (Physics.ComputePenetration(
                own, transform.position, transform.rotation,
                other, other.transform.position, other.transform.rotation,
                out Vector3 direction, out float distance))
            {
                Vector3 push = direction * (distance + 0.01f);
                push.y = 0f;
                if (controller != null && controller.enabled)
                    controller.Move(push);
                else
                    transform.position += push;
            }
        }
    }

    void SetSpeed(float blendValue, float actualSpeed)
    {
        if (animator == null)
            return;

        animator.SetFloat(HashSpeed, blendValue, 0.12f, Time.deltaTime);
        SetAnimatorPlayback(blendValue, actualSpeed);
    }

    // The clips (Walking/Running/RunFast) have zero baked root motion - pure in-place loops - so
    // nothing inherently ties their leg-cycle pace to how fast the boss is actually translating
    // via MoveWithCollision. Rescaling animator.speed by actualSpeed/reference each frame (same
    // approach as the regular goblins' EnemyAI.SetAnimatorSpeed) keeps the visual stride matched
    // to real movement instead of playing every tier at a fixed guessed rate, which read as
    // sliding/surfing whenever the guess didn't match runSpeed/superRunSpeed exactly.
    void SetAnimatorPlayback(float speedParam, float actualSpeed)
    {
        if (animator == null)
            return;

        if (attacking || jumping || dead || speedParam <= 0.05f)
        {
            animator.speed = attacking && waterEffects != null && waterEffects.IsInWater
                ? waterEffects.ActionScale : 1f;
            return;
        }

        float reference = speedParam > 1.5f ? superRunAnimSpeed : speedParam > 0.75f ? runAnimSpeed : walkAnimSpeed;
        animator.speed = reference <= 0.01f ? 1f : Mathf.Clamp(actualSpeed / reference, 0.5f, 2.5f);
    }

}

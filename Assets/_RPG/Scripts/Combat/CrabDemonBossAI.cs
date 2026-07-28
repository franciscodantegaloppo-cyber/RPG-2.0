using System.Collections;
using UnityEngine;

// Movement/obstacle-avoidance core (CapsuleCast probing + side-step, CharacterController-driven,
// terrain-slope-aware grounding) is the same proven approach as KingGoblinBossAI - this project
// has no NavMesh-baked pathing for enemies, so "smart path that avoids houses" means the same
// capsule-probe side-step technique already shipped and tuned for that boss, not a new system.
[RequireComponent(typeof(EnemyStats))]
public class CrabDemonBossAI : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] float detectionRadius = 10f;
    [SerializeField] float loseDetectionRadius = 50f;
    [SerializeField] float attackRadius = 0.8f;
    [SerializeField] float fireballMinRange = 6f;

    [Header("Movement")]
    [SerializeField] float walkSpeed = 2.2f;
    [SerializeField] float runSpeed = 6f;
    // Native leg-cycle pace of the Walking/Running clips at anim.speed=1 - without these,
    // SetSpeed() only drove the Speed blend parameter and left anim.speed locked at 1x regardless
    // of actual travel speed, causing the same skating/sliding ("surfing") bug King Goblin had
    // before it was fixed. They are the native clip paces: the faster configured movement speeds
    // intentionally scale animator.speed to keep each footfall aligned with travel.
    // Reference stays at the clip's native 1.4 m/s pace. With the faster 2.2 m/s walk above,
    // SetSpeed plays the leg cycle at ~1.57x so feet remain in sync with travel.
    [SerializeField] float walkAnimSpeed = 1.4f;
    [SerializeField] float runAnimSpeed = 4.2f;
    [SerializeField] float turnSpeed = 560f;
    [SerializeField] float gravity = 18f;
    [SerializeField] float obstacleProbeDistance = 1.05f;
    [SerializeField] float obstacleSideStep = 1.6f;
    [SerializeField] LayerMask obstacleMask = ~0;
    [SerializeField] bool ignoreTerrainInObstacleProbe;
    [SerializeField] bool forceTransformMovement;
    [SerializeField, Range(0f, 1f)] float directRushChance = 0.35f;
    [SerializeField] float directRushDecisionInterval = 2.5f;
    [SerializeField] bool allowMeleeAttacks = true;

    [Header("Combat")]
    [SerializeField] float attackCooldown = 1.6f;
    [SerializeField] float attackDamageDelay = 0.32f;
    [SerializeField] float attackAnimationSpeed = 1.45f;
    [SerializeField, Range(0f, 1f)] float blockChance = 0.5f;
    [SerializeField, Range(0f, 1f)] float retreatChance = 0.4f;
    [SerializeField] float retreatDistance = 5f;
    [SerializeField] float retreatSpeed = 5f;

    [Header("Fireball")]
    [SerializeField] float fireballCooldown = 4f;
    [SerializeField] Transform fireballSpawnPoint;
    [SerializeField] float fireballVisualScale = 1.1f;
    [SerializeField] float fireballSpeed = 14f;
    [SerializeField] float fireballHitRadius = 1.2f;
    [SerializeField] bool fireballLeavesGroundFire;
    [SerializeField] float groundFireDuration = 5f;
    [SerializeField] float groundFireDamagePerTick = 25f;

    [Header("Jump (reach elevated player)")]
    [SerializeField] float jumpCooldown = 4f;
    [SerializeField] bool allowJumping = true;
    [SerializeField] float jumpHeightThreshold = 1.4f;
    [SerializeField] float jumpForce = 7f;
    [SerializeField] float jumpForwardSpeed = 2.4f;
    [SerializeField] float blockedJumpForceMultiplier = 2f;
    [SerializeField] float maxBlockedJumpForce = 64f;
    [SerializeField] float repeatedJumpPositionTolerance = 5f;
    [SerializeField] float blockedReleaseDistance = 5f;

    EnemyStats stats;
    Animator animator;
    Transform player;
    Terrain terrain;
    CapsuleCollider capsule;
    CharacterController controller;
    WaterEnemyEffects waterEffects;
    CrabDemonCombatRealism combatRealism;
    Vector3 verticalVelocity;

    float lastAttackTime;
    float lastFireballTime;
    float lastJumpTime;
    float nextDirectRushDecisionTime;
    float staggeredUntil;
    bool dead;
    bool attacking;
    bool jumping;

    public bool IsPerformingAttack => attacking;
    bool retreating;
    bool playerDetected;
    bool forcedPursuitRun;
    bool locomotionRunning;
    bool directRush;
    bool blockedByObstacle;
    bool hasBlockedPositionAnchor;
    Vector3 blockedPositionAnchor;
    int blockedJumpLevel;
    int repeatedStuckJumpCount;
    bool hasLastStuckJumpPosition;
    Vector3 lastStuckJumpPosition;
    float stuckJumpAnchorPlayerDistance;
    int nextAttack;
    int activeAttackIndex;
    bool activeAttackIsHeavy;
    float maxWalkableSlopeAngle = 45f;

    static readonly int HashSpeed = Animator.StringToHash("Speed");
    static readonly int HashPush = Animator.StringToHash("Push");
    // Six distinct strikes cycled through in order (never repeating the same one twice in a row) -
    // "las animaciones de golpe las puede alternar" - uses every remaining combat-appropriate clip
    // from the Meshy asset instead of only 2 (Parkour_Vault_3/Unarmed_Vault are vault-traversal
    // clips, not strikes, so they're intentionally left out of this rotation).
    static readonly int[] HashAttacks =
    {
        Animator.StringToHash("Attack1"), // Right_Hand_Sword_Slash
        Animator.StringToHash("Attack2"), // Charged_Upward_Slash
        Animator.StringToHash("Attack3"), // Punch_Combo_1
        Animator.StringToHash("Attack4"), // Spartan_Kick
        Animator.StringToHash("Attack5"), // High_Kick
        Animator.StringToHash("Attack6"), // Sweeping_Kick
    };
    static readonly int HashBlock = Animator.StringToHash("Block");
    static readonly int HashHit = Animator.StringToHash("Hit");
    static readonly int HashJump = Animator.StringToHash("Jump");
    static readonly int HashFireball = Animator.StringToHash("FireballCast");
    static readonly int HashVictoryDance = Animator.StringToHash("VictoryDance");
    static readonly int HashDeath = Animator.StringToHash("Death");
    // Retreating starts while the melee attack coroutine is still active.  Going straight to
    // this state prevents the boss from being translated by the AI while the strike pose is
    // still playing (the visible "surfing" / moonwalk).
    static readonly int HashRunState = Animator.StringToHash("Base Layer.Run");
    static readonly int HashWalkState = Animator.StringToHash("Base Layer.Walk");
    static readonly int HashIdleState = Animator.StringToHash("Base Layer.Idle");
    static readonly int HashPushState = Animator.StringToHash("Base Layer.Push");
    static readonly int[] HashAttackStates =
    {
        Animator.StringToHash("Base Layer.Attack1"),
        Animator.StringToHash("Base Layer.Attack2"),
        Animator.StringToHash("Base Layer.Attack3"),
        Animator.StringToHash("Base Layer.Attack4"),
        Animator.StringToHash("Base Layer.Attack5"),
        Animator.StringToHash("Base Layer.Attack6"),
    };

    void Awake()
    {
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
        if (!gameObject.name.Contains("DemonioAnomalo"))
            combatRealism = GetComponent<CrabDemonCombatRealism>() ??
                            gameObject.AddComponent<CrabDemonCombatRealism>();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.detectCollisions = false; }

        terrain = Terrain.activeTerrain;
        if (obstacleMask.value == ~0)
            obstacleMask = ~LayerMask.GetMask("Enemy", "Player", "Interactable");

        if (GetComponent<EnemyHealthBar>() is EnemyHealthBar normalBar)
            Destroy(normalBar);
        if (GetComponent<CrabDemonHealthBar>() == null)
            gameObject.AddComponent<CrabDemonHealthBar>();
        // DemonioAnomalo shares this controller but already owns its violet tornado identity.
        // The blue speed afterimages belong specifically to the Crab Demon boss.
        if (!gameObject.name.Contains("DemonioAnomalo") &&
            GetComponent<CrabDemonAfterimageTrail>() == null)
            gameObject.AddComponent<CrabDemonAfterimageTrail>();
        if (!gameObject.name.Contains("DemonioAnomalo") &&
            GetComponent<CrabDemonNightEchoController>() == null)
            gameObject.AddComponent<CrabDemonNightEchoController>();
        if (gameObject.name.Contains("DemonioAnomalo") && GetComponent<DemonioAnomaloDeathAnimation>() == null)
            gameObject.AddComponent<DemonioAnomaloDeathAnimation>();
        if (gameObject.name.Contains("DemonioAnomalo") && GetComponent<DemonioAnomaloTreeBurner>() == null)
            gameObject.AddComponent<DemonioAnomaloTreeBurner>();
    }

    void OnEnable()
    {
        stats.OnDeath += HandleDeath;
        PlayerStats.GlobalOnDeath += HandlePlayerDeath;
    }

    void OnDisable()
    {
        stats.OnDeath -= HandleDeath;
        PlayerStats.GlobalOnDeath -= HandlePlayerDeath;
    }

    void Update()
    {
        if (dead) return;
        combatRealism?.RefreshPhase();
        if (Time.time < staggeredUntil)
        {
            SetSpeed(0f, 0f);
            return;
        }

        Vector3 frameStartPosition = transform.position;
        AcquirePlayer();

        if (player == null)
        {
            SetSpeed(0f, 0f);
            return;
        }

        // Combat is decided on the ground plane. The boss mesh has a visual ground offset while
        // the player root has its own controller offset; including Y made a boss that was already
        // beside the player keep running because it never met the 3D attack-distance check.
        float distance = PlanarDistance(transform.position, player.position);
        if (!playerDetected)
        {
            if (distance <= detectionRadius)
                playerDetected = true;
            else
            {
                SetSpeed(0f, 0f);
                return;
            }
        }

        // Detection begins at 10 m, but once engaged the boss keeps the player as its target
        // until the player has actually escaped the encounter (50 m).
        if (distance > loseDetectionRadius)
        {
            playerDetected = false;
            forcedPursuitRun = false;
            locomotionRunning = false;
            directRush = false;
            blockedByObstacle = false;
            hasBlockedPositionAnchor = false;
            StopAllCoroutines();
            attacking = false;
            jumping = false;
            retreating = false;
            SetSpeed(0f, 0f);
            return;
        }

        if (retreating || attacking || jumping)
            return;

        // Hysteresis around 10 m: crossing one exact threshold every frame previously flipped
        // Walk/Run continuously. Once pursuit starts, keep running until the gap is genuinely
        // closed (8 m); only then may normal close-range movement choose walking.
        if (distance > detectionRadius)
            forcedPursuitRun = true;
        else if (distance <= detectionRadius * 0.8f)
            forcedPursuitRun = false;

        // Jump to reach a player who climbed somewhere this capsule can't walk up to.
        float heightDiff = player.position.y - transform.position.y;
        if (allowJumping && heightDiff >= jumpHeightThreshold && distance > attackRadius &&
            Time.time - lastJumpTime >= jumpCooldown)
        {
            StartCoroutine(JumpTowardPlayer());
            return;
        }

        if (allowMeleeAttacks && distance <= GetMeleeEngageRadius())
        {
            // A direct rush has reached its objective. Stop only here, at actual strike range,
            // then immediately attempt the hit instead of resuming an indecisive walk/run loop.
            directRush = false;
            FacePlayer();
            TryMeleeAttack();
            return;
        }

        // Ranged-only bosses keep their distance instead of pressing their capsule into the
        // player and repeatedly attempting a melee move they do not possess.
        if (!allowMeleeAttacks && distance <= fireballMinRange)
        {
            directRush = false;
            FacePlayer();
            SetSpeed(0f, 0f);
            if (Time.time - lastFireballTime >= EffectiveFireballCooldown())
                StartCoroutine(CastFireball());
            return;
        }

        // Beyond the encounter's close-combat radius the boss must close the gap at a run.
        // Do not let the ranged attack interrupt that pursuit from 10-50 m away.
        // A blocked boss must keep trying to reach the player, never turn the obstacle into a
        // permanent firing position.  Check the direct capsule route before casting and also
        // forbid fireballs while a pursuit run is still active.
        Vector3 directToPlayer = player.position - transform.position;
        directToPlayer.y = 0f;
        bool directRouteBlocked = directToPlayer.sqrMagnitude > 0.001f &&
            CapsuleBlocked(directToPlayer.normalized, obstacleProbeDistance, out _);
        if (directRouteBlocked)
            MarkBlockedAtCurrentPosition();
        if (!directRush && !blockedByObstacle && !forcedPursuitRun && !directRouteBlocked &&
            distance <= detectionRadius && distance >= fireballMinRange &&
            Time.time - lastFireballTime >= EffectiveFireballCooldown())
        {
            StartCoroutine(CastFireball());
            return;
        }

        MoveTowardPlayer(distance, frameStartPosition);
    }

    void AcquirePlayer()
    {
        if (player != null) return;
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null) player = found.transform;
    }

    void MoveTowardPlayer(float distance, Vector3 frameStartPosition)
    {
        if (!directRush && Time.time >= nextDirectRushDecisionTime)
        {
            nextDirectRushDecisionTime = Time.time + directRushDecisionInterval;
            directRush = Random.value < directRushChance;
        }

        // Use a continuous speed curve inside the 10 m engagement range instead of flipping
        // between two fixed speeds at 8/4 m. This avoids the visible run-stop-run jitter when
        // the player is near either threshold.
        float closeRange = GetMeleeEngageRadius() + 0.5f;
        float distanceFactor = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(closeRange, detectionRadius + 1f, distance));
        if (forcedPursuitRun)
            distanceFactor = Mathf.Max(distanceFactor, 0.72f);
        if (directRush)
            distanceFactor = 1f;

        float speed = Mathf.Lerp(walkSpeed, runSpeed, distanceFactor) *
                      (combatRealism != null ? combatRealism.MovementMultiplier : 1f);
        // The Anomalous Demon keeps its deliberately heavy gait, but covers twice the ground
        // per step so it can actually pressure the player at its huge scale.
        if (gameObject.name.Contains("DemonioAnomalo"))
            speed *= 2f;
        float speedParam = Mathf.Lerp(0.5f, 1f, distanceFactor);

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        // During a rush, hold the direct vector. MoveWithCollision still handles a real wall or
        // steep slope safely, but the AI will not voluntarily pause to choose a side route or a
        // fireball before it has reached its attack attempt.
        Vector3 moveDirection = directRush ? direction.normalized : AvoidObstacles(direction.normalized);
        if (moveDirection.sqrMagnitude < 0.001f)
        {
            MarkBlockedAtCurrentPosition();
            if (distance <= GetMeleeEngageRadius() + 0.7f)
                TryMeleeAttack();
            else if (allowJumping && Time.time - lastJumpTime >= jumpCooldown)
                StartCoroutine(JumpTowardPlayer(true));
            else
                SetSpeed(0f, 0f);
            return;
        }

        FaceDirection(moveDirection.normalized);
        Vector3 positionBeforeMove = transform.position;
        // At the final metre, move through the player's controller rather than letting two
        // character capsules keep the boss just outside punch/kick range. Real world obstacles
        // are still checked by the capsule probe before this direct step is allowed.
        bool finalMeleeApproach = distance <= 1.8f;
        MoveWithCollision(moveDirection.normalized, speed, true, false, finalMeleeApproach);

        float movedDistance = PlanarDistance(positionBeforeMove, transform.position);
        bool movementBlocked = movedDistance < speed * Time.deltaTime * 0.15f;

        // "Blocked" is a spatial state, not a one-frame collision. Once trapped, it stays
        // blocked until it has moved 5 horizontal metres away from the spot where it got stuck.
        // This prevents tiny slides, animation movement or a one-frame clear probe from making
        // it start firing again on the same slope.
        if (movementBlocked)
            MarkBlockedAtCurrentPosition();
        else if (blockedByObstacle && hasBlockedPositionAnchor &&
                 PlanarDistance(blockedPositionAnchor, transform.position) >= blockedReleaseDistance)
        {
            blockedByObstacle = false;
            hasBlockedPositionAnchor = false;
        }

        // A CharacterController can stop against the player before bounds overlap. Treat a
        // blocked run at close range as arrival, so the boss never runs in place beside them.
        if (movementBlocked && distance <= GetMeleeEngageRadius() + 0.35f)
        {
            FacePlayer();
            TryMeleeAttack();
            return;
        }

        // If it is blocked away from the player, the obstruction is level geometry rather than
        // the target. Try the existing forward jump to clear a curb, rock or low wall.
        if (movementBlocked && allowJumping && Time.time - lastJumpTime >= jumpCooldown)
        {
            StartCoroutine(JumpTowardPlayer(true));
            return;
        }

        float measuredSpeed = (transform.position - frameStartPosition).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
        SetSpeed(speedParam, measuredSpeed);
        EnsureLocomotionAnimation(speedParam);
    }

    // Rarest-feeling variety first: Push occasionally, otherwise alternate the two claw/punch
    // attacks - matches "golpea de diferentes formas" without a rigid fixed rotation.
    void TryMeleeAttack()
    {
        float actionScale = waterEffects != null ? waterEffects.ActionScale : 1f;
        float phaseSpeed = combatRealism != null ? combatRealism.ActionMultiplier : 1f;
        if (attacking || Time.time - lastAttackTime <
            attackCooldown / (actionScale * phaseSpeed)) return;

        lastAttackTime = Time.time;
        attacking = true;
        StartCoroutine(ExecuteTelegraphedMelee(actionScale, phaseSpeed));
    }

    IEnumerator ExecuteTelegraphedMelee(float actionScale, float phaseSpeed)
    {
        activeAttackIndex = SelectContextualAttack();
        activeAttackIsHeavy = activeAttackIndex == 1 || activeAttackIndex == 5;
        float telegraph = activeAttackIsHeavy ? .46f : .24f;
        if (combatRealism != null)
        {
            telegraph /= Mathf.Lerp(1f, phaseSpeed, .35f);
            combatRealism.PlayTelegraph(activeAttackIndex, telegraph, activeAttackIsHeavy);
        }
        yield return new WaitForSeconds(telegraph);
        if (dead || player == null)
        {
            attacking = false;
            yield break;
        }

        FacePlayer();
        if (activeAttackIndex < 0)
            animator?.Play(HashPushState, 0, 0f);
        else
            animator?.Play(HashAttackStates[activeAttackIndex], 0, 0f);

        if (animator != null)
            animator.speed = attackAnimationSpeed * actionScale * phaseSpeed;

        StartCoroutine(DealDamageAfterDelay(
            attackDamageDelay / (actionScale * phaseSpeed)));
        float recovery = (activeAttackIsHeavy ? .92f : .58f) /
                         Mathf.Lerp(1f, phaseSpeed, .45f);
        StartCoroutine(UnlockAttack(recovery));
    }

    int SelectContextualAttack()
    {
        if (player == null)
            return nextAttack++ % HashAttacks.Length;

        Vector3 local = transform.InverseTransformPoint(player.position);
        float planarDistance = new Vector2(local.x, local.z).magnitude;
        // A target at the flank/back calls for a sweep; very close targets get kicked away.
        if (local.z < -.15f || Mathf.Abs(local.x) > Mathf.Max(.45f, Mathf.Abs(local.z)))
            return 5;
        if (planarDistance < 1.05f)
            return Random.value < .5f ? 3 : 4;
        if (combatRealism != null && combatRealism.Phase >= 3 && Random.value < .38f)
            return 1;
        int selected = nextAttack++ % HashAttacks.Length;
        return selected;
    }

    IEnumerator DealDamageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (dead || player == null) yield break;
        // Claw swings need a small reach allowance after the body has approached its close
        // contact radius. This affects hit registration only, not where the boss stops moving.
        float damage = stats != null ? stats.Attack : 150f;
        bool hit = combatRealism != null
            ? combatRealism.TryDealAnimatedHit(player, activeAttackIndex, damage,
                activeAttackIsHeavy)
            : IsAtPlayerContact() ||
              PlanarDistance(transform.position, player.position) <=
              Mathf.Max(1.5f, attackRadius * 1.75f);
        if (hit)
        {
            if (combatRealism == null)
                player.GetComponent<PlayerStats>()?.TakeDamage(damage, transform.position);

            // "de manera aleatoria puede salir corriendo 5 metros y volver para pegarte
            // nuevamente o seguir pegándote" - only rolled after actually landing a hit.
            if (!retreating && Random.value < retreatChance)
                StartCoroutine(RetreatAndReturn());
        }
    }

    IEnumerator UnlockAttack(float delay)
    {
        yield return new WaitForSeconds(delay);
        attacking = false;
    }

    public void Stagger(float duration)
    {
        if (dead)
            return;
        StopAllCoroutines();
        attacking = false;
        jumping = false;
        retreating = false;
        staggeredUntil = Mathf.Max(staggeredUntil, Time.time + duration);
        animator?.SetTrigger(HashHit);
        if (animator != null)
            animator.speed = .72f;
        SetSpeed(0f, 0f);
    }

    IEnumerator RetreatAndReturn()
    {
        retreating = true;
        // Damage is dealt part-way through a melee clip, before its normal exit time.  Do not
        // wait for the remaining attack frames: the boss is already physically running away.
        PlayLocomotionState(HashRunState);
        float traveled = 0f;
        while (traveled < retreatDistance && !dead)
        {
            Vector3 away = player != null ? (transform.position - player.position) : -transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f) away = -transform.forward;
            away = AvoidObstacles(away.normalized);
            if (away.sqrMagnitude < 0.001f) break;

            // Turn to face the direction it's fleeing and run forward into it - facing -away
            // (toward the player) while moving away was making it moonwalk backward instead of
            // turning around and running like "que se de la vuelta y corra como deberia".
            FaceDirection(away);
            float step = retreatSpeed * Time.deltaTime;
            MoveWithCollision(away.normalized, retreatSpeed);
            traveled += step;
            SetSpeed(1f, retreatSpeed);
            EnsureLocomotionAnimation(1f);
            yield return null;
        }
        retreating = false;
    }

    IEnumerator CastFireball()
    {
        attacking = true;
        lastFireballTime = Time.time;
        FacePlayer();
        SetSpeed(0f, 0f);
        animator?.SetTrigger(HashFireball);
        combatRealism?.PlayTelegraph(0, .46f, true);

        yield return new WaitForSeconds(0.5f);

        if (!dead && player != null)
        {
            Vector3 origin = fireballSpawnPoint != null ? fireballSpawnPoint.position : transform.position + Vector3.up * 1.6f + transform.forward;
            Vector3 dir = (player.position + Vector3.up * 0.9f) - origin;
            GameObject fb = fireballVisualScale > 1.11f
                ? Fireball.BuildRedFireball(fireballVisualScale)
                : Fireball.BuildPrefab();
            fb.transform.position = origin;
            Fireball projectile = fb.GetComponent<Fireball>();
            float phaseProjectileSpeed = combatRealism != null &&
                                         combatRealism.Phase >= 3 ? 1.22f : 1f;
            projectile.Configure(fireballSpeed * phaseProjectileSpeed,
                fireballHitRadius);
            bool leavesDangerZone = fireballLeavesGroundFire ||
                                    (combatRealism != null &&
                                     combatRealism.Phase >= 2);
            projectile.ConfigureGroundFire(leavesDangerZone, groundFireDuration,
                groundFireDamagePerTick);
            projectile.Launch(dir, stats != null ? stats.Attack : 150f);
        }

        // The projectile is the release point of the cast.  The source clip has a long recovery
        // tail, so explicitly leave it instead of keeping the boss frozen in a casting pose.
        yield return new WaitForSeconds(0.08f);
        if (!dead)
            PlayLocomotionState(HashIdleState);
        attacking = false;
    }

    float EffectiveFireballCooldown()
    {
        return fireballCooldown /
               (combatRealism != null ? combatRealism.ActionMultiplier : 1f);
    }

    IEnumerator JumpTowardPlayer(bool escalatingObstacleJump = false)
    {
        jumping = true;
        lastJumpTime = Time.time;
        animator?.SetTrigger(HashJump);

        if (escalatingObstacleJump)
        {
            // This coroutine is entered in escalating mode only after movement was blocked.
            // Keep one escalation chain until the crab has made real forward progress toward the
            // player. A jump can drift sideways or move a little on a steep slope; neither is a
            // successful escape and therefore must not reset the next jump back to its base force.
            float currentPlayerDistance = player != null
                ? PlanarDistance(transform.position, player.position)
                : float.PositiveInfinity;
            if (!hasLastStuckJumpPosition ||
                currentPlayerDistance <= stuckJumpAnchorPlayerDistance - repeatedJumpPositionTolerance)
            {
                lastStuckJumpPosition = transform.position;
                hasLastStuckJumpPosition = true;
                stuckJumpAnchorPlayerDistance = currentPlayerDistance;
                repeatedStuckJumpCount = 1;
                // The first blocked attempt is already an obstacle jump: double its vertical
                // impulse, then double again for every repeat (2x, 4x, 8x...).
                blockedJumpLevel = 1;
            }
            else
            {
                repeatedStuckJumpCount++;
                blockedJumpLevel = repeatedStuckJumpCount;
            }
        }

        float currentJumpForce = escalatingObstacleJump
            ? Mathf.Min(jumpForce * Mathf.Pow(blockedJumpForceMultiplier, blockedJumpLevel), maxBlockedJumpForce)
            : jumpForce;
        if (waterEffects != null) currentJumpForce *= waterEffects.JumpScale;
        // Higher attempts need more airtime as well; otherwise the old fixed 0.9 s duration
        // forced a tall jump straight back down before it could clear the obstacle.
        float gravityScale = waterEffects != null ? waterEffects.GravityScale : 1f;
        float duration = Mathf.Clamp((currentJumpForce * 2f / (gravity * gravityScale)) + 0.1f, 0.9f, 7.5f);
        float elapsed = 0f;
        verticalVelocity.y = currentJumpForce;
        while (elapsed < duration && !dead)
        {
            elapsed += Time.deltaTime;
            Vector3 direction = player != null ? player.position - transform.position : transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                // A jump is the escape from the selected route being blocked. Aim directly at
                // the player rather than reusing a near-equal side-route score.
                Vector3 jumpDirection = escalatingObstacleJump
                    ? direction.normalized
                    : AvoidObstacles(direction.normalized);
                // If both side-step probes are blocked, this is the exact case the jump is for:
                // keep the forward vector and let the airborne CharacterController try to clear
                // the obstacle instead of cancelling all horizontal motion.
                if (jumpDirection.sqrMagnitude < 0.001f)
                    jumpDirection = direction.normalized;
                FaceDirection(jumpDirection);
                // Let the CharacterController resolve the actual contact while airborne instead
                // of rejecting the forward step during the preliminary obstacle capsule probe.
                MoveWithCollision(jumpDirection, jumpForwardSpeed, false, true);
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
    }

    // Called by EnemyStats.TakeDamage BEFORE damage is applied - returning true fully negates
    // the hit (per "50% de probabilidad de bloquear un golpe de espada").
    public bool TryBlock(Vector3 attackerPosition = default, float incomingDamage = 0f)
    {
        if (dead) return false;
        bool blocked = combatRealism != null
            ? combatRealism.CanBlock(attackerPosition, incomingDamage)
            : Random.value < blockChance;
        if (blocked)
        {
            animator?.SetTrigger(HashBlock);
            // A hit can land at any moment regardless of what the AI's Update() was last doing -
            // without resetting anim.speed here, Block could inherit a leftover scaled rate from
            // whatever movement speed preceded it (SetSpeed() now rescales anim.speed for the
            // walk/run surfing fix below).
            if (animator != null) animator.speed = 1f;
        }
        return blocked;
    }

    public void OnHurt(float incomingDamage = 0f)
    {
        if (combatRealism != null &&
            combatRealism.RegisterPostureDamage(incomingDamage))
            return;
        if (dead || attacking || jumping || retreating) return;
        animator?.SetTrigger(HashHit);
        SetSpeed(0f, 0f);
    }

    void HandlePlayerDeath()
    {
        if (dead || player == null) return;
        if (Vector3.Distance(transform.position, player.position) > detectionRadius) return;
        StopAllCoroutines();
        attacking = true; // freeze combat state while dancing
        animator?.SetTrigger(HashVictoryDance);
        SetSpeed(0f, 0f);
    }

    void HandleDeath()
    {
        dead = true;
        StopAllCoroutines();
        SetSpeed(0f, 0f);
        animator?.SetTrigger(HashDeath);
        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;
        if (controller != null) controller.enabled = false;
        Destroy(gameObject, 6f);
    }

    void FacePlayer()
    {
        if (player == null) return;
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
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
        float groundY = GroundUtility.GetGroundY(pos, transform, 5f, 8f);
        if (float.IsNegativeInfinity(groundY) && terrain != null)
            groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;
        else if (float.IsNegativeInfinity(groundY) && Physics.Raycast(pos + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            groundY = hit.point.y;

        if (float.IsNegativeInfinity(groundY))
            return;

        if (force || pos.y < groundY)
        {
            pos.y = groundY;
            transform.position = pos;
            verticalVelocity.y = 0f;
        }
    }

    float GetMeleeEngageRadius()
    {
        // Physical collider contact is tested separately in IsAtPlayerContact(). Do not inflate
        // this authored approach range by collider radii, or the boss visibly stops too far away.
        return attackRadius;
    }

    void MarkBlockedAtCurrentPosition()
    {
        if (!blockedByObstacle || !hasBlockedPositionAnchor)
        {
            blockedPositionAnchor = transform.position;
            hasBlockedPositionAnchor = true;
        }
        blockedByObstacle = true;
    }

    static float PlanarDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    bool IsAtPlayerContact()
    {
        if (player == null || controller == null || !controller.enabled)
            return false;

        Bounds bossBounds = controller.bounds;
        const float contactSlack = 0.12f;

        CharacterController playerController = player.GetComponent<CharacterController>();
        if (playerController != null && HorizontalBoundsDistance(bossBounds, playerController.bounds) <= contactSlack)
            return true;

        foreach (Collider playerCollider in player.GetComponentsInChildren<Collider>(true))
        {
            if (playerCollider != null && HorizontalBoundsDistance(bossBounds, playerCollider.bounds) <= contactSlack)
                return true;
        }
        return false;
    }

    static float HorizontalBoundsDistance(Bounds first, Bounds second)
    {
        float x = Mathf.Max(0f, first.min.x - second.max.x, second.min.x - first.max.x);
        float z = Mathf.Max(0f, first.min.z - second.max.z, second.min.z - first.max.z);
        return Mathf.Sqrt(x * x + z * z);
    }

    void MoveWithCollision(Vector3 direction, float speed, bool groundAfterMove = true,
        bool ignoreObstacleProbe = false, bool bypassCharacterController = false)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        if (waterEffects != null) speed *= waterEffects.MovementScale;
        float distance = speed * Time.deltaTime;
        if (!ignoreObstacleProbe && CapsuleBlocked(direction, distance + 0.08f, out RaycastHit hit))
        {
            Vector3 slide = Vector3.ProjectOnPlane(direction, hit.normal);
            slide.y = 0f;
            if (slide.sqrMagnitude < 0.001f || CapsuleBlocked(slide.normalized, distance + 0.03f, out _))
                return;
            direction = slide.normalized;
        }

        // The Anomalous Demon is an unusually large imported model. Its controller can remain
        // intersecting the terrain and reject every Move() even while the walk clip plays.
        // We still run the obstacle probe above, then translate directly on clear ground.
        if (controller != null && controller.enabled && !bypassCharacterController &&
            !forceTransformMovement && !gameObject.name.Contains("DemonioAnomalo"))
            controller.Move(direction * distance);
        else
            transform.position += direction * distance;

        if (groundAfterMove) ApplyGrounding();
        ResolveOverlaps();
    }

    Vector3 AvoidObstacles(Vector3 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude < 0.001f) return Vector3.zero;

        if (!CapsuleBlocked(desiredDirection, obstacleProbeDistance, out _))
            return desiredDirection;

        Vector3 right = Vector3.Cross(Vector3.up, desiredDirection).normalized;
        Vector3 leftCandidate = (desiredDirection + right * obstacleSideStep).normalized;
        Vector3 rightCandidate = (desiredDirection - right * obstacleSideStep).normalized;

        bool leftBlocked = CapsuleBlocked(leftCandidate, obstacleProbeDistance, out _);
        bool rightBlocked = CapsuleBlocked(rightCandidate, obstacleProbeDistance, out _);
        if (!leftBlocked && !rightBlocked && player != null)
            return PlanarDistance(transform.position + leftCandidate, player.position) <
                PlanarDistance(transform.position + rightCandidate, player.position) ? leftCandidate : rightCandidate;
        if (!leftBlocked) return leftCandidate;
        if (!rightBlocked) return rightCandidate;
        return Vector3.zero;
    }

    bool CapsuleBlocked(Vector3 direction, float distance, out RaycastHit hit)
    {
        hit = default;
        if (direction.sqrMagnitude < 0.001f) return true;

        GetCapsulePoints(out Vector3 bottom, out Vector3 top, out float radius);
        if (!Physics.CapsuleCast(bottom, top, radius, direction.normalized, out hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
            return false;
        if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) return false;
        if (gameObject.name.Contains("DemonioAnomalo") && IsGrassCollider(hit.collider)) return false;
        if (hit.collider is TerrainCollider)
        {
            // A large capsule can begin by grazing a harmless terrain ripple. The controller
            // still enforces slopeLimit; this switch only prevents the look-ahead probe from
            // treating that first step as a wall.
            if (ignoreTerrainInObstacleProbe || gameObject.name.Contains("DemonioAnomalo")) return false;
            return IsTerrainTooSteepAt(hit.point);
        }
        return true;
    }

    static bool IsGrassCollider(Collider collider)
    {
        for (Transform current = collider.transform; current != null; current = current.parent)
        {
            string name = current.name.ToLowerInvariant();
            if (name.Contains("grass") || name.Contains("hierba") || name.Contains("pasto") || name.Contains("foliage"))
                return true;
        }
        return false;
    }

    bool IsTerrainTooSteepAt(Vector3 worldPoint)
    {
        if (terrain == null) return false;
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
        if (controller == null) return;
        controller.center = capsule != null ? capsule.center : new Vector3(0f, 1.55f, 0f);
        controller.height = capsule != null ? capsule.height : 3.1f;
        controller.radius = capsule != null ? capsule.radius : 0.65f;
        controller.stepOffset = 0.45f;
        controller.slopeLimit = maxWalkableSlopeAngle;
        controller.skinWidth = 0.08f;
        if (capsule != null) capsule.isTrigger = true;
    }

    void ResolveOverlaps()
    {
        GetCapsulePoints(out Vector3 bottom, out Vector3 top, out float radius);
        Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, obstacleMask, QueryTriggerInteraction.Ignore);
        foreach (Collider other in overlaps)
        {
            if (other == null || other.transform.IsChildOf(transform) || other is TerrainCollider) continue;
            if (gameObject.name.Contains("DemonioAnomalo") && IsGrassCollider(other)) continue;
            Collider own = controller != null ? (Collider)controller : capsule;
            if (own == null) continue;
            if (Physics.ComputePenetration(
                own, transform.position, transform.rotation,
                other, other.transform.position, other.transform.rotation,
                out Vector3 direction, out float distance))
            {
                Vector3 push = direction * (distance + 0.01f);
                push.y = 0f;
                if (controller != null && controller.enabled) controller.Move(push);
                else transform.position += push;
            }
        }
    }

    void SetSpeed(float blendValue, float actualSpeed)
    {
        if (animator == null) return;
        animator.SetFloat(HashSpeed, blendValue, 0.12f, Time.deltaTime);

        float locomotionT = Mathf.InverseLerp(0.5f, 1f, blendValue);
        float reference = Mathf.Lerp(walkAnimSpeed, runAnimSpeed, locomotionT);
        float playbackSpeed = actualSpeed <= 0.05f || reference <= 0.01f ? 1f : Mathf.Clamp(actualSpeed / reference, 0.5f, 2.5f);
        // Its long strides read better at a slower playback rate; movement speed remains
        // unchanged, so this only changes the perceived cadence of the walk animation.
        if (gameObject.name.Contains("DemonioAnomalo") && actualSpeed > 0.05f)
            playbackSpeed *= 0.55f;
        animator.speed = playbackSpeed;
    }

    void PlayLocomotionState(int stateHash)
    {
        if (animator == null) return;
        animator.speed = 1f;
        // A blended transition kept enough of the long melee pose alive to make the retreat
        // visibly slide.  This is used only for an abrupt state change (retreat / cast release),
        // so start locomotion at frame zero with no residual attack weighting.
        animator.Play(stateHash, 0, 0f);
        animator.Update(0f);
    }

    // Some imported FBX clips do not carry Loop Time metadata.  Animator then holds their final
    // pose after a single cycle even though the AI continues translating the boss.  Keep the
    // movement state authoritative while the AI is moving, and restart the cycle just before its
    // end so a non-looping import can never turn into a sliding pose.
    void EnsureLocomotionAnimation(float blendValue)
    {
        if (animator == null) return;

        // The anomalous demon uses a small generic controller with just Idle and Walk, driven
        // by its Speed parameter.  The crab controller's explicit Walk/Run state names do not
        // exist there, so forcing them would continually reset the animator and make it appear
        // to walk in place.
        if (gameObject.name.Contains("DemonioAnomalo")) return;

        // Separate enter/exit thresholds provide animation hysteresis: slight player-distance
        // changes cannot repeatedly reset Walk and Run in alternating frames.
        if (blendValue >= 0.84f) locomotionRunning = true;
        else if (blendValue <= 0.66f) locomotionRunning = false;

        int desiredState = locomotionRunning ? HashRunState : HashWalkState;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        // Walk/Run are imported as real looping clips by CrabDemonBossSetup. Restarting at 98%
        // cut off the last frames of every stride, which looked like the run animation stuttered
        // into itself.
        if (state.fullPathHash != desiredState)
            animator.Play(desiredState, 0, 0f);
    }
}

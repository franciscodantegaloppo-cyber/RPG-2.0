using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyStats))]
public class SuperiorTreeAnomalyAI : MonoBehaviour
{
    [SerializeField] float detectionRadius = 10f;
    [SerializeField] float preferredRange = 10f;
    [SerializeField] float meleeRange = 2.2f;
    [SerializeField] float moveSpeed = 3.4f;
    [SerializeField] float retreatSpeed = 4.1f;
    [SerializeField] float attackCooldown = 0.48f;
    [SerializeField] float fireballCooldown = 3.0f;
    [SerializeField] float castReleaseDelay = 0.48f;
    [SerializeField] float castDuration = 0.86f;
    [SerializeField] float fireballDamage = 75f;

    static readonly int Speed = Animator.StringToHash("Speed");
    static readonly int Melee = Animator.StringToHash("Melee");
    static readonly int Cast = Animator.StringToHash("Cast");
    static readonly int Death = Animator.StringToHash("Death");

    EnemyStats stats;
    CharacterController controller;
    Animator animator;
    Terrain terrain;
    Transform player;
    float nextAttackTime;
    float nextFireballTime;
    bool busy;
    bool dead;
    bool engaged;
    float displayedLocomotionSpeed;
    float requestedLocomotionSpeed;
    float verticalVelocity;
    Vector3 previousPosition;

    void Awake()
    {
        // Activation is intentionally fixed to the requested horizontal 10 m.
        // This also updates previously saved prefab instances immediately.
        detectionRadius = 10f;
        stats = GetComponent<EnemyStats>();
        controller = GetComponent<CharacterController>();
        if (controller == null) controller = gameObject.AddComponent<CharacterController>();
        controller.enabled = true;
        terrain = Terrain.activeTerrain;
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        SnapToGroundOnce();
        previousPosition = transform.position;
        stats.OnDeath += Die;
    }

    void OnEnable()
    {
        dead = false;
        engaged = false;
        verticalVelocity = -2f;
        previousPosition = transform.position;
        FindPlayer();
    }

    void OnDestroy()
    {
        if (stats != null) stats.OnDeath -= Die;
    }

    void Update()
    {
        if (dead) return;
        requestedLocomotionSpeed = 0f;
        ApplyControllerGravity();
        if (player == null) FindPlayer();
        if (player == null) { SetLocomotion(0f); return; }

        float distance = PlanarDistance(transform.position, player.position);
        // Wake only within 10 m, then retain the target so it can actually move
        // to maintain its preferred casting distance when the player backs away.
        if (!engaged)
        {
            if (distance > detectionRadius) { player = null; SetLocomotion(0f); return; }
            engaged = true;
        }
        if (busy) return;

        if (distance <= meleeRange)
        {
            StartCoroutine(MeleeThenRetreat(Random.Range(3, 5)));
            return;
        }

        if (distance < preferredRange - 0.75f)
        {
            MoveAway();
            return;
        }

        if (distance > preferredRange + 0.75f)
        {
            MoveTowards();
            return;
        }

        FacePlayer();
        SetLocomotion(0f);
        if (Time.time >= nextFireballTime)
            StartCoroutine(CastLargeFireball());
    }

    void FindPlayer()
    {
        // The visible player can be a nested Meshy/RPG-character object and its
        // tag is not guaranteed to be on the same transform as PlayerController.
        // Prefer the actual controller, then retain the tag lookup as a fallback.
        PlayerController controllerPlayer = FindAnyObjectByType<PlayerController>();
        if (controllerPlayer != null)
        {
            player = controllerPlayer.transform;
            return;
        }

        GameObject tagged = GameObject.FindWithTag("Player");
        player = tagged != null ? tagged.transform : null;
    }

    IEnumerator MeleeThenRetreat(int hits)
    {
        busy = true;
        for (int i = 0; i < hits && player != null && PlanarDistance(transform.position, player.position) <= meleeRange * 1.18f; i++)
        {
            FacePlayer();
            animator?.SetTrigger(Melee);
            yield return new WaitForSeconds(attackCooldown * 0.52f);
            if (player != null && PlanarDistance(transform.position, player.position) <= meleeRange * 1.25f)
                player.GetComponent<PlayerStats>()?.TakeDamage(stats.Attack, transform.position);
            yield return new WaitForSeconds(attackCooldown * 0.48f);
        }

        float retreatUntil = Time.time + 1.25f;
        while (player != null && Time.time < retreatUntil && PlanarDistance(transform.position, player.position) < preferredRange - 0.4f)
        {
            MoveAway();
            yield return null;
        }
        nextFireballTime = Time.time + 0.35f;
        busy = false;
    }

    IEnumerator CastLargeFireball()
    {
        busy = true;
        FacePlayer();
        SetLocomotion(0f);
        animator?.SetTrigger(Cast);
        yield return new WaitForSeconds(castReleaseDelay);
        if (!dead && player != null)
        {
            Vector3 origin = transform.position + transform.forward * 1.05f + Vector3.up * 1.35f;
            GameObject fireball = Fireball.BuildArcanePrefab();
            fireball.name = "SuperiorTreeAnomaly_ArcaneBolt";
            fireball.transform.position = origin;
            fireball.GetComponent<Fireball>().Launch((player.position + Vector3.up - origin).normalized, fireballDamage);
        }
        yield return new WaitForSeconds(Mathf.Max(0.02f, castDuration - castReleaseDelay));
        nextFireballTime = Time.time + fireballCooldown;
        busy = false;
    }

    void MoveTowards() => MoveInDirection((player.position - transform.position).normalized, moveSpeed);
    void MoveAway() => MoveInDirection((transform.position - player.position).normalized, retreatSpeed);

    void MoveInDirection(Vector3 direction, float speed)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        direction.Normalize();
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10f * Time.deltaTime);
        // Same collision resolver used by the player: houses, walls, bridges and
        // all regular non-trigger world colliders physically block this enemy.
        controller.Move(direction * speed * Time.deltaTime);
        SetLocomotion(speed / moveSpeed);
    }

    void FacePlayer()
    {
        if (player == null) return;
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 12f * Time.deltaTime);
    }

    // Snap only on spawn. Writing transform.position after CharacterController.Move
    // makes the controller resolve the same contact twice and produces visible
    // hitching on slopes. From then on gravity and the controller own the position.
    void SnapToGroundOnce()
    {
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.position = ProjectToGround(transform.position);
        controller.enabled = wasEnabled;
    }

    void ApplyControllerGravity()
    {
        if (!controller.enabled) return;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += Physics.gravity.y * Time.deltaTime;

        CollisionFlags flags = controller.Move(Vector3.up * (verticalVelocity * Time.deltaTime));
        if ((flags & CollisionFlags.Below) != 0)
            verticalVelocity = -2f;
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
        if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y;
        return position;
    }

    void SetLocomotion(float normalizedSpeed)
    {
        requestedLocomotionSpeed = Mathf.Max(requestedLocomotionSpeed, normalizedSpeed);
    }

    void LateUpdate()
    {
        if (animator == null) return;

        Vector3 displacement = transform.position - previousPosition;
        displacement.y = 0f;
        float actualSpeed = displacement.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        float actualNormalized = actualSpeed < 0.06f ? 0f : Mathf.Clamp01(actualSpeed / Mathf.Max(moveSpeed, 0.1f));

        // The feet animate from real displacement, not the requested speed. If a
        // house, wall or steep surface blocks the controller, it now goes idle
        // instead of playing walk/run in place (the visible "surfing").
        float target = requestedLocomotionSpeed > 0f
            ? Mathf.Min(actualNormalized, Mathf.Clamp01(requestedLocomotionSpeed))
            : 0f;
        float response = target > displayedLocomotionSpeed ? 7.5f : 12f;
        displayedLocomotionSpeed = Mathf.MoveTowards(displayedLocomotionSpeed, target, response * Time.deltaTime);
        animator.SetFloat(Speed, displayedLocomotionSpeed);
        previousPosition = transform.position;
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y; return Vector3.Distance(a, b);
    }

    void Die()
    {
        if (dead) return;
        dead = true;
        StopAllCoroutines();
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        animator?.SetTrigger(Death);
        StartCoroutine(RemoveAfterDeathAnimation());
    }

    IEnumerator RemoveAfterDeathAnimation()
    {
        yield return new WaitForSeconds(3f);
        Destroy(gameObject);
    }
}

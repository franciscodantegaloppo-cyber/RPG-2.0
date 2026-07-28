using System.Collections;
using ithappy.Animals_FREE;
using UnityEngine;

public enum AnimalKind { Tiger, Dog, Horse, Deer, Kitty, Chicken, Pinguin }
public enum AnimalTemperament { Passive, Timid, Aggressive }

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CreatureMover))]
public class AnimalAI : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] AnimalKind animalKind = AnimalKind.Deer;
    [SerializeField] AnimalTemperament temperament = AnimalTemperament.Passive;

    public AnimalKind Kind => animalKind;
    public bool IsCombatHostile => temperament == AnimalTemperament.Aggressive && canAttack;

    [Header("Awareness")]
    [SerializeField] float detectionRadius = 8f;
    [SerializeField] float fleeRadius = 5f;
    [SerializeField] float leashRadius = 18f;
    [SerializeField] LayerMask playerMask;

    [Header("Wander")]
    [SerializeField] float wanderRadius = 8f;
    [SerializeField] float stepDistance = 3f;
    [SerializeField] float waitMin = 1.5f;
    [SerializeField] float waitMax = 4f;

    [Header("Combat")]
    [SerializeField] float attackRadius = 1.6f;
    [SerializeField] float attackCooldown = 1.6f;
    [SerializeField] float lungeSeconds = 0.35f;
    [SerializeField] bool canAttack;

    [Header("Retreat (flee / hide / search)")]
    [SerializeField] float fleeDistance = 7f;
    [SerializeField] float hideSeconds = 2.2f;
    [SerializeField] float maxFleeSeconds = 3f;

    CreatureMover mover;
    EnemyStats stats;
    CharacterController characterController;
    Animator animator;
    Renderer[] renderers;
    MaterialPropertyBlock tintBlock;
    Transform player;
    Terrain terrain;

    enum RetreatPhase { None, Fleeing, Hiding, Returning }

    Vector3 homePosition;
    Vector3 destination;
    float nextDecisionTime;
    float lastAttackTime;
    RetreatPhase retreatPhase = RetreatPhase.None;
    Vector3 hideSpot;
    Vector3 lastKnownPlayerPos;
    float retreatDeadline;
    float hideUntilTime;
    bool running;
    bool dead;
    bool hurtReacting;
    Coroutine hurtRoutine;
    Coroutine deathRoutine;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        mover = GetComponent<CreatureMover>();
        stats = GetComponent<EnemyStats>();
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        renderers = GetComponentsInChildren<Renderer>(true);
        tintBlock = new MaterialPropertyBlock();
        terrain = Terrain.activeTerrain;
        homePosition = ProjectToGround(transform.position);
        destination = homePosition;

        if (playerMask.value == 0)
            playerMask = LayerMask.GetMask("Player");

        MovePlayerInput playerInput = GetComponent<MovePlayerInput>();
        if (playerInput != null)
            playerInput.enabled = false;
    }

    void OnEnable()
    {
        if (stats != null)
            stats.OnDeath += OnDeath;
        nextDecisionTime = Time.time + Random.Range(0.2f, 1.2f);
    }

    void OnDisable()
    {
        if (stats != null)
            stats.OnDeath -= OnDeath;
    }

    void Update()
    {
        if (dead || hurtReacting)
            return;

        FindPlayer();

        if (temperament == AnimalTemperament.Aggressive && canAttack && player != null)
            AggressiveUpdate();
        else if (temperament == AnimalTemperament.Timid && player != null && DistanceTo(player.position) <= fleeRadius)
            FleeUpdate();
        else
            WanderUpdate();
    }

    void AggressiveUpdate()
    {
        float distance = DistanceTo(player.position);
        if (distance > leashRadius)
        {
            player = null;
            WanderUpdate();
            return;
        }

        if (retreatPhase != RetreatPhase.None)
        {
            HandleRetreatPhases();
            return;
        }

        if (distance <= attackRadius)
        {
            Face(player.position);
            SetMove(Vector3.zero, false);
            if (Time.time - lastAttackTime >= attackCooldown)
                StartCoroutine(LungeAttack());
            return;
        }

        destination = ProjectToGround(player.position);
        running = true;
        MoveToward(destination, true);
    }

    // Runs after a lunge lands: turn tail and actually run away (letting MoveToward/CreatureMover
    // turn the body to face the flee direction, instead of forcing it to face the player while
    // backpedaling - that's what made it look like it was reversing in place), pause somewhere
    // as if hiding, then head back toward where the player was last seen instead of beelining
    // straight at their current position, so it reads as "searching" rather than omniscient chase.
    void HandleRetreatPhases()
    {
        switch (retreatPhase)
        {
            case RetreatPhase.Fleeing:
                if (Reached(hideSpot) || Time.time > retreatDeadline)
                {
                    retreatPhase = RetreatPhase.Hiding;
                    hideUntilTime = Time.time + hideSeconds;
                    SetMove(Vector3.zero, false);
                }
                else
                {
                    MoveToward(hideSpot, true);
                }
                break;

            case RetreatPhase.Hiding:
                SetMove(Vector3.zero, false);
                if (Time.time >= hideUntilTime)
                    retreatPhase = RetreatPhase.Returning;
                break;

            case RetreatPhase.Returning:
                if (Reached(lastKnownPlayerPos))
                {
                    retreatPhase = RetreatPhase.None;
                }
                else
                {
                    MoveToward(ProjectToGround(lastKnownPlayerPos), false);
                }
                break;
        }
    }

    Vector3 PickFleeSpot()
    {
        Vector3 away = transform.position - (player != null ? player.position : transform.position - transform.forward);
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = -transform.forward;

        Vector3 spot = transform.position + away.normalized * fleeDistance;

        // Don't let fleeing drag it off past its usual roaming area.
        Vector3 fromHome = spot - homePosition;
        fromHome.y = 0f;
        float maxFromHome = wanderRadius * 1.5f;
        if (fromHome.magnitude > maxFromHome)
            spot = homePosition + fromHome.normalized * maxFromHome;

        return ProjectToGround(spot);
    }

    IEnumerator LungeAttack()
    {
        lastAttackTime = Time.time;
        float end = Time.time + lungeSeconds;
        while (Time.time < end && player != null && !dead)
        {
            MoveToward(player.position, true);
            yield return null;
        }

        SetMove(Vector3.zero, false);
        if (player != null && DistanceTo(player.position) <= attackRadius * 1.25f)
            player.GetComponent<PlayerStats>()?.TakeDamage(stats != null ? stats.Attack : 8f, transform.position);

        // Back off, hide for a moment, then head back toward where the player was instead of
        // staying glued to them and repeating the lunge immediately.
        lastKnownPlayerPos = player != null ? player.position : transform.position;
        hideSpot = PickFleeSpot();
        retreatDeadline = Time.time + maxFleeSeconds;
        retreatPhase = RetreatPhase.Fleeing;
    }

    void FleeUpdate()
    {
        Vector3 away = transform.position - player.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = transform.forward;

        destination = ProjectToGround(transform.position + away.normalized * stepDistance * 1.8f);
        ClampToHome();
        MoveToward(destination, true);
    }

    void WanderUpdate()
    {
        if (Time.time >= nextDecisionTime || Reached(destination))
        {
            PickWanderDestination();
            running = Random.value < RunChance();
            nextDecisionTime = Time.time + Random.Range(waitMin, waitMax);
        }

        if (Reached(destination))
        {
            SetMove(Vector3.zero, false);
            return;
        }

        MoveToward(destination, running);
    }

    void PickWanderDestination()
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 random = Random.insideUnitCircle.normalized * Random.Range(stepDistance * 0.6f, stepDistance * 1.4f);
            destination = ProjectToGround(transform.position + new Vector3(random.x, 0f, random.y));
            ClampToHome();
            if (!Reached(destination))
                return;
        }

        destination = homePosition;
    }

    void MoveToward(Vector3 target, bool run)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.04f)
        {
            SetMove(Vector3.zero, false);
            return;
        }

        SetMove(dir.normalized, run);
    }

    void SetMove(Vector3 direction, bool run)
    {
        Vector2 axis = direction.sqrMagnitude > 0.001f
            ? new Vector2(direction.x, direction.z).normalized
            : Vector2.zero;

        Vector3 lookTarget = direction.sqrMagnitude > 0.001f
            ? transform.position + direction.normalized * 4f
            : transform.position + transform.forward;

        mover.SetInput(axis, lookTarget, run, false);
    }

    void FindPlayer()
    {
        if (player != null && DistanceTo(player.position) <= detectionRadius)
            return;

        player = null;
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, playerMask, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
            player = hits[0].transform;
    }

    void Face(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
            return;

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 540f * Time.deltaTime);
    }

    void ClampToHome()
    {
        Vector3 offset = destination - homePosition;
        offset.y = 0f;
        if (offset.magnitude > wanderRadius)
            destination = ProjectToGround(homePosition + offset.normalized * wanderRadius);
    }

    Vector3 ProjectToGround(Vector3 position)
    {
        if (terrain != null)
        {
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 0.05f;
            return position;
        }

        Vector3 origin = position + Vector3.up * 4f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y + 0.05f;
        return position;
    }

    float DistanceTo(Vector3 target) =>
        Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(target.x, target.z));

    bool Reached(Vector3 target) => DistanceTo(target) <= 0.35f;

    float RunChance()
    {
        return animalKind switch
        {
            AnimalKind.Tiger => 0.35f,
            AnimalKind.Horse => 0.28f,
            AnimalKind.Deer => 0.25f,
            AnimalKind.Dog => 0.18f,
            AnimalKind.Kitty => 0.12f,
            AnimalKind.Chicken => 0.08f,
            _ => 0.08f
        };
    }

    public void OnHurt(Vector3 attackerPos)
    {
        if (dead)
            return;

        if (hurtRoutine != null)
            StopCoroutine(hurtRoutine);

        hurtRoutine = StartCoroutine(HurtReaction(attackerPos));
    }

    IEnumerator HurtReaction(Vector3 attackerPos)
    {
        hurtReacting = true;
        destination = transform.position;
        nextDecisionTime = Time.time + 0.55f;
        SetMove(Vector3.zero, false);
        SetTint(new Color(1f, 0.35f, 0.25f, 1f));

        Vector3 away = transform.position - attackerPos;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = -transform.forward;
        away.Normalize();

        float end = Time.time + 0.18f;
        while (Time.time < end && !dead)
        {
            Vector3 motion = away * (2.2f * Time.deltaTime);
            if (characterController != null && characterController.enabled)
                characterController.Move(motion);
            else
                transform.position += motion;

            yield return null;
        }

        ClearTint();
        hurtReacting = false;
        hurtRoutine = null;
    }

    void OnDeath()
    {
        if (dead)
            return;

        dead = true;
        SetMove(Vector3.zero, false);

        if (hurtRoutine != null)
            StopCoroutine(hurtRoutine);

        if (deathRoutine != null)
            StopCoroutine(deathRoutine);

        deathRoutine = StartCoroutine(DeathReaction());
    }

    IEnumerator DeathReaction()
    {
        SetTint(new Color(0.75f, 0.18f, 0.14f, 1f));

        if (mover != null)
            mover.enabled = false;

        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (characterController != null)
            characterController.enabled = false;

        if (animator != null)
            animator.speed = 0f;

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0f, 0f, 88f);
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + Vector3.down * 0.2f;

        const float duration = 0.75f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        Destroy(gameObject, 4f);
    }

    void SetTint(Color color)
    {
        if (renderers == null)
            return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(tintBlock);
            tintBlock.SetColor(BaseColorId, color);
            tintBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(tintBlock);
        }
    }

    void ClearTint()
    {
        if (renderers == null)
            return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
                renderer.SetPropertyBlock(null);
        }
    }
}

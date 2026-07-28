using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class NPCWander : MonoBehaviour
{
    [Header("Wander")]
    [SerializeField] float stepDistance = 2.2f;
    [SerializeField] float moveSpeed = 0.75f;
    [SerializeField] float animationWalkSpeed = 1f;
    [SerializeField] RuntimeAnimatorController walkController;
    [SerializeField] float waitMin = 2.5f;
    [SerializeField] float waitMax = 5f;
    [SerializeField] float maxRadius = 5f;
    [SerializeField] float awareDistance = 2.8f;
    [SerializeField] float turnSpeed = 540f;
    [SerializeField] float terrainStickOffset = 0f;
    [SerializeField] float maxSurfaceStepHeight = 0.85f;
    [SerializeField] float maxDropFromInterior = 0.45f;
    [SerializeField] bool useCharacterControllerMovement = true;

    CharacterController cc;
    Animator anim;
    Transform playerTf;
    GroundSnapOnStart groundSnap;
    NPCVisualGroundAligner visualGroundAligner;

    static readonly int HashMoving = Animator.StringToHash("Moving");
    static readonly int HashVelocityZ = Animator.StringToHash("Velocity Z");
    static readonly int HashVelocityX = Animator.StringToHash("Velocity X");
    static readonly int HashWeapon = Animator.StringToHash("Weapon");
    static readonly int HashRightWeapon = Animator.StringToHash("RightWeapon");
    static readonly int HashWalkState = Animator.StringToHash("Walk");
    static readonly int HashWalkStateFull = Animator.StringToHash("Base Layer.Walk");
    static readonly int HashIdleState = Animator.StringToHash("Idle");
    static readonly int HashIdleStateFull = Animator.StringToHash("Base Layer.Idle");

    bool hasMovingParam;
    bool hasVelocityZParam;
    bool hasVelocityXParam;
    bool hasWeaponParam;
    bool hasRightWeaponParam;
    bool hasWalkState;
    bool hasIdleState;
    int walkStateHashToPlay;
    int idleStateHashToPlay;
    bool lastMoving;

    Vector3 spawnPoint;
    Collider spawnSurface;
    Vector3 horizontalDir;
    float verticalVel;
    float nextSnapTime;
    bool wanderActive = true;
    Coroutine wanderRoutine;
    float externalMoveSpeed = -1f;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cc == null)
            cc = gameObject.AddComponent<CharacterController>();
        anim = GetComponentInChildren<Animator>();
        groundSnap = GetComponent<GroundSnapOnStart>();
        if (groundSnap == null)
            groundSnap = gameObject.AddComponent<GroundSnapOnStart>();
        groundSnap.UseVisualFooting(terrainStickOffset);

        visualGroundAligner = GetComponent<NPCVisualGroundAligner>();
        if (visualGroundAligner == null)
            visualGroundAligner = gameObject.AddComponent<NPCVisualGroundAligner>();
        visualGroundAligner.Configure(terrainStickOffset);

        if (anim != null)
        {
#if UNITY_EDITOR
            if (walkController == null)
                walkController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_RPG/Animations/MerchantWalk2.controller");
#endif
            if (walkController != null)
                anim.runtimeAnimatorController = walkController;
            anim.applyRootMotion = false;
            anim.speed = 1f;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        moveSpeed = Mathf.Max(0.35f, moveSpeed);
        animationWalkSpeed = Mathf.Max(0.55f, animationWalkSpeed);
        turnSpeed = Mathf.Max(90f, turnSpeed);
        terrainStickOffset = Mathf.Max(0f, terrainStickOffset);

        if (walkController == null)
            walkController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_RPG/Animations/MerchantWalk2.controller");
    }
#endif

    void Start()
    {
        groundSnap?.SnapNow();
        visualGroundAligner?.AlignNow();
        AlignControllerFeetToGround();
        StartCoroutine(SnapVisualAfterPose());
        spawnPoint = transform.position;
        spawnSurface = GetCurrentGroundSurface();

        var player = GameObject.FindWithTag("Player");
        if (player != null)
            playerTf = player.transform;

        CacheAnimatorSetup();
        SetAnim(false);
        RestartWander();
    }

    void Update()
    {
        if (useCharacterControllerMovement && cc == null)
            return;

        verticalVel = cc != null && cc.isGrounded ? -2f : Mathf.Max(verticalVel + Physics.gravity.y * Time.deltaTime, -15f);

        if (horizontalDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime);
        }

        float activeMoveSpeed = externalMoveSpeed >= 0f ? externalMoveSpeed : moveSpeed;
        Vector3 move = horizontalDir.sqrMagnitude > 0.01f ? transform.forward * activeMoveSpeed : Vector3.zero;
        if (useCharacterControllerMovement && cc != null && cc.enabled)
        {
            cc.Move((move + Vector3.up * verticalVel) * Time.deltaTime);
        }
        else
        {
            Vector3 next = transform.position + move * Time.deltaTime;
            if (GroundUtility.TryProjectToGround(next, transform, out Vector3 grounded, 5f, 8f))
                transform.position = grounded;
        }
        KeepFeetOnTerrain();
        visualGroundAligner?.AlignNow();
        AlignControllerFeetToGround();
    }

    IEnumerator SnapVisualAfterPose()
    {
        yield return null;
        groundSnap?.SnapNow();
        visualGroundAligner?.AlignNow();
        AlignControllerFeetToGround();
        yield return new WaitForSeconds(0.1f);
        groundSnap?.SnapNow();
        visualGroundAligner?.AlignNow();
        AlignControllerFeetToGround();
    }

    void KeepFeetOnTerrain()
    {
        if (groundSnap == null || Time.time < nextSnapTime)
            return;

        nextSnapTime = Time.time + 0.06f;
        // GetGroundY (not raw terrain.SampleHeight) so NPCs entering a house pick up its floor/
        // stairs colliders instead of only ever tracking the outdoor terrain height beneath them.
        float groundY = GroundUtility.GetGroundY(transform.position, transform);
        if (float.IsNegativeInfinity(groundY) || !groundSnap.TryGetVisualBottomY(out float bottomY))
            return;

        float yDelta = groundY + terrainStickOffset - bottomY;
        if (Mathf.Abs(yDelta) <= 0.004f)
            return;

        bool wasEnabled = cc != null && cc.enabled;
        if (wasEnabled)
            cc.enabled = false;

        transform.position += Vector3.up * yDelta;

        if (wasEnabled)
            cc.enabled = true;

        verticalVel = -2f;
    }

    void AlignControllerFeetToGround()
    {
        if (cc == null || !cc.enabled)
            return;

        float groundY = GroundUtility.GetGroundY(transform.position, transform);
        if (float.IsNegativeInfinity(groundY))
            return;

        Vector3 center = cc.center;
        center.y = groundY - transform.position.y + cc.height * 0.5f + terrainStickOffset;
        cc.center = center;
    }

    public void Configure(float speed, float radius, float step, float minWait, float maxWait)
    {
        moveSpeed = Mathf.Max(0.35f, speed);
        maxRadius = Mathf.Max(0.5f, radius);
        stepDistance = Mathf.Max(0.25f, step);
        waitMin = Mathf.Max(0f, minWait);
        waitMax = Mathf.Max(waitMin, maxWait);
    }

    public void UseTransformMovement(bool value)
    {
        useCharacterControllerMovement = !value;
        if (cc != null)
            cc.enabled = !value;
    }

    IEnumerator WanderLoop()
    {
        while (true)
        {
            SetAnim(false);
            horizontalDir = Vector3.zero;

            float wait = Random.Range(waitMin, waitMax);
            for (float t = 0f; t < wait; t += Time.deltaTime)
            {
                LookAtPlayerIfNear();
                yield return null;
            }

            if (!wanderActive)
            {
                yield return null;
                continue;
            }

            if (TryNextStep(out Vector3 dest))
            {
                SetAnim(true);
                yield return WalkStep(dest);
            }
            else
            {
                SetAnim(false);
                horizontalDir = Vector3.zero;
                yield return null;
            }
        }
    }

    bool TryNextStep(out Vector3 destination)
    {
        for (int i = 0; i < 8; i++)
        {
            float angle = Random.Range(0f, 360f);
            float dist = Random.Range(stepDistance * 0.7f, stepDistance * 1.3f);
            Vector3 candidate = transform.position + new Vector3(
                Mathf.Sin(angle * Mathf.Deg2Rad) * dist,
                0f,
                Mathf.Cos(angle * Mathf.Deg2Rad) * dist);

            Vector3 fromSpawn = candidate - spawnPoint;
            fromSpawn.y = 0f;
            if (fromSpawn.magnitude > maxRadius)
                candidate = spawnPoint + fromSpawn.normalized * maxRadius;

            if (!TryProjectWalkCandidate(candidate, out candidate))
                continue;

            if (HorizontalDist(transform.position, candidate) > 0.35f)
            {
                destination = candidate;
                return true;
            }
        }

        destination = transform.position;
        return false;
    }

    IEnumerator WalkStep(Vector3 dest)
    {
        float timeout = Mathf.Max(6f, stepDistance / Mathf.Max(0.1f, moveSpeed) * 4f);
        float elapsed = 0f;

        while (true)
        {
            if (PlayerNearby())
            {
                SetAnim(false);
                horizontalDir = Vector3.zero;
                LookAtPlayerIfNear();
                elapsed += Time.deltaTime;
                if (elapsed > timeout) break;
                yield return null;
                continue;
            }

            if (HorizontalDist(transform.position, dest) < 0.25f)
                break;

            horizontalDir = HorizontalDir(transform.position, dest);
            SetAnim(true);

            elapsed += Time.deltaTime;
            if (elapsed > timeout) break;

            yield return null;
        }

        horizontalDir = Vector3.zero;
        SetAnim(false);
    }

    bool PlayerNearby() =>
        playerTf != null && HorizontalDist(transform.position, playerTf.position) < awareDistance;

    void LookAtPlayerIfNear()
    {
        if (!PlayerNearby()) return;

        Vector3 dir = playerTf.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                Time.deltaTime * 4f);
        }
    }

    void SetAnim(bool moving)
    {
        if (anim == null) return;

        SetAnimInt(HashWeapon, 0, hasWeaponParam);
        SetAnimInt(HashRightWeapon, 0, hasRightWeaponParam);
        SetAnimFloat(HashVelocityZ, moving ? 1f : 0f, hasVelocityZParam);
        SetAnimFloat(HashVelocityX, 0f, hasVelocityXParam);
        SetAnimBool(HashMoving, moving, hasMovingParam);

        if (moving && !lastMoving && hasWalkState)
            anim.CrossFade(walkStateHashToPlay, 0.15f);
        else if (!moving && lastMoving && hasIdleState)
            anim.CrossFade(idleStateHashToPlay, 0.2f);

        anim.speed = moving ? Mathf.Max(0.55f, animationWalkSpeed) : 1f;
        lastMoving = moving;
    }

    void CacheAnimatorSetup()
    {
        if (anim == null) return;

        hasMovingParam = HasAnimatorParameter(HashMoving, AnimatorControllerParameterType.Bool);
        hasVelocityZParam = HasAnimatorParameter(HashVelocityZ, AnimatorControllerParameterType.Float);
        hasVelocityXParam = HasAnimatorParameter(HashVelocityX, AnimatorControllerParameterType.Float);
        hasWeaponParam = HasAnimatorParameter(HashWeapon, AnimatorControllerParameterType.Int);
        hasRightWeaponParam = HasAnimatorParameter(HashRightWeapon, AnimatorControllerParameterType.Int);

        bool hasShortWalkState = anim.layerCount > 0 && anim.HasState(0, HashWalkState);
        bool hasFullWalkState = anim.layerCount > 0 && anim.HasState(0, HashWalkStateFull);
        hasWalkState = hasShortWalkState || hasFullWalkState;
        walkStateHashToPlay = hasShortWalkState ? HashWalkState : HashWalkStateFull;
        bool hasShortIdleState = anim.layerCount > 0 && anim.HasState(0, HashIdleState);
        bool hasFullIdleState = anim.layerCount > 0 && anim.HasState(0, HashIdleStateFull);
        hasIdleState = hasShortIdleState || hasFullIdleState;
        idleStateHashToPlay = hasShortIdleState ? HashIdleState : HashIdleStateFull;
    }

    bool HasAnimatorParameter(int hash, AnimatorControllerParameterType type)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return false;

        foreach (var parameter in anim.parameters)
        {
            if (parameter.nameHash == hash && parameter.type == type)
                return true;
        }

        return false;
    }

    void SetAnimInt(int hash, int value, bool exists)
    {
        if (exists) anim.SetInteger(hash, value);
    }

    void SetAnimFloat(int hash, float value, bool exists)
    {
        if (exists) anim.SetFloat(hash, value);
    }

    void SetAnimBool(int hash, bool value, bool exists)
    {
        if (exists) anim.SetBool(hash, value);
    }

    static float HorizontalDist(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    static Vector3 HorizontalDir(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        d.y = 0f;
        return d.normalized;
    }

    bool TryProjectWalkCandidate(Vector3 candidate, out Vector3 projected)
    {
        projected = candidate;

        if (!GroundUtility.TryGetGround(candidate, transform, out var candidateGround, 5f, 8f))
            return false;
        if (!GroundUtility.TryGetGround(transform.position, transform, out var currentGround, 5f, 8f))
            return false;

        float delta = candidateGround.y - currentGround.y;
        if (delta > maxSurfaceStepHeight)
            return false;

        bool currentlyOnInterior = currentGround.collider != null && !(currentGround.collider is TerrainCollider);
        bool candidateOnTerrain = candidateGround.IsTerrain;
        if (spawnSurface != null && candidateOnTerrain)
            return false;
        if (currentlyOnInterior && candidateOnTerrain && delta < -maxDropFromInterior)
            return false;

        if (spawnSurface != null && !SameWalkableArea(spawnSurface, candidateGround.collider))
            return false;

        projected.y = candidateGround.y;
        return true;
    }

    Collider GetCurrentGroundSurface()
    {
        return GroundUtility.TryGetGround(transform.position, transform, out var ground, 5f, 8f) ? ground.collider : null;
    }

    static bool SameWalkableArea(Collider a, Collider b)
    {
        if (a == b)
            return true;

        Transform rootA = FindWalkableRoot(a.transform);
        Transform rootB = FindWalkableRoot(b.transform);
        return rootA != null && rootA == rootB;
    }

    static Transform FindWalkableRoot(Transform t)
    {
        while (t != null)
        {
            if (t.name == "VillageWalkableSurfaces" || t.name.StartsWith("House_", System.StringComparison.Ordinal))
                return t.name == "VillageWalkableSurfaces" && t.parent != null ? t.parent : t;
            t = t.parent;
        }

        return null;
    }

    public void PauseForInteraction()
    {
        if (wanderRoutine != null)
            StopCoroutine(wanderRoutine);
        wanderRoutine = null;
        horizontalDir = Vector3.zero;
        externalMoveSpeed = -1f;
        SetAnim(false);
    }

    public void MoveForCombat(Vector3 direction, float speed)
    {
        direction.y = 0f;
        horizontalDir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
        externalMoveSpeed = Mathf.Max(0.2f, speed);
        SetAnim(horizontalDir.sqrMagnitude > 0.001f);
    }

    public void StopCombatMove()
    {
        horizontalDir = Vector3.zero;
        externalMoveSpeed = -1f;
        SetAnim(false);
    }

    public void ResumeWander()
    {
        if (!wanderActive) return;
        RestartWander();
    }

    public void SetMoving(bool value)
    {
        wanderActive = value;
        if (wanderRoutine != null)
            StopCoroutine(wanderRoutine);
        wanderRoutine = null;
        horizontalDir = Vector3.zero;
        externalMoveSpeed = -1f;
        SetAnim(false);
        if (value)
            RestartWander();
    }

    void RestartWander()
    {
        if (!wanderActive || wanderRoutine != null) return;
        wanderRoutine = StartCoroutine(WanderLoop());
    }
}

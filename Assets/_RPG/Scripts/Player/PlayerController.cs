using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float walkSpeed = 3f;
    [SerializeField] float runSpeed = 6f;
    [SerializeField] float movementAcceleration = 24f;
    [SerializeField] float movementDeceleration = 34f;
    [SerializeField] float jumpHeight = 3.5f;
    [SerializeField] float gravityMultiplier = 2f;
    [Header("Water movement")]
    [SerializeField, Range(0.05f, 1f)] float waterSpeedMultiplier = 0.18f;
    [SerializeField, Range(0.1f, 4f)] float waterJumpMultiplier = 3f;
    [SerializeField, Range(0.01f, 1f)] float waterGravityMultiplier = 0.025f;

    [Header("Ground")]
    [SerializeField] float groundCheckDistance = 0.4f;
    [SerializeField] LayerMask groundMask;
    [SerializeField] float steepSlopeSlideSpeed = 7.5f;
    [SerializeField] float steepSlopeContactMemory = .16f;
    [SerializeField, Range(80f, 89.5f)] float steepSlopeSlideMinimumAngle = 86f;

    [Header("Fall damage")]
    [SerializeField] float fallDamageStepMeters = 2f;
    [SerializeField, Range(0f, 1f)] float fallDamagePercentPerStep = 0.1f;

    [Header("References")]
    [SerializeField] Transform cameraTransform;

    CharacterController cc;
    PlayerStats stats;
    PlayerAnimatorBridge animBridge;
    PlayerWaterBreathing waterBreathing;
    PlayerRpgAnimationPackController rpgAnimations;

    Vector3 velocity;
    Vector3 planarMotion;
    bool isGrounded;
    bool isJumping;
    bool hasFallTriggered;
    bool landAnimationCleared;
    bool trackingFallHeight;
    float highestAirborneY;
    bool onTooSteepSlope;
    Vector3 steepSlopeNormal = Vector3.up;
    float lastSteepSlopeContactTime = -10f;

    public bool IsMoving { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsGrounded => isGrounded;
    public Vector3 MoveDirection { get; private set; }
    public Vector3 CurrentPlanarMotion => planarMotion;

    public void ResetAfterRespawn()
    {
        velocity = Vector3.zero;
        planarMotion = Vector3.zero;
        isGrounded = false;
        isJumping = false;
        hasFallTriggered = false;
        landAnimationCleared = false;
        trackingFallHeight = false;
        onTooSteepSlope = false;
        lastSteepSlopeContactTime = -10f;
        IsMoving = false;
        IsSprinting = false;
        MoveDirection = Vector3.zero;
    }

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (GetComponent<PlayerWaterBreathing>() == null)
            gameObject.AddComponent<PlayerWaterBreathing>();
        waterBreathing = GetComponent<PlayerWaterBreathing>();
        stats = GetComponent<PlayerStats>();
        animBridge = GetComponent<PlayerAnimatorBridge>();
        rpgAnimations = GetComponent<PlayerRpgAnimationPackController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive()) return;

        CheckGround();
        HandleMovement();
        ApplySteepSlopeSlide();
        HandleJump();
        ApplyGravity();
        ApplyWindPush();
    }

    void ApplyWindPush()
    {
        if (WindManager.Instance == null || (stats != null && stats.MaxWindResistanceEnabled)) return;
        Vector3 push = WindManager.Instance.GetPushVector();
        if (push.sqrMagnitude > 0.0001f)
            CompleteJumpOnGroundContact(cc.Move(push * Time.deltaTime));
    }

    void CheckGround()
    {
        // Bottom of the capsule = transform + cc.center - up*(height/2 - radius)
        Vector3 sphereCenter = transform.position + cc.center + Vector3.down * (cc.height * 0.5f - cc.radius);
        // groundMask was tuned against SpawnVillage's terrain/ground layer; scenes with a
        // different floor layer (e.g. the dungeon's floor meshes) would otherwise never read
        // as grounded and silently block jumping. cc.isGrounded reflects the CharacterController's
        // own last collision resolution regardless of layer, so OR it in as a mask-agnostic fallback.
        // Thin bridge meshes can report a contact for only the Move frame that
        // landed on them. A short downward sphere cast keeps that contact valid
        // on the following frame, so the jump state reliably finishes.
        bool hasGroundProbe = Physics.SphereCast(
            sphereCenter + Vector3.up * 0.12f,
            cc.radius * 0.82f,
            Vector3.down,
            out RaycastHit groundHit,
            groundCheckDistance + 0.32f,
            ~0,
            QueryTriggerInteraction.Ignore) &&
            groundHit.collider != null && !groundHit.collider.transform.IsChildOf(transform);

        bool recentSteepContact = Time.time - lastSteepSlopeContactTime <= steepSlopeContactMemory;
        float probedSlopeAngle = hasGroundProbe ? Vector3.Angle(groundHit.normal, Vector3.up) : 0f;
        // Sliding is a terrain anti-climb rule, not a generic collider response. Door frames,
        // stairs and architecture may have steep bevels and must never push the player back.
        bool probeIsTooSteep = hasGroundProbe && groundHit.collider is TerrainCollider &&
                               probedSlopeAngle >= steepSlopeSlideMinimumAngle;
        bool walkableProbe = hasGroundProbe && !probeIsTooSteep;
        onTooSteepSlope = probeIsTooSteep || recentSteepContact;
        if (probeIsTooSteep)
            steepSlopeNormal = groundHit.normal;

        // CharacterController.isGrounded does not expose the contact normal. Never let that
        // flag turn a remembered steep contact back into valid jumpable ground.
        isGrounded = walkableProbe || (cc.isGrounded && !onTooSteepSlope);

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        if (!isGrounded)
        {
            if (!trackingFallHeight)
            {
                trackingFallHeight = true;
                highestAirborneY = transform.position.y;
            }
            else
            {
                highestAirborneY = Mathf.Max(highestAirborneY, transform.position.y);
            }
        }
        else if (trackingFallHeight)
        {
            ApplyFallDamage();
            trackingFallHeight = false;
        }

        // Transition to fall animation at apex (velocity goes negative)
        if (isJumping && !isGrounded && velocity.y < 0f && !hasFallTriggered)
        {
            hasFallTriggered = true;
            animBridge?.TriggerFall();
        }

        // Ground probes can be more reliable than CharacterController's one-frame Below flag on
        // thin bridges and angled meshes. Force the animator back to grounded as soon as either
        // one reports terrain, even if its jump state got out of sync earlier.
        if (isGrounded && velocity.y <= 0f)
            CompleteJumpOnGroundContact(CollisionFlags.Below);
        else if (isGrounded)
            animBridge?.SetGroundedState();
    }

    void CompleteJumpOnGroundContact(CollisionFlags collisionFlags)
    {
        if ((collisionFlags & CollisionFlags.Below) == 0)
            return;

        // A terrain/bridge hit is authoritative: do not leave the visual state
        // in Jump/Fall just because the upward velocity had not been cleared yet.
        if (velocity.y > 0f)
            velocity.y = -2f;

        if (!landAnimationCleared)
        {
            animBridge?.TriggerLand();
        }
        else
            animBridge?.SetGroundedState();
        isJumping = false;
        hasFallTriggered = false;
        landAnimationCleared = true;
    }

    void ApplyFallDamage()
    {
        float fallDistance = highestAirborneY - transform.position.y;
        int steps = Mathf.FloorToInt(fallDistance / Mathf.Max(0.01f, fallDamageStepMeters));
        if (steps > 0)
            stats?.TakeFallDamage(steps * fallDamagePercentPerStep);
    }

    void HandleMovement()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);

        Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 camRight   = cameraTransform != null ? cameraTransform.right   : Vector3.right;
        camForward.y = 0f; camForward.Normalize();
        camRight.y   = 0f; camRight.Normalize();

        MoveDirection = camForward * v + camRight * h;
        bool hasMoveInput = MoveDirection.magnitude > 0.1f;

        bool wantSprint = kb.leftShiftKey.isPressed && hasMoveInput && stats.HasStamina();
        IsSprinting = wantSprint;

        float waterSpeed = waterBreathing != null && waterBreathing.IsBodyInWater ? waterSpeedMultiplier : 1f;
        if (rpgAnimations == null)
            rpgAnimations = GetComponent<PlayerRpgAnimationPackController>();
        float actionMovement = rpgAnimations != null
            ? rpgAnimations.MovementMultiplier
            : 1f;
        float speed = (IsSprinting ? runSpeed : walkSpeed) *
                      (stats != null ? stats.MoveSpeedMultiplier : 1f) *
                      waterSpeed * actionMovement;
        if (IsSprinting) stats.DrainStamina(Time.deltaTime * 15f);

        Vector3 desiredMotion = hasMoveInput
            ? MoveDirection.normalized * speed
            : Vector3.zero;
        float response = hasMoveInput ? movementAcceleration : movementDeceleration;
        planarMotion = Vector3.MoveTowards(planarMotion, desiredMotion,
            response * Time.deltaTime);
        IsMoving = planarMotion.sqrMagnitude > .01f;
        cc.Move(planarMotion * Time.deltaTime);

        if (hasMoveInput)
        {
            Quaternion targetRot = Quaternion.LookRotation(MoveDirection.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        float animationSpeed = IsSprinting ? 1f : 0.5f;
        if (waterBreathing != null && waterBreathing.IsBodyInWater)
            animationSpeed *= 0.72f;
        animBridge?.SetMoveParams(IsMoving ? animationSpeed : 0f);
    }

    void HandleJump()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame && isGrounded && !onTooSteepSlope && stats.HasStamina(10f))
        {
            float waterJump = waterBreathing != null && waterBreathing.IsBodyInWater ? waterJumpMultiplier : 1f;
            velocity.y = Mathf.Sqrt(jumpHeight * waterJump * 2f * Physics.gravity.magnitude);
            stats.DrainStamina(10f);
            isJumping = true;
            hasFallTriggered = false;
            landAnimationCleared = false;
            animBridge?.TriggerJump();
        }
    }

    void ApplyGravity()
    {
        float waterGravity = waterBreathing != null && waterBreathing.IsBodyInWater ? waterGravityMultiplier : 1f;
        velocity.y += Physics.gravity.y * gravityMultiplier * waterGravity * Time.deltaTime;
        CollisionFlags flags = cc.Move(velocity * Time.deltaTime);
        CompleteJumpOnGroundContact(flags);
    }

    void ApplySteepSlopeSlide()
    {
        if (!onTooSteepSlope) return;

        // Project gravity onto the contact plane. The result always points downhill, so pressing
        // toward the mountain cannot overpower it by repeatedly alternating movement and jump.
        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, steepSlopeNormal);
        if (downhill.sqrMagnitude < .0001f) downhill = Vector3.down;
        downhill.Normalize();
        cc.Move(downhill * steepSlopeSlideSpeed * Time.deltaTime);
        velocity.y = Mathf.Min(velocity.y, -2f);
        isGrounded = false;
    }

    // This is the final ground-contact authority. Sphere checks can miss a thin
    // top face for one frame, but the CharacterController reports the real collider
    // it just touched, including sloped terrain and bridge meshes.
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null)
            return;

        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        if (hit.collider is TerrainCollider && hit.normal.y > .01f &&
            slopeAngle >= steepSlopeSlideMinimumAngle)
        {
            steepSlopeNormal = hit.normal;
            lastSteepSlopeContactTime = Time.time;
            onTooSteepSlope = true;
            isGrounded = false;
            return;
        }

        if (hit.normal.y < 0.18f || velocity.y > 0.15f)
            return;

        isGrounded = true;
        velocity.y = -2f;
        CompleteJumpOnGroundContact(CollisionFlags.Below);
        animBridge?.SetGroundedState();
    }
}

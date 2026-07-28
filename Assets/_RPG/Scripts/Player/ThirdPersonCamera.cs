using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform target;
    [SerializeField] Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Distance")]
    [SerializeField] float defaultDistance = 5f;
    [SerializeField] float minDistance = 1f;
    [SerializeField] float maxDistance = 10f;
    [SerializeField] float zoomSpeed = 2f;

    [Header("Orbit")]
    [SerializeField] float orbitSpeedX = 200f;
    [SerializeField] float orbitSpeedY = 150f;
    [SerializeField] float minVerticalAngle = -20f;
    [SerializeField] float maxVerticalAngle = 60f;

    [Header("Collision")]
    [SerializeField] float collisionRadius = 0.3f;
    [SerializeField] LayerMask collisionMask;

    [Header("Interior Mode")]
    public bool InteriorMode = false;
    [SerializeField] float interiorDistance = 2.5f;

    [Header("Boss Room Mode")]
    public bool BossRoomMode = false;
    [SerializeField] float bossRoomDistance = 9f;
    [SerializeField] float bossRoomVerticalAngle = 32f;

    float currentX;
    float currentY = 20f;
    float currentDistance;
    float impulseStrength;
    float impulseEndsAt;
    float impulseDuration;

    void Awake()
    {
        // Pickups (coins, runes, etc.) use the Interactable layer so the player can target them
        // with E. They are not camera walls: a floating/rotating coin crossing this SphereCast
        // made the camera repeatedly snap inward and outward every frame.
        collisionMask &= ~LayerMask.GetMask("Interactable", "Player");
    }

    void Start()
    {
        currentDistance = defaultDistance;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "NewGame")
            return;
        if (target == null && GameManager.Instance?.PlayerTransform != null)
            target = GameManager.Instance.PlayerTransform;

        // Lock cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "NewGame")
            return;
        if (target == null)
        {
            var p = GameManager.Instance?.PlayerTransform;
            if (p != null) target = p;
            else return;
        }

        if (GameManager.Instance == null || GameManager.Instance.IsGameplayActive())
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var delta = mouse.delta.ReadValue();
                currentX += delta.x * orbitSpeedX * Time.deltaTime;
                currentY -= delta.y * orbitSpeedY * Time.deltaTime;
                currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);

                float scroll = mouse.scroll.ReadValue().y;
                currentDistance -= scroll * zoomSpeed * 0.1f;
                currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
            }
        }

        float effectiveDistance = BossRoomMode ? bossRoomDistance : (InteriorMode ? interiorDistance : currentDistance);
        float effectiveVertical = BossRoomMode ? bossRoomVerticalAngle : currentY;

        Vector3 pivot = target.position + targetOffset;
        Quaternion rotation = Quaternion.Euler(effectiveVertical, currentX, 0f);
        Vector3 desiredPos = pivot - rotation * Vector3.forward * effectiveDistance;

        if (Physics.SphereCast(pivot, collisionRadius, desiredPos - pivot, out RaycastHit hit,
            effectiveDistance, collisionMask))
        {
            effectiveDistance = hit.distance;
            desiredPos = pivot - rotation * Vector3.forward * effectiveDistance;
        }

        if (Time.unscaledTime < impulseEndsAt && impulseStrength > .001f)
        {
            float remaining = Mathf.Clamp01((impulseEndsAt - Time.unscaledTime) /
                                            Mathf.Max(.001f, impulseDuration));
            float noiseTime = Time.unscaledTime * 43f;
            Vector3 shake = new Vector3(
                Mathf.PerlinNoise(noiseTime, 1.7f) - .5f,
                Mathf.PerlinNoise(2.3f, noiseTime) - .5f,
                0f) * (impulseStrength * remaining * 2f);
            desiredPos += transform.TransformDirection(shake);
        }

        transform.position = desiredPos;
        transform.LookAt(pivot);
    }

    public void SetTarget(Transform t) => target = t;

    public void AddImpulse(float strength, float duration)
    {
        impulseStrength = Mathf.Max(impulseStrength, Mathf.Clamp(strength, 0f, .32f));
        impulseDuration = Mathf.Max(.02f, duration);
        impulseEndsAt = Mathf.Max(impulseEndsAt, Time.unscaledTime + impulseDuration);
    }
}

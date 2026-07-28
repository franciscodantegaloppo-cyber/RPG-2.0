using UnityEngine;

// Heavy footsteps are measured from real world travel, so their timing stays correct even when
// the boss has a temporary faster pursuit stride.
public class DemonioAnomaloFootsteps : MonoBehaviour
{
    [SerializeField] float stepDistance = 1.4f;
    [SerializeField] float shakeStrength = 0.075f;
    [SerializeField] float shakeDuration = 0.11f;
    [SerializeField] float audibleDistance = 30f;

    Vector3 lastPosition;
    float distanceSinceStep;

    void Awake()
    {
        // Existing scene instances may still contain the older, much stronger
        // serialized shake values.
        shakeStrength = Mathf.Min(shakeStrength, .075f);
        shakeDuration = Mathf.Min(shakeDuration, .11f);
    }

    void OnEnable()
    {
        lastPosition = transform.position;
        distanceSinceStep = 0f;
    }

    void Update()
    {
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;
        if (delta.sqrMagnitude < 0.000001f)
            return;

        distanceSinceStep += delta.magnitude;
        if (distanceSinceStep < stepDistance)
            return;
        distanceSinceStep = 0f;

        Camera camera = Camera.main;
        if (camera == null || Vector3.Distance(camera.transform.position, transform.position) > audibleDistance)
            return;

        DemonioAnomaloCameraShake shake = camera.GetComponent<DemonioAnomaloCameraShake>();
        if (shake == null)
            shake = camera.gameObject.AddComponent<DemonioAnomaloCameraShake>();
        shake.Shake(shakeStrength, shakeDuration);
    }
}

[DefaultExecutionOrder(1000)]
public class DemonioAnomaloCameraShake : MonoBehaviour
{
    Vector3 appliedOffset;
    float remaining;
    float duration;
    float strength;

    public void Shake(float newStrength, float newDuration)
    {
        strength = Mathf.Max(strength, newStrength);
        duration = Mathf.Max(0.01f, newDuration);
        remaining = duration;
    }

    void LateUpdate()
    {
        // First undo our previous offset, leaving any camera-follow controller's current pose
        // intact. This prevents the shake from drifting the camera over repeated footsteps.
        if (appliedOffset != Vector3.zero)
        {
            transform.localPosition -= appliedOffset;
            appliedOffset = Vector3.zero;
        }

        if (remaining <= 0f)
        {
            strength = 0f;
            return;
        }

        remaining -= Time.deltaTime;
        float fade = Mathf.Clamp01(remaining / duration);
        Vector2 random = Random.insideUnitCircle * strength * fade;
        appliedOffset = new Vector3(random.x, random.y, 0f);
        transform.localPosition += appliedOffset;
    }

    void OnDisable()
    {
        if (appliedOffset != Vector3.zero)
            transform.localPosition -= appliedOffset;
        appliedOffset = Vector3.zero;
    }
}

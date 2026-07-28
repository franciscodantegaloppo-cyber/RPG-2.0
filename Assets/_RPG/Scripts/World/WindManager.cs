using UnityEngine;

// Drives ambient wind: a slowly drifting direction, a base breeze strength, and a strong gust
// that fires roughly every 30 minutes (jittered so it isn't perfectly clockwork) and can shove
// the player around. Bootstraps itself like PauseManager so it exists in every scene without
// needing to be hand-placed.
public class WindManager : MonoBehaviour
{
    public static WindManager Instance { get; private set; }

    [Header("Gust timing")]
    [SerializeField] float gustIntervalMin = 25f * 60f;   // 25 min
    [SerializeField] float gustIntervalMax = 35f * 60f;   // 35 min - jitter around the ~30 min ask
    [SerializeField] float gustDurationMin = 18f;
    [SerializeField] float gustDurationMax = 35f;

    [Header("Strength")]
    [SerializeField, Range(0f, 1f)] float baseStrength = 0.18f;
    [SerializeField] float gustStrengthMin = 0.75f;
    [SerializeField] float gustStrengthMax = 1f;
    [SerializeField] float strengthSmoothing = 0.6f; // higher = snappier transitions

    [Header("Direction")]
    [SerializeField] float directionDriftDegPerSec = 1.5f;
    [SerializeField] float gustDirectionKickDeg = 50f;

    [Header("Player push")]
    [SerializeField] float pushForce = 5.5f;

    [Header("Altitude")]
    // 350 very gentle 1 m steps: even a sizeable hill adds only a subtle breeze, while the full
    // altitude bonus is deliberately reserved for very high terrain.
    [SerializeField] float altitudeStepMeters = 1f;
    [SerializeField] float strengthPerAltitudeStep = 0.0034285714f;
    [SerializeField] float maxAltitudeStrengthBonus = 1.2f;

    float currentStrength;
    float targetStrength;
    float weatherStrength;
    float altitudeStrengthBonus;
    float encounterStrength;
    float directionAngleDeg;
    float gustTimer;
    float gustRemaining;
    Transform player;
    float altitudeReferenceY;
    bool hasAltitudeReference;

    public float CurrentStrength01 => currentStrength;
    public float EncounterIntensity { get; private set; }
    public bool IsGusting { get; private set; }
    public bool IsExtremeGust { get; private set; }
    public float ExtremeGustRemaining => IsExtremeGust ? gustRemaining : 0f;
    public Vector3 WindDirection { get; private set; } = Vector3.forward;

    // (strength 0..1, isGusting)
    public event System.Action<float, bool> OnWindChanged;
    public event System.Action OnGustStart;
    public event System.Action OnGustEnd;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<WindManager>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("RuntimeWindManager");
        go.AddComponent<WindManager>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        directionAngleDeg = Random.Range(0f, 360f);
        WindDirection = DirectionFromAngle(directionAngleDeg);
        currentStrength = baseStrength;
        weatherStrength = baseStrength;
        targetStrength = baseStrength;
        gustTimer = Random.Range(gustIntervalMin, gustIntervalMax);
    }

    void Update()
    {
        UpdateAltitudeStrength();

        float driftSpeed = directionDriftDegPerSec * (IsGusting ? 2.5f : 1f);
        directionAngleDeg += driftSpeed * Time.deltaTime;
        WindDirection = DirectionFromAngle(directionAngleDeg);

        if (IsGusting)
        {
            gustRemaining -= Time.deltaTime;
            if (gustRemaining <= 0f)
            {
                IsGusting = false;
                IsExtremeGust = false;
                weatherStrength = baseStrength;
                OnGustEnd?.Invoke();
            }
        }
        else
        {
            gustTimer -= Time.deltaTime;
            if (gustTimer <= 0f)
            {
                StartGust();
            }
        }

        targetStrength = weatherStrength + altitudeStrengthBonus + encounterStrength;
        currentStrength = Mathf.MoveTowards(currentStrength, targetStrength, strengthSmoothing * Time.deltaTime);
        OnWindChanged?.Invoke(currentStrength, IsGusting);
    }

    // Encounter effects layer over normal weather rather than replacing it, and are set every
    // frame by nearby bosses so they always fall back to the ordinary breeze on disengage.
    public void SetEncounterWind(float intensity)
    {
        EncounterIntensity = Mathf.Clamp01(intensity);
        encounterStrength = EncounterIntensity * 0.95f;
    }

    void StartGust()
    {
        IsGusting = true;
        gustRemaining = Random.Range(gustDurationMin, gustDurationMax);
        weatherStrength = Random.Range(gustStrengthMin, gustStrengthMax);
        directionAngleDeg += Random.Range(-gustDirectionKickDeg, gustDirectionKickDeg);
        gustTimer = Random.Range(gustIntervalMin, gustIntervalMax);
        OnGustStart?.Invoke();
    }

    public void BeginExtremeGust(float durationSeconds = 300f)
    {
        IsGusting = true;
        IsExtremeGust = true;
        gustRemaining = Mathf.Max(1f, durationSeconds);
        // Twice the maximum normal wind, before altitude/encounter additions.
        weatherStrength = gustStrengthMax * 2f;
        directionAngleDeg += Random.Range(-gustDirectionKickDeg, gustDirectionKickDeg);
        OnGustStart?.Invoke();
    }

    public void PreviewExtremeGust(float normalizedStrength)
    {
        if (IsExtremeGust) return;
        weatherStrength = Mathf.Lerp(baseStrength, gustStrengthMax * 2f,
            Mathf.Clamp01(normalizedStrength));
    }

    public void StopExtremeGust()
    {
        if (!IsExtremeGust) return;
        IsGusting = false;
        IsExtremeGust = false;
        gustRemaining = 0f;
        weatherStrength = baseStrength;
        gustTimer = Random.Range(gustIntervalMin, gustIntervalMax);
        OnGustEnd?.Invoke();
    }

    static Vector3 DirectionFromAngle(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }

    void UpdateAltitudeStrength()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found == null) return;
            player = found.transform;
            hasAltitudeReference = false;
        }

        // The player's initial ground elevation is the zero point. Each full 2 m climbed adds
        // one step, while descending removes those steps again. This tracks real map elevation,
        // not the visual camera height.
        if (!hasAltitudeReference)
        {
            altitudeReferenceY = player.position.y;
            hasAltitudeReference = true;
        }

        float climbed = Mathf.Max(0f, player.position.y - altitudeReferenceY);
        int steps = Mathf.FloorToInt(climbed / Mathf.Max(0.01f, altitudeStepMeters));
        altitudeStrengthBonus = Mathf.Min(steps * strengthPerAltitudeStep, maxAltitudeStrengthBonus);
    }

    // World-space push. At the starting elevation the gentle ambient breeze remains visual;
    // altitude and gust strength are physical, so they can carry an idle player or slow one
    // moving against the wind.
    public Vector3 GetPushVector()
    {
        float physicalStrength = Mathf.Max(0f, currentStrength - baseStrength);
        float shelter = IsExtremeGust && WindShelterDetector.IsPlayerSheltered
            ? .1f
            : 1f;
        return WindDirection * (pushForce * physicalStrength * shelter);
    }
}

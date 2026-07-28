using UnityEngine;

/// <summary>
/// Tracks the player's breath while their head is below a WaterSurfaceVolume.
/// The visual meter is hidden again as soon as the player gets their head out
/// of the water.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerWaterBreathing : MonoBehaviour
{
    [SerializeField, Min(1f)] float breathDuration = 10f;
    [SerializeField, Min(0.1f)] float recoveryPerSecond = 8f;
    [SerializeField, Range(0.01f, 1f)] float drowningDamagePercentPerSecond = 0.10f;

    CharacterController characterController;
    PlayerStats playerStats;
    HUDController hud;
    float oxygen;
    float drowningTickTimer;

    public bool IsHeadSubmerged { get; private set; }
    public bool IsBodyInWater { get; private set; }
    // Player-only action/animation scale. 0.5 means every player action takes
    // twice as long while their body is in the water.
    public float ActionSpeedMultiplier => IsBodyInWater ? 0.5f : 1f;
    public float Oxygen01 => breathDuration > 0f ? oxygen / breathDuration : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AddToPlayer()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && player.GetComponent<PlayerWaterBreathing>() == null)
            player.AddComponent<PlayerWaterBreathing>();
    }

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerStats = GetComponent<PlayerStats>();
        oxygen = breathDuration;
    }

    void Update()
    {
        IsBodyInWater = TryGetWaterSurface(BodyPosition(), 0.18f, out _);
        IsHeadSubmerged = TryGetWaterSurface(HeadPosition(), -0.02f, out _);
        if (IsHeadSubmerged)
        {
            oxygen = Mathf.Max(0f, oxygen - Time.deltaTime);
            ApplyDrowningDamageAfterBreathRunsOut();
        }
        else
        {
            oxygen = Mathf.MoveTowards(oxygen, breathDuration, recoveryPerSecond * Time.deltaTime);
            drowningTickTimer = 0f;
        }

        if (hud == null) hud = FindAnyObjectByType<HUDController>();
        if (hud != null) hud.SetOxygenMeter(IsHeadSubmerged, Oxygen01, oxygen);
    }

    void ApplyDrowningDamageAfterBreathRunsOut()
    {
        if (oxygen > 0f)
        {
            drowningTickTimer = 0f;
            return;
        }

        drowningTickTimer += Time.deltaTime;
        while (drowningTickTimer >= 1f)
        {
            drowningTickTimer -= 1f;
            playerStats?.TakeDrowningDamage(drowningDamagePercentPerSecond);
        }
    }

    Vector3 HeadPosition()
    {
        Vector3 localHead = characterController.center + Vector3.up *
            (characterController.height * 0.5f - Mathf.Max(0.04f, characterController.radius * 0.15f));
        return transform.TransformPoint(localHead);
    }

    Vector3 BodyPosition() => transform.TransformPoint(characterController.center);

    static bool TryGetWaterSurface(Vector3 point, float surfaceTolerance, out WaterSurfaceVolume foundSurface)
    {
        for (int i = 0; i < WaterSurfaceVolume.ActiveSurfaces.Count; i++)
        {
            WaterSurfaceVolume surface = WaterSurfaceVolume.ActiveSurfaces[i];
            if (surface != null && surface.ContainsHorizontal(point) && point.y < surface.SurfaceHeight + surfaceTolerance)
            {
                foundSurface = surface;
                return true;
            }
        }
        foundSurface = null;
        return false;
    }
}

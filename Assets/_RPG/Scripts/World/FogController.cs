using UnityEngine;

// Distance fog used as a cheap draw-distance/perf knob: dragging it down doesn't just hide
// far geometry visually, it also pulls the camera's far clip plane in so Unity stops rendering
// past that distance. Setting persists across sessions via PlayerPrefs. Bootstraps itself like
// PauseManager/WindManager.
public class FogController : MonoBehaviour
{
    public static FogController Instance { get; private set; }

    public const float MinDistance = 50f;
    public const float MaxDistance = 500f;
    const string PrefsKey = "FogDistance";
    const float DefaultDistance = 350f;

    public float CurrentDistance { get; private set; }

    public event System.Action<float> OnFogDistanceChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<FogController>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("RuntimeFogController");
        go.AddComponent<FogController>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        float saved = PlayerPrefs.GetFloat(PrefsKey, DefaultDistance);
        SetDistance(saved);
    }

    void OnEnable()
    {
        // Reapply on every scene load - RenderSettings/camera far clip are per-scene state.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Apply();
    }

    public void SetDistance(float distance)
    {
        CurrentDistance = Mathf.Clamp(distance, MinDistance, MaxDistance);
        PlayerPrefs.SetFloat(PrefsKey, CurrentDistance);
        Apply();
        OnFogDistanceChanged?.Invoke(CurrentDistance);
    }

    void Apply()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = CurrentDistance * 0.35f;
        RenderSettings.fogEndDistance = CurrentDistance;
        if (RenderSettings.fogColor.a <= 0f || RenderSettings.fogColor == Color.clear)
        {
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.82f);
        }

        if (Camera.main != null)
        {
            // A little headroom past the fog's own end distance so nothing pops at the fog edge.
            Camera.main.farClipPlane = CurrentDistance + 40f;
        }
    }
}

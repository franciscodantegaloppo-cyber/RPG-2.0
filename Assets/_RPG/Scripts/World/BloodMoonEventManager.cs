using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Every seventh world night becomes a Blood Moon. The event is entirely runtime-driven,
// so it works in SpawnVillage and in scenes opened directly without modifying scene files.
public sealed class BloodMoonEventManager : MonoBehaviour
{
    public static BloodMoonEventManager Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.eventActive;

    const float PlayerLightRadius = 7f;
    const int VillageBaseCap = 6; // x3 during the event = 18 near the village.
    const int PlayerBaseCap = 5;  // x3 during the event = 15 near the player.

    readonly Dictionary<Light, float> directionalIntensities =
        new Dictionary<Light, float>();
    readonly List<GoblinSpawner> satelliteSpawners =
        new List<GoblinSpawner>();

    TenkokuDayNightCycle cycle;
    Transform player;
    Light playerLight;
    GameObject moonDisc;
    Camera gameplayCamera;
    CameraClearFlags savedClearFlags;
    Color savedBackground;
    bool savedFog;
    FogMode savedFogMode;
    Color savedFogColor;
    float savedFogStart;
    float savedFogEnd;
    float savedFogDensity;
    AmbientMode savedAmbientMode;
    Color savedAmbientLight;
    float savedAmbientIntensity;
    float savedReflectionIntensity;
    bool eventActive;
    float nextLookup;
    float nextSpawnerRefresh;
    int activeWorldDay = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("BloodMoonEventManager")
            .AddComponent<BloodMoonEventManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        cycle = null;
        player = null;
        gameplayCamera = null;
        nextLookup = 0f;
        if (eventActive)
        {
            DestroySatellites();
            RestoreEnvironment();
            eventActive = false;
            activeWorldDay = -1;
        }
    }

    void Update()
    {
        if (Time.unscaledTime >= nextLookup)
        {
            nextLookup = Time.unscaledTime + 1f;
            if (cycle == null)
                cycle = FindAnyObjectByType<TenkokuDayNightCycle>();
            if (player == null)
                player = FindAnyObjectByType<PlayerStats>()?.transform;
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;
        }

        bool shouldBeActive = cycle != null &&
                              (cycle.WorldDay + 1) % 7 == 0 &&
                              NightEnemyEventManager.IsNightNow;
        if (shouldBeActive && !eventActive)
            BeginBloodMoon();
        else if (!shouldBeActive && eventActive)
            EndBloodMoon();

        if (!eventActive) return;
        FollowPlayer();
        if (Time.unscaledTime >= nextSpawnerRefresh)
        {
            nextSpawnerRefresh = Time.unscaledTime + 2f;
            ApplySpawnerBoost(true);
            EnsureSatellites();
        }
    }

    // Run after the normal sky/day-night scripts so the Blood Moon darkness wins visually.
    void LateUpdate()
    {
        if (!eventActive) return;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.002f, 0f, .004f);
        RenderSettings.ambientIntensity = .015f;
        RenderSettings.reflectionIntensity = .035f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 1.5f;
        RenderSettings.fogEndDistance = 22f;
        RenderSettings.fogColor = new Color(.006f, 0f, .008f);

        foreach (Light light in directionalIntensities.Keys)
            if (light != null) light.intensity = .002f;

        if (gameplayCamera != null)
        {
            gameplayCamera.clearFlags = CameraClearFlags.SolidColor;
            gameplayCamera.backgroundColor = new Color(.001f, 0f, .003f);
        }
    }

    void BeginBloodMoon()
    {
        eventActive = true;
        activeWorldDay = cycle.WorldDay;
        CaptureEnvironment();
        CreatePlayerLight();
        CreateMoonDisc();
        ApplySpawnerBoost(true);
        EnsureSatellites();
        nextSpawnerRefresh = 0f;

        if (player != null)
            QuestOverheadThought.Show(player,
                "La Luna de Sangre ha comenzado... los goblins invaden Boat Stain.",
                7f);
        Debug.Log("[BloodMoon] Luna de Sangre activa en el día " +
                  (activeWorldDay + 1) + ".");
    }

    void EndBloodMoon()
    {
        eventActive = false;
        ApplySpawnerBoost(false);
        DestroySatellites();
        if (playerLight != null) Destroy(playerLight.gameObject);
        if (moonDisc != null) Destroy(moonDisc);
        playerLight = null;
        moonDisc = null;
        RestoreEnvironment();
        activeWorldDay = -1;
        Debug.Log("[BloodMoon] La Luna de Sangre terminó.");
    }

    void CaptureEnvironment()
    {
        savedFog = RenderSettings.fog;
        savedFogMode = RenderSettings.fogMode;
        savedFogColor = RenderSettings.fogColor;
        savedFogStart = RenderSettings.fogStartDistance;
        savedFogEnd = RenderSettings.fogEndDistance;
        savedFogDensity = RenderSettings.fogDensity;
        savedAmbientMode = RenderSettings.ambientMode;
        savedAmbientLight = RenderSettings.ambientLight;
        savedAmbientIntensity = RenderSettings.ambientIntensity;
        savedReflectionIntensity = RenderSettings.reflectionIntensity;

        directionalIntensities.Clear();
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include))
            if (light != null && light.type == LightType.Directional)
                directionalIntensities[light] = light.intensity;

        gameplayCamera = Camera.main;
        if (gameplayCamera != null)
        {
            savedClearFlags = gameplayCamera.clearFlags;
            savedBackground = gameplayCamera.backgroundColor;
        }
    }

    void RestoreEnvironment()
    {
        RenderSettings.fog = savedFog;
        RenderSettings.fogMode = savedFogMode;
        RenderSettings.fogColor = savedFogColor;
        RenderSettings.fogStartDistance = savedFogStart;
        RenderSettings.fogEndDistance = savedFogEnd;
        RenderSettings.fogDensity = savedFogDensity;
        RenderSettings.ambientMode = savedAmbientMode;
        RenderSettings.ambientLight = savedAmbientLight;
        RenderSettings.ambientIntensity = savedAmbientIntensity;
        RenderSettings.reflectionIntensity = savedReflectionIntensity;
        foreach (KeyValuePair<Light, float> entry in directionalIntensities)
            if (entry.Key != null) entry.Key.intensity = entry.Value;
        directionalIntensities.Clear();
        if (gameplayCamera != null)
        {
            gameplayCamera.clearFlags = savedClearFlags;
            gameplayCamera.backgroundColor = savedBackground;
        }
    }

    void CreatePlayerLight()
    {
        if (playerLight != null) return;
        GameObject lightObject = new GameObject("BloodMoon_PlayerConcentricLight");
        DontDestroyOnLoad(lightObject);
        playerLight = lightObject.AddComponent<Light>();
        playerLight.type = LightType.Point;
        playerLight.color = new Color(1f, .045f, .08f);
        playerLight.range = PlayerLightRadius;
        playerLight.intensity = 5.2f;
        playerLight.shadows = LightShadows.None;
        playerLight.renderMode = LightRenderMode.ForcePixel;
        FollowPlayer();
    }

    void CreateMoonDisc()
    {
        if (moonDisc != null) return;
        moonDisc = GameObject.CreatePrimitive(PrimitiveType.Quad);
        moonDisc.name = "BloodMoon_RedDisc";
        Destroy(moonDisc.GetComponent<Collider>());
        Renderer renderer = moonDisc.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("Unlit/Color");
        if (shader != null)
        {
            Material material = new Material(shader)
            {
                name = "BloodMoon_RedDisc_Runtime",
                color = new Color(.72f, .005f, .015f, 1f)
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", material.color);
            renderer.sharedMaterial = material;
        }
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        moonDisc.transform.localScale = Vector3.one * 11f;
        FollowPlayer();
    }

    void FollowPlayer()
    {
        if (playerLight != null && player != null)
            playerLight.transform.position = player.position + Vector3.up * 1.15f;

        if (moonDisc != null && gameplayCamera != null)
        {
            Transform cameraTransform = gameplayCamera.transform;
            moonDisc.transform.position = cameraTransform.position +
                cameraTransform.forward * 62f + Vector3.up * 24f;
            moonDisc.transform.rotation = Quaternion.LookRotation(
                cameraTransform.position - moonDisc.transform.position,
                Vector3.up);
        }
    }

    void ApplySpawnerBoost(bool enabled)
    {
        foreach (GoblinSpawner spawner in
                 FindObjectsByType<GoblinSpawner>(FindObjectsInactive.Include))
            if (spawner != null)
                spawner.SetBloodMoonSpawnBoost(enabled);
    }

    void EnsureSatellites()
    {
        satelliteSpawners.RemoveAll(spawner => spawner == null);
        if (satelliteSpawners.Count >= 2) return;

        GoblinSpawner source = null;
        foreach (GoblinSpawner candidate in
                 FindObjectsByType<GoblinSpawner>(FindObjectsInactive.Include))
        {
            if (candidate == null ||
                candidate.name.StartsWith("BloodMoon_", System.StringComparison.Ordinal))
                continue;
            source = candidate;
            break;
        }
        if (source == null) return;

        if (satelliteSpawners.Count == 0)
        {
            Transform spawn = FindVillageSpawn();
            Vector3 centre = spawn != null ? spawn.position :
                             player != null ? player.position : source.transform.position;
            GoblinSpawner village = source.CreateBloodMoonSatellite(
                "BloodMoon_VillageGoblinSpawner", centre, null, 18f,
                VillageBaseCap);
            if (village != null) satelliteSpawners.Add(village);
        }
        if (satelliteSpawners.Count == 1 && player != null)
        {
            GoblinSpawner hunter = source.CreateBloodMoonSatellite(
                "BloodMoon_PlayerHunterSpawner", player.position, player, 15f,
                PlayerBaseCap);
            if (hunter != null) satelliteSpawners.Add(hunter);
        }
    }

    static Transform FindVillageSpawn()
    {
        GameObject spawn = GameObject.Find("SpawnPoint") ??
                           GameObject.Find("PlayerSpawn") ??
                           GameObject.Find("Spawn");
        return spawn != null ? spawn.transform : null;
    }

    void DestroySatellites()
    {
        foreach (GoblinSpawner spawner in satelliteSpawners)
            if (spawner != null) Destroy(spawner.gameObject);
        satelliteSpawners.Clear();
    }
}

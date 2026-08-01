using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// One complete world cycle lasts 50 real minutes: 30 minutes of daylight and 20 of night.
// Tenkoku versions use slightly different public member names, so the bridge discovers its
// module at runtime instead of hard-linking to a particular package version.
public class TenkokuDayNightCycle : MonoBehaviour
{
    [SerializeField] float dayDurationMinutes = 30f;
    [SerializeField] float nightDurationMinutes = 20f;
    [SerializeField] float startHour = 8f;

    MonoBehaviour tenkoku;
    FieldInfo timeField;
    PropertyInfo timeProperty;
    Light sun;
    float elapsed;
    int worldDay;
    URPDynamicSkyWeather urpSky;
    PlayerStats playerStats;
    float nextPlayerLookup;
    int nightOverrideCount;
    float nightOverrideHour = 0f;
    float nextFastForwardStep;

    public float CurrentHour => GetHour(elapsed);
    public int WorldDay => worldDay;
    public bool IsEquinox => worldDay % 180 == 0;
    public float SeasonalDeclination => 23.44f * Mathf.Sin(worldDay / 360f * Mathf.PI * 2f);
    public bool NightOverrideActive => nightOverrideCount > 0;
    public bool GodModeTimeControlEnabled =>
        playerStats != null && playerStats.GodModeEnabled;

    void Awake()
    {
        elapsed = HourToElapsed(startHour);
        FindTenkoku();
        FindSun();
        urpSky = GetComponent<URPDynamicSkyWeather>() ?? gameObject.AddComponent<URPDynamicSkyWeather>();
    }

    void Update()
    {
        if (tenkoku == null) FindTenkoku();
        // Scene-wide lookups allocate/work through every object. The player only needs to be
        // recovered after a scene change or respawn, not on every rendered frame.
        if (playerStats == null && Time.unscaledTime >= nextPlayerLookup)
        {
            nextPlayerLookup = Time.unscaledTime + 1f;
            playerStats = FindAnyObjectByType<PlayerStats>();
        }
        bool godFastForwarded = HandleGodModeFastForward();
        if (NightOverrideActive)
            elapsed = HourToElapsed(nightOverrideHour);
        else if (!godFastForwarded)
            elapsed += Time.deltaTime;
        if (elapsed >= TotalDuration)
        {
            elapsed -= TotalDuration;
            worldDay++;
        }
        float hour = GetHour(elapsed);
        ApplyTenkoku(hour);
        ApplyFallbackLighting(hour);
        urpSky?.Apply(CurrentHour, worldDay, IsEquinox);
    }

    bool HandleGodModeFastForward()
    {
        Keyboard keyboard = Keyboard.current;
        bool allowed = playerStats != null && playerStats.GodModeEnabled &&
                       keyboard != null &&
                       !RuntimeChatConsole.IsTyping;
        if (!allowed || !keyboard.tKey.isPressed)
        {
            nextFastForwardStep = 0f;
            return false;
        }

        // One press advances exactly 30 game minutes. Holding T repeats that step once per
        // real second, instead of advancing an almost invisible fraction on a quick press.
        bool firstStep = keyboard.tKey.wasPressedThisFrame;
        bool repeatedStep = !firstStep &&
                            Time.unscaledTime >= nextFastForwardStep;
        if (!firstStep && !repeatedStep)
            return false;

        // God-mode time controls deliberately release quest/boss night locks so the
        // requested time remains visible instead of being overwritten next frame.
        nightOverrideCount = 0;
        AdvanceGameMinutes(30f);
        nextFastForwardStep = Time.unscaledTime + 1f;
        return true;
    }

    public void AdvanceGameMinutes(float minutes)
    {
        float targetHour = CurrentHour + minutes / 60f;
        while (targetHour >= 24f) { targetHour -= 24f; worldDay++; }
        elapsed = HourToElapsed(targetHour);
    }

    public void SetCurrentHour(float hour)
    {
        elapsed = HourToElapsed(hour);
        float currentHour = GetHour(elapsed);
        ApplyTenkoku(currentHour);
        ApplyFallbackLighting(currentHour);
        urpSky?.Apply(currentHour, worldDay, IsEquinox);
    }

    public void SetGodModeHour(float hour)
    {
        nightOverrideCount = 0;
        nextFastForwardStep = 0f;
        SetCurrentHour(hour);
    }

    public void BeginNightOverride(float hour = 0f)
    {
        nightOverrideCount++;
        nightOverrideHour = Mathf.Repeat(hour, 24f);
        SetCurrentHour(nightOverrideHour);
    }

    public void EndNightOverride()
    {
        nightOverrideCount = Mathf.Max(0, nightOverrideCount - 1);
    }

    float DaySeconds => Mathf.Max(1f, dayDurationMinutes * 60f);
    float NightSeconds => Mathf.Max(1f, nightDurationMinutes * 60f);
    float TotalDuration => DaySeconds + NightSeconds;

    // Day runs 06:00-18:00 in 30 minutes; night runs 18:00-06:00 in 20 minutes.
    float GetHour(float seconds)
    {
        if (seconds < DaySeconds) return Mathf.Lerp(6f, 18f, seconds / DaySeconds);
        return Mathf.Lerp(18f, 30f, (seconds - DaySeconds) / NightSeconds) % 24f;
    }

    float HourToElapsed(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        return hour >= 6f && hour < 18f
            ? Mathf.InverseLerp(6f, 18f, hour) * DaySeconds
            : DaySeconds + Mathf.InverseLerp(18f, 30f, hour < 6f ? hour + 24f : hour) * NightSeconds;
    }

    void FindTenkoku()
    {
        foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (component == null || !component.GetType().Name.Contains("Tenkoku", StringComparison.OrdinalIgnoreCase)) continue;
            Type type = component.GetType();
            timeField = FindMember<FieldInfo>(type, "currentTime", "timeOfDay", "time", "currentTimeOfDay");
            timeProperty = FindMember<PropertyInfo>(type, "currentTime", "timeOfDay", "time", "currentTimeOfDay");
            if (timeField == null && timeProperty == null) continue;
            tenkoku = component;
            // Tenkoku's legacy renderer edits RenderSettings.skybox (including its exposure)
            // every frame and is not compatible with URP. Keep its astronomical/time data
            // available to this bridge, but let the URP Fantasy presentation be the sole owner
            // of the visible sky.
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null &&
                component.enabled)
                component.enabled = false;
            SetBoolMember(type, component, false, "autoTime", "useAutoTime", "enableAutoTime");
            Debug.Log("[DayNight] Conectado a " + type.Name + ". Día: 30 min; noche: 20 min.");
            return;
        }
    }

    static T FindMember<T>(Type type, params string[] names) where T : MemberInfo
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (string name in names)
        {
            MemberInfo member = typeof(T) == typeof(FieldInfo) ? type.GetField(name, flags) : type.GetProperty(name, flags);
            if (member is T result) return result;
        }
        return null;
    }

    static void SetBoolMember(Type type, object target, bool value, params string[] names)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (string name in names)
        {
            FieldInfo field = type.GetField(name, flags);
            if (field?.FieldType == typeof(bool)) { field.SetValue(target, value); return; }
            PropertyInfo property = type.GetProperty(name, flags);
            if (property?.PropertyType == typeof(bool) && property.CanWrite) { property.SetValue(target, value); return; }
        }
    }

    void ApplyTenkoku(float hour)
    {
        if (tenkoku == null) return;
        try
        {
            if (timeField != null)
            {
                if (timeField.FieldType == typeof(float)) timeField.SetValue(tenkoku, hour);
                else if (timeField.FieldType == typeof(double)) timeField.SetValue(tenkoku, (double)hour);
            }
            else if (timeProperty != null && timeProperty.CanWrite)
            {
                if (timeProperty.PropertyType == typeof(float)) timeProperty.SetValue(tenkoku, hour);
                else if (timeProperty.PropertyType == typeof(double)) timeProperty.SetValue(tenkoku, (double)hour);
            }
        }
        catch { tenkoku = null; }
    }

    void FindSun()
    {
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include))
            if (light.type == LightType.Directional && (sun == null || light.intensity > sun.intensity)) sun = light;
    }

    void ApplyFallbackLighting(float hour)
    {
        if (sun == null) FindSun();
        float daylight = Mathf.Clamp01(Mathf.Sin((hour - 6f) / 12f * Mathf.PI));
        RenderSettings.ambientLight = Color.Lerp(new Color(.012f, .008f, .03f), new Color(.56f, .61f, .70f), daylight);
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler((hour - 6f) / 24f * 360f - SeasonalDeclination, 160f, 0f);
            sun.intensity = Mathf.Lerp(.04f, 1f, daylight);
        }
    }
}

// URP-native replacement for the Tenkoku presentation layer. It gives the project a continuously
// changing sky, cloud veil, seasonal/equinox colour variation and an occasional equinox rainbow
// without changing the render pipeline or existing materials.
public class URPDynamicSkyWeather : MonoBehaviour
{
    Material skybox;
    Material fantasyBlendSkybox;
    FantasySkyboxDayNightLibrary fantasySkybox;
    Camera cameraMain;
    ParticleSystem clouds;
    ParticleSystemRenderer cloudRenderer;

    void Awake()
    {
        FindFantasySkybox();
        // The Fantasy Skybox panoramas contain their own realistic cloud layers. Do not add
        // the old particle-cloud veil over them, as it makes the image look artificial.
        if (fantasySkybox != null && fantasySkybox.HasDaySky) return;

        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader != null)
        {
            skybox = new Material(shader) { name = "URP_DynamicSky_Runtime" };
            RenderSettings.skybox = skybox;
        }
        BuildClouds();
    }

    public void Apply(float hour, int day, bool equinox)
    {
        float daylight = Mathf.Clamp01(Mathf.Sin((hour - 6f) / 12f * Mathf.PI));
        float dawn = Mathf.Clamp01(1f - Mathf.Abs(hour - 6f) / 1.5f);
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(hour - 18f) / 1.5f);
        Color night = new Color(.012f, .008f, .055f);
        Color dayColor = new Color(.34f, .58f, .92f);
        Color horizon = Color.Lerp(night, dayColor, daylight);
        horizon = Color.Lerp(horizon, new Color(1f, .30f, .10f), Mathf.Max(dawn, dusk) * .6f);

        if (fantasySkybox == null) FindFantasySkybox();
        if (fantasySkybox != null && fantasySkybox.HasDaySky)
        {
            Material selected = ApplyFantasySky(hour, daylight);
            EnsureMainCameraSkybox(selected);
            RenderSettings.fogColor = Color.Lerp(new Color(.018f, .008f, .05f), new Color(.55f, .64f, .78f), daylight);
            return;
        }

        if (skybox != null)
        {
            skybox.SetColor("_SkyTint", horizon);
            skybox.SetColor("_GroundColor", Color.Lerp(new Color(.004f, .003f, .012f), new Color(.26f, .32f, .36f), daylight));
            skybox.SetFloat("_Exposure", Mathf.Lerp(.22f, 1.28f, daylight));
            skybox.SetFloat("_AtmosphereThickness", Mathf.Lerp(.26f, .72f, daylight));
        }
        RenderSettings.fogColor = Color.Lerp(new Color(.018f, .008f, .05f), new Color(.55f, .64f, .78f), daylight);

        UpdateClouds(daylight, horizon);
    }

    void FindFantasySkybox()
    {
        fantasySkybox = FindAnyObjectByType<FantasySkyboxDayNightLibrary>();
        if (fantasySkybox != null) return;

        // The setup script mirrors these material references into Resources. This makes the
        // Fantasy sky work even when the scene was opened directly in Play Mode before it was
        // saved with its library object.
        Material day = Resources.Load<Material>("FantasySkybox/FS017_Day");
        if (day == null) return;
        GameObject libraryObject = new GameObject("FantasySkyboxDayNight_Runtime");
        DontDestroyOnLoad(libraryObject);
        fantasySkybox = libraryObject.AddComponent<FantasySkyboxDayNightLibrary>();
        fantasySkybox.day = day;
        fantasySkybox.sunrise = Resources.Load<Material>("FantasySkybox/FS017_Sunrise");
        fantasySkybox.sunset = Resources.Load<Material>("FantasySkybox/FS017_Sunset");
        fantasySkybox.night = Resources.Load<Material>("FantasySkybox/FS017_Night");
        fantasySkybox.moonlessNight = Resources.Load<Material>("FantasySkybox/FS017_Night_Moonless");
    }

    void EnsureMainCameraSkybox(Material selected)
    {
        if (cameraMain == null) cameraMain = Camera.main;
        if (cameraMain == null) return;

        // The old distant-mountain setup explicitly changed the gameplay camera to SolidColor.
        // That hides every RenderSettings skybox, no matter which material is assigned.
        cameraMain.clearFlags = CameraClearFlags.Skybox;
        Skybox cameraOverride = cameraMain.GetComponent<Skybox>();
        if (cameraOverride != null) cameraOverride.material = selected;
    }

    Material ApplyFantasySky(float hour, float daylight)
    {
        if (fantasyBlendSkybox == null)
        {
            Shader blendShader = Shader.Find("RPG/Fantasy Panorama Blend");
            if (blendShader != null)
                fantasyBlendSkybox = new Material(blendShader) { name = "FantasySkybox_AnimatedBlend_Runtime" };
        }

        GetFantasyTransition(hour, out Material from, out Material to, out float blend);
        if (from == null) from = fantasySkybox.day;
        if (to == null) to = from;
        blend = Mathf.SmoothStep(0f, 1f, blend);

        if (fantasyBlendSkybox == null || !from.HasProperty("_MainTex") || !to.HasProperty("_MainTex"))
        {
            Material fallback = blend < .5f ? from : to;
            if (fallback != null && RenderSettings.skybox != fallback) { RenderSettings.skybox = fallback; DynamicGI.UpdateEnvironment(); }
            return fallback;
        }

        fantasyBlendSkybox.SetTexture("_MainTex", from.GetTexture("_MainTex"));
        fantasyBlendSkybox.SetTexture("_BlendTex", to.GetTexture("_MainTex"));
        fantasyBlendSkybox.SetFloat("_Blend", blend);
        fantasyBlendSkybox.SetFloat("_Exposure", Mathf.Lerp(.38f, 1.15f, daylight));
        // A very slow rotation gives the cloud formations perceptible movement without making
        // the horizon look like it is spinning. One full revolution takes roughly three hours.
        fantasyBlendSkybox.SetFloat("_Rotation", Mathf.Repeat(Time.time * .033f, 360f));
        if (RenderSettings.skybox != fantasyBlendSkybox)
        {
            RenderSettings.skybox = fantasyBlendSkybox;
            DynamicGI.UpdateEnvironment();
        }
        return fantasyBlendSkybox;
    }

    void GetFantasyTransition(float hour, out Material from, out Material to, out float blend)
    {
        // Wide transition windows make the 50-minute world cycle feel gradual: each hand-off
        // remains on screen for several real-time minutes rather than snapping at one hour.
        if (hour >= 5f && hour < 8f) { from = fantasySkybox.night; to = fantasySkybox.sunrise; blend = (hour - 5f) / 3f; return; }
        if (hour >= 8f && hour < 10f) { from = fantasySkybox.sunrise; to = fantasySkybox.day; blend = (hour - 8f) / 2f; return; }
        if (hour >= 16f && hour < 19f) { from = fantasySkybox.day; to = fantasySkybox.sunset; blend = (hour - 16f) / 3f; return; }
        if (hour >= 19f && hour < 21f) { from = fantasySkybox.sunset; to = fantasySkybox.night; blend = (hour - 19f) / 2f; return; }
        from = to = hour >= 10f && hour < 16f ? fantasySkybox.day : fantasySkybox.night;
        blend = 0f;
    }

    void BuildClouds()
    {
        GameObject cloudGo = new GameObject("URP_DynamicCloudLayers");
        cloudGo.transform.SetParent(transform, false);
        clouds = cloudGo.AddComponent<ParticleSystem>();
        var main = clouds.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 110;
        main.startLifetime = new ParticleSystem.MinMaxCurve(45f, 80f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.08f, .25f);
        main.startSize = new ParticleSystem.MinMaxCurve(13f, 30f);
        main.startColor = new Color(.86f, .90f, 1f, .22f);
        var emission = clouds.emission;
        emission.rateOverTime = 3.2f;
        var shape = clouds.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(130f, 7f, 130f);
        cloudRenderer = cloudGo.GetComponent<ParticleSystemRenderer>();
        cloudRenderer.material = WindVisualEffect.CreateWindStreakMaterial();
        cloudRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        cloudRenderer.alignment = ParticleSystemRenderSpace.View;
        clouds.Play();
    }

    void UpdateClouds(float daylight, Color skyColor)
    {
        if (cameraMain == null) cameraMain = Camera.main;
        if (cameraMain == null || clouds == null) return;
        clouds.transform.position = cameraMain.transform.position + Vector3.up * 30f;
        var main = clouds.main;
        main.startColor = Color.Lerp(new Color(.18f, .20f, .30f, .12f), new Color(.95f, .97f, 1f, .28f), daylight);
    }
}

public static class TenkokuDayNightCycleBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateCycle()
    {
        EnsureForScene(SceneManager.GetActiveScene());
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForScene(scene);
    }

    public static TenkokuDayNightCycle EnsureForScene(Scene scene)
    {
        TenkokuDayNightCycle existing =
            UnityEngine.Object.FindAnyObjectByType<TenkokuDayNightCycle>();
        if (existing != null)
            return existing;

        // The character-creation scene does not drive world lighting.
        if (!scene.IsValid() || scene.name == "NewGame")
            return null;

        GameObject root = new GameObject("RuntimeDayNightCycle");
        TenkokuDayNightCycle cycle = root.AddComponent<TenkokuDayNightCycle>();
        UnityEngine.Object.DontDestroyOnLoad(root);
        Debug.Log("[DayNight] Ciclo persistente restaurado para " + scene.name + ".");
        return cycle;
    }
}

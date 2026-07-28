using UnityEngine;

// Valheim-style wind streaks: soft stretched-billboard particles drifting past the player in
// the current wind direction, thickening up during a gust. Bootstraps itself and re-finds the
// player after scene loads (village <-> dungeon), same pattern as HUDController.
public class WindVisualEffect : MonoBehaviour
{
    ParticleSystem ps;
    ParticleSystem dustPs;
    ParticleSystem dirtPs;
    ParticleSystem grassPs;
    ParticleSystem bubblePs;
    ParticleSystem.EmissionModule emission;
    ParticleSystem.EmissionModule dustEmission;
    ParticleSystem.EmissionModule dirtEmission;
    ParticleSystem.EmissionModule grassEmission;
    ParticleSystem.EmissionModule bubbleEmission;
    ParticleSystem.VelocityOverLifetimeModule velocityModule;
    ParticleSystem.VelocityOverLifetimeModule dustVelocityModule;
    ParticleSystem.VelocityOverLifetimeModule dirtVelocityModule;
    ParticleSystem.VelocityOverLifetimeModule grassVelocityModule;
    Transform target;
    PlayerWaterBreathing waterBreathing;
    bool wasSubmerged;
    float nextVisualUpdate;

    const float CalmEmissionRate = 6f;
    const float GustEmissionRate = 55f;
    const float CalmDustEmissionRate = 14f;
    const float GustDustEmissionRate = 120f;
    const float BaseSpeed = 6f;
    const float DustBaseSpeed = 3.4f;
    const float GustSpeedMultiplier = 2.2f;

    // Concentrated enough to be visibly readable around the player, without becoming a solid
    // dust cloud. The previous 2/s across a 30 m box was effectively invisible in play.
    const float CalmDirtEmissionRate = 22f;
    const float GustDirtEmissionRate = 65f;
    const float CalmGrassEmissionRate = 9f;
    const float GustGrassEmissionRate = 40f;
    const float DirtBaseSpeed = 2.6f;
    const float GrassBaseSpeed = 3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<WindVisualEffect>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("RuntimeWindVisualEffect");
        go.AddComponent<WindVisualEffect>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        BuildParticleSystem();
    }

    void Update()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (target != null)
        {
            transform.position = target.position + Vector3.up * 1.5f;
            if (waterBreathing == null) waterBreathing = target.GetComponent<PlayerWaterBreathing>();
        }

        bool submerged = waterBreathing != null && waterBreathing.IsHeadSubmerged;
        if (submerged != wasSubmerged)
        {
            wasSubmerged = submerged;
            SetWindVisible(!submerged);
            if (submerged) bubblePs.Play(true);
            else bubblePs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (submerged)
        {
            bubbleEmission.rateOverTime = 16f;
            return;
        }

        if (WindManager.Instance == null) return;

        // Particle module writes are comparatively expensive. Updating them at 20 Hz keeps
        // wind movement visually continuous while greatly reducing CPU work.
        if (Time.time < nextVisualUpdate) return;
        nextVisualUpdate = Time.time + 0.05f;

        float encounter = WindManager.Instance.EncounterIntensity;
        float strength = Mathf.Clamp01(WindManager.Instance.CurrentStrength01);
        bool gusting = WindManager.Instance.IsGusting;
        float extremeDensity = WindManager.Instance.IsExtremeGust ? 4f : 1f;

        float encounterDensity = 1f + encounter * 2.2f;
        emission.rateOverTime = Mathf.Lerp(CalmEmissionRate, GustEmissionRate, strength) * encounterDensity * extremeDensity;
        dustEmission.rateOverTime = Mathf.Lerp(CalmDustEmissionRate, GustDustEmissionRate, strength) * encounterDensity * extremeDensity;

        Vector3 dir = WindManager.Instance.WindDirection;
        // At a low wind force the particles visibly drift; as force rises their speed scales
        // continuously rather than keeping the old full base speed at every intensity.
        float speed = BaseSpeed * (0.25f + strength * (GustSpeedMultiplier - 0.25f));
        Vector3 vel = dir * speed;
        velocityModule.x = vel.x;
        velocityModule.y = 0f;
        velocityModule.z = vel.z;

        float dustSpeed = DustBaseSpeed * (0.2f + strength * 2.4f);
        Vector3 dustVel = dir * dustSpeed;
        // Unity 6 requires X/Y/Z in Velocity over Lifetime to use exactly the same
        // MinMaxCurve mode. Keep all three as Constant; vertical variation comes from the
        // particles' spawn distribution instead of mixing TwoConstants with Constant axes.
        dustVelocityModule.x = dustVel.x;
        dustVelocityModule.y = 0.02f;
        dustVelocityModule.z = dustVel.z;

        dirtEmission.rateOverTime = Mathf.Lerp(CalmDirtEmissionRate, GustDirtEmissionRate, strength) * encounterDensity * extremeDensity;
        grassEmission.rateOverTime = Mathf.Lerp(CalmGrassEmissionRate, GustGrassEmissionRate, strength) * encounterDensity * extremeDensity;

        float dirtSpeed = DirtBaseSpeed * (0.2f + strength * 2.6f);
        Vector3 dirtVel = dir * dirtSpeed;
        dirtVelocityModule.x = dirtVel.x;
        dirtVelocityModule.y = 0.1f;
        dirtVelocityModule.z = dirtVel.z;

        float grassSpeed = GrassBaseSpeed * (0.2f + strength * 2.6f);
        Vector3 grassVel = dir * grassSpeed;
        grassVelocityModule.x = grassVel.x;
        grassVelocityModule.y = 0.125f;
        grassVelocityModule.z = grassVel.z;

        ParticleSystem.MainModule main = ps.main;
        main.startColor = Color.Lerp(new Color(1f, 1f, 1f, gusting ? 0.5f : 0.22f), new Color(1f, 0.06f, 0.01f, 0.8f), encounter);

        ParticleSystem.MainModule dustMain = dustPs.main;
        dustMain.startColor = Color.Lerp(new Color(0.92f, 0.97f, 1f, gusting ? 0.22f : 0.11f), new Color(1f, 0.05f, 0.01f, 0.55f), encounter);

        ParticleSystem.MainModule dirtMain = dirtPs.main;
        dirtMain.startColor = Color.Lerp(new Color(0.55f, 0.43f, 0.28f, 0.32f), new Color(0.9f, 0.025f, 0.005f, 0.7f), encounter);
        ParticleSystem.MainModule grassMain = grassPs.main;
        grassMain.startColor = Color.Lerp(new Color(0.4f, 0.62f, 0.24f, 0.28f), new Color(1f, 0.035f, 0.005f, 0.65f), encounter);
    }

    void BuildParticleSystem()
    {
        ps = gameObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 2.5f;
        main.startSpeed = 0f; // driven entirely by VelocityOverLifetime
        // Thin base width - the Stretch render mode below turns this into a long thin line
        // rather than a fat rectangle, closer to Valheim's wisp-like wind streaks.
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor = new Color(1f, 1f, 1f, 0.22f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 260;

        emission = ps.emission;
        emission.rateOverTime = CalmEmissionRate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(35f, 8f, 35f);

        velocityModule = ps.velocityOverLifetime;
        velocityModule.enabled = true;
        velocityModule.space = ParticleSystemSimulationSpace.World;
        velocityModule.x = BaseSpeed;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        // Stretch length = startSize * lengthScale + speed * velocityScale. With speed doing
        // almost all the work here and the base size now tiny, the result is a long thin
        // wisp instead of a short fat streak.
        psRenderer.velocityScale = 0.55f;
        psRenderer.lengthScale = 1f;
        psRenderer.material = BuildStreakMaterial();
        psRenderer.alignment = ParticleSystemRenderSpace.World;

        BuildDustParticleSystem();
        BuildDirtParticleSystem();
        BuildGrassParticleSystem();
        BuildBubbleParticleSystem();
    }

    void SetWindVisible(bool visible)
    {
        ParticleSystem[] systems = { ps, dustPs, dirtPs, grassPs };
        foreach (ParticleSystem system in systems)
        {
            if (visible) system.Play(true);
            else system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void BuildBubbleParticleSystem()
    {
        GameObject bubbles = new GameObject("UnderwaterBubbles");
        bubbles.transform.SetParent(transform, false);
        bubblePs = bubbles.AddComponent<ParticleSystem>();
        var main = bubblePs.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
        main.startColor = new Color(0.82f, 0.96f, 1f, 0.58f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        bubbleEmission = bubblePs.emission;
        bubbleEmission.rateOverTime = 0f;
        var shape = bubblePs.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.42f;
        var velocity = bubblePs.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        // X/Y/Z default to Constant mode, so Y must also remain Constant in Unity 6.
        velocity.x = 0f;
        velocity.y = 0.6f;
        velocity.z = 0f;
        ParticleSystemRenderer renderer = bubblePs.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = BuildDustMaterial();
        renderer.alignment = ParticleSystemRenderSpace.View;
    }

    void BuildDustParticleSystem()
    {
        GameObject dust = new GameObject("TinyTransparentWindParticles");
        dust.transform.SetParent(transform, false);

        dustPs = dust.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = dustPs.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
        main.startColor = new Color(0.92f, 0.97f, 1f, 0.11f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 450;

        dustEmission = dustPs.emission;
        dustEmission.rateOverTime = CalmDustEmissionRate;

        ParticleSystem.ShapeModule shape = dustPs.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(32f, 6f, 32f);

        dustVelocityModule = dustPs.velocityOverLifetime;
        dustVelocityModule.enabled = true;
        dustVelocityModule.space = ParticleSystemSimulationSpace.World;
        dustVelocityModule.x = DustBaseSpeed;
        dustVelocityModule.y = 0.02f;
        dustVelocityModule.z = 0f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = dustPs.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.88f, 0.95f, 1f), 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.55f, 0.18f),
                new GradientAlphaKey(0.35f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = dustPs.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = BuildDustMaterial();
        renderer.alignment = ParticleSystemRenderSpace.View;
    }

    // Square brown flecks simulating dirt - previously used the same soft round falloff texture
    // as the dust speck above, which (combined with a low peak alpha meant to look "leve") read
    // as functionally invisible rather than a light dusting, per the user's report that only the
    // main wind streak was ever actually visible. Sharp-edged square texture + higher contrast
    // peak alpha + slightly bigger size fixes visibility while keeping the emission rate itself
    // low (still "leve" - a light amount, just each one clearly readable when it appears).
    void BuildDirtParticleSystem()
    {
        GameObject dirt = new GameObject("WindDirtParticles");
        dirt.transform.SetParent(transform, false);

        dirtPs = dirt.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = dirtPs.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startColor = new Color(0.42f, 0.31f, 0.18f, 0.82f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 220;

        dirtEmission = dirtPs.emission;
        dirtEmission.rateOverTime = CalmDirtEmissionRate;

        ParticleSystem.ShapeModule shape = dirtPs.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        // Keep dirt in the visible air space around the player instead of distributing it across
        // a full 30 m field where individual flecks are impossible to notice.
        shape.scale = new Vector3(14f, 3f, 14f);

        dirtVelocityModule = dirtPs.velocityOverLifetime;
        dirtVelocityModule.enabled = true;
        dirtVelocityModule.space = ParticleSystemSimulationSpace.World;
        dirtVelocityModule.x = DirtBaseSpeed;
        dirtVelocityModule.y = 0.1f;
        dirtVelocityModule.z = 0f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = dirtPs.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.5f, 0.36f, 0.2f), 0f),
                new GradientColorKey(new Color(0.38f, 0.27f, 0.15f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.7f, 0.16f),
                new GradientAlphaKey(0.52f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = dirtPs.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = BuildSquareDirtMaterial();
        renderer.alignment = ParticleSystemRenderSpace.View;
    }

    // Green blades, emitted noticeably more often than dirt (~4.5x, per the ask) - rendered as
    // short stretched streaks rather than round dots so they read as flying grass, not dust.
    void BuildGrassParticleSystem()
    {
        GameObject grass = new GameObject("WindGrassParticles");
        grass.transform.SetParent(transform, false);

        grassPs = grass.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = grassPs.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startColor = new Color(0.32f, 0.58f, 0.2f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 260;

        grassEmission = grassPs.emission;
        grassEmission.rateOverTime = CalmGrassEmissionRate;

        ParticleSystem.ShapeModule shape = grassPs.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 4f, 30f);

        grassVelocityModule = grassPs.velocityOverLifetime;
        grassVelocityModule.enabled = true;
        grassVelocityModule.space = ParticleSystemSimulationSpace.World;
        grassVelocityModule.x = GrassBaseSpeed;
        grassVelocityModule.y = 0.125f;
        grassVelocityModule.z = 0f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = grassPs.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.38f, 0.65f, 0.22f), 0f),
                new GradientColorKey(new Color(0.24f, 0.48f, 0.16f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.5f, 0.2f),
                new GradientAlphaKey(0.35f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = grassPs.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.2f;
        renderer.lengthScale = 2.5f;
        renderer.material = BuildDustMaterial();
        renderer.alignment = ParticleSystemRenderSpace.World;
    }

    static Material BuildStreakMaterial()
    {
        // Sprites/Default is alpha-blended (Blend SrcAlpha OneMinusSrcAlpha, ZWrite Off) out of
        // the box, so vertex/texture alpha just works. URP's Lit/Unlit shaders need their whole
        // Blend/ZWrite render state reconfigured to go transparent - poking a couple of
        // properties at runtime (what this used to do) isn't enough and rendered opaque.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

        Material mat = new Material(shader);
        mat.name = "WindStreak_Runtime";
        Texture2D tex = BuildSoftDotTexture();
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        return mat;
    }

    // Reused by encounter effects so their vortex keeps the same soft, transparent wind-edge
    // material instead of falling back to opaque default particle squares.
    public static Material CreateWindStreakMaterial()
    {
        return BuildStreakMaterial();
    }

    static Texture2D BuildSoftDotTexture()
    {
        int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "WindStreak_SoftDot";
        Vector2 center = new Vector2(size / 2f, size / 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    static Material BuildSquareDirtMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

        Material mat = new Material(shader);
        mat.name = "WindDirtSquare_Runtime";
        Texture2D tex = BuildSquareTexture();
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        return mat;
    }

    // Soft rounded dirt fleck. It keeps a compact irregular-looking particle silhouette without
    // the visible four square corners that made every particle read as a tiny UI rectangle.
    static Texture2D BuildSquareTexture()
    {
        int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "WindDirt_Square";
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) /
                    (size * 0.5f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.15f) * 0.9f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    static Material BuildDustMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

        Material mat = new Material(shader);
        mat.name = "WindTinyParticles_Runtime";
        Texture2D tex = BuildFineSpeckTexture();
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        return mat;
    }

    static Texture2D BuildFineSpeckTexture()
    {
        int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "WindTinyParticle_SoftSpeck";
        Vector2 center = new Vector2(size / 2f, size / 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.Pow(a, 3f) * 0.65f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }
}

using UnityEngine;

// A compact wind-like vortex that permanently surrounds the Insectoid Crab Boss.
// Particle steering is done directly instead of with VelocityOverLifetime curves,
// which avoids mixed curve-mode warnings and produces a real inward spiral.
public sealed class InsectoidCrabDarkAura : MonoBehaviour
{
    const int MaxParticles = 190;
    ParticleSystem particles;
    ParticleSystem.Particle[] buffer;
    float emitAccumulator;
    float nextSteeringUpdate;
    Bounds bossBounds;
    bool auraActive = true;
    float opacityMultiplier = 1f;

    void Awake()
    {
        Build();
        RefreshBounds();
    }

    public void SetAuraActive(bool active)
    {
        auraActive = active;
        if (particles == null)
            Build();
        if (particles != null)
            particles.gameObject.SetActive(active);
    }

    public void SetOpacityMultiplier(float multiplier)
    {
        opacityMultiplier = Mathf.Clamp(multiplier, .5f, 2.25f);
        ApplyStartColors();
    }

    void ApplyStartColors()
    {
        if (particles == null)
            return;
        ParticleSystem.MainModule main = particles.main;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(.35f, .035f, .62f, Mathf.Min(1f, .13f * opacityMultiplier)),
            new Color(.72f, .18f, 1f, Mathf.Min(1f, .34f * opacityMultiplier)));
    }

    void Build()
    {
        Transform existing = transform.Find("InsectoidCrab_DarkWindAura");
        if (existing != null)
        {
            particles = existing.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                buffer = new ParticleSystem.Particle[MaxParticles];
                return;
            }
        }

        GameObject root = new GameObject("InsectoidCrab_DarkWindAura");
        root.transform.SetParent(transform, false);
        particles = root.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 3f;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(.045f, .16f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(.35f, .035f, .62f, Mathf.Min(1f, .13f * opacityMultiplier)),
            new Color(.72f, .18f, 1f, Mathf.Min(1f, .34f * opacityMultiplier)));
        main.maxParticles = MaxParticles;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(.2f, .015f, .38f), 0f),
                new GradientColorKey(new Color(.7f, .16f, 1f), .48f),
                new GradientColorKey(new Color(.08f, .005f, .16f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(.42f, .18f),
                new GradientAlphaKey(.25f, .72f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = fade;

        ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 3.1f;
        renderer.velocityScale = .16f;
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sharedMaterial = WindVisualEffect.CreateWindStreakMaterial();

        buffer = new ParticleSystem.Particle[MaxParticles];
        particles.Play(true);
    }

    void Update()
    {
        if (!auraActive || particles == null)
            return;

        emitAccumulator += 58f * Time.deltaTime;
        int emitCount = Mathf.FloorToInt(emitAccumulator);
        emitAccumulator -= emitCount;
        for (int i = 0; i < emitCount; i++)
            EmitParticle();

        if (Time.time < nextSteeringUpdate)
            return;
        nextSteeringUpdate = Time.time + 1f / 15f;
        RefreshBounds();

        Vector3 center = bossBounds.center;
        float outerRadius = Mathf.Max(.9f, Mathf.Max(bossBounds.extents.x, bossBounds.extents.z) * 1.25f);
        int count = particles.GetParticles(buffer);
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.Particle particle = buffer[i];
            Vector3 radial = Vector3.ProjectOnPlane(particle.position - center, Vector3.up);
            float distance = Mathf.Max(.08f, radial.magnitude);
            Vector3 radialDirection = radial / distance;
            Vector3 tangent = Vector3.Cross(Vector3.up, radialDirection);
            float pull = Mathf.Lerp(.3f, 1.15f, Mathf.Clamp01(distance / outerRadius));
            particle.velocity = tangent * Random.Range(2.8f, 4.4f)
                                - radialDirection * pull
                                + Vector3.up * Random.Range(.2f, .7f);
            buffer[i] = particle;
        }
        particles.SetParticles(buffer, count);
    }

    void EmitParticle()
    {
        RefreshBounds();
        Vector3 center = bossBounds.center;
        float radius = Mathf.Max(.9f, Mathf.Max(bossBounds.extents.x, bossBounds.extents.z) * 1.25f);
        Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(radius * .55f, radius);
        var emit = new ParticleSystem.EmitParams
        {
            position = center + new Vector3(circle.x,
                Random.Range(-bossBounds.extents.y * .7f, bossBounds.extents.y * .95f), circle.y),
            startLifetime = Random.Range(1.8f, 3.2f),
            startSize = Random.Range(.045f, .16f),
            startColor = Color.Lerp(
                new Color(.26f, .02f, .48f, Mathf.Min(1f, .12f * opacityMultiplier)),
                new Color(.7f, .16f, 1f, Mathf.Min(1f, .34f * opacityMultiplier)), Random.value)
        };
        particles.Emit(emit, 1);
    }

    void RefreshBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool found = false;
        Bounds combined = new Bounds(transform.position + Vector3.up, Vector3.one * 2f);
        foreach (Renderer renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer)
                continue;
            if (!found)
            {
                combined = renderer.bounds;
                found = true;
            }
            else combined.Encapsulate(renderer.bounds);
        }
        bossBounds = combined;
    }
}

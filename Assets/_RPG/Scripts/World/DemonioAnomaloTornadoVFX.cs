using UnityEngine;

// A particle-driven vortex: every particle is continuously pulled inward while tangential
// velocity makes it orbit the demon, producing a genuine spiral instead of a static ring.
public class DemonioAnomaloTornadoVFX : MonoBehaviour
{
    ParticleSystem particles;
    ParticleSystem.Particle[] particleBuffer;
    DemonioAnomaloEncounterEffects encounter;
    float emitAccumulator;
    float nextParticleSimulation;

    void Awake()
    {
        encounter = GetComponent<DemonioAnomaloEncounterEffects>();
        Build();
    }

    void Build()
    {
        GameObject root = new GameObject("DemonioAnomalo_WindTornado");
        root.transform.SetParent(transform, false);
        particles = root.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 4f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
        main.startColor = new Color(0.55f, 0.2f, 1f, 0.22f);
        // 450 streaks are still a dense tornado at this scale, but avoid rewriting 1,200
        // particle structs every frame when the boss is visible.
        main.maxParticles = 450;
        var emission = particles.emission;
        emission.enabled = false;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.5f;
        renderer.velocityScale = 0.14f;
        renderer.material = WindVisualEffect.CreateWindStreakMaterial();
        renderer.alignment = ParticleSystemRenderSpace.World;

        // Fade at birth and death and use the wind system's soft-dot material, giving every
        // violet streak transparent edges rather than a hard rectangular particle boundary.
        var colourOverLifetime = particles.colorOverLifetime;
        colourOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.72f, 0.18f),
                    new GradientAlphaKey(0.5f, 0.76f), new GradientAlphaKey(0f, 1f) });
        colourOverLifetime.color = fade;
        particleBuffer = new ParticleSystem.Particle[450];
    }

    void Update()
    {
        if (particles == null) return;
        float intensity = encounter != null ? encounter.Intensity : 0f;
        if (intensity <= 0.001f)
        {
            if (particles.particleCount > 0) particles.Clear();
            return;
        }

        float rate = Mathf.Lerp(10f, 160f, intensity);
        emitAccumulator += rate * Time.deltaTime;
        int emitCount = Mathf.FloorToInt(emitAccumulator);
        emitAccumulator -= emitCount;
        for (int i = 0; i < emitCount; i++) EmitParticle(intensity);

        // Particle motion only needs a 12 Hz steering update; Unity continues interpolating
        // velocities between these updates and the visual remains smooth.
        if (Time.time < nextParticleSimulation) return;
        nextParticleSimulation = Time.time + (1f / 12f);

        int count = particles.GetParticles(particleBuffer);
        Vector3 center = transform.position + Vector3.up * 1.1f;
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.Particle particle = particleBuffer[i];
            Vector3 radial = particle.position - center;
            radial.y = 0f;
            float distance = Mathf.Max(0.15f, radial.magnitude);
            radial /= distance;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial);
            float rise = Mathf.Lerp(0.55f, 2.9f, intensity) * (1f - Mathf.Clamp01(distance / 7f) * 0.35f);
            particle.velocity = tangent * Mathf.Lerp(2f, 9f, intensity) - radial * Mathf.Lerp(0.8f, 3.8f, intensity) + Vector3.up * rise;
            particle.startColor = Color.Lerp(new Color(0.72f, 0.8f, 1f, 0.08f), new Color(0.62f, 0.18f, 1f, 0.46f), intensity);
            particleBuffer[i] = particle;
        }
        particles.SetParticles(particleBuffer, count);
    }

    void EmitParticle(float intensity)
    {
        float radius = Mathf.Lerp(3.5f, 7f, intensity) * Random.Range(0.45f, 1f);
        Vector2 circle = Random.insideUnitCircle.normalized * radius;
        Vector3 center = transform.position + Vector3.up * 1.1f;
        var emit = new ParticleSystem.EmitParams();
        emit.position = center + new Vector3(circle.x, Random.Range(-0.9f, 4.5f), circle.y);
        emit.startLifetime = Random.Range(2.2f, 4.2f);
        emit.startSize = Random.Range(0.06f, 0.18f) * (0.7f + intensity);
        emit.startColor = Color.Lerp(new Color(0.72f, 0.8f, 1f, 0.08f), new Color(0.62f, 0.18f, 1f, 0.46f), intensity);
        particles.Emit(emit, 1);
    }
}

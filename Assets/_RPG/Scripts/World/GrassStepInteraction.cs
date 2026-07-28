using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// Distance-driven grass contact shared by the player and enemies. Terrain grass emits
// small cut/bent blades at each footfall; nearby GameObject grass briefly bends away.
public sealed class GrassStepInteraction : MonoBehaviour
{
    [SerializeField] float stepDistance = .72f;
    [SerializeField] float bendRadius = .7f;
    [SerializeField] int particlesPerStep = 7;

    static Material sharedMaterial;
    ParticleSystem particles;
    Vector3 previousPosition;
    float travelled;

    void Awake()
    {
        if (GetComponent<EnemyStats>() != null)
        {
            stepDistance = .9f;
            particlesPerStep = 5;
        }
        BuildParticles();
        previousPosition = transform.position;
    }

    void Update()
    {
        Vector3 delta = transform.position - previousPosition;
        previousPosition = transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude > 4f) { travelled = 0f; return; }
        travelled += delta.magnitude;
        if (travelled < stepDistance || delta.sqrMagnitude < .0001f) return;
        travelled = 0f;

        if (!TryFindGrassGround(out Vector3 point)) return;
        EmitGrass(point, delta.normalized);
        BendNearbyGrass(point, delta.normalized);
    }

    bool TryFindGrassGround(out Vector3 point)
    {
        point = transform.position;
        if (!Physics.Raycast(transform.position + Vector3.up * 1.2f,
                Vector3.down, out RaycastHit hit, 3.2f, ~0,
                QueryTriggerInteraction.Ignore))
            return false;
        point = hit.point;
        if (hit.collider.GetComponent<Terrain>() != null) return true;
        string surface = (hit.collider.name + " " +
            (hit.collider.sharedMaterial != null
                ? hit.collider.sharedMaterial.name : "")).ToLowerInvariant();
        return surface.Contains("grass") || surface.Contains("pasto");
    }

    void EmitGrass(Vector3 point, Vector3 movement)
    {
        if (particles == null) return;
        for (int i = 0; i < particlesPerStep; i++)
        {
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = point + Random.insideUnitSphere * .18f +
                           Vector3.up * .06f,
                velocity = -movement * Random.Range(.25f, .8f) +
                           Vector3.up * Random.Range(.45f, 1.2f) +
                           Random.insideUnitSphere * .22f,
                startLifetime = Random.Range(.35f, .75f),
                startSize3D = new Vector3(Random.Range(.012f, .025f),
                    Random.Range(.09f, .18f), .012f),
                startColor = Color.Lerp(new Color(.15f, .34f, .06f, .78f),
                    new Color(.46f, .62f, .16f, .72f), Random.value)
            };
            particles.Emit(emit, 1);
        }
    }

    void BendNearbyGrass(Vector3 point, Vector3 movement)
    {
        Collider[] colliders = Physics.OverlapSphere(point, bendRadius, ~0,
            QueryTriggerInteraction.Ignore);
        foreach (Collider collider in colliders)
        {
            string name = collider.name.ToLowerInvariant();
            if (!name.Contains("grass") && !name.Contains("pasto")) continue;
            TemporaryGrassBend bend =
                collider.GetComponent<TemporaryGrassBend>() ??
                collider.gameObject.AddComponent<TemporaryGrassBend>();
            bend.Bend(movement);
        }
    }

    void BuildParticles()
    {
        GameObject go = new GameObject("GrassStepParticles");
        go.transform.SetParent(null);
        particles = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        main.startLifetime = .55f;
        main.startSpeed = 0f;
        main.startSize3D = true;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(.8f, 0f),
                    new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.7f;
        renderer.sharedMaterial = GetMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Material GetMaterial()
    {
        if (sharedMaterial != null) return sharedMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Sprites/Default");
        sharedMaterial = new Material(shader)
        {
            name = "GrassStepBlade_Runtime",
            color = Color.white,
            renderQueue = 3000
        };
        return sharedMaterial;
    }

    void OnDestroy()
    {
        if (particles != null) Destroy(particles.gameObject);
    }
}

public sealed class TemporaryGrassBend : MonoBehaviour
{
    Quaternion original;
    Coroutine routine;

    void Awake() => original = transform.localRotation;

    public void Bend(Vector3 movement)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Animate(movement));
    }

    IEnumerator Animate(Vector3 movement)
    {
        Vector3 axis = Vector3.Cross(Vector3.up,
            transform.InverseTransformDirection(movement)).normalized;
        Quaternion bent = original * Quaternion.AngleAxis(14f, axis);
        for (float t = 0f; t < 1f; t += Time.deltaTime * 12f)
        {
            transform.localRotation = Quaternion.Slerp(original, bent, t);
            yield return null;
        }
        for (float t = 0f; t < 1f; t += Time.deltaTime * 3.5f)
        {
            transform.localRotation = Quaternion.Slerp(bent, original, t);
            yield return null;
        }
        transform.localRotation = original;
        routine = null;
    }
}

using UnityEngine;

// Shared enemy projectile. Its visual comes from the imported Free Fire VFX pack while
// movement, damage and collision remain here so every enemy uses the same reliable logic.
public class Fireball : MonoBehaviour
{
    const string FreeFireVisualResource = "VFX/FreeFireProjectileVFX";

    [SerializeField] float speed = 14f;
    [SerializeField] float damage = 150f;
    [SerializeField] float lifetime = 5f;
    [SerializeField] float hitRadius = 0.6f;
    [SerializeField] bool leaveGroundFireOnImpact;
    [SerializeField] float groundFireDuration = 5f;
    [SerializeField] float groundFireDamagePerTick = 25f;

    static GameObject cachedFreeFireVisual;
    static Material normalCoreMaterial;
    static Material normalHotCoreMaterial;
    static Material arcaneCoreMaterial;
    static Material arcaneHotCoreMaterial;
    Vector3 direction;
    float spawnTime;
    // Reused per projectile to avoid allocating a Collider array on every frame of flight.
    readonly Collider[] playerHitBuffer = new Collider[8];

    public void Launch(Vector3 dir, float dmg)
    {
        direction = dir.normalized;
        damage = dmg;
        spawnTime = Time.time;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    void Update()
    {
        Vector3 previousPosition = transform.position;
        float travelDistance = speed * Time.deltaTime;
        int worldMask = ~LayerMask.GetMask("Player", "Enemy");
        if (Physics.SphereCast(previousPosition, hitRadius * 0.45f, direction, out RaycastHit worldHit,
            travelDistance, worldMask, QueryTriggerInteraction.Ignore))
        {
            Impact(worldHit.point, worldHit.normal);
            return;
        }

        transform.position += direction * travelDistance;

        if (Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, hitRadius, playerHitBuffer, LayerMask.GetMask("Player"));
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = playerHitBuffer[i];
            PlayerStats player = hit.GetComponentInParent<PlayerStats>();
            if (player != null)
            {
                player.TakeDamage(damage, transform.position);
                Destroy(gameObject);
                return;
            }
        }
    }

    void Impact(Vector3 impactPoint, Vector3 normal)
    {
        if (leaveGroundFireOnImpact)
            GroundFireHazard.Create(impactPoint + normal * 0.035f, groundFireDuration,
                groundFireDamagePerTick, Mathf.Max(1.5f, hitRadius * 1.8f));
        Destroy(gameObject);
    }

    public static GameObject BuildPrefab()
    {
        return BuildVisual(new Color(1f, 0.34f, 0.05f), 1.1f, "Fireball", false);
    }

    // Bosses can opt into a larger projectile without duplicating the complete VFX setup.
    // The red/orange material is deliberately shared with the regular fireball; only its
    // physical size, collision radius and travel speed are configured by the caller.
    public static GameObject BuildRedFireball(float visualScale)
    {
        return BuildVisual(new Color(1f, 0.12f, 0.015f), visualScale, "GiantRedFireball", false);
    }

    public void Configure(float newSpeed, float newHitRadius, float newLifetime = 5f)
    {
        speed = Mathf.Max(0.1f, newSpeed);
        hitRadius = Mathf.Max(0.05f, newHitRadius);
        lifetime = Mathf.Max(0.1f, newLifetime);
    }

    public void ConfigureGroundFire(bool enabled, float duration = 5f, float damagePerTick = 25f)
    {
        leaveGroundFireOnImpact = enabled;
        groundFireDuration = Mathf.Max(0.1f, duration);
        groundFireDamagePerTick = Mathf.Max(0f, damagePerTick);
    }

    public static GameObject BuildArcanePrefab()
    {
        // Superior Tree Anomaly keeps its purple identity, but now uses the same pack VFX.
        return BuildVisual(new Color(0.68f, 0.18f, 1f), 0.84f, "SuperiorTreeAnomaly_ArcaneBolt", true);
    }

    static GameObject BuildVisual(Color lightColor, float visualScale, string objectName, bool arcaneTint)
    {
        GameObject projectile = new GameObject(objectName);
        Fireball fireball = projectile.AddComponent<Fireball>();
        fireball.hitRadius = 1.2f;

        CreateGlowingCore(projectile.transform, visualScale, arcaneTint);

        if (cachedFreeFireVisual == null)
            cachedFreeFireVisual = Resources.Load<GameObject>(FreeFireVisualResource);

        if (cachedFreeFireVisual != null)
        {
            GameObject visual = Object.Instantiate(cachedFreeFireVisual, projectile.transform, false);
            visual.name = "FreeFireVFX_Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * visualScale;

            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                Object.Destroy(collider);

            foreach (ParticleSystem particles in visual.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = Mathf.Max(main.maxParticles, Mathf.CeilToInt(main.maxParticles * 2f));
                main.startSizeMultiplier *= 1.18f;
                main.startLifetimeMultiplier *= 1.15f;
                if (arcaneTint)
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(0.9f, 0.5f, 1f, 1f),
                        new Color(0.42f, 0.05f, 1f, 0.72f));

                // Keep the pack's original curves and simply make the surrounding fire denser.
                // Changing curve modes here can trigger Unity's "curves must all be in the same
                // mode" error. Scale the value appropriate to the existing mode instead.
                var emission = particles.emission;
                ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
                const float emissionMultiplier = 2.15f;
                switch (rate.mode)
                {
                    case ParticleSystemCurveMode.Constant:
                        rate.constant *= emissionMultiplier;
                        break;
                    case ParticleSystemCurveMode.TwoConstants:
                        rate.constantMin *= emissionMultiplier;
                        rate.constantMax *= emissionMultiplier;
                        break;
                    case ParticleSystemCurveMode.Curve:
                    case ParticleSystemCurveMode.TwoCurves:
                        rate.curveMultiplier *= emissionMultiplier;
                        break;
                }
                emission.rateOverTime = rate;
                particles.Play(true);
            }
        }
        else
        {
            Debug.LogWarning("[Fireball] No se encontro el prefab Free Fire VFX en Resources/VFX. " +
                "Ejecuta RPG > VFX > Preparar Free Fire VFX.");
        }

        Light glow = projectile.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = lightColor;
        glow.range = arcaneTint ? 6.4f : 5.6f;
        glow.intensity = arcaneTint ? 4.8f : 4.2f;
        glow.shadows = LightShadows.None;

        return projectile;
    }

    static void CreateGlowingCore(Transform parent, float visualScale, bool arcane)
    {
        float diameter = visualScale * 0.82f;
        CreateCoreSphere(parent, "Fireball_SolidCore", diameter,
            GetCoreMaterial(arcane, false));
        CreateCoreSphere(parent, "Fireball_WhiteHotCenter", diameter * 0.54f,
            GetCoreMaterial(arcane, true));
    }

    static void CreateCoreSphere(Transform parent, string name, float diameter, Material material)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localRotation = Quaternion.identity;
        sphere.transform.localScale = Vector3.one * diameter;
        Object.Destroy(sphere.GetComponent<Collider>());

        MeshRenderer renderer = sphere.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static Material GetCoreMaterial(bool arcane, bool hotCenter)
    {
        Material cached = arcane
            ? (hotCenter ? arcaneHotCoreMaterial : arcaneCoreMaterial)
            : (hotCenter ? normalHotCoreMaterial : normalCoreMaterial);
        if (cached != null)
            return cached;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        cached = new Material(shader)
        {
            name = arcane
                ? (hotCenter ? "ArcaneFireball_HotCore" : "ArcaneFireball_Core")
                : (hotCenter ? "Fireball_HotCore" : "Fireball_Core"),
            enableInstancing = true
        };

        Color color;
        if (arcane)
            color = hotCenter ? new Color(2.6f, 1.25f, 4f, 1f) : new Color(1.15f, 0.12f, 3.4f, 1f);
        else
            color = hotCenter ? new Color(4f, 2.2f, 0.35f, 1f) : new Color(3.5f, 0.38f, 0.025f, 1f);

        if (cached.HasProperty("_BaseColor")) cached.SetColor("_BaseColor", color);
        if (cached.HasProperty("_Color")) cached.SetColor("_Color", color);

        if (arcane)
        {
            if (hotCenter) arcaneHotCoreMaterial = cached;
            else arcaneCoreMaterial = cached;
        }
        else
        {
            if (hotCenter) normalHotCoreMaterial = cached;
            else normalCoreMaterial = cached;
        }
        return cached;
    }
}

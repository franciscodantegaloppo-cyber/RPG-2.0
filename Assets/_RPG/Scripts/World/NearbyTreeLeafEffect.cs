using System.Collections.Generic;
using UnityEngine;

// Emits imported leaf particles only while the player is surrounded by at least five distinct
// trees inside ten metres. Trees are deduplicated because one tree can have several colliders.
public class NearbyTreeLeafEffect : MonoBehaviour
{
    const float DetectionRadius = 10f;
    const float AmbientDetectionRadius = 17f;
    const int RequiredTrees = 5;
    readonly Collider[] nearby = new Collider[192];
    readonly HashSet<Transform> distinctTrees = new HashSet<Transform>();
    readonly List<TreeEmitter> ambientTrees = new List<TreeEmitter>();

    ParticleSystem leaves;
    ParticleSystem fallingLeaves;
    ParticleSystem pollen;
    static Material sharedPollenMaterial;
    ParticleSystem.EmissionModule emission;
    float nextScan;
    float nextAmbientLeaf;
    float nextPollen;
    bool active;

    void Awake()
    {
        CreateLeafSystem();
        CreateAmbientFallingLeaves();
        CreatePollenSystem();
    }

    void Update()
    {
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + .65f;
            bool shouldBeActive = ScanNearbyTrees() >= RequiredTrees;
            if (shouldBeActive != active)
            {
                active = shouldBeActive;
                emission.rateOverTime = active ? 24f : 0f;
                if (active && !leaves.isPlaying) leaves.Play(true);
            }
            UpdateAmbientWind();
        }
        UpdateCalmTreeParticles();
    }

    int ScanNearbyTrees()
    {
        distinctTrees.Clear();
        ambientTrees.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position,
            AmbientDetectionRadius, nearby, ~0,
            QueryTriggerInteraction.Ignore);
        int closeTrees = 0;
        for (int i = 0; i < count; i++)
        {
            Collider hit = nearby[i];
            if (hit == null || hit is TerrainCollider) continue;
            Transform tree = FindTreeRoot(hit.transform);
            if (tree != null) distinctTrees.Add(tree);
        }

        foreach (Transform tree in distinctTrees)
        {
            if (tree == null) continue;
            ambientTrees.Add(BuildEmitter(tree));
            Vector3 flat = tree.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude <= DetectionRadius * DetectionRadius)
                closeTrees++;
        }
        return closeTrees;
    }

    static Transform FindTreeRoot(Transform source)
    {
        Transform match = null;
        for (Transform current = source; current != null; current = current.parent)
        {
            if (current.GetComponent<EnemyStats>() != null) return null;
            string name = current.name.ToLowerInvariant();
            bool isTree = name.Contains("tree") || name.Contains("arbol") || name.Contains("spruce") ||
                          name.Contains("pine") || name.Contains("pino");
            if (match == null && isTree && !name.Contains("superior") && !name.Contains("anomaly"))
                match = current;
            if (current.parent == null || current.CompareTag("Player")) break;
        }
        return match;
    }

    void CreateLeafSystem()
    {
        GameObject effect = new GameObject("LeavesFlying_NearbyTrees");
        effect.transform.SetParent(transform, false);
        effect.transform.localPosition = new Vector3(0f, 2.8f, 0f);
        leaves = effect.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = leaves.main;
        main.loop = true;
        main.playOnAwake = true;
        main.maxParticles = 110;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 5.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.35f, 1.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(.09f, .23f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(.025f, .12f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(.22f, .48f, .055f, .92f), new Color(.95f, .52f, .08f, .95f));

        emission = leaves.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = leaves.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(9.5f, 4.2f, 9.5f);

        ParticleSystem.NoiseModule noise = leaves.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = new ParticleSystem.MinMaxCurve(.7f, 1.65f);
        noise.strengthY = new ParticleSystem.MinMaxCurve(.22f, .8f);
        noise.strengthZ = new ParticleSystem.MinMaxCurve(.7f, 1.65f);
        noise.frequency = .32f;
        noise.scrollSpeed = .22f;
        noise.damping = true;

        ParticleSystem.RotationOverLifetimeModule rotation = leaves.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-2.8f, 2.8f);
        rotation.y = new ParticleSystem.MinMaxCurve(-4.5f, 4.5f);
        rotation.z = new ParticleSystem.MinMaxCurve(-3.4f, 3.4f);

        ParticleSystem.ColorOverLifetimeModule colour = leaves.colorOverLifetime;
        colour.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(.35f, .7f, .08f), 0f),
                new GradientColorKey(new Color(.92f, .55f, .08f), .58f),
                new GradientColorKey(new Color(.35f, .15f, .025f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.95f, .12f),
                new GradientAlphaKey(.82f, .78f), new GradientAlphaKey(0f, 1f)
            });
        colour.color = fade;

        ParticleSystemRenderer leafRenderer = effect.GetComponent<ParticleSystemRenderer>();
        leafRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        leafRenderer.alignment = ParticleSystemRenderSpace.View;
        leafRenderer.minParticleSize = .002f;
        leafRenderer.maxParticleSize = .08f;
        leafRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        leafRenderer.receiveShadows = false;
        Material importedLeaves = Resources.Load<Material>("NatureFX/FlyingLeaves");
        if (importedLeaves != null) leafRenderer.sharedMaterial = importedLeaves;

        leaves.Play(true);
    }

    void CreateAmbientFallingLeaves()
    {
        GameObject effect = new GameObject("LeavesFalling_FromTrees");
        effect.transform.SetParent(transform, false);
        fallingLeaves = effect.AddComponent<ParticleSystem>();
        fallingLeaves.Stop(true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = fallingLeaves.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 170;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.8f, 7.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(.055f, .16f);
        main.startRotation = new ParticleSystem.MinMaxCurve(
            0f, Mathf.PI * 2f);
        main.gravityModifier =
            new ParticleSystem.MinMaxCurve(.045f, .11f);

        ParticleSystem.EmissionModule calmEmission = fallingLeaves.emission;
        calmEmission.enabled = false;
        ParticleSystem.ShapeModule shape = fallingLeaves.shape;
        shape.enabled = false;

        ParticleSystem.NoiseModule noise = fallingLeaves.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = new ParticleSystem.MinMaxCurve(.18f, .72f);
        noise.strengthY = new ParticleSystem.MinMaxCurve(.04f, .16f);
        noise.strengthZ = new ParticleSystem.MinMaxCurve(.18f, .72f);
        noise.frequency = .27f;
        noise.scrollSpeed = .16f;
        noise.damping = true;

        ParticleSystem.RotationOverLifetimeModule rotation =
            fallingLeaves.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
        rotation.y = new ParticleSystem.MinMaxCurve(-3.8f, 3.8f);
        rotation.z = new ParticleSystem.MinMaxCurve(-2.7f, 2.7f);

        ParticleSystem.ColorOverLifetimeModule colour =
            fallingLeaves.colorOverLifetime;
        colour.enabled = true;
        Gradient leafFade = new Gradient();
        leafFade.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(.32f, .58f, .08f), 0f),
                new GradientColorKey(new Color(.68f, .43f, .07f), .72f),
                new GradientColorKey(new Color(.25f, .12f, .025f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(.86f, .08f),
                new GradientAlphaKey(.78f, .82f),
                new GradientAlphaKey(0f, 1f)
            });
        colour.color = leafFade;

        ParticleSystem.CollisionModule collision = fallingLeaves.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.dampen = new ParticleSystem.MinMaxCurve(.72f, .94f);
        collision.bounce = new ParticleSystem.MinMaxCurve(0f, .035f);
        collision.lifetimeLoss = new ParticleSystem.MinMaxCurve(.35f, .72f);
        collision.collidesWith = ~0;
        collision.maxCollisionShapes = 96;

        ParticleSystemRenderer renderer =
            effect.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.minParticleSize = .001f;
        renderer.maxParticleSize = .045f;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Material importedLeaves = Resources.Load<Material>(
            "NatureFX/FlyingLeaves");
        if (importedLeaves != null)
            renderer.sharedMaterial = importedLeaves;
    }

    void CreatePollenSystem()
    {
        GameObject effect = new GameObject("TreePollen_Ambient");
        effect.transform.SetParent(transform, false);
        pollen = effect.AddComponent<ParticleSystem>();
        pollen.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = pollen.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 260;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 6.8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(.018f, .065f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, .88f, .45f, .14f),
            new Color(.82f, 1f, .62f, .42f));

        ParticleSystem.EmissionModule pollenEmission = pollen.emission;
        pollenEmission.enabled = false;
        ParticleSystem.ShapeModule shape = pollen.shape;
        shape.enabled = false;

        ParticleSystem.NoiseModule noise = pollen.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = new ParticleSystem.MinMaxCurve(.06f, .28f);
        noise.strengthY = new ParticleSystem.MinMaxCurve(.025f, .14f);
        noise.strengthZ = new ParticleSystem.MinMaxCurve(.06f, .28f);
        noise.frequency = .18f;
        noise.scrollSpeed = .09f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule colour =
            pollen.colorOverLifetime;
        colour.enabled = true;
        Gradient pollenFade = new Gradient();
        pollenFade.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, .92f, .55f), 0f),
                new GradientColorKey(new Color(.8f, 1f, .7f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(.42f, .18f),
                new GradientAlphaKey(.24f, .72f),
                new GradientAlphaKey(0f, 1f)
            });
        colour.color = pollenFade;

        ParticleSystemRenderer renderer =
            effect.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = CreatePollenMaterial();
    }

    void UpdateCalmTreeParticles()
    {
        bool gustLeavesActive = active ||
            (WindManager.Instance != null &&
             WindManager.Instance.IsGusting);
        if (gustLeavesActive || ambientTrees.Count == 0)
            return;

        int density = Mathf.Clamp(ambientTrees.Count, 1, 9);
        if (Time.time >= nextAmbientLeaf)
        {
            nextAmbientLeaf = Time.time +
                Random.Range(.22f, .48f) / Mathf.Sqrt(density);
            EmitFallingLeaf(ambientTrees[
                Random.Range(0, ambientTrees.Count)]);
        }
        if (Time.time >= nextPollen)
        {
            nextPollen = Time.time +
                Random.Range(.11f, .24f) / Mathf.Sqrt(density);
            TreeEmitter source = ambientTrees[
                Random.Range(0, ambientTrees.Count)];
            int count = Random.Range(1, 4);
            for (int i = 0; i < count; i++)
                EmitPollen(source);
        }
    }

    void EmitFallingLeaf(TreeEmitter source)
    {
        if (fallingLeaves == null || source.transform == null) return;
        Vector2 circle = Random.insideUnitCircle * source.radius;
        Vector3 wind = WindManager.Instance != null
            ? WindManager.Instance.WindDirection *
              Mathf.Lerp(.08f, .42f,
                  Mathf.Clamp01(
                      WindManager.Instance.CurrentStrength01))
            : Vector3.zero;
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = source.crown +
                       new Vector3(circle.x,
                           Random.Range(-.25f, .45f), circle.y),
            velocity = wind +
                       new Vector3(Random.Range(-.16f, .16f),
                           Random.Range(-.22f, -.07f),
                           Random.Range(-.16f, .16f)),
            startLifetime = Random.Range(4.2f, 7.1f),
            startSize = Random.Range(.055f, .155f),
            rotation3D = Random.insideUnitSphere * 180f,
            startColor = Color.Lerp(
                new Color(.24f, .55f, .055f, .88f),
                new Color(.9f, .47f, .05f, .82f), Random.value)
        };
        fallingLeaves.Emit(emit, 1);
    }

    void EmitPollen(TreeEmitter source)
    {
        if (pollen == null || source.transform == null) return;
        Vector2 circle = Random.insideUnitCircle *
                         Mathf.Max(.35f, source.radius * .78f);
        Vector3 wind = WindManager.Instance != null
            ? WindManager.Instance.WindDirection *
              Random.Range(.025f, .12f)
            : Vector3.zero;
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = source.crown +
                       new Vector3(circle.x,
                           Random.Range(-.8f, .2f), circle.y),
            velocity = wind +
                       new Vector3(Random.Range(-.035f, .035f),
                           Random.Range(-.025f, .055f),
                           Random.Range(-.035f, .035f)),
            startLifetime = Random.Range(3.1f, 6.7f),
            startSize = Random.Range(.016f, .06f),
            startColor = Color.Lerp(
                new Color(1f, .88f, .42f, .13f),
                new Color(.78f, 1f, .63f, .4f), Random.value)
        };
        pollen.Emit(emit, 1);
    }

    void UpdateAmbientWind()
    {
        if (fallingLeaves == null || pollen == null) return;
        Vector3 wind = WindManager.Instance != null
            ? WindManager.Instance.WindDirection *
              Mathf.Lerp(.06f, .34f,
                  Mathf.Clamp01(
                      WindManager.Instance.CurrentStrength01))
            : Vector3.zero;
        SetWorldVelocity(fallingLeaves, wind);
        SetWorldVelocity(pollen, wind * .42f + Vector3.up * .025f);
    }

    static void SetWorldVelocity(ParticleSystem system, Vector3 velocity)
    {
        ParticleSystem.VelocityOverLifetimeModule module =
            system.velocityOverLifetime;
        module.enabled = true;
        module.space = ParticleSystemSimulationSpace.World;
        module.x = new ParticleSystem.MinMaxCurve(velocity.x);
        module.y = new ParticleSystem.MinMaxCurve(velocity.y);
        module.z = new ParticleSystem.MinMaxCurve(velocity.z);
    }

    static TreeEmitter BuildEmitter(Transform tree)
    {
        Renderer[] renderers = tree.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(tree.position + Vector3.up * 4f,
            new Vector3(2f, 6f, 2f));
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
                bounds.Encapsulate(renderer.bounds);
        }
        float radius = Mathf.Clamp(
            Mathf.Max(bounds.extents.x, bounds.extents.z) * .72f,
            .55f, 3.8f);
        Vector3 crown = new Vector3(bounds.center.x,
            Mathf.Lerp(bounds.center.y, bounds.max.y, .64f),
            bounds.center.z);
        return new TreeEmitter(tree, crown, radius);
    }

    static Material CreatePollenMaterial()
    {
        if (sharedPollenMaterial != null)
            return sharedPollenMaterial;
        Shader shader = Shader.Find(
                            "Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Particles/Standard Unlit") ??
                        Shader.Find("Sprites/Default");
        Material material = new Material(shader)
        {
            name = "TreePollen_Radial_Runtime",
            color = Color.white,
            renderQueue = 3000
        };
        Texture2D texture = new Texture2D(32, 32,
            TextureFormat.RGBA32, false)
        {
            name = "TreePollen_Radial_Runtime",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < texture.height; y++)
        for (int x = 0; x < texture.width; x++)
        {
            Vector2 uv = new Vector2(
                (x + .5f) / texture.width * 2f - 1f,
                (y + .5f) / texture.height * 2f - 1f);
            float alpha = Mathf.Pow(
                Mathf.Clamp01(1f - uv.magnitude), 1.8f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply(false, true);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend",
                (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend",
                (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        sharedPollenMaterial = material;
        return sharedPollenMaterial;
    }

    readonly struct TreeEmitter
    {
        public readonly Transform transform;
        public readonly Vector3 crown;
        public readonly float radius;

        public TreeEmitter(Transform transform, Vector3 crown, float radius)
        {
            this.transform = transform;
            this.crown = crown;
            this.radius = radius;
        }
    }
}

public static class NearbyTreeLeafBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && player.GetComponent<NearbyTreeLeafEffect>() == null)
            player.AddComponent<NearbyTreeLeafEffect>();
        else if (player == null && Object.FindAnyObjectByType<NearbyTreeLeafBootstrapRunner>() == null)
            new GameObject("NearbyTreeLeafBootstrap").AddComponent<NearbyTreeLeafBootstrapRunner>();
    }
}

public class NearbyTreeLeafBootstrapRunner : MonoBehaviour
{
    void Update()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;
        if (player.GetComponent<NearbyTreeLeafEffect>() == null)
            player.AddComponent<NearbyTreeLeafEffect>();
        Destroy(gameObject);
    }
}

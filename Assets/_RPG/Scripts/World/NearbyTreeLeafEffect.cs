using System.Collections.Generic;
using UnityEngine;

// Emits imported leaf particles only while the player is surrounded by at least five distinct
// trees inside ten metres. Trees are deduplicated because one tree can have several colliders.
public class NearbyTreeLeafEffect : MonoBehaviour
{
    const float DetectionRadius = 10f;
    const int RequiredTrees = 5;
    readonly Collider[] nearby = new Collider[96];
    readonly HashSet<Transform> distinctTrees = new HashSet<Transform>();

    ParticleSystem leaves;
    ParticleSystem.EmissionModule emission;
    float nextScan;
    bool active;

    void Awake()
    {
        CreateLeafSystem();
    }

    void Update()
    {
        if (Time.time < nextScan) return;
        nextScan = Time.time + .55f;
        bool shouldBeActive = CountNearbyTrees() >= RequiredTrees;
        if (shouldBeActive == active) return;
        active = shouldBeActive;
        emission.rateOverTime = active ? 24f : 0f;
        if (active && !leaves.isPlaying) leaves.Play(true);
    }

    int CountNearbyTrees()
    {
        distinctTrees.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position, DetectionRadius, nearby, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = nearby[i];
            if (hit == null || hit is TerrainCollider) continue;
            Transform tree = FindTreeRoot(hit.transform);
            if (tree != null) distinctTrees.Add(tree);
            if (distinctTrees.Count >= RequiredTrees) return distinctTrees.Count;
        }
        return distinctTrees.Count;
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

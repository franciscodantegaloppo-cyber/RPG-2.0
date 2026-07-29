using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Distance-driven grass contact shared by the player and enemies. It only reacts
// where painted Terrain details or actual grass objects exist; ordinary Terrain,
// paths and bare earth must never leave a fake green trail behind the character.
public sealed class GrassStepInteraction : MonoBehaviour
{
    [SerializeField] float stepDistance = .72f;
    [SerializeField] float bendRadius = .82f;
    [SerializeField] int particlesPerStep = 7;

    static Material sharedMaterial;
    static readonly Dictionary<Vector2Int, List<Transform>> vegetationGrid =
        new Dictionary<Vector2Int, List<Transform>>();
    static ulong cachedSceneHandle = ulong.MaxValue;
    const float VegetationCellSize = 3f;
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
        EnsureVegetationCache();
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

        bool standingOnGrass = TryFindGrassGround(out Vector3 point);
        if (!standingOnGrass)
            point = FindContactGroundPoint();
        if (standingOnGrass)
            EmitContactSway(point, delta.normalized);
        BendNearbyGrass(point, delta.normalized);
    }

    Vector3 FindContactGroundPoint()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 1.2f,
                Vector3.down, out RaycastHit hit, 3.2f, ~0,
                QueryTriggerInteraction.Ignore))
            return hit.point;
        return transform.position;
    }

    bool TryFindGrassGround(out Vector3 point)
    {
        point = transform.position;
        if (!Physics.Raycast(transform.position + Vector3.up * 1.2f,
                Vector3.down, out RaycastHit hit, 3.2f, ~0,
                QueryTriggerInteraction.Ignore))
            return false;
        point = hit.point;
        Terrain terrain = hit.collider.GetComponent<Terrain>();
        if (terrain != null)
            return HasPaintedGrass(terrain, point);
        string surface = (hit.collider.name + " " +
            (hit.collider.sharedMaterial != null
                ? hit.collider.sharedMaterial.name : "")).ToLowerInvariant();
        return surface.Contains("grass") || surface.Contains("pasto");
    }

    static bool HasPaintedGrass(Terrain terrain, Vector3 worldPoint)
    {
        TerrainData data = terrain != null ? terrain.terrainData : null;
        if (data == null || data.detailPrototypes == null ||
            data.detailPrototypes.Length == 0 ||
            data.detailWidth <= 0 || data.detailHeight <= 0)
            return false;

        Vector3 local = worldPoint - terrain.transform.position;
        int x = Mathf.Clamp(Mathf.FloorToInt(
            local.x / Mathf.Max(.01f, data.size.x) * data.detailWidth),
            0, data.detailWidth - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(
            local.z / Mathf.Max(.01f, data.size.z) * data.detailHeight),
            0, data.detailHeight - 1);

        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            int[,] density = data.GetDetailLayer(x, z, 1, 1, layer);
            if (density.Length > 0 && density[0, 0] > 0)
                return true;
        }
        return false;
    }

    void EmitContactSway(Vector3 point, Vector3 movement)
    {
        if (particles == null) return;
        for (int i = 0; i < particlesPerStep; i++)
        {
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = point + Random.insideUnitSphere * .18f +
                           Vector3.up * .06f,
                // A short sideways flex reads as grass being pushed aside.
                // No upward launch: blades are not cut from the ground.
                velocity = -movement * Random.Range(.08f, .22f) +
                           Random.insideUnitSphere * .035f,
                startLifetime = Random.Range(.24f, .4f),
                startSize3D = new Vector3(Random.Range(.014f, .03f),
                    Random.Range(.12f, .24f), .014f),
                startColor = Color.Lerp(new Color(.15f, .34f, .06f, .78f),
                    new Color(.46f, .62f, .16f, .72f), Random.value)
            };
            particles.Emit(emit, 1);
        }
    }

    void BendNearbyGrass(Vector3 point, Vector3 movement)
    {
        Collider[] colliders = Physics.OverlapSphere(point, bendRadius, ~0,
            QueryTriggerInteraction.Collide);
        HashSet<Transform> affected = new HashSet<Transform>();
        foreach (Collider collider in colliders)
        {
            Transform vegetation = FindVegetationRoot(collider.transform);
            if (vegetation != null)
                BendVegetation(vegetation, point, movement, affected);
        }

        // Nature-pack grass and flowers usually have no colliders. The spatial
        // cache lets those meshes react without adding thousands of colliders.
        EnsureVegetationCache();
        Vector2Int center = Cell(point);
        for (int z = -1; z <= 1; z++)
        for (int x = -1; x <= 1; x++)
        {
            if (!vegetationGrid.TryGetValue(
                    center + new Vector2Int(x, z), out List<Transform> entries))
                continue;
            foreach (Transform vegetation in entries)
            {
                if (vegetation == null) continue;
                Vector3 offset = vegetation.position - point;
                offset.y = 0f;
                if (offset.sqrMagnitude <= bendRadius * bendRadius)
                    BendVegetation(vegetation, point, movement, affected);
            }
        }
    }

    static void BendVegetation(Transform vegetation, Vector3 contactPoint,
        Vector3 movement, HashSet<Transform> affected)
    {
        if (vegetation == null || !affected.Add(vegetation)) return;
        Vector3 away = vegetation.position - contactPoint;
        away.y = 0f;
        if (away.sqrMagnitude < .0025f)
            away = Vector3.Cross(Vector3.up, movement);
        if (IsBushHierarchy(vegetation))
        {
            TemporaryBushBend bush =
                vegetation.GetComponent<TemporaryBushBend>() ??
                vegetation.gameObject.AddComponent<TemporaryBushBend>();
            bush.Bend(away.normalized);
        }
        else
        {
            TemporaryGrassBend bend =
                vegetation.GetComponent<TemporaryGrassBend>() ??
                vegetation.gameObject.AddComponent<TemporaryGrassBend>();
            bend.Bend(away.normalized);
        }
    }

    static void EnsureVegetationCache()
    {
        ulong sceneHandle =
            SceneManager.GetActiveScene().handle.GetRawData();
        if (cachedSceneHandle == sceneHandle && vegetationGrid.Count > 0)
            return;
        cachedSceneHandle = sceneHandle;
        vegetationGrid.Clear();
        HashSet<Transform> unique = new HashSet<Transform>();
        foreach (Renderer renderer in FindObjectsByType<Renderer>(
                     FindObjectsInactive.Exclude))
        {
            if (renderer is ParticleSystemRenderer ||
                renderer is SkinnedMeshRenderer) continue;
            Transform root = FindVegetationRoot(renderer.transform);
            if (root == null || !unique.Add(root)) continue;
            Vector2Int cell = Cell(root.position);
            if (!vegetationGrid.TryGetValue(cell,
                    out List<Transform> entries))
            {
                entries = new List<Transform>();
                vegetationGrid.Add(cell, entries);
            }
            entries.Add(root);
        }
    }

    static Vector2Int Cell(Vector3 position) => new Vector2Int(
        Mathf.FloorToInt(position.x / VegetationCellSize),
        Mathf.FloorToInt(position.z / VegetationCellSize));

    static Transform FindVegetationRoot(Transform candidate)
    {
        Transform current = candidate;
        for (int depth = 0; current != null && depth < 6;
             depth++, current = current.parent)
            if (IsVegetationName(current.name))
                return current;
        return null;
    }

    static bool IsVegetationName(string objectName)
    {
        string name = objectName.ToLowerInvariant();
        return name.Contains("grass") || name.Contains("pasto") ||
               name.Contains("flower") || name.Contains("flor") ||
               name.Contains("plant") || name.Contains("planta") ||
               name.Contains("bush") || name.Contains("arbusto") ||
               name.Contains("shrub") || name.Contains("matorral") ||
               name.Contains("fern") || name.Contains("helecho") ||
               name.Contains("mushroom") || name.Contains("hongo");
    }

    static bool IsBushName(string objectName)
    {
        string name = objectName.ToLowerInvariant();
        return name.Contains("bush") || name.Contains("arbusto") ||
               name.Contains("shrub") || name.Contains("matorral");
    }

    static bool IsBushHierarchy(Transform vegetation)
    {
        Transform current = vegetation;
        for (int depth = 0; current != null && depth < 3;
             depth++, current = current.parent)
            if (IsBushName(current.name))
                return true;
        return false;
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

    public void Bend(Vector3 awayFromContact)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Animate(awayFromContact));
    }

    IEnumerator Animate(Vector3 awayFromContact)
    {
        Vector3 axis = Vector3.Cross(Vector3.up,
            transform.InverseTransformDirection(awayFromContact)).normalized;
        Quaternion bent = original * Quaternion.AngleAxis(27f, axis);
        for (float t = 0f; t < 1f; t += Time.deltaTime * 15f)
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

// Bushes need a heavier response than individual grass blades. Their render
// transforms compress, spread and spring sideways while the collider-bearing
// root remains untouched whenever the prefab provides a separate visual child.
public sealed class TemporaryBushBend : MonoBehaviour
{
    readonly List<VisualPose> visuals = new List<VisualPose>();
    Coroutine routine;

    void Awake()
    {
        CacheVisuals();
    }

    void CacheVisuals()
    {
        visuals.Clear();
        HashSet<Transform> unique = new HashSet<Transform>();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null ||
                renderer is ParticleSystemRenderer ||
                renderer is SkinnedMeshRenderer)
                continue;
            Transform visual = renderer.transform;
            if (!unique.Add(visual)) continue;
            visuals.Add(new VisualPose(visual));
        }

        // Some simple bush prefabs keep their MeshRenderer on the root.
        if (visuals.Count == 0)
            visuals.Add(new VisualPose(transform));
    }

    public void Bend(Vector3 awayFromContact)
    {
        if (visuals.Count == 0)
            CacheVisuals();
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(Animate(awayFromContact));
    }

    IEnumerator Animate(Vector3 awayFromContact)
    {
        const float pressDuration = .14f;
        for (float elapsed = 0f; elapsed < pressDuration;
             elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f,
                elapsed / pressDuration);
            ApplyPose(awayFromContact, t, t);
            yield return null;
        }

        // Damped spring: the branches overshoot slightly in the opposite
        // direction before settling back into their exact original pose.
        const float recoveryDuration = 1.35f;
        for (float elapsed = 0f; elapsed < recoveryDuration;
             elapsed += Time.deltaTime)
        {
            float normalized = elapsed / recoveryDuration;
            float envelope = Mathf.Exp(-3.4f * normalized);
            float spring = Mathf.Cos(normalized * Mathf.PI * 4.5f) *
                           envelope;
            ApplyPose(awayFromContact, spring, Mathf.Abs(spring));
            yield return null;
        }

        Restore();
        routine = null;
    }

    void ApplyPose(Vector3 worldAway, float bendAmount,
        float compressionAmount)
    {
        for (int i = 0; i < visuals.Count; i++)
        {
            VisualPose pose = visuals[i];
            if (pose.transform == null) continue;
            Transform parent = pose.transform.parent;
            Vector3 localAway = parent != null
                ? parent.InverseTransformDirection(worldAway).normalized
                : worldAway.normalized;
            Vector3 axis = Vector3.Cross(Vector3.up, localAway).normalized;
            float variation = 1f + (i % 3) * .09f;
            Quaternion bend = Quaternion.AngleAxis(
                18f * bendAmount * variation, axis);
            pose.transform.localRotation = pose.rotation * bend;

            float compression = Mathf.Clamp01(compressionAmount);
            pose.transform.localScale = Vector3.Scale(pose.scale,
                new Vector3(1f + .09f * compression,
                    1f - .2f * compression,
                    1f + .09f * compression));
            pose.transform.localPosition = pose.position +
                localAway * (.1f * bendAmount * variation) +
                Vector3.down * (.035f * compression);
        }
    }

    void Restore()
    {
        foreach (VisualPose pose in visuals)
        {
            if (pose.transform == null) continue;
            pose.transform.localPosition = pose.position;
            pose.transform.localRotation = pose.rotation;
            pose.transform.localScale = pose.scale;
        }
    }

    void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        Restore();
    }

    readonly struct VisualPose
    {
        public readonly Transform transform;
        public readonly Vector3 position;
        public readonly Quaternion rotation;
        public readonly Vector3 scale;

        public VisualPose(Transform transform)
        {
            this.transform = transform;
            position = transform.localPosition;
            rotation = transform.localRotation;
            scale = transform.localScale;
        }
    }
}

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Invisible in-game (no renderer, nothing to see at runtime) but the OnDrawGizmos sphere below
// only ever draws in the Scene view, so it stays visible/selectable in the Editor. By default it
// tries to add one goblin every spawnInterval seconds while the local population is below the cap.
// Every pinkGoblinEvery-th spawn is a stronger pink variant (2x health/attack) built at runtime.
public class GoblinSpawner : MonoBehaviour
{
    [Header("Initial trigger")]
    [SerializeField] bool requireInitialGoblinsDefeated;
    [SerializeField] EnemyStats[] initialGoblins;
    [SerializeField] float activationDelay;

    [Header("Spawning")]
    [SerializeField] GameObject[] goblinPrefabs;
    [SerializeField] float spawnRadius = 10f;
    [SerializeField] int maxGoblinsInRadius = 10;
    [SerializeField] float spawnInterval = 10f;
    [SerializeField] int pinkGoblinEvery = 5;
    [SerializeField] Color pinkTint = new Color(1f, 0.35f, 0.75f);

    [Header("Elite goblins")]
    [SerializeField, Range(0f, 1f)] float eliteChance = 0.05f;
    [SerializeField] float eliteStatMultiplier = 10f;
    [SerializeField] Color eliteTint = new Color(0.5f, 0.01f, 0.01f);
    [SerializeField] float eliteStarHeight = 2.6f;

    bool activated;
    float activationTime;
    float nextSpawnTime;
    int spawnedCount;
    Terrain terrain;
    bool prefabsResolved;
    bool nightSpawnBoost;
    bool bloodMoonSpawnBoost;
    Transform bloodMoonFollowTarget;

    static readonly string[] FallbackPrefabPaths =
    {
        "Assets/_RPG/Prefabs/Enemies/GoblinEnemy1.prefab",
        "Assets/_RPG/Prefabs/Enemies/GoblinEnemy2.prefab",
        "Assets/_RPG/Prefabs/Enemies/GoblinEnemy3.prefab",
    };

    void Awake()
    {
        terrain = Terrain.activeTerrain;
        activated = !requireInitialGoblinsDefeated || AllInitialGoblinsDead();
        activationTime = Time.time + Mathf.Max(0f, activationDelay);
        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
    }

    void Update()
    {
        if (bloodMoonFollowTarget != null)
            transform.position = bloodMoonFollowTarget.position;

        if (!activated)
        {
            if (!AllInitialGoblinsDead())
                return;

            activated = true;
            activationTime = Time.time + Mathf.Max(0f, activationDelay);
            nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
            return;
        }

        if (Time.time < activationTime || Time.time < nextSpawnTime)
            return;

        float frequencyMultiplier = bloodMoonSpawnBoost
            ? 4.5f
            : nightSpawnBoost ? 2.4f : 1f;
        nextSpawnTime = Time.time + spawnInterval / frequencyMultiplier;
        TrySpawn();
    }

    bool AllInitialGoblinsDead()
    {
        if (initialGoblins == null || initialGoblins.Length == 0)
            return true;

        foreach (EnemyStats g in initialGoblins)
        {
            if (g != null && !g.IsDead)
                return false;
        }
        return true;
    }

    void TrySpawn()
    {
        ResolvePrefabsIfNeeded();
        if (goblinPrefabs == null || goblinPrefabs.Length == 0)
            return;
        int populationMultiplier = bloodMoonSpawnBoost
            ? 3
            : nightSpawnBoost ? 2 : 1;
        if (CountGoblinsInRadius() >= maxGoblinsInRadius * populationMultiplier)
            return;

        Vector3 position = FindFlatSpawnPosition();

        GameObject prefab = goblinPrefabs[Random.Range(0, goblinPrefabs.Length)];
        GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        SpawnedEnemyLifetimeLimiter.Ensure(instance, transform.position);
        spawnedCount++;

        if (eliteChance > 0f && Random.value < eliteChance)
            MakeElite(instance);
        // The elite roll always happens first and keeps the exact normal spawner chance.
        // During a Blood Moon most non-elites are pink, without turning elites pink or
        // modifying their established rarity.
        else if (bloodMoonSpawnBoost ? Random.value < .78f :
                 pinkGoblinEvery > 0 && spawnedCount % pinkGoblinEvery == 0)
            MakePink(instance);
        NightEnemyEventManager.Instance?.ApplySpawnModifiers(instance);
    }

    public void SetNightSpawnBoost(bool enabled) => nightSpawnBoost = enabled;

    public void SetBloodMoonSpawnBoost(bool enabled)
    {
        bloodMoonSpawnBoost = enabled;
        if (enabled)
            nextSpawnTime = Mathf.Min(nextSpawnTime,
                Time.time + Random.Range(.35f, 1.2f));
    }

    // Creates a lightweight runtime spawner using the exact same prefabs and elite settings.
    // This is used for the village/player pressure of the Blood Moon and destroyed afterwards.
    public GoblinSpawner CreateBloodMoonSatellite(string objectName,
        Vector3 centre, Transform followTarget, float radius, int localBaseCap)
    {
        ResolvePrefabsIfNeeded();
        if (goblinPrefabs == null || goblinPrefabs.Length == 0)
            return null;

        GameObject root = new GameObject(objectName);
        root.transform.position = centre;
        GoblinSpawner satellite = root.AddComponent<GoblinSpawner>();
        satellite.requireInitialGoblinsDefeated = false;
        satellite.initialGoblins = null;
        satellite.activationDelay = 0f;
        satellite.goblinPrefabs = goblinPrefabs;
        satellite.spawnRadius = Mathf.Max(8f, radius);
        satellite.maxGoblinsInRadius = Mathf.Max(3, localBaseCap);
        satellite.spawnInterval = Mathf.Max(2.5f, spawnInterval);
        satellite.pinkGoblinEvery = pinkGoblinEvery;
        satellite.pinkTint = pinkTint;
        satellite.eliteChance = eliteChance;
        satellite.eliteStatMultiplier = eliteStatMultiplier;
        satellite.eliteTint = eliteTint;
        satellite.eliteStarHeight = eliteStarHeight;
        satellite.bloodMoonFollowTarget = followTarget;
        satellite.SetBloodMoonSpawnBoost(true);
        return satellite;
    }

    Vector3 FindFlatSpawnPosition()
    {
        Vector3 best = transform.position;
        float bestSlope = 90f;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);
            if (terrain == null) return candidate;
            candidate.y = terrain.SampleHeight(candidate) + terrain.transform.position.y;
            Vector3 normal = terrain.terrainData.GetInterpolatedNormal(
                Mathf.InverseLerp(terrain.transform.position.x, terrain.transform.position.x + terrain.terrainData.size.x, candidate.x),
                Mathf.InverseLerp(terrain.transform.position.z, terrain.transform.position.z + terrain.terrainData.size.z, candidate.z));
            float slope = Vector3.Angle(normal, Vector3.up);
            if (slope < bestSlope) { bestSlope = slope; best = candidate; }
            if (slope <= 12f) break;
        }
        return best;
    }

    void ResolvePrefabsIfNeeded()
    {
        if (prefabsResolved)
            return;
        prefabsResolved = true;

        if (goblinPrefabs != null && goblinPrefabs.Length > 0)
            return;

#if UNITY_EDITOR
        var loaded = new System.Collections.Generic.List<GameObject>();
        foreach (string path in FallbackPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                loaded.Add(prefab);
        }
        if (loaded.Count > 0)
            goblinPrefabs = loaded.ToArray();
#endif
    }

    int CountGoblinsInRadius()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, spawnRadius);
        int count = 0;
        foreach (Collider hit in hits)
        {
            EnemyStats stats = hit.GetComponentInParent<EnemyStats>();
            if (stats != null && !stats.IsDead && stats.gameObject.name.Contains("Goblin"))
                count++;
        }
        return count;
    }

    void MakePink(GameObject instance)
    {
        instance.name += "_Pink";
        instance.GetComponent<EnemyStats>()?.ApplyStatMultiplier(2f, 2f);
        TintGoblin(instance, pinkTint);
    }

    void MakeElite(GameObject instance)
    {
        instance.name += "_Elite";
        EnemyStats stats = instance.GetComponent<EnemyStats>();
        stats?.ApplyStatMultiplier(eliteStatMultiplier, eliteStatMultiplier);
        if (stats != null)
            stats.xpReward = Mathf.RoundToInt(stats.xpReward * eliteStatMultiplier);
        TintGoblin(instance, eliteTint);
        SpawnEliteStar(instance);
        EliteEnemyAura.Ensure(instance);
    }

    void TintGoblin(GameObject instance, Color tint)
    {
        foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material[] mats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                Material tinted = new Material(smr.sharedMaterials[i]);
                if (tinted.HasProperty("_BaseColor")) tinted.SetColor("_BaseColor", tint);
                if (tinted.HasProperty("_Color")) tinted.SetColor("_Color", tint);
                mats[i] = tinted;
            }
            smr.materials = mats;
        }
    }

    void SpawnEliteStar(GameObject instance)
    {
        GameObject go = new GameObject("EliteStarIcon");
        go.transform.SetParent(instance.transform, false);
        go.transform.localPosition = Vector3.up * eliteStarHeight;
        go.transform.localScale = Vector3.one * 0.35f;

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.mesh = BuildStarMesh();

        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        Color gold = new Color(1f, 0.82f, 0.1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gold);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", gold);
        meshRenderer.sharedMaterial = mat;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        go.AddComponent<BillboardToCamera>();
    }

    // Text glyphs for "★" aren't guaranteed to exist in the project's font asset (they rendered
    // as a solid missing-glyph box instead of a star), so build the shape directly as a mesh -
    // a 5-pointed star polygon fanned from the center, with both triangle windings so it's
    // visible from either side without depending on shader-specific culling settings.
    static Mesh BuildStarMesh()
    {
        const int spikes = 5;
        const float outerRadius = 1f;
        const float innerRadius = 0.42f;
        int totalRingPoints = spikes * 2;

        var vertices = new System.Collections.Generic.List<Vector3> { Vector3.zero };
        for (int i = 0; i < totalRingPoints; i++)
        {
            float angle = Mathf.PI / 2f + i * Mathf.PI / spikes;
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        var triangles = new System.Collections.Generic.List<int>();
        for (int i = 0; i < totalRingPoints; i++)
        {
            int a = 0;
            int b = i + 1;
            int c = (i + 1) % totalRingPoints + 1;
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(b);
        }

        Mesh mesh = new Mesh { name = "EliteStar" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = activated ? new Color(1f, 0.5f, 0.5f, 0.25f) : new Color(0.4f, 0.7f, 1f, 0.25f);
        Gizmos.DrawSphere(transform.position, spawnRadius);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}

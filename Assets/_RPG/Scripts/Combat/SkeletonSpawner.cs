using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Same shape as GoblinSpawner (invisible at runtime, gizmo sphere only in the Scene view, tries
// to add one skeleton every spawnInterval seconds while the local population is below the cap),
// extended with three independent star-rarity tiers instead of a single elite tier. Rolled
// rarest-first so a lucky 3-star roll can't also "double up" as a 1-star or 2-star spawn.
public class SkeletonSpawner : MonoBehaviour
{
    [Header("Initial trigger")]
    [SerializeField] bool requireInitialSkeletonsDefeated;
    [SerializeField] EnemyStats[] initialSkeletons;
    [SerializeField] float activationDelay;

    [Header("Spawning")]
    [SerializeField] GameObject[] skeletonPrefabs;
    [SerializeField] float spawnRadius = 10f;
    [SerializeField] int maxSkeletonsInRadius = 5;
    [SerializeField] float spawnInterval = 10f;

    [Header("Every-10th variant (2x damage, red/violet tint)")]
    [SerializeField] int strongSkeletonEvery = 10;
    [SerializeField] float strongDamageMultiplier = 2f;
    [SerializeField] Color strongTint = new Color(0.55f, 0.05f, 0.35f);

    [Header("Star-rarity tiers (checked rarest first)")]
    [SerializeField, Range(0f, 1f)] float star3Chance = 0.000001f; // "0000.1%" per the request
    [SerializeField] float star3Multiplier = 100f;
    [SerializeField, Range(0f, 1f)] float star2Chance = 0.01f;
    [SerializeField] float star2Multiplier = 50f;
    [SerializeField, Range(0f, 1f)] float star1Chance = 0.05f;
    [SerializeField] float star1Multiplier = 10f;
    [SerializeField] Color starTint = new Color(0.55f, 0.01f, 0.01f);
    [SerializeField] float starIconHeight = 2.6f;
    [SerializeField] float starIconSpacing = 0.4f;

    bool activated;
    float activationTime;
    float nextSpawnTime;
    int spawnedCount;
    Terrain terrain;
    bool prefabsResolved;
    bool nightSpawnBoost;
    float nextQuestEliteAttempt;

    static readonly string[] FallbackPrefabPaths =
    {
        "Assets/_RPG/Prefabs/Enemies/SkeletonEnemy.prefab",
    };

    void Awake()
    {
        terrain = Terrain.activeTerrain;
        activated = !requireInitialSkeletonsDefeated || AllInitialSkeletonsDead();
        activationTime = Time.time + Mathf.Max(0f, activationDelay);
        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
    }

    void Update()
    {
        TrySpawnRequiredQuestElite();
        if (!activated)
        {
            if (!AllInitialSkeletonsDead())
                return;

            activated = true;
            activationTime = Time.time + Mathf.Max(0f, activationDelay);
            nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
            return;
        }

        if (Time.time < activationTime || Time.time < nextSpawnTime)
            return;

        nextSpawnTime = Time.time + spawnInterval / (nightSpawnBoost ? 2.4f : 1f);
        TrySpawn();
    }

    bool AllInitialSkeletonsDead()
    {
        if (initialSkeletons == null || initialSkeletons.Length == 0)
            return true;

        foreach (EnemyStats s in initialSkeletons)
        {
            if (s != null && !s.IsDead)
                return false;
        }
        return true;
    }

    void TrySpawn()
    {
        ResolvePrefabsIfNeeded();
        if (skeletonPrefabs == null || skeletonPrefabs.Length == 0)
            return;
        if (CountSkeletonsInRadius() >= maxSkeletonsInRadius * (nightSpawnBoost ? 2 : 1))
            return;

        Vector3 position = FindFlatSpawnPosition();

        GameObject prefab = skeletonPrefabs[Random.Range(0, skeletonPrefabs.Length)];
        GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        SpawnedEnemyLifetimeLimiter.Ensure(instance, transform.position);
        spawnedCount++;

        // Rarest first - a 3-star roll shouldn't also register as a cheaper 1-star/2-star spawn,
        // and a star-rarity spawn takes priority over the every-10th strong variant rather than
        // stacking with it (matches GoblinSpawner's elite-vs-pink precedence).
        if (star3Chance > 0f && Random.value < star3Chance)
            MakeStarVariant(instance, 3, star3Multiplier);
        else if (star2Chance > 0f && Random.value < star2Chance)
            MakeStarVariant(instance, 2, star2Multiplier);
        else if (star1Chance > 0f && Random.value < star1Chance)
            MakeStarVariant(instance, 1, star1Multiplier);
        else if (strongSkeletonEvery > 0 && spawnedCount % strongSkeletonEvery == 0)
            MakeStrongVariant(instance);
        NightEnemyEventManager.Instance?.ApplySpawnModifiers(instance);
    }

    public void SetNightSpawnBoost(bool enabled) => nightSpawnBoost = enabled;

    void TrySpawnRequiredQuestElite()
    {
        QuestManager quests = QuestManager.Instance;
        if (quests == null || quests.MerchantIntroductionState != PrimaryQuestState.HuntEliteSkeletonAtNight ||
            !NightEnemyEventManager.IsNightNow || Time.time < nextQuestEliteAttempt) return;
        nextQuestEliteAttempt = Time.time + 4f;
        if (FindAnyObjectByType<QuestEliteSkeletonMarker>() != null || FindAnyObjectByType<DungeonKeyPickup>() != null) return;
        ResolvePrefabsIfNeeded();
        if (skeletonPrefabs == null || skeletonPrefabs.Length == 0) return;
        Vector3 position = FindFlatSpawnPosition();
        GameObject elite = Instantiate(skeletonPrefabs[0], position, Quaternion.Euler(0f, Random.Range(0f,360f),0f));
        elite.name = "Skeleton_QuestElite_1Star";
        elite.GetComponent<EnemyStats>()?.ApplyStatMultiplier(3f, 3f);
        TintSkeleton(elite, starTint);
        SpawnStarIcons(elite, 1);
        elite.AddComponent<QuestEliteSkeletonMarker>();
        EliteEnemyAura.Ensure(elite);
        NightEnemyEventManager.Instance?.ApplySpawnModifiers(elite);
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

        if (skeletonPrefabs != null && skeletonPrefabs.Length > 0)
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
            skeletonPrefabs = loaded.ToArray();
#endif
    }

    int CountSkeletonsInRadius()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, spawnRadius);
        int count = 0;
        foreach (Collider hit in hits)
        {
            EnemyStats stats = hit.GetComponentInParent<EnemyStats>();
            if (stats != null && !stats.IsDead && stats.gameObject.name.Contains("Skeleton"))
                count++;
        }
        return count;
    }

    // Only the attack stat gets the damage multiplier here - "2x damage" per the request, not 2x
    // health too (that's what separates this from the star tiers, which scale both).
    void MakeStrongVariant(GameObject instance)
    {
        instance.name += "_Strong";
        instance.GetComponent<EnemyStats>()?.ApplyStatMultiplier(1f, strongDamageMultiplier);
        TintSkeleton(instance, strongTint);
    }

    void MakeStarVariant(GameObject instance, int starCount, float multiplier)
    {
        instance.name += "_" + starCount + "Star";
        EnemyStats stats = instance.GetComponent<EnemyStats>();
        stats?.ApplyStatMultiplier(multiplier, multiplier);
        if (stats != null)
            stats.xpReward = Mathf.RoundToInt(stats.xpReward * multiplier);
        TintSkeleton(instance, starTint);
        SpawnStarIcons(instance, starCount);
        EliteEnemyAura.Ensure(instance);
    }

    void TintSkeleton(GameObject instance, Color tint)
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

    void SpawnStarIcons(GameObject instance, int count)
    {
        // Centered row of `count` stars above the head, same billboarded-gold-star look as
        // GoblinSpawner's single elite star.
        float totalWidth = (count - 1) * starIconSpacing;
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("StarIcon" + i);
            go.transform.SetParent(instance.transform, false);
            float x = -totalWidth * 0.5f + i * starIconSpacing;
            go.transform.localPosition = new Vector3(x, starIconHeight, 0f);
            go.transform.localScale = Vector3.one * 0.3f;

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
    }

    // Same star-polygon-as-mesh approach as GoblinSpawner - TMP's font asset has no guaranteed
    // "★" glyph, so the shape is built directly instead of relying on text.
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

        Mesh mesh = new Mesh { name = "SkeletonStar" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = activated ? new Color(0.6f, 0.6f, 0.7f, 0.25f) : new Color(0.4f, 0.7f, 1f, 0.25f);
        Gizmos.DrawSphere(transform.position, spawnRadius);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Adds one Superior Tree Anomaly every two minutes until the living population
// inside its 20 m encounter area reaches five.
public class SuperiorTreeAnomalySpawner : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] GameObject superiorAnomalyPrefab;
    [SerializeField] float spawnRadius = 20f;
    [SerializeField] int maxAnomaliesInRadius = 5;
    [SerializeField] float spawnInterval = 120f;

    [Header("Elite chances")]
    [SerializeField, Range(0f, 1f)] float oneStarChance = 0.05f;
    [SerializeField, Range(0f, 1f)] float twoStarChance = 0.01f;
    [SerializeField, Range(0f, 1f)] float threeStarChance = 0.001f;
    [SerializeField] float oneStarMultiplier = 10f;
    [SerializeField] float twoStarMultiplier = 50f;
    [SerializeField] float threeStarMultiplier = 100f;
    [SerializeField] float starHeight = 2.75f;

    const string FallbackPrefabPath = "Assets/_RPG/Prefabs/Enemies/SuperiorTreeAnomaly.prefab";

    float nextSpawnTime;
    Terrain terrain;
    static Mesh starMesh;
    static Material starMaterial;

    void Awake()
    {
        terrain = Terrain.activeTerrain;
        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
    }

    void Update()
    {
        if (Time.time < nextSpawnTime)
            return;

        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
        TrySpawn();
    }

    void TrySpawn()
    {
        ResolvePrefabIfNeeded();
        if (superiorAnomalyPrefab == null || CountLivingAnomalies() >= maxAnomaliesInRadius)
            return;

        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        Vector3 position = transform.position + new Vector3(offset.x, 0f, offset.y);
        position = ProjectToGround(position);

        GameObject instance = Instantiate(superiorAnomalyPrefab, position,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        instance.hideFlags = HideFlags.None;
        instance.name = "SuperiorTreeAnomaly_Spawned";
        ApplyEliteRoll(instance);
        instance.SetActive(true);
    }

    Vector3 ProjectToGround(Vector3 position)
    {
        if (GroundUtility.TryProjectToGround(position, transform, out Vector3 grounded, 8f, 16f))
            return grounded;

        if (terrain != null)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        return position;
    }

    int CountLivingAnomalies()
    {
        int count = 0;
        SuperiorTreeAnomalyAI[] anomalies = FindObjectsByType<SuperiorTreeAnomalyAI>();
        foreach (SuperiorTreeAnomalyAI anomaly in anomalies)
        {
            Vector3 delta = anomaly.transform.position - transform.position;
            delta.y = 0f;
            EnemyStats stats = anomaly.GetComponent<EnemyStats>();
            if (delta.sqrMagnitude <= spawnRadius * spawnRadius && stats != null && !stats.IsDead)
                count++;
        }
        return count;
    }

    void ApplyEliteRoll(GameObject instance)
    {
        float roll = Random.value;
        int stars = 0;
        float multiplier = 1f;

        // Exclusive probabilities: 0.1% three-star, 1% two-star, 5% one-star.
        if (roll < threeStarChance)
        {
            stars = 3;
            multiplier = threeStarMultiplier;
        }
        else if (roll < threeStarChance + twoStarChance)
        {
            stars = 2;
            multiplier = twoStarMultiplier;
        }
        else if (roll < threeStarChance + twoStarChance + oneStarChance)
        {
            stars = 1;
            multiplier = oneStarMultiplier;
        }

        if (stars == 0)
            return;

        instance.name = $"SuperiorTreeAnomaly_{stars}Star";
        EnemyStats stats = instance.GetComponent<EnemyStats>();
        if (stats != null)
        {
            stats.ApplyStatMultiplier(multiplier, multiplier);
            stats.ApplyExperienceMultiplier(multiplier);
        }
        AddStarMarkers(instance, stars);
        EliteEnemyAura.Ensure(instance);
    }

    void AddStarMarkers(GameObject instance, int count)
    {
        GameObject group = new GameObject("EliteStars");
        group.transform.SetParent(instance.transform, false);
        group.transform.localPosition = Vector3.up * starHeight;
        group.AddComponent<BillboardToCamera>();

        float spacing = 0.48f;
        for (int i = 0; i < count; i++)
        {
            GameObject star = new GameObject($"Star_{i + 1}");
            star.transform.SetParent(group.transform, false);
            star.transform.localPosition = Vector3.right * ((i - (count - 1) * 0.5f) * spacing);
            star.transform.localScale = Vector3.one * 0.3f;
            star.AddComponent<MeshFilter>().sharedMesh = GetStarMesh();
            MeshRenderer renderer = star.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetStarMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    static Mesh GetStarMesh()
    {
        if (starMesh != null) return starMesh;

        const int spikes = 5;
        var vertices = new List<Vector3> { Vector3.zero };
        for (int i = 0; i < spikes * 2; i++)
        {
            float angle = Mathf.PI / 2f + i * Mathf.PI / spikes;
            float radius = i % 2 == 0 ? 1f : 0.42f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        var triangles = new List<int>();
        for (int i = 0; i < spikes * 2; i++)
        {
            int next = (i + 1) % (spikes * 2) + 1;
            triangles.Add(0); triangles.Add(i + 1); triangles.Add(next);
            triangles.Add(0); triangles.Add(next); triangles.Add(i + 1);
        }

        starMesh = new Mesh { name = "SuperiorAnomalyEliteStar" };
        starMesh.SetVertices(vertices);
        starMesh.SetTriangles(triangles, 0);
        starMesh.RecalculateNormals();
        return starMesh;
    }

    static Material GetStarMaterial()
    {
        if (starMaterial != null) return starMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        starMaterial = new Material(shader) { name = "SuperiorAnomalyStar_Gold" };
        Color gold = new Color(1f, 0.82f, 0.08f, 1f);
        if (starMaterial.HasProperty("_BaseColor")) starMaterial.SetColor("_BaseColor", gold);
        if (starMaterial.HasProperty("_Color")) starMaterial.SetColor("_Color", gold);
        return starMaterial;
    }

    void ResolvePrefabIfNeeded()
    {
        if (superiorAnomalyPrefab != null) return;
#if UNITY_EDITOR
        superiorAnomalyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackPrefabPath);
#endif
    }

    // SpawnVillage can be left open and dirty while scripts recompile. If that
    // prevents the editor installer from persisting the object, create the same
    // spawner at runtime from the already placed Superior Anomaly.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRuntimeSpawner()
    {
        if (SceneManager.GetActiveScene().name != "SpawnVillage" ||
            FindAnyObjectByType<SuperiorTreeAnomalySpawner>() != null)
            return;

        SuperiorTreeAnomalyAI source = FindAnyObjectByType<SuperiorTreeAnomalyAI>();
        if (source == null)
            return;

        GameObject spawnerObject = new GameObject("SuperiorTreeAnomalySpawner");
        spawnerObject.transform.position = source.transform.position;
        SuperiorTreeAnomalySpawner spawner = spawnerObject.AddComponent<SuperiorTreeAnomalySpawner>();

        // Keep an inactive private template so the spawner still works if the
        // initially placed anomaly is killed before the first two-minute tick.
        GameObject template = Instantiate(source.gameObject, spawnerObject.transform);
        template.name = "SuperiorTreeAnomaly_RuntimeTemplate";
        template.hideFlags = HideFlags.HideInHierarchy;
        template.SetActive(false);
        spawner.superiorAnomalyPrefab = template;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.65f, 0.2f, 0.95f, 0.18f);
        Gizmos.DrawSphere(transform.position, spawnRadius);
        Gizmos.color = new Color(0.75f, 0.35f, 1f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}

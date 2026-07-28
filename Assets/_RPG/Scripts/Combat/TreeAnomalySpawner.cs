using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Local population spawner for Tree Anomalies.  It creates at most one anomaly per interval and
// only while fewer than five living anomalies occupy its 10 m encounter circle.
public class TreeAnomalySpawner : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] GameObject treeAnomalyPrefab;
    [SerializeField] float spawnRadius = 10f;
    [SerializeField] int maxAnomaliesInRadius = 5;
    [SerializeField] float spawnInterval = 20f;

    [Header("One-star anomaly")]
    [SerializeField, Range(0f, 1f)] float oneStarChance = 0.05f;
    [SerializeField] float oneStarMultiplier = 10f;
    [SerializeField] float starHeight = 2.35f;

    float nextSpawnTime;
    Terrain terrain;

    const string FallbackPrefabPath = "Assets/_RPG/Prefabs/Enemies/TreeAnomaly.prefab";

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
        if (treeAnomalyPrefab == null || CountAnomaliesInRadius() >= maxAnomaliesInRadius)
            return;

        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        Vector3 position = transform.position + new Vector3(offset.x, 0f, offset.y);
        if (terrain != null)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;

        GameObject instance = Instantiate(treeAnomalyPrefab, position,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        instance.name = "TreeAnomaly_Spawned";

        if (oneStarChance > 0f && Random.value < oneStarChance)
            MakeOneStar(instance);
    }

    void ResolvePrefabIfNeeded()
    {
        if (treeAnomalyPrefab != null)
            return;

#if UNITY_EDITOR
        treeAnomalyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackPrefabPath);
#endif
    }

    int CountAnomaliesInRadius()
    {
        var found = new HashSet<EnemyStats>();
        foreach (Collider hit in Physics.OverlapSphere(transform.position, spawnRadius))
        {
            EnemyStats stats = hit.GetComponentInParent<EnemyStats>();
            if (stats != null && !stats.IsDead && stats.gameObject.name.Contains("TreeAnomaly"))
                found.Add(stats);
        }
        return found.Count;
    }

    void MakeOneStar(GameObject instance)
    {
        instance.name = "TreeAnomaly_1Star";
        EnemyStats stats = instance.GetComponent<EnemyStats>();
        if (stats != null)
        {
            stats.ApplyStatMultiplier(oneStarMultiplier, oneStarMultiplier);
            stats.ApplyRewardMultiplier(oneStarMultiplier);
        }

        GameObject star = new GameObject("OneStarIcon");
        star.transform.SetParent(instance.transform, false);
        star.transform.localPosition = Vector3.up * starHeight;
        star.transform.localScale = Vector3.one * 0.34f;
        MeshFilter filter = star.AddComponent<MeshFilter>();
        filter.mesh = BuildStarMesh();
        MeshRenderer renderer = star.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        Color gold = new Color(1f, 0.82f, 0.1f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", gold);
        if (material.HasProperty("_Color")) material.SetColor("_Color", gold);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        star.AddComponent<BillboardToCamera>();
        EliteEnemyAura.Ensure(instance);
    }

    static Mesh BuildStarMesh()
    {
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

        Mesh mesh = new Mesh { name = "TreeAnomalyStar" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.35f, 0.95f, 0.5f, 0.22f);
        Gizmos.DrawSphere(transform.position, spawnRadius);
        Gizmos.color = new Color(0.35f, 0.95f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}

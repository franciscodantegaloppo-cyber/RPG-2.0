using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] GameObject enemyPrefab;
    [SerializeField] int maxEnemies = 5;
    [SerializeField] float spawnRadius = 5f;
    [SerializeField] float respawnDelay = 10f;

    int activeCount;

    void Start()
    {
        SpawnAll();
    }

    void SpawnAll()
    {
        for (int i = 0; i < maxEnemies; i++)
            SpawnOne();
    }

    void SpawnOne()
    {
        if (enemyPrefab == null) return;

        Vector2 rand = Random.insideUnitCircle * spawnRadius;
        Vector3 pos = transform.position + new Vector3(rand.x, 0f, rand.y);

        var enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);
        activeCount++;

        var es = enemy.GetComponent<EnemyStats>();
        if (es != null)
            es.OnDeath += () => StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        activeCount--;
        yield return new WaitForSeconds(respawnDelay);
        SpawnOne();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}

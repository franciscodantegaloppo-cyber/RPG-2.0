using UnityEngine;

// Runtime spawners use a local population cap. Enemies that chased the player far away
// stopped counting toward that cap, so a long session could accumulate hundreds of active
// AIs. This retires only ordinary runtime-spawned enemies when they are old and far from
// both their spawn area and the player. Bosses and hand-placed quest enemies never receive it.
public sealed class SpawnedEnemyLifetimeLimiter : MonoBehaviour
{
    Vector3 spawnOrigin;
    float retireAfter;
    float maxDistanceFromOrigin;
    float createdAt;
    float nextCheck;
    Transform player;
    EnemyStats stats;

    public static void Ensure(GameObject enemy, Vector3 origin,
        float lifetimeSeconds = 240f, float maximumDistance = 75f)
    {
        if (enemy == null) return;
        SpawnedEnemyLifetimeLimiter limiter =
            enemy.GetComponent<SpawnedEnemyLifetimeLimiter>() ??
            enemy.AddComponent<SpawnedEnemyLifetimeLimiter>();
        limiter.spawnOrigin = origin;
        limiter.retireAfter = Mathf.Max(60f, lifetimeSeconds);
        limiter.maxDistanceFromOrigin = Mathf.Max(35f, maximumDistance);
        limiter.createdAt = Time.time;
        limiter.nextCheck = Time.time + Random.Range(8f, 14f);
    }

    void Awake() => stats = GetComponent<EnemyStats>();

    void Update()
    {
        if (Time.time < nextCheck || stats == null || stats.IsDead) return;
        nextCheck = Time.time + Random.Range(4f, 7f);
        if (Time.time - createdAt < retireAfter &&
            (transform.position - spawnOrigin).sqrMagnitude <=
            maxDistanceFromOrigin * maxDistanceFromOrigin)
            return;

        if (player == null)
        {
            PlayerStats found = FindAnyObjectByType<PlayerStats>();
            if (found != null) player = found.transform;
        }

        // Never remove something the player can currently see/fight.
        if (player != null &&
            (transform.position - player.position).sqrMagnitude <= 38f * 38f)
            return;

        Destroy(gameObject);
    }
}

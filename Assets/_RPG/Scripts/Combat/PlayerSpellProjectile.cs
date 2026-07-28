using UnityEngine;

// Lightweight player-owned projectile. Fireball supplies its visual; this component supplies
// enemy collision so the same VFX can be used by player spells without harming the player.
public class PlayerSpellProjectile : MonoBehaviour
{
    Vector3 direction;
    float damage;
    float speed;
    float expireAt;
    Transform owner;
    Transform collisionIgnoreRoot;
    EnemyStats intendedTarget;
    readonly Collider[] hitBuffer = new Collider[12];
    readonly RaycastHit[] castBuffer = new RaycastHit[16];

    public void Launch(Vector3 travelDirection, float spellDamage, float travelSpeed = 18f,
        Transform spellOwner = null, EnemyStats requiredTarget = null, Transform ignoredWorldRoot = null)
    {
        direction = travelDirection.normalized;
        damage = spellDamage;
        speed = travelSpeed;
        owner = spellOwner;
        collisionIgnoreRoot = ignoredWorldRoot;
        intendedTarget = requiredTarget;
        expireAt = Time.time + 4f;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    void Update()
    {
        float step = speed * Time.deltaTime;
        int castCount = Physics.SphereCastNonAlloc(transform.position, .34f, direction, castBuffer, step,
            ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);
        RaycastHit worldHit = default;
        bool foundWorldHit = false;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < castCount; i++)
        {
            Collider candidate = castBuffer[i].collider;
            if (candidate == null || (owner != null && candidate.transform.IsChildOf(owner)) ||
                (collisionIgnoreRoot != null &&
                 (candidate.transform == collisionIgnoreRoot || candidate.transform.IsChildOf(collisionIgnoreRoot))))
                continue;
            EnemyStats candidateEnemy = candidate.GetComponentInParent<EnemyStats>();
            if (intendedTarget != null && candidateEnemy != null && candidateEnemy != intendedTarget) continue;
            if (castBuffer[i].distance >= nearestDistance) continue;
            nearestDistance = castBuffer[i].distance;
            worldHit = castBuffer[i];
            foundWorldHit = true;
        }
        if (foundWorldHit)
        {
            EnemyStats enemy = worldHit.collider.GetComponentInParent<EnemyStats>();
            if (enemy != null) Hit(enemy);
            else Destroy(gameObject);
            return;
        }
        transform.position += direction * step;
        int count = Physics.OverlapSphereNonAlloc(transform.position, .8f, hitBuffer, 1 << LayerMask.NameToLayer("Enemy"));
        for (int i = 0; i < count; i++)
        {
            EnemyStats enemy = hitBuffer[i] != null ? hitBuffer[i].GetComponentInParent<EnemyStats>() : null;
            if (intendedTarget != null && enemy != intendedTarget) continue;
            if (enemy != null) { Hit(enemy); return; }
        }
        if (Time.time >= expireAt) Destroy(gameObject);
    }

    void Hit(EnemyStats enemy)
    {
        enemy.TakeDamage(damage, transform.position);
        DamageNumberPool.Instance?.ShowMagicDamage(enemy.transform.position + Vector3.up, damage);
        Destroy(gameObject);
    }
}

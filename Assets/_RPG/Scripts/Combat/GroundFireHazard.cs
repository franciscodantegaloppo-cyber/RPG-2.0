using UnityEngine;

// Temporary damaging fire left by the Anomalous Demon's giant fireball.
public class GroundFireHazard : MonoBehaviour
{
    const string FireVfxResource = "VFX/FreeFireProjectileVFX";
    float endTime;
    float damagePerTick;
    float radius;
    float nextDamageTime;

    public static void Create(Vector3 position, float duration, float damage, float fireRadius)
    {
        GameObject fire = new GameObject("DemonioAnomalo_GroundFire");
        fire.transform.position = position;
        GroundFireHazard hazard = fire.AddComponent<GroundFireHazard>();
        hazard.endTime = Time.time + duration;
        hazard.damagePerTick = damage;
        hazard.radius = fireRadius;

        GameObject prefab = Resources.Load<GameObject>(FireVfxResource);
        if (prefab != null)
        {
            GameObject visual = Instantiate(prefab, fire.transform, false);
            visual.name = "GroundFire_FreeFireVFX";
            visual.transform.localScale = Vector3.one * fireRadius * 1.35f;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            foreach (ParticleSystem particles in visual.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                particles.Play(true);
            }
        }

        Light glow = fire.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.16f, 0.02f);
        glow.range = fireRadius * 4.5f;
        glow.intensity = 5f;
        glow.shadows = LightShadows.None;
    }

    void Update()
    {
        if (Time.time >= endTime) { Destroy(gameObject); return; }
        if (Time.time < nextDamageTime) return;
        nextDamageTime = Time.time + 0.75f;

        foreach (Collider hit in Physics.OverlapSphere(transform.position, radius, LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
        {
            PlayerStats player = hit.GetComponentInParent<PlayerStats>();
            if (player != null) player.TakeDamage(damagePerTick, transform.position);
        }
    }
}

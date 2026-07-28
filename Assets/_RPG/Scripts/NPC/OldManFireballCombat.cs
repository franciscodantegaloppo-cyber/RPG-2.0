using System.Collections;
using UnityEngine;

public class OldManFireballCombat : MonoBehaviour
{
    const float DetectionRadius = 10f;
    const float FireballDamage = 42f;
    const float FireballSpeed = 13f;
    const float CastCooldown = 2.35f;

    NPCWander wander;
    Animator animator;
    PlayerSpellAnimationPlayer spellAnimation;
    EnemyStats target;
    GameObject handFire;
    float nextScan;
    float nextCast;
    bool casting;
    bool inCombat;
    public bool HasTarget => target != null;

    void Awake()
    {
        wander = GetComponent<NPCWander>();
        animator = GetComponentInChildren<Animator>(true);
        spellAnimation = GetComponent<PlayerSpellAnimationPlayer>();
        if (spellAnimation == null) spellAnimation = gameObject.AddComponent<PlayerSpellAnimationPlayer>();
    }

    void Update()
    {
        bool gameplay = GameManager.Instance == null || GameManager.Instance.IsGameplayActive();
        if (!gameplay) return;

        if (Time.time >= nextScan)
        {
            nextScan = Time.time + .25f;
            SetTarget(FindTarget());
        }

        if (target == null) return;
        FaceTarget(target.transform.position);
        if (!casting && Time.time >= nextCast)
            StartCoroutine(CastFireball(target));
    }

    EnemyStats FindTarget()
    {
        EnemyStats best = null;
        float bestDistance = DetectionRadius * DetectionRadius;
        foreach (EnemyStats enemy in FindObjectsByType<EnemyStats>(FindObjectsInactive.Exclude))
        {
            if (enemy == null || enemy.IsDead || enemy.GetComponent<TonioRetaliation>() != null ||
                enemy.GetComponent<OldManDialogue>() != null) continue;
            float distance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distance > bestDistance) continue;
            bestDistance = distance;
            best = enemy;
        }
        return best;
    }

    void SetTarget(EnemyStats newTarget)
    {
        if (target == newTarget) return;
        target = newTarget;
        if (target != null)
        {
            if (!inCombat) wander?.PauseForInteraction();
            inCombat = true;
            CreateHandFire();
        }
        else
        {
            inCombat = false;
            if (!casting) wander?.ResumeWander();
            RemoveHandFire();
        }
    }

    IEnumerator CastFireball(EnemyStats castTarget)
    {
        if (castTarget == null || castTarget.IsDead) yield break;
        casting = true;
        nextCast = Time.time + CastCooldown;
        FaceTarget(castTarget.transform.position);
        spellAnimation?.Play(0);

        // Release the projectile when the imported animation extends the casting hand.
        yield return new WaitForSeconds(.28f);
        if (castTarget != null && !castTarget.IsDead &&
            Vector3.Distance(transform.position, castTarget.transform.position) <= DetectionRadius + 1f)
            LaunchAt(castTarget);

        yield return new WaitForSeconds(.48f);
        casting = false;
        if (target == null)
        {
            RemoveHandFire();
            wander?.ResumeWander();
        }
    }

    void LaunchAt(EnemyStats enemy)
    {
        Vector3 destination = EnemyCenter(enemy);
        Vector3 origin = handFire != null
            ? handFire.transform.position
            : transform.position + Vector3.up * 1.35f + transform.forward * .65f;
        Vector3 direction = (destination - origin).normalized;
        GameObject prefab = Resources.Load<GameObject>("PlayerSpells/Fireball");
        GameObject projectile;
        if (prefab != null)
        {
            projectile = Instantiate(prefab, origin + direction * .18f, Quaternion.LookRotation(direction));
            projectile.transform.localScale *= .58f;
            foreach (Collider collider in projectile.GetComponentsInChildren<Collider>(true)) Destroy(collider);
        }
        else
        {
            projectile = Fireball.BuildRedFireball(.58f);
            Destroy(projectile.GetComponent<Fireball>());
            projectile.transform.position = origin + direction * .18f;
        }
        TowerGuardStationary towerGuard = GetComponent<TowerGuardStationary>();
        projectile.name = towerGuard != null ? "TowerGuard_Fireball" : "ElViejo_Fireball";
        projectile.AddComponent<PlayerSpellProjectile>().Launch(direction, FireballDamage, FireballSpeed,
            transform, enemy, towerGuard != null ? towerGuard.ProjectileCollisionIgnoreRoot : null);
    }

    void CreateHandFire()
    {
        if (handFire != null) return;
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        Transform hand = animator != null && animator.avatar != null && animator.avatar.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (hand == null) hand = transform;

        handFire = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        handFire.name = "ElViejo_FireballEquipped";
        handFire.transform.SetParent(hand, false);
        handFire.transform.localPosition = hand == transform ? new Vector3(.38f, 1.3f, .4f) : new Vector3(.11f, .035f, .015f);
        handFire.transform.localScale = Vector3.one * .15f;
        Collider coreCollider = handFire.GetComponent<Collider>();
        if (coreCollider != null) Destroy(coreCollider);

        Renderer coreRenderer = handFire.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (coreRenderer != null && shader != null)
        {
            Material core = new Material(shader);
            Color fire = new Color(1f, .18f, .015f);
            core.color = fire;
            if (core.HasProperty("_BaseColor")) core.SetColor("_BaseColor", fire * 2.5f);
            coreRenderer.material = core;
        }

        Light light = handFire.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, .22f, .025f);
        light.range = 2.4f;
        light.intensity = 1.8f;
        light.shadows = LightShadows.None;

        GameObject auraPrefab = Resources.Load<GameObject>("PlayerSpellHands/Fire");
        if (auraPrefab == null) return;
        GameObject aura = Instantiate(auraPrefab, handFire.transform);
        aura.name = "ElViejo_ImportedFireAura";
        aura.transform.localPosition = Vector3.zero;
        aura.transform.localRotation = Quaternion.identity;
        aura.transform.localScale = Vector3.one * .9f;
        foreach (Collider collider in aura.GetComponentsInChildren<Collider>(true)) Destroy(collider);
        foreach (ParticleSystem particles in aura.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            particles.Play(true);
        }
    }

    void RemoveHandFire()
    {
        if (handFire != null) Destroy(handFire);
        handFire = null;
    }

    void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > .01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), 540f * Time.deltaTime);
    }

    static Vector3 EnemyCenter(EnemyStats enemy)
    {
        Collider collider = enemy != null ? enemy.GetComponentInChildren<Collider>() : null;
        return collider != null ? collider.bounds.center : enemy.transform.position + Vector3.up;
    }

    void OnDestroy() => RemoveHandFire();
}

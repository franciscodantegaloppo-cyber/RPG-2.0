using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Keeps the Crab Demon encounter at night and creates at most two short-lived blue
// combat echoes. Echoes mirror the boss animation and movement without registering as
// enemies, dropping rewards, blocking navigation or recursively creating more echoes.
[RequireComponent(typeof(EnemyStats))]
public sealed class CrabDemonNightEchoController : MonoBehaviour
{
    [SerializeField] float echoLifetime = 5f;
    [SerializeField] float echoInterval = 6.5f;
    [SerializeField] float echoActivationDistance = 22f;
    [SerializeField] int maximumEchoes = 2;

    readonly List<GameObject> activeEchoes = new List<GameObject>();
    EnemyStats stats;
    CrabDemonBossAI bossAI;
    Animator sourceAnimator;
    Transform player;
    TenkokuDayNightCycle cycle;
    float nextEchoTime;
    bool nightOwned;
    bool dead;

    void Awake()
    {
        stats = GetComponent<EnemyStats>();
        bossAI = GetComponent<CrabDemonBossAI>();
        sourceAnimator = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        dead = false;
        stats.OnDeath += HandleDeath;
        StartCoroutine(AcquireNight());
        nextEchoTime = Time.time + 2.5f;
    }

    void OnDisable()
    {
        stats.OnDeath -= HandleDeath;
        ReleaseNight();
        for (int i = activeEchoes.Count - 1; i >= 0; i--)
            if (activeEchoes[i] != null) Destroy(activeEchoes[i]);
        activeEchoes.Clear();
    }

    IEnumerator AcquireNight()
    {
        while (!dead && cycle == null)
        {
            cycle = FindAnyObjectByType<TenkokuDayNightCycle>();
            if (cycle == null) yield return null;
        }
        if (!dead && cycle != null && !nightOwned)
        {
            cycle.BeginNightOverride(0f);
            nightOwned = true;
        }
    }

    void Update()
    {
        activeEchoes.RemoveAll(echo => echo == null);
        if (dead || Time.time < nextEchoTime || activeEchoes.Count >= maximumEchoes)
            return;
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }
        if (player == null || Vector3.Distance(transform.position, player.position) >
            echoActivationDistance) return;

        nextEchoTime = Time.time + echoInterval;
        SpawnEcho(activeEchoes.Count);
    }

    void SpawnEcho(int index)
    {
        if (sourceAnimator == null) return;
        GameObject visual = Instantiate(sourceAnimator.gameObject);
        visual.name = "CrabDemon_BlueEcho";
        visual.transform.SetPositionAndRotation(sourceAnimator.transform.position,
            sourceAnimator.transform.rotation);
        visual.transform.localScale = sourceAnimator.transform.lossyScale;

        StripGameplayComponents(visual);

        TintBlueTransparent(visual);
        CrabDemonEchoFollower follower = visual.AddComponent<CrabDemonEchoFollower>();
        float side = index == 0 ? -1f : 1f;
        follower.Configure(transform, sourceAnimator, bossAI, stats,
            new Vector3(side * 1.65f, 0f, -1.1f), echoLifetime);
        activeEchoes.Add(visual);
    }

    static void StripGameplayComponents(GameObject visual)
    {
        // Instantiate() copies the complete boss root because its Animator lives on that same
        // object. Remove every gameplay script immediately, but remove EnemyStats last:
        // CrabDemonBossAI, EnemySeparationController, CrabDemonHealthBar and this echo controller
        // all RequireComponent(EnemyStats). Destroying EnemyStats first makes Unity reject the
        // removal and leaves a functional invisible enemy inside what should be a visual echo.
        MonoBehaviour[] behaviours =
            visual.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && !(behaviour is EnemyStats))
                DestroyImmediate(behaviour);
        }
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is EnemyStats stats && stats != null)
                DestroyImmediate(stats);
        }

        // The echo is visual-only: it cannot collide, push, illuminate, block navigation or
        // receive physics impulses. Immediate removal is safe here because this is a brand-new
        // disposable runtime clone and prevents one frame of copied boss behaviour from running.
        foreach (Collider col in visual.GetComponentsInChildren<Collider>(true))
            if (col != null) DestroyImmediate(col);
        foreach (Rigidbody rb in visual.GetComponentsInChildren<Rigidbody>(true))
            if (rb != null) DestroyImmediate(rb);
        foreach (Light light in visual.GetComponentsInChildren<Light>(true))
            if (light != null) DestroyImmediate(light);
    }

    static void TintBlueTransparent(GameObject visual)
    {
        Shader transparent = Shader.Find("Universal Render Pipeline/Unlit") ??
                             Shader.Find("Sprites/Default");
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                Material material = transparent != null
                    ? new Material(transparent)
                    : new Material(source);
                Color blue = new Color(.08f, .42f, 1f, .34f);
                material.color = blue;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", blue);
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                material.renderQueue = 3000;
                materials[i] = material;
            }
            renderer.materials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    void HandleDeath()
    {
        dead = true;
        ReleaseNight();
        QuestManager.Instance?.NotifyCrabDemonDefeatedWithRedDiamond();
    }

    void ReleaseNight()
    {
        if (!nightOwned) return;
        nightOwned = false;
        cycle?.EndNightOverride();
    }
}

public sealed class CrabDemonEchoFollower : MonoBehaviour
{
    Transform source;
    Animator sourceAnimator;
    Animator echoAnimator;
    CrabDemonBossAI sourceAI;
    EnemyStats sourceStats;
    Vector3 localOffset;
    float remaining;
    bool sourceWasAttacking;
    Material[] fadeMaterials;

    public void Configure(Transform sourceTransform, Animator animator,
        CrabDemonBossAI ai, EnemyStats stats, Vector3 offset, float lifetime)
    {
        source = sourceTransform;
        sourceAnimator = animator;
        sourceAI = ai;
        sourceStats = stats;
        echoAnimator = GetComponent<Animator>();
        localOffset = offset;
        remaining = lifetime;
        CacheFadeMaterials();
    }

    void CacheFadeMaterials()
    {
        // Cache the generated blue material instances instead of keeping Renderer references.
        // Particle-system renderers may disappear when copied VFX scripts clean themselves up;
        // accessing renderer.materials afterwards raises MissingReferenceException.
        List<Material> materials = new List<Material>();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            foreach (Material material in renderer.materials)
                if (material != null && !materials.Contains(material))
                    materials.Add(material);
        }
        fadeMaterials = materials.ToArray();
    }

    void LateUpdate()
    {
        if (source == null || sourceAnimator == null)
        {
            Destroy(gameObject);
            return;
        }
        remaining -= Time.deltaTime;
        transform.position = source.TransformPoint(localOffset);
        transform.rotation = source.rotation;

        if (echoAnimator != null)
        {
            AnimatorStateInfo state = sourceAnimator.GetCurrentAnimatorStateInfo(0);
            echoAnimator.Play(state.fullPathHash, 0, state.normalizedTime);
            echoAnimator.speed = sourceAnimator.speed;
        }

        bool attackingNow = sourceAI != null && sourceAI.IsPerformingAttack;
        if (attackingNow && !sourceWasAttacking)
            ApplyMirroredHit();
        sourceWasAttacking = attackingNow;

        float alpha = Mathf.Clamp01(remaining / .8f) * .34f;
        if (fadeMaterials != null)
        {
            foreach (Material material in fadeMaterials)
            {
                if (material == null) continue;
                Color color = material.color;
                color.a = alpha;
                material.color = color;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
            }
        }
        if (remaining <= 0f) Destroy(gameObject);
    }

    void ApplyMirroredHit()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        float reach = 2.5f;
        Vector3 delta = player.transform.position - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude > reach * reach) return;
        // The echoes reproduce the strike but at reduced power so two simultaneous
        // copies remain dangerous without tripling every boss hit.
        float damage = sourceStats != null ? sourceStats.Attack * .4f : 20f;
        player.GetComponent<PlayerStats>()?.TakeDamage(damage, transform.position);
    }

    void OnDestroy()
    {
        if (fadeMaterials == null) return;
        foreach (Material material in fadeMaterials)
            if (material != null) Destroy(material);
    }
}

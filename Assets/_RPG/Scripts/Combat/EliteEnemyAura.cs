using System.Collections.Generic;
using UnityEngine;

// Permanent yellow aura shared by every star/elite enemy. It uses the imported
// Travis Game Assets Buff_03a_Aura prefab already prepared under Resources for URP.
// The effect is visual-only: it never adds colliders, rigidbodies, damage or lights.
public sealed class EliteEnemyAura : MonoBehaviour
{
    const string AuraResource = "VFX/StatusEffects/LevelUp";
    const string AuraObjectName = "Elite_YellowPackAura";

    readonly List<Material> runtimeMaterials = new List<Material>();
    EnemyStats stats;
    GameObject aura;

    public static EliteEnemyAura Ensure(GameObject enemy)
    {
        if (enemy == null) return null;
        EliteEnemyAura result = enemy.GetComponent<EliteEnemyAura>();
        if (result == null) result = enemy.AddComponent<EliteEnemyAura>();
        result.EnsureAura();
        return result;
    }

    void Awake()
    {
        stats = GetComponent<EnemyStats>();
        EnsureAura();
    }

    void OnEnable()
    {
        if (stats == null) stats = GetComponent<EnemyStats>();
        if (stats != null) stats.OnDeath += HandleDeath;
        EnsureAura();
    }

    void OnDisable()
    {
        if (stats != null) stats.OnDeath -= HandleDeath;
    }

    void EnsureAura()
    {
        if (aura != null) return;
        Transform existing = transform.Find(AuraObjectName);
        if (existing != null)
        {
            aura = existing.gameObject;
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(AuraResource);
        if (prefab == null)
        {
            Debug.LogWarning("[EliteEnemyAura] No se encontró el aura importada: " +
                             AuraResource, this);
            return;
        }

        Bounds visualBounds = CalculateVisualBounds();
        Vector3 localTop = transform.InverseTransformPoint(
            new Vector3(visualBounds.center.x, visualBounds.max.y, visualBounds.center.z));
        Vector3 localBottom = transform.InverseTransformPoint(
            new Vector3(visualBounds.center.x, visualBounds.min.y, visualBounds.center.z));
        float localHeight = Mathf.Abs(localTop.y - localBottom.y);
        float scale = Mathf.Clamp(localHeight / 1.8f, .62f, 3.2f);

        // The pack's particle shapes are authored with y=0 at the character's feet
        // (rising up from there), so anchor the aura at the enemy's feet rather than
        // its body center — otherwise the "rising from the ground" look starts mid-torso.
        aura = Instantiate(prefab, transform, false);
        aura.name = AuraObjectName;
        aura.transform.localPosition = localBottom;
        aura.transform.localRotation = Quaternion.identity;
        aura.transform.localScale = Vector3.one * scale;
        SetLayerRecursively(aura, gameObject.layer);

        foreach (Collider collider in aura.GetComponentsInChildren<Collider>(true))
            Destroy(collider);
        foreach (Rigidbody body in aura.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        Color gold = new Color(1f, .72f, .08f, .82f);
        Color paleGold = new Color(1f, .94f, .34f, .62f);
        foreach (ParticleSystem particles in
                 aura.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startColor = new ParticleSystem.MinMaxGradient(gold, paleGold);
            particles.Play(true);
        }

        foreach (ParticleSystemRenderer renderer in
                 aura.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null) continue;
                runtimeMaterials.Add(material);
                Color emission = new Color(2.4f, 1.32f, .08f, .68f);
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", gold);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", gold);
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", emission);
            }
        }
    }

    Bounds CalculateVisualBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Transform auraRoot = transform.Find(AuraObjectName);
        bool found = false;
        Bounds bounds = new Bounds(transform.position + Vector3.up, Vector3.one * 1.8f);
        foreach (Renderer renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer ||
                (auraRoot != null && renderer.transform.IsChildOf(auraRoot)) ||
                renderer.GetComponentInParent<BillboardToCamera>() != null ||
                renderer.name.IndexOf("Star", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    void HandleDeath()
    {
        if (aura == null) return;
        foreach (ParticleSystem particles in
                 aura.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(aura, 1.25f);
        aura = null;
    }

    void OnDestroy()
    {
        for (int i = 0; i < runtimeMaterials.Count; i++)
            if (runtimeMaterials[i] != null) Destroy(runtimeMaterials[i]);
        runtimeMaterials.Clear();
    }

    static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Runs on the anomalous demon.  It checks the colliders it physically overlaps so transform
// movement cannot ghost harmlessly through a tree: a struck tree burns, then falls with an
// accelerating gravity-like motion.
public class DemonioAnomaloTreeBurner : MonoBehaviour
{
    [SerializeField] float contactRadius = 2.35f;
    [SerializeField] float checkInterval = 0.12f;
    float nextCheck;

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;
        foreach (Collider hit in Physics.OverlapSphere(transform.position + Vector3.up * 1.1f, contactRadius, ~0, QueryTriggerInteraction.Collide))
        {
            AnomalyDemonStatue statue = hit.GetComponentInParent<AnomalyDemonStatue>();
            if (statue != null)
            {
                statue.KnockDownAndBurn(transform.position);
                continue;
            }
            Transform tree = FindTreeRoot(hit.transform);
            if (tree == null || tree.GetComponent<BurningTreeFall>() != null) continue;
            BurningTreeFall fall = tree.gameObject.AddComponent<BurningTreeFall>();
            fall.IgniteAndFall(transform.position);
        }
    }

    static Transform FindTreeRoot(Transform source)
    {
        Transform fallback = null;
        for (Transform current = source; current != null; current = current.parent)
        {
            string lowerName = current.name.ToLowerInvariant();
            bool isTreeName = lowerName.Contains("tree") || lowerName.Contains("arbol") || lowerName.Contains("árbol") ||
                              lowerName.Contains("spruce") || lowerName.Contains("pine") || lowerName.Contains("oak") ||
                              lowerName.Contains("fir") || lowerName.Contains("conifer");
            if (isTreeName)
            {
                // Enemies whose names contain TreeAnomaly must never be treated as scenery.
                if (current.GetComponentInParent<EnemyStats>() != null)
                    return null;
                fallback ??= current;
                // Prefer the nearest named object that owns visible tree geometry. This avoids
                // rotating only a child collider while its actual Spruce/Pine mesh stays put.
                if (current.GetComponentInChildren<Renderer>(true) != null)
                    return current;
            }
        }
        return fallback;
    }
}

public class BurningTreeFall : MonoBehaviour
{
    const string FireVfxResource = "VFX/FreeFireProjectileVFX";
    bool ignited;

    public void IgniteAndFall(Vector3 sourcePosition)
    {
        if (ignited) return;
        ignited = true;
        StartCoroutine(BurnAndFall(sourcePosition));
    }

    IEnumerator BurnAndFall(Vector3 sourcePosition)
    {
        Bounds bounds = GetBounds();
        GameObject firePrefab = Resources.Load<GameObject>(FireVfxResource);
        if (firePrefab != null)
        {
            GameObject fire = Instantiate(firePrefab, bounds.center, Quaternion.identity, transform);
            fire.name = "TreeBurningFire";
            fire.transform.localScale = Vector3.one * Mathf.Clamp(bounds.size.y * 0.25f, 1.5f, 4f);
            Destroy(fire, 10f);
        }

        // A brief burn makes the impact readable before the trunk gives way.
        yield return new WaitForSeconds(0.45f);
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;

        Vector3 horizontal = transform.position - sourcePosition;
        horizontal.y = 0f;
        if (horizontal.sqrMagnitude < 0.01f) horizontal = transform.right;
        horizontal.Normalize();
        Vector3 fallAxis = Vector3.Cross(Vector3.up, horizontal).normalized;
        Vector3 pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        const float duration = 1.15f;
        float elapsed = 0f;
        float previousAngle = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // t² gives the fall its gravity-like acceleration instead of a mechanical rotation.
            float angle = 84f * t * t;
            transform.RotateAround(pivot, fallAxis, angle - previousAngle);
            previousAngle = angle;
            yield return null;
        }

        // Leave the fallen, burned trunk on the ground long enough for the player to see the
        // consequence of the charge, then clean it up so destroyed scenery cannot accumulate.
        yield return new WaitForSeconds(10f);
        Destroy(gameObject);
    }

    Bounds GetBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one * 2f);
        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);
        return combined;
    }
}

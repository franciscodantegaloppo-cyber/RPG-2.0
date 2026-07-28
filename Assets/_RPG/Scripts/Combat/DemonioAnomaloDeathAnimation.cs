using System.Collections;
using UnityEngine;

// The imported anomalous demon has only an idle/walk clip.  This supplies a readable, physical
// death motion instead of leaving the model frozen upright when its health reaches zero.
[RequireComponent(typeof(EnemyStats))]
public class DemonioAnomaloDeathAnimation : MonoBehaviour
{
    EnemyStats stats;
    bool dying;

    void Awake() => stats = GetComponent<EnemyStats>();

    void OnEnable()
    {
        if (stats == null) stats = GetComponent<EnemyStats>();
        stats.OnDeath += BeginDeath;
    }

    void OnDisable()
    {
        if (stats != null) stats.OnDeath -= BeginDeath;
    }

    void BeginDeath()
    {
        if (dying) return;
        dying = true;
        DemonioAnomaloEncounterEffects encounter = GetComponent<DemonioAnomaloEncounterEffects>();
        if (encounter != null) encounter.enabled = false;
        StartCoroutine(Collapse());
    }

    IEnumerator Collapse()
    {
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.enabled = false;

        Quaternion startRotation = transform.rotation;
        Quaternion fallenRotation = startRotation * Quaternion.Euler(78f, 0f, -10f);
        Vector3 startPosition = transform.position;
        Vector3 fallenPosition = startPosition + Vector3.down * 0.18f;
        const float duration = 1.35f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.rotation = Quaternion.Slerp(startRotation, fallenRotation, t);
            transform.position = Vector3.Lerp(startPosition, fallenPosition, t);
            yield return null;
        }
        transform.rotation = fallenRotation;
        transform.position = fallenPosition;
    }
}

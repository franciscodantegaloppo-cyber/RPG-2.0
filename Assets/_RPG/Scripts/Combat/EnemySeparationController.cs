using UnityEngine;
using UnityEngine.AI;

// Adds a small local crowd-separation correction after each AI update. It prevents
// goblins and other enemies from occupying the same point without replacing their
// existing pursuit, attack, terrain grounding or animation systems.
[DefaultExecutionOrder(850)]
[RequireComponent(typeof(EnemyStats))]
public sealed class EnemySeparationController : MonoBehaviour
{
    [SerializeField] float separationSpeed = 1.45f;
    [SerializeField] float minimumRadius = .55f;
    [SerializeField] float maximumCorrectionPerFrame = .075f;

    readonly Collider[] nearby = new Collider[24];
    CharacterController controller;
    NavMeshAgent agent;
    EnemyStats stats;
    float radius;
    float nextSeparationCheck;
    float lastSeparationCheck;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<EnemyStats>();
        radius = CalculateRadius();
        lastSeparationCheck = Time.time;
        nextSeparationCheck = Time.time + Random.Range(.02f, .12f);
    }

    float CalculateRadius()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
            return Mathf.Max(minimumRadius,
                capsule.radius * Mathf.Max(transform.lossyScale.x,
                    transform.lossyScale.z));
        if (controller != null)
            return Mathf.Max(minimumRadius, controller.radius *
                Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
        return minimumRadius;
    }

    void LateUpdate()
    {
        if (stats == null || stats.IsDead) return;
        if (Time.time < nextSeparationCheck) return;
        float stepSeconds = Mathf.Clamp(Time.time - lastSeparationCheck, .05f, .2f);
        lastSeparationCheck = Time.time;
        nextSeparationCheck = Time.time + Random.Range(.08f, .13f);
        int count = Physics.OverlapSphereNonAlloc(transform.position,
            radius * 2.15f, nearby, ~0, QueryTriggerInteraction.Ignore);
        Vector3 correction = Vector3.zero;
        int contributors = 0;
        for (int i = 0; i < count; i++)
        {
            Collider collider = nearby[i];
            if (collider == null) continue;
            EnemyStats other = collider.GetComponentInParent<EnemyStats>();
            if (other == null || other == stats || other.IsDead) continue;
            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float distance = away.magnitude;
            if (distance < .001f)
                away = Random.insideUnitSphere;
            float otherRadius = other.GetComponent<EnemySeparationController>()?.radius ??
                                minimumRadius;
            float desired = (radius + otherRadius) * .86f;
            if (distance >= desired) continue;
            correction += away.normalized * (1f - distance / desired);
            contributors++;
        }
        if (contributors == 0) return;

        Vector3 movement = correction.normalized *
            Mathf.Min(maximumCorrectionPerFrame,
                separationSpeed * stepSeconds * correction.magnitude / contributors);
        if (controller != null && controller.enabled)
            controller.Move(movement);
        else if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.Move(movement);
        else
            transform.position += movement;
    }
}

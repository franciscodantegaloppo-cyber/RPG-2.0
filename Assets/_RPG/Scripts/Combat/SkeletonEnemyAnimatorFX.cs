using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Runs after EnemyAI's default execution order (0) so the walk-bob below applies on top of
// whatever position EnemyAI.LateUpdate() already moved this frame, instead of racing it.
[DefaultExecutionOrder(100)]
public class SkeletonEnemyAnimatorFX : MonoBehaviour
{
    [Header("Visual Reaction")]
    [SerializeField] bool useProceduralMotion;
    [SerializeField] float attackLunge = 0.08f;
    [SerializeField] float attackDuration = 0.28f;
    [SerializeField] float hitRecoil = 0.05f;
    [SerializeField] float knockbackDistance;
    [SerializeField] float hitDuration = 0.22f;
    [SerializeField] float deathTilt = 78f;

    [Header("Walk Bob")]
    // Some clips (the goblin pack) have zero baked motion at all - not just no root motion, but
    // no vertical bob either - so a code-translated character holding one of those clips glides
    // in a perfectly straight line with legs cycling underneath it, reading as "surfing"/skating
    // rather than walking regardless of how well the stride pace is matched to travel speed.
    // This fakes the missing footstep bob. Off by default so it doesn't double up on packs whose
    // clips already have their own natural bob (the skeleton).
    [SerializeField] bool enableWalkBob;
    [SerializeField] float walkBobAmplitude = 0.035f;
    [SerializeField] float walkBobCyclesPerSecond = 2f;

    Transform visualRoot;
    Animator animator;
    NavMeshAgent agent;
    Terrain terrain;
    Vector3 originalLocalPosition;
    Quaternion originalLocalRotation;
    Coroutine reactionRoutine;
    float bobPhase;
    float appliedBobY;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        visualRoot = animator != null ? animator.transform : transform;
        agent = GetComponent<NavMeshAgent>();
        terrain = Terrain.activeTerrain;

        originalLocalPosition = visualRoot.localPosition;
        originalLocalRotation = visualRoot.localRotation;
    }

    // LateUpdate (not Update) and after EnemyAI's execution order (see [DefaultExecutionOrder]
    // above): the goblin pack's Animator sits directly on the root GameObject (unlike the
    // skeleton, whose rig is a separate child), so visualRoot can BE the same transform EnemyAI
    // moves every frame. Resetting it to a fixed "original position" each frame (the old
    // approach) fought EnemyAI's own movement and reset the enemy back to its spawn point every
    // frame - looked like, and effectively was, a total movement freeze. Adding/removing a small
    // delta on top of whatever position is already there - after movement has been applied - rides
    // along with it instead, so this is safe whether visualRoot is the root or a separate child.
    void LateUpdate()
    {
        if (!enableWalkBob || animator == null || reactionRoutine != null)
            return;

        // Undo last frame's bob before recomputing, so we never accumulate.
        if (appliedBobY != 0f)
        {
            visualRoot.position -= Vector3.up * appliedBobY;
            appliedBobY = 0f;
        }

        float speedParam = animator.GetFloat("Speed");
        if (speedParam <= 0.05f)
        {
            bobPhase = 0f;
            return;
        }

        bobPhase += Time.deltaTime * animator.speed * walkBobCyclesPerSecond * Mathf.PI * 2f;
        appliedBobY = Mathf.Abs(Mathf.Sin(bobPhase)) * walkBobAmplitude;
        visualRoot.position += Vector3.up * appliedBobY;
    }

    public void PlayAttack()
    {
        if (!useProceduralMotion || !isActiveAndEnabled)
            return;

        StartReaction(AttackRoutine());
    }

    public void PlayHit(Vector3 attackerPosition)
    {
        if (!useProceduralMotion || !isActiveAndEnabled)
            return;

        StartReaction(HitRoutine(attackerPosition));
    }

    public void PlayDeath()
    {
        if (!useProceduralMotion)
            return;

        if (reactionRoutine != null)
            StopCoroutine(reactionRoutine);

        visualRoot.localPosition = originalLocalPosition + Vector3.down * 0.18f;
        visualRoot.localRotation = originalLocalRotation * Quaternion.Euler(deathTilt, 0f, Random.Range(-22f, 22f));
    }

    void StartReaction(IEnumerator routine)
    {
        if (reactionRoutine != null)
            StopCoroutine(reactionRoutine);

        reactionRoutine = StartCoroutine(routine);
    }

    IEnumerator AttackRoutine()
    {
        Vector3 startPos = originalLocalPosition;
        Quaternion startRot = originalLocalRotation;
        Vector3 endPos = originalLocalPosition + Vector3.forward * attackLunge + Vector3.down * 0.04f;
        Quaternion endRot = originalLocalRotation * Quaternion.Euler(-14f, 0f, 0f);

        float half = Mathf.Max(0.03f, attackDuration * 0.5f);
        yield return BlendVisual(startPos, endPos, startRot, endRot, half);
        yield return BlendVisual(endPos, originalLocalPosition, endRot, originalLocalRotation, half);
        reactionRoutine = null;
    }

    IEnumerator HitRoutine(Vector3 attackerPosition)
    {
        Vector3 away = transform.position - attackerPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = -transform.forward;
        away.Normalize();

        Vector3 rootStart = transform.position;
        Vector3 rootEnd = ProjectToGround(rootStart + away * knockbackDistance);
        Vector3 visualStart = originalLocalPosition;
        Vector3 visualEnd = originalLocalPosition - Vector3.forward * hitRecoil + Vector3.up * 0.03f;
        Quaternion rotStart = originalLocalRotation;
        Quaternion rotEnd = originalLocalRotation * Quaternion.Euler(12f, 0f, 0f);

        float half = Mathf.Max(0.03f, hitDuration * 0.5f);
        float elapsed = 0f;
        while (elapsed < half)
        {
            float t = Ease(elapsed / half);
            MoveRoot(Vector3.Lerp(rootStart, rootEnd, t));
            visualRoot.localPosition = Vector3.Lerp(visualStart, visualEnd, t);
            visualRoot.localRotation = Quaternion.Slerp(rotStart, rotEnd, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        MoveRoot(rootEnd);
        yield return BlendVisual(visualEnd, originalLocalPosition, rotEnd, originalLocalRotation, half);
        reactionRoutine = null;
    }

    IEnumerator BlendVisual(Vector3 fromPos, Vector3 toPos, Quaternion fromRot, Quaternion toRot, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Ease(elapsed / duration);
            visualRoot.localPosition = Vector3.Lerp(fromPos, toPos, t);
            visualRoot.localRotation = Quaternion.Slerp(fromRot, toRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        visualRoot.localPosition = toPos;
        visualRoot.localRotation = toRot;
    }

    void MoveRoot(Vector3 position)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Warp(position);
            return;
        }

        transform.position = position;
    }

    Vector3 ProjectToGround(Vector3 position)
    {
        if (terrain != null)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        return position;
    }

    static float Ease(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}

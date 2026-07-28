using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Makes the Red Diamond visibly become the source of the encounter while guaranteeing that the
// boss finishes with its root on the sampled terrain rather than below it.
[DisallowMultipleComponent]
public sealed class CrabDemonSummonEmergence : MonoBehaviour
{
    Coroutine routine;

    public void Begin(Vector3 groundPosition, Transform player)
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(Emerge(groundPosition, player));
    }

    IEnumerator Emerge(Vector3 groundPosition, Transform player)
    {
        CrabDemonBossAI ai = GetComponent<CrabDemonBossAI>();
        CharacterController controller = GetComponent<CharacterController>();
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        var colliderStates = new List<bool>(colliders.Length);
        foreach (Collider collider in colliders)
        {
            colliderStates.Add(collider.enabled);
            collider.enabled = false;
        }
        if (ai != null) ai.enabled = false;
        if (controller != null) controller.enabled = false;

        Vector3 towardPlayer = player != null
            ? Vector3.ProjectOnPlane(player.position - groundPosition, Vector3.up)
            : Vector3.forward;
        if (towardPlayer.sqrMagnitude > .01f)
            transform.rotation = Quaternion.LookRotation(towardPlayer.normalized, Vector3.up);

        Vector3 start = groundPosition - Vector3.up * 1.15f;
        transform.position = start;
        ParticleSystem portal = BuildPortal(groundPosition + Vector3.up * .12f);
        Camera.main?.GetComponent<ThirdPersonCamera>()?.AddImpulse(.13f, .85f);

        const float duration = 1.15f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            transform.position = Vector3.LerpUnclamped(start, groundPosition, eased);
            yield return null;
        }
        transform.position = groundPosition;

        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null)
                colliders[i].enabled = colliderStates[i];
        if (ai != null) ai.enabled = true;

        if (portal != null)
        {
            portal.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(portal.gameObject, .65f);
        }
        routine = null;
    }

    static ParticleSystem BuildPortal(Vector3 position)
    {
        GameObject root = new GameObject("RedDiamond_CrabSummoningPortal");
        root.transform.position = position;
        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.28f, .7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.18f, .85f);
        main.startSize = new ParticleSystem.MinMaxCurve(.018f, .075f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(.7f, .01f, .02f, .25f),
            new Color(.45f, .08f, 1f, .68f));
        main.maxParticles = 160;
        var emission = particles.emission;
        emission.rateOverTime = 115f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.25f;
        shape.radiusThickness = .18f;
        shape.rotation = new Vector3(90f, 0f, 0f);
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        // Keep the three axes in Constant mode. Assigning TwoConstants one axis at a time makes
        // Unity 6 report a transient mismatch before the remaining axes can be configured.
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(.72f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(2.2f);
        var color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(.7f, .02f, .03f), 0f),
                new GradientColorKey(new Color(.42f, .12f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(.62f, .2f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = fade;
        ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.8f;
        renderer.velocityScale = .14f;
        renderer.sharedMaterial = WindVisualEffect.CreateWindStreakMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return particles;
    }
}

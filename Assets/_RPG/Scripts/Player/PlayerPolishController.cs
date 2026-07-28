using System;
using System.Collections.Generic;
using UnityEngine;

// Optional polish layer. It never moves the CharacterController, changes stats, replaces an
// AnimatorController or writes animator parameters, so the existing player systems remain the
// authority for gameplay.
[DisallowMultipleComponent]
public sealed class PlayerPolishController : MonoBehaviour
{
    enum SurfaceKind { Grass, Stone, Wood, Water }

    [SerializeField] float walkStepInterval = .48f;
    [SerializeField] float runStepInterval = .31f;
    [SerializeField] float breathingDegrees = .38f;

    PlayerController controller;
    CombatSystem combat;
    PlayerSpellAnimationPlayer spellAnimation;
    PlayerStats stats;
    AudioSource footsteps;
    readonly Dictionary<SurfaceKind, AudioClip> stepClips =
        new Dictionary<SurfaceKind, AudioClip>();
    readonly HashSet<Animator> preparedAnimators = new HashSet<Animator>();
    Transform chest;
    float nextStep;
    float nextAnimatorScan;
    Transform contactShadow;
    Material contactShadowMaterial;

    void Awake()
    {
        if (GetComponent<GrassStepInteraction>() == null)
            gameObject.AddComponent<GrassStepInteraction>();
        controller = GetComponent<PlayerController>();
        combat = GetComponent<CombatSystem>();
        spellAnimation = GetComponent<PlayerSpellAnimationPlayer>();
        stats = GetComponent<PlayerStats>();
        BuildFootsteps();
        BuildContactShadow();
        EnsureAnimatorPolish();
    }

    void Update()
    {
        if (Time.unscaledTime >= nextAnimatorScan)
        {
            nextAnimatorScan = Time.unscaledTime + 1f;
            EnsureAnimatorPolish();
        }

        UpdateFootsteps();
        UpdateContactShadow();
    }

    void LateUpdate()
    {
        if (chest == null || controller == null || stats == null || stats.IsDead)
            return;
        if (!controller.IsGrounded || controller.IsMoving ||
            (combat != null && combat.IsAttacking) ||
            (spellAnimation != null && spellAnimation.IsPlaying))
            return;

        float breath = Mathf.Sin(Time.unscaledTime * 1.65f) * breathingDegrees;
        chest.localRotation *= Quaternion.Euler(breath * .34f, 0f, breath * .12f);
    }

    void EnsureAnimatorPolish()
    {
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
        {
            if (animator == null || preparedAnimators.Contains(animator))
                continue;
            preparedAnimators.Add(animator);
            PlayerFootIKPolish ik = animator.GetComponent<PlayerFootIKPolish>();
            if (ik == null)
                ik = animator.gameObject.AddComponent<PlayerFootIKPolish>();
            ik.Configure(transform);

            if (chest == null && animator.isHuman)
                chest = animator.GetBoneTransform(HumanBodyBones.Chest) ??
                        animator.GetBoneTransform(HumanBodyBones.UpperChest);
        }
    }

    void BuildFootsteps()
    {
        footsteps = gameObject.AddComponent<AudioSource>();
        footsteps.playOnAwake = false;
        footsteps.spatialBlend = .72f;
        footsteps.rolloffMode = AudioRolloffMode.Linear;
        footsteps.minDistance = 1.2f;
        footsteps.maxDistance = 11f;
        footsteps.volume = .22f;

        stepClips[SurfaceKind.Grass] = BuildStepClip("Step_Grass", .22f, .18f, 90f, 520f);
        stepClips[SurfaceKind.Stone] = BuildStepClip("Step_Stone", .31f, .07f, 170f, 1500f);
        stepClips[SurfaceKind.Wood] = BuildStepClip("Step_Wood", .27f, .1f, 125f, 850f);
        stepClips[SurfaceKind.Water] = BuildStepClip("Step_Water", .16f, .25f, 60f, 420f);
    }

    void UpdateFootsteps()
    {
        if (controller == null || !controller.IsGrounded || !controller.IsMoving ||
            stats == null || stats.IsDead ||
            (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive()))
            return;
        if (Time.time < nextStep)
            return;

        float speedScale = Mathf.Max(.65f, stats.MoveSpeedMultiplier);
        nextStep = Time.time +
                   (controller.IsSprinting ? runStepInterval : walkStepInterval) / speedScale;
        SurfaceKind surface = DetectSurface(out Vector3 point);
        AudioClip clip = stepClips[surface];
        footsteps.pitch = UnityEngine.Random.Range(.92f, 1.08f);
        footsteps.volume = controller.IsSprinting ? .27f : .18f;
        footsteps.transform.position = point;
        footsteps.PlayOneShot(clip);
    }

    SurfaceKind DetectSurface(out Vector3 point)
    {
        point = transform.position;
        if (!Physics.Raycast(transform.position + Vector3.up * .35f, Vector3.down,
                out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
            return SurfaceKind.Grass;
        point = hit.point;
        string id = ((hit.collider != null ? hit.collider.name : "") + " " +
                     (hit.collider != null && hit.collider.sharedMaterial != null
                         ? hit.collider.sharedMaterial.name : "")).ToLowerInvariant();
        if (id.Contains("water") || id.Contains("agua") || id.Contains("river"))
            return SurfaceKind.Water;
        if (id.Contains("wood") || id.Contains("madera") || id.Contains("bridge"))
            return SurfaceKind.Wood;
        if (hit.collider is TerrainCollider || id.Contains("grass") || id.Contains("pasto"))
            return SurfaceKind.Grass;
        return SurfaceKind.Stone;
    }

    static AudioClip BuildStepClip(string name, float noiseAmount, float toneAmount,
        float lowFrequency, float highCut)
    {
        const int rate = 22050;
        const float duration = .115f;
        int count = Mathf.CeilToInt(rate * duration);
        float[] samples = new float[count];
        var random = new System.Random(name.GetHashCode());
        float filtered = 0f;
        float smoothing = Mathf.Clamp01(highCut / rate);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float envelope = Mathf.Exp(-t * 34f) * Mathf.SmoothStep(0f, 1f,
                Mathf.Min(1f, t * 180f));
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            filtered = Mathf.Lerp(filtered, white, smoothing);
            float tone = Mathf.Sin(t * lowFrequency * Mathf.PI * 2f) * toneAmount;
            samples[i] = (filtered * noiseAmount + tone) * envelope;
        }
        AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void BuildContactShadow()
    {
        GameObject shadow = new GameObject("PlayerContactShadow",
            typeof(MeshFilter), typeof(MeshRenderer), typeof(PlayerPolishVisualMarker));
        contactShadow = shadow.transform;
        contactShadow.SetParent(transform, true);
        shadow.GetComponent<MeshFilter>().sharedMesh = BuildQuad();

        // Sprites/Default always honours texture alpha. The URP Unlit shader defaults to an
        // opaque surface when created at runtime, which rendered the transparent texture as a
        // black rectangle around the feet.
        Shader shader = Shader.Find("Sprites/Default") ??
                        Shader.Find("Universal Render Pipeline/Particles/Unlit");
        contactShadowMaterial = new Material(shader)
        {
            name = "PlayerContactShadow_Runtime",
            renderQueue = 3000,
            mainTexture = BuildRadialShadowTexture()
        };
        if (contactShadowMaterial.HasProperty("_BaseMap"))
            contactShadowMaterial.SetTexture("_BaseMap", contactShadowMaterial.mainTexture);
        Color shadowColor = new Color(0f, 0f, 0f, .34f);
        if (contactShadowMaterial.HasProperty("_BaseColor"))
            contactShadowMaterial.SetColor("_BaseColor", shadowColor);
        if (contactShadowMaterial.HasProperty("_Color"))
            contactShadowMaterial.SetColor("_Color", shadowColor);
        contactShadowMaterial.SetOverrideTag("RenderType", "Transparent");
        if (contactShadowMaterial.HasProperty("_Surface"))
            contactShadowMaterial.SetFloat("_Surface", 1f);
        if (contactShadowMaterial.HasProperty("_SrcBlend"))
            contactShadowMaterial.SetFloat("_SrcBlend",
                (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (contactShadowMaterial.HasProperty("_DstBlend"))
            contactShadowMaterial.SetFloat("_DstBlend",
                (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (contactShadowMaterial.HasProperty("_ZWrite"))
            contactShadowMaterial.SetFloat("_ZWrite", 0f);
        contactShadowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        shadow.GetComponent<MeshRenderer>().sharedMaterial = contactShadowMaterial;
        shadow.GetComponent<MeshRenderer>().shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        shadow.GetComponent<MeshRenderer>().receiveShadows = false;
    }

    void UpdateContactShadow()
    {
        if (contactShadow == null || controller == null)
            return;
        bool found = Physics.Raycast(transform.position + Vector3.up * .5f, Vector3.down,
            out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore);
        contactShadow.gameObject.SetActive(found && !stats.IsDead);
        if (!found)
            return;
        contactShadow.position = hit.point + hit.normal * .018f;
        contactShadow.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        float distance = Mathf.Max(0f, transform.position.y - hit.point.y);
        float scale = Mathf.Lerp(1.12f, .62f, Mathf.InverseLerp(.1f, 2.2f, distance));
        contactShadow.localScale = new Vector3(scale, scale, scale);
    }

    static Mesh BuildQuad()
    {
        Mesh mesh = new Mesh { name = "PlayerContactShadowQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-.62f, 0f, -.38f), new Vector3(.62f, 0f, -.38f),
            new Vector3(-.62f, 0f, .38f), new Vector3(.62f, 0f, .38f)
        };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    static Texture2D BuildRadialShadowTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "PlayerContactShadowSoft",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(x / (size - 1f), y / (size - 1f)) * 2f -
                        Vector2.one;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - p.sqrMagnitude), 1.8f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply(false, true);
        return texture;
    }

    void OnDestroy()
    {
        foreach (AudioClip clip in stepClips.Values)
            if (clip != null)
                Destroy(clip);
        if (contactShadowMaterial != null)
        {
            Texture texture = contactShadowMaterial.mainTexture;
            Destroy(contactShadowMaterial);
            if (texture != null)
                Destroy(texture);
        }
    }
}

// Marker keeps purely presentational world helpers out of the inventory mannequin clone.
public sealed class PlayerPolishVisualMarker : MonoBehaviour { }

// OnAnimatorIK is ignored automatically by controllers whose layer has IK Pass disabled.
// Therefore this component improves compatible rigs and is harmless for every existing one.
[RequireComponent(typeof(Animator))]
public sealed class PlayerFootIKPolish : MonoBehaviour
{
    Animator animator;
    Transform playerRoot;
    PlayerController controller;

    public void Configure(Transform root)
    {
        playerRoot = root;
        controller = root != null ? root.GetComponent<PlayerController>() : null;
        animator = GetComponent<Animator>();
    }

    void Awake() => animator = GetComponent<Animator>();

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || !animator.isHuman || playerRoot == null ||
            controller == null || !controller.IsGrounded)
            return;
        float weight = controller.IsMoving ? .28f : .72f;
        ApplyFoot(AvatarIKGoal.LeftFoot, HumanBodyBones.LeftFoot, weight);
        ApplyFoot(AvatarIKGoal.RightFoot, HumanBodyBones.RightFoot, weight);
    }

    void ApplyFoot(AvatarIKGoal goal, HumanBodyBones bone, float weight)
    {
        Transform foot = animator.GetBoneTransform(bone);
        if (foot == null)
            return;
        Vector3 origin = foot.position + Vector3.up * .42f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, .9f, ~0,
                QueryTriggerInteraction.Ignore) ||
            hit.transform.IsChildOf(playerRoot))
        {
            animator.SetIKPositionWeight(goal, 0f);
            animator.SetIKRotationWeight(goal, 0f);
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(playerRoot.forward, hit.normal).normalized;
        Quaternion rotation = forward.sqrMagnitude > .01f
            ? Quaternion.LookRotation(forward, hit.normal)
            : foot.rotation;
        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKRotationWeight(goal, weight * .7f);
        animator.SetIKPosition(goal, hit.point + hit.normal * .035f);
        animator.SetIKRotation(goal, rotation);
    }
}

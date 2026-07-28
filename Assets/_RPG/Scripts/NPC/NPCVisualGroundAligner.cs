using UnityEngine;

[ExecuteAlways]
public class NPCVisualGroundAligner : MonoBehaviour
{
    [SerializeField] float groundOffset = 0f;
    [SerializeField] float maxCorrectionPerFrame = 30f;

    Animator animator;
    Transform visualRoot;

    void Awake()
    {
        CacheVisualRoot();
    }

    void OnEnable()
    {
        CacheVisualRoot();
        AlignNow();
    }

    void LateUpdate()
    {
        // Editor-only guard: without this, [ExecuteAlways] keeps re-snapping the visual root
        // every frame in Edit Mode too, fighting any manual reposition of the NPC in the Scene view.
        if (!Application.isPlaying)
            return;

        AlignNow();
    }

    public void Configure(float offset)
    {
        groundOffset = Mathf.Max(0f, offset);
        CacheVisualRoot();
        AlignNow();
    }

    public void AlignNow()
    {
        if (visualRoot == null)
            CacheVisualRoot();
        if (visualRoot == null)
            return;

        if (!TryGetFootBottomY(out float footBottomY))
            return;

        // GetGroundY (not raw terrain.SampleHeight) so NPCs standing inside a house pick up its
        // floor/stairs colliders instead of only ever tracking the outdoor terrain height.
        float groundY = GroundUtility.GetGroundY(transform.position, transform);
        if (float.IsNegativeInfinity(groundY))
            return;

        float yDelta = groundY + groundOffset - footBottomY;
        if (Mathf.Abs(yDelta) < 0.002f)
            return;

        if (Mathf.Abs(yDelta) <= maxCorrectionPerFrame)
            visualRoot.position += Vector3.up * yDelta;
    }

    void CacheVisualRoot()
    {
        animator = GetComponentInChildren<Animator>(true);
        visualRoot = animator != null ? animator.transform : null;

        if (visualRoot == null)
        {
            Renderer renderer = GetComponentInChildren<Renderer>(true);
            visualRoot = renderer != null ? renderer.transform : transform;
        }
    }

    bool TryGetFootBottomY(out float bottomY)
    {
        if (TryGetNamedFootTransformBottomY(out bottomY))
            return true;
        if (TryGetAnimatorFootBottomY(out bottomY))
            return true;
        if (TryGetNamedFootRendererBottomY(out bottomY))
            return true;
        return TryGetAnyRendererBottomY(out bottomY);
    }

    bool TryGetAnimatorFootBottomY(out float bottomY)
    {
        bottomY = 0f;
        if (animator == null || !animator.isHuman)
            return false;

        Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        bool found = false;

        if (left != null)
        {
            bottomY = left.position.y - 0.08f;
            found = true;
        }

        if (right != null)
        {
            float candidate = right.position.y - 0.08f;
            bottomY = found ? Mathf.Min(bottomY, candidate) : candidate;
            found = true;
        }

        return found;
    }

    bool TryGetNamedFootTransformBottomY(out float bottomY)
    {
        bottomY = 0f;
        bool found = false;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            string lowerName = child.name.ToLowerInvariant();
            if (!lowerName.Contains("foot") && !lowerName.Contains("feet") && !lowerName.Contains("toe"))
                continue;
            if (lowerName.Contains("ik") || lowerName.Contains("target"))
                continue;

            float candidate = child.position.y - 0.08f;
            bottomY = found ? Mathf.Min(bottomY, candidate) : candidate;
            found = true;
        }

        return found;
    }

    bool TryGetNamedFootRendererBottomY(out float bottomY)
    {
        bottomY = 0f;
        bool found = false;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (ShouldIgnoreRenderer(renderer))
                continue;

            string lowerName = renderer.transform.name.ToLowerInvariant();
            if (!IsFootingName(lowerName))
                continue;

            bottomY = found ? Mathf.Min(bottomY, renderer.bounds.min.y) : renderer.bounds.min.y;
            found = true;
        }

        return found;
    }

    bool TryGetAnyRendererBottomY(out float bottomY)
    {
        bottomY = 0f;
        bool found = false;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (ShouldIgnoreRenderer(renderer))
                continue;

            bottomY = found ? Mathf.Min(bottomY, renderer.bounds.min.y) : renderer.bounds.min.y;
            found = true;
        }

        return found;
    }

    static bool ShouldIgnoreRenderer(Renderer renderer)
    {
        if (renderer == null)
            return true;
        if (renderer is ParticleSystemRenderer)
            return true;

        string lowerName = renderer.transform.name.ToLowerInvariant();
        if (lowerName.StartsWith("blacksmith", System.StringComparison.Ordinal))
            return true;
        if (lowerName.Contains("weapon") || lowerName.Contains("sword") || lowerName.Contains("axe") ||
            lowerName.Contains("shield") || lowerName.Contains("staff") || lowerName.Contains("bow"))
            return true;
        return false;
    }

    static bool IsFootingName(string lowerName)
    {
        return lowerName.Contains("foot") ||
            lowerName.Contains("feet") ||
            lowerName.Contains("boot") ||
            lowerName.Contains("shoe") ||
            lowerName.Contains("leg");
    }
}

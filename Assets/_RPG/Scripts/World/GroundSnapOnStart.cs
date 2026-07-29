using UnityEngine;

public class GroundSnapOnStart : MonoBehaviour
{
    [SerializeField] float groundOffset = 0f;
    [SerializeField] bool useRendererBottom = true;

    System.Collections.IEnumerator Start()
    {
        SnapNow();
        yield return null;
        SnapNow();
    }

    [ContextMenu("Snap To Terrain")]
    public void SnapNow()
    {
        Vector3 position = transform.position;
        NPCWander wander = GetComponent<NPCWander>();
        float groundY;
        bool hasGround = wander != null
            ? wander.TryGetSafeGroundY(position, out groundY)
            : TryTerrainGround(position, out groundY);
        if (!hasGround)
            return;

        float bottomOffset = GetBottomOffset();
        position.y = groundY - bottomOffset + groundOffset;

        CharacterController controller = GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        transform.position = position;

        if (controllerWasEnabled)
            controller.enabled = true;
    }

    static bool TryTerrainGround(Vector3 position, out float groundY)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
                continue;
            Vector3 local = position - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x < 0f || local.z < 0f ||
                local.x > size.x || local.z > size.z)
                continue;
            groundY = terrain.SampleHeight(position) + terrain.transform.position.y;
            return true;
        }
        groundY = float.NegativeInfinity;
        return false;
    }

    public void UseVisualFooting(float offset)
    {
        useRendererBottom = true;
        groundOffset = Mathf.Max(0f, offset);
    }

    public bool TryGetVisualBottomY(out float bottomY)
    {
        if (TryGetNamedFootTransformBottomY(out bottomY))
            return true;

        if (TryGetAnimatorFootBottomY(out bottomY))
            return true;

        if (TryGetNamedFootRendererBottomY(out bottomY))
            return true;

        return TryGetAnyRendererBottomY(out bottomY);
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

            float candidateY = child.position.y - 0.08f;
            if (!found || candidateY < bottomY)
            {
                bottomY = candidateY;
                found = true;
            }
        }

        return found;
    }

    bool TryGetNamedFootRendererBottomY(out float bottomY)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bottomY = 0f;
        bool found = false;

        foreach (Renderer renderer in renderers)
        {
            if (ShouldIgnoreRenderer(renderer))
                continue;

            string lowerName = renderer.transform.name.ToLowerInvariant();
            if (!IsFootingName(lowerName))
                continue;

            float minY = renderer.bounds.min.y;
            if (!found || minY < bottomY)
            {
                bottomY = minY;
                found = true;
            }
        }

        return found;
    }

    bool TryGetAnimatorFootBottomY(out float bottomY)
    {
        bottomY = 0f;

        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
            return false;

        Transform left = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
        Transform right = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
        bool found = false;

        if (left != null)
        {
            bottomY = left.position.y - 0.08f;
            found = true;
        }

        if (right != null)
        {
            float rightBottom = right.position.y - 0.08f;
            bottomY = found ? Mathf.Min(bottomY, rightBottom) : rightBottom;
            found = true;
        }

        return found;
    }

    bool TryGetAnyRendererBottomY(out float bottomY)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bottomY = 0f;
        bool found = false;

        foreach (Renderer renderer in renderers)
        {
            if (ShouldIgnoreRenderer(renderer))
                continue;

            float minY = renderer.bounds.min.y;
            if (!found || minY < bottomY)
            {
                bottomY = minY;
                found = true;
            }
        }

        return found;
    }

    bool ShouldIgnoreRenderer(Renderer renderer)
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

    float GetBottomOffset()
    {
        if (useRendererBottom)
        {
            float visualBottomY;
            if (TryGetVisualBottomY(out visualBottomY))
                return visualBottomY - transform.position.y;
            return GetRendererBottomOffset();
        }

        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
            return controller.center.y - controller.height * 0.5f;

        Collider collider = GetComponent<Collider>();
        return collider != null ? collider.bounds.min.y - transform.position.y : 0f;
    }

    float GetRendererBottomOffset()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return 0f;

        bool found = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (ShouldIgnoreRenderer(renderer))
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found ? bounds.min.y - transform.position.y : 0f;
    }
}

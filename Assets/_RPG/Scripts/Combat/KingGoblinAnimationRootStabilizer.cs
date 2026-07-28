using UnityEngine;

public class KingGoblinAnimationRootStabilizer : MonoBehaviour
{
    [SerializeField] Transform visualRoot;
    [SerializeField] Transform rootBone;
    [SerializeField] bool lockRootBoneY;

    Vector3 visualLocalPosition;
    Quaternion visualLocalRotation;
    Vector3 rootBoneLocalPosition;

    void Awake()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (visualRoot == null && animator != null && animator.transform != transform)
            visualRoot = animator.transform;

        if (rootBone == null)
        {
            SkinnedMeshRenderer skinned = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned != null)
                rootBone = skinned.rootBone;
        }

        if (visualRoot != null)
        {
            visualLocalPosition = visualRoot.localPosition;
            visualLocalRotation = visualRoot.localRotation;
        }

        if (rootBone != null)
            rootBoneLocalPosition = rootBone.localPosition;
    }

    void LateUpdate()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualLocalPosition;
            visualRoot.localRotation = visualLocalRotation;
        }

        if (rootBone != null)
        {
            Vector3 pos = rootBone.localPosition;
            pos.x = rootBoneLocalPosition.x;
            pos.z = rootBoneLocalPosition.z;
            if (lockRootBoneY)
                pos.y = rootBoneLocalPosition.y;
            rootBone.localPosition = pos;
        }
    }
}

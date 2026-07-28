using UnityEngine;

public class KingGoblinVisualRootLock : MonoBehaviour
{
    [SerializeField] Transform visualRoot;
    [SerializeField] Transform rootBone;
    [SerializeField] bool lockRootBonePlanarMotion;
    [SerializeField] bool lockRootBoneYMotion;

    Vector3 visualLocalPosition;
    Quaternion visualLocalRotation;
    Vector3 boneLocalPosition;

    void Awake()
    {
        Animator animator = GetComponentInChildren<Animator>();
        if (visualRoot == null && animator != null && animator.transform != transform)
            visualRoot = animator.transform;

        if (rootBone == null && animator != null && animator.isHuman)
            rootBone = animator.GetBoneTransform(HumanBodyBones.Hips);
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
            boneLocalPosition = rootBone.localPosition;
    }

    void LateUpdate()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualLocalPosition;
            visualRoot.localRotation = visualLocalRotation;
        }

        if (lockRootBonePlanarMotion && rootBone != null)
        {
            Vector3 pos = rootBone.localPosition;
            pos.x = boneLocalPosition.x;
            pos.z = boneLocalPosition.z;
            if (lockRootBoneYMotion)
                pos.y = boneLocalPosition.y;
            rootBone.localPosition = pos;
        }
    }
}

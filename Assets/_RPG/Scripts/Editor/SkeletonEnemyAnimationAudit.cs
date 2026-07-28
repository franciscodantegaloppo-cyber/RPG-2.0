using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Repeatable edit-mode validation for the skeleton enemy. Besides catching a disconnected
// controller or weapon, it samples both attacks and proves that the sword actually follows the
// animated hand instead of remaining frozen in a world-space pose.
public static class SkeletonEnemyAnimationAudit
{
    const string PrefabPath = "Assets/_RPG/Prefabs/Enemies/SkeletonEnemy.prefab";
    const string ReportPrefix = "[SkeletonAnimationAudit]";

    [MenuItem("RPG/Enemies/Validate Skeleton Animations And Sword")]
    public static void Validate()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError(ReportPrefix + " Missing prefab: " + PrefabPath);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError(ReportPrefix + " No Animator found.");
                return;
            }

            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null)
            {
                Debug.LogError(ReportPrefix + " Skeleton combat controller is not assigned.");
                return;
            }

            HashSet<string> required = new HashSet<string>
            {
                "Idle", "Walk", "Run", "Attack", "AttackAlt", "Hit", "Death"
            };
            Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                required.Remove(child.state.name);
                if (child.state.motion is AnimationClip clip)
                    clips[child.state.name] = clip;
            }

            if (required.Count > 0)
                Debug.LogError(ReportPrefix + " Missing states: " + string.Join(", ", required));

            Transform hand = animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                : FindChild(animator.transform, "Wrist_R");
            SkeletonWeaponMarker marker = instance.GetComponentInChildren<SkeletonWeaponMarker>(true);
            Transform sword = marker != null ? marker.transform : null;
            if (hand == null || sword == null)
            {
                Debug.LogError(ReportPrefix + " Right hand or sword is missing.");
                return;
            }
            if (sword.parent != hand)
                Debug.LogError(ReportPrefix + " Sword is not parented directly to the right hand.");

            Bounds localBounds = CalculateLocalBounds(sword);
            Vector3 bladeDirection = sword.TransformDirection(Vector3.forward).normalized;
            float upAlignment = Vector3.Dot(bladeDirection, Vector3.up);
            Debug.Log(ReportPrefix + " states=7/7 avatarHuman=" + animator.isHuman +
                      " swordParent=" + sword.parent.name +
                      " localPosition=" + sword.localPosition.ToString("F4") +
                      " localEuler=" + sword.localEulerAngles.ToString("F2") +
                      " meshBounds=" + localBounds +
                      " idleBladeDirection=" + bladeDirection.ToString("F3") +
                      " upAlignment=" + upAlignment.ToString("F3"));
            if (upAlignment < .97f)
                Debug.LogError(ReportPrefix +
                               " Sword blade is not vertical in the relaxed pose.");

            ValidateAttackMotion(instance, sword, clips, "Attack");
            ValidateAttackMotion(instance, sword, clips, "AttackAlt");
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    static void ValidateAttackMotion(GameObject instance, Transform sword,
        Dictionary<string, AnimationClip> clips, string stateName)
    {
        if (!clips.TryGetValue(stateName, out AnimationClip clip) || clip == null)
        {
            Debug.LogError(ReportPrefix + " " + stateName + " has no clip.");
            return;
        }

        AnimationMode.StartAnimationMode();
        try
        {
            AnimationMode.SampleAnimationClip(instance, clip, 0f);
            Vector3 startPosition = sword.position;
            Quaternion startRotation = sword.rotation;
            AnimationMode.SampleAnimationClip(instance, clip, clip.length * .55f);
            float displacement = Vector3.Distance(startPosition, sword.position);
            float rotation = Quaternion.Angle(startRotation, sword.rotation);
            if (displacement < .08f || rotation < 20f)
                Debug.LogError(ReportPrefix + " " + stateName +
                               " does not move the sword enough. displacement=" +
                               displacement.ToString("F3") + " rotation=" + rotation.ToString("F1"));
            else
                Debug.Log(ReportPrefix + " " + stateName + " swordMotion displacement=" +
                          displacement.ToString("F3") + " rotation=" + rotation.ToString("F1"));
        }
        finally
        {
            AnimationMode.StopAnimationMode();
        }
    }

    static Bounds CalculateLocalBounds(Transform root)
    {
        bool initialized = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;
            Bounds bounds = mesh.bounds;
            Vector3[] corners =
            {
                bounds.center + Vector3.Scale(bounds.extents, new Vector3(-1,-1,-1)),
                bounds.center + Vector3.Scale(bounds.extents, new Vector3(-1,-1, 1)),
                bounds.center + Vector3.Scale(bounds.extents, new Vector3(-1, 1,-1)),
                bounds.center + Vector3.Scale(bounds.extents, new Vector3(-1, 1, 1)),
                bounds.center + Vector3.Scale(bounds.extents, new Vector3( 1,-1,-1)),
                bounds.center + Vector3.Scale(bounds.extents, new Vector3( 1,-1, 1)),
                bounds.center + Vector3.Scale(bounds.extents, new Vector3( 1, 1,-1)),
                bounds.center + Vector3.Scale(bounds.extents, new Vector3( 1, 1, 1))
            };
            foreach (Vector3 corner in corners)
            {
                Vector3 local = root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!initialized)
                {
                    result = new Bounds(local, Vector3.zero);
                    initialized = true;
                }
                else result.Encapsulate(local);
            }
        }
        return result;
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }
}

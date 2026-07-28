using UnityEngine;

// Bends the character's off-hand onto a two-handed weapon's grip point using Unity's built-in
// humanoid IK solver, so both hands visibly hold the weapon instead of only the hand it's
// attached to. WeaponSocket adds this to the Animator's own GameObject (IK callbacks only fire
// on the object that owns the Animator) and drives target/weight every frame. Requires "IK Pass"
// enabled on the animator controller's base layer, or OnAnimatorIK never fires.
public class TwoHandGripIK : MonoBehaviour
{
    public Transform target;
    public float weight;

    Animator anim;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (anim == null)
            return;

        float w = target != null ? weight : 0f;
        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
        // Forcing full rotation IK fights the solver at this reach angle (reaching across the
        // body to a raised grip) and produces a wildly twisted wrist even though the position
        // lands exactly on target. Position alone already reads as "hand is on the handle";
        // let the underlying animation's own forearm twist supply the rest.
        anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, w * 0.25f);
        if (w <= 0f)
            return;

        anim.SetIKPosition(AvatarIKGoal.LeftHand, target.position);
        anim.SetIKRotation(AvatarIKGoal.LeftHand, target.rotation);
    }
}

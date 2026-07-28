using UnityEngine;

/// Receives all animation events baked into RPG Character Mecanim clips.
/// Must be on the same GameObject as the Animator.
public class NPCAnimationEvents : MonoBehaviour
{
    void Hit()          { }
    void FootR()        { }
    void FootL()        { }
    void Land()         { }
    void Shoot()        { }
    void WeaponSwitch() { }
    void Step()         { }
    void Sheath()       { }
    void Unsheath()     { }
}

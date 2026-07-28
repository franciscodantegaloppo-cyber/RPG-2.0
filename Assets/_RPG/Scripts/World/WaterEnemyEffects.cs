using UnityEngine;

/// <summary>Shared water state for enemy AI and bosses.</summary>
public class WaterEnemyEffects : MonoBehaviour
{
    public const float MovementMultiplier = 0.25f;
    public const float ActionSpeedMultiplier = 0.25f;
    public const float GravityMultiplier = 0.08f;
    public const float JumpForceMultiplier = 1.28f;

    CharacterController characterController;
    CapsuleCollider capsule;

    public bool IsInWater { get; private set; }

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        capsule = GetComponent<CapsuleCollider>();
    }

    void Update()
    {
        Vector3 center = transform.position;
        if (characterController != null) center = transform.TransformPoint(characterController.center);
        else if (capsule != null) center = transform.TransformPoint(capsule.center);

        IsInWater = false;
        for (int i = 0; i < WaterSurfaceVolume.ActiveSurfaces.Count; i++)
        {
            WaterSurfaceVolume surface = WaterSurfaceVolume.ActiveSurfaces[i];
            if (surface != null && surface.ContainsHorizontal(center) && center.y < surface.SurfaceHeight + 0.18f)
            {
                IsInWater = true;
                break;
            }
        }
    }

    public float MovementScale => IsInWater ? MovementMultiplier : 1f;
    public float ActionScale => IsInWater ? ActionSpeedMultiplier : 1f;
    public float GravityScale => IsInWater ? GravityMultiplier : 1f;
    public float JumpScale => IsInWater ? JumpForceMultiplier : 1f;
}

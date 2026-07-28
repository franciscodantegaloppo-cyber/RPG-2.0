using UnityEngine;

public class WeaponController : MonoBehaviour
{
    [SerializeField] WeaponData weaponData;
    [SerializeField] TrailRenderer trailRenderer;

    public WeaponData Data => weaponData;

    void Awake()
    {
        if (trailRenderer != null)
            trailRenderer.emitting = false;
    }

    public void Initialize(WeaponData data)
    {
        weaponData = data;
        if (trailRenderer != null)
            trailRenderer.emitting = false;
    }

    public float GetDamage() => weaponData != null ? weaponData.baseDamage : 5f;
    public AudioClip GetSwingSound() => weaponData?.swingSound;
    public AudioClip GetHitSound() => weaponData?.hitSound;

    public void EnableTrail(bool enable)
    {
        if (trailRenderer != null)
            trailRenderer.emitting = enable;
    }
}

using UnityEngine;

// Keeps tower sentries on their assigned platform while allowing them to rotate and aim.
[DisallowMultipleComponent]
public class TowerGuardStationary : MonoBehaviour
{
    [SerializeField] Transform projectileCollisionIgnoreRoot;
    Vector3 stationPosition;
    Animator animator;

    public Transform ProjectileCollisionIgnoreRoot => projectileCollisionIgnoreRoot;

    public void ConfigureProjectileCollisionIgnore(Transform towerRoot)
    {
        projectileCollisionIgnoreRoot = towerRoot;
    }

    void Awake()
    {
        stationPosition = transform.position;
        animator = GetComponentInChildren<Animator>(true);
        if (animator != null) animator.applyRootMotion = false;
    }

    void OnEnable() => stationPosition = transform.position;

    void LateUpdate()
    {
        transform.position = stationPosition;
        if (animator != null && animator.applyRootMotion) animator.applyRootMotion = false;
    }
}

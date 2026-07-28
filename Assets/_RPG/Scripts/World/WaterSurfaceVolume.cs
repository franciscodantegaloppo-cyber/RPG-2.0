using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a flat WaterWorks plane as a water surface. Detection is deliberately
/// horizontal only: water is entered by moving beneath its visual surface, not
/// by colliding against it.
/// </summary>
[DisallowMultipleComponent]
public class WaterSurfaceVolume : MonoBehaviour
{
    static readonly List<WaterSurfaceVolume> activeSurfaces = new List<WaterSurfaceVolume>();
    public static IReadOnlyList<WaterSurfaceVolume> ActiveSurfaces => activeSurfaces;

    MeshFilter meshFilter;
    [SerializeField] bool circular;

    void Awake() => meshFilter = GetComponent<MeshFilter>();

    void OnEnable()
    {
        if (!activeSurfaces.Contains(this)) activeSurfaces.Add(this);
    }

    void OnDisable() => activeSurfaces.Remove(this);

    public bool ContainsHorizontal(Vector3 worldPosition)
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) return false;

        Bounds bounds = meshFilter.sharedMesh.bounds;
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        const float edgePadding = 0.08f;
        if (circular)
        {
            float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) + edgePadding;
            Vector2 fromCenter = new Vector2(
                local.x - bounds.center.x, local.z - bounds.center.z);
            return fromCenter.sqrMagnitude <= radius * radius;
        }
        return local.x >= bounds.min.x - edgePadding && local.x <= bounds.max.x + edgePadding &&
               local.z >= bounds.min.z - edgePadding && local.z <= bounds.max.z + edgePadding;
    }

    public void ConfigureCircular(bool value) => circular = value;

    public float SurfaceHeight
    {
        get
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            float localY = meshFilter != null && meshFilter.sharedMesh != null
                ? meshFilter.sharedMesh.bounds.center.y : 0f;
            return transform.TransformPoint(0f, localY, 0f).y;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureExistingWaterWorksPlanesAreMarked()
    {
        GameObject river = GameObject.Find("WaterWorks_River_Valley");
        if (river == null) return;

        foreach (MeshFilter filter in river.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name == "Water_Depth_Volume")
                continue;
            if (filter.GetComponent<WaterSurfaceVolume>() == null)
                filter.gameObject.AddComponent<WaterSurfaceVolume>();
        }
    }
}

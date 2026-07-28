using System.Collections.Generic;
using UnityEngine;

/// <summary>Finds the principal (longitudinal) local axis of a preview model.</summary>
public static class PreviewSpinAxisUtility
{
    public static Vector3 FindLongitudinalAxis(Transform modelRoot)
    {
        if (modelRoot == null) return Vector3.up;

        var points = new List<Vector3>(128);
        foreach (Renderer renderer in modelRoot.GetComponentsInChildren<Renderer>(true))
        {
            Bounds bounds = renderer.localBounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 localCorner = new Vector3(x == 0 ? min.x : max.x,
                                                  y == 0 ? min.y : max.y,
                                                  z == 0 ? min.z : max.z);
                Vector3 worldCorner = renderer.transform.TransformPoint(localCorner);
                points.Add(modelRoot.InverseTransformPoint(worldCorner));
            }
        }

        if (points.Count == 0) return Vector3.up;

        Vector3 mean = Vector3.zero;
        foreach (Vector3 point in points) mean += point;
        mean /= points.Count;

        float xx = 0f, xy = 0f, xz = 0f, yy = 0f, yz = 0f, zz = 0f;
        Bounds localBounds = new Bounds(points[0], Vector3.zero);
        foreach (Vector3 point in points)
        {
            localBounds.Encapsulate(point);
            Vector3 d = point - mean;
            xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
            yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
        }

        Vector3 size = localBounds.size;
        Vector3 axis = size.x >= size.y && size.x >= size.z ? Vector3.right
            : size.y >= size.z ? Vector3.up : Vector3.forward;

        // Power iteration over the covariance matrix gives the principal axis,
        // including prefabs whose blade / diamond is authored diagonally.
        for (int i = 0; i < 12; i++)
        {
            Vector3 next = new Vector3(
                xx * axis.x + xy * axis.y + xz * axis.z,
                xy * axis.x + yy * axis.y + yz * axis.z,
                xz * axis.x + yz * axis.y + zz * axis.z);
            if (next.sqrMagnitude < 0.000001f) break;
            axis = next.normalized;
        }

        return axis.sqrMagnitude > 0.5f ? axis.normalized : Vector3.up;
    }

    public static Quaternion AlignLongitudinalAxisUp(Vector3 axis)
    {
        if (axis.sqrMagnitude < 0.5f) return Quaternion.identity;
        axis.Normalize();

        // PCA axes have no inherent sign. Keep the dominant authored component
        // positive so the orientation is deterministic between previews.
        float dominant = Mathf.Abs(axis.x) >= Mathf.Abs(axis.y) && Mathf.Abs(axis.x) >= Mathf.Abs(axis.z)
            ? axis.x : Mathf.Abs(axis.y) >= Mathf.Abs(axis.z) ? axis.y : axis.z;
        if (dominant < 0f) axis = -axis;

        return Quaternion.FromToRotation(axis, Vector3.up);
    }
}

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public static class HouseColliderSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/HouseColliderSetup.generate";

    // RPG > Setup House Colliders
    // Adds a MeshCollider (fitted to the actual mesh shape, not just its AABB) to every
    // large MeshRenderer on layer 0 (Default) that doesn't already have a collider.
    // Using the real mesh avoids the "invisible wall" problem boxes give sloped roofs/eaves.
    // Safe to run multiple times.
    [MenuItem("RPG/Setup House Colliders")]
    public static void AddHouseColliders()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[HouseColliderSetup] Ignorado durante Play Mode. Sali de Play para ejecutar el setup.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        int removed = RemoveGeneratedHouseColliders();
        int added = 0;

        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);

        foreach (var mr in renderers)
        {
            var go = mr.gameObject;

            // Only target Default layer objects
            if (go.layer != 0) continue;
            if (!IsBuildingColliderCandidate(go, mr.bounds)) continue;

            // Skip if already has collider
            if (go.GetComponentInParent<Collider>() != null ||
                go.GetComponentInChildren<Collider>() != null) continue;

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Bounds b = mr.bounds;
            float height = b.size.y;
            float footprint = Mathf.Max(b.size.x, b.size.z);
            float minDim = Mathf.Min(b.size.x, b.size.y, b.size.z);
            if (minDim < 0.08f) continue;                   // too thin (decoration)
            if (height < 0.8f && footprint < 1.0f) continue; // too small

            var mesh = Undo.AddComponent<MeshCollider>(go);
            mesh.sharedMesh = mf.sharedMesh;
            mesh.convex = false; // static geometry, non-convex is fine and hugs the real shape

            EditorUtility.SetDirty(go);
            added++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"[HouseColliderSetup] Removed {removed} generated colliders and added {added} mesh-fitted MeshColliders.");
    }

    [InitializeOnLoadMethod]
    static void RunRequestedSetup()
    {
        EditorApplication.delayCall += TryRunRequestedSetup;
    }

    static void TryRunRequestedSetup()
    {
        string absoluteRequestPath = Path.GetFullPath(RequestPath);
        if (!File.Exists(absoluteRequestPath))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        File.Delete(absoluteRequestPath);
        string metaPath = absoluteRequestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        AddHouseColliders();
    }

    static int RemoveGeneratedHouseColliders()
    {
        int removed = 0;

        // Old-style bounds-fit BoxColliders from a previous run of this tool.
        var boxes = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude);
        foreach (var bc in boxes)
        {
            if (bc == null || bc.gameObject.layer != 0)
                continue;

            MeshRenderer mr = bc.GetComponent<MeshRenderer>();
            if (mr == null)
                continue;

            if (!LooksLikeRendererBoundsCollider(bc, mr))
                continue;

            Undo.DestroyObjectImmediate(bc);
            removed++;
        }

        // Mesh-fitted colliders from a previous run of this tool (sharedMesh matches the renderer's mesh).
        var meshColliders = Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude);
        foreach (var mc in meshColliders)
        {
            if (mc == null || mc.gameObject.layer != 0)
                continue;

            MeshFilter mf = mc.GetComponent<MeshFilter>();
            if (mf == null || mc.sharedMesh != mf.sharedMesh)
                continue;

            Undo.DestroyObjectImmediate(mc);
            removed++;
        }

        return removed;
    }

    static bool LooksLikeRendererBoundsCollider(BoxCollider box, MeshRenderer renderer)
    {
        Vector3 rendererLocalSize = renderer.transform.InverseTransformVector(renderer.bounds.size);
        rendererLocalSize = new Vector3(Mathf.Abs(rendererLocalSize.x), Mathf.Abs(rendererLocalSize.y), Mathf.Abs(rendererLocalSize.z));
        return Vector3.Distance(box.size, rendererLocalSize) < 0.08f;
    }

    static bool IsBuildingColliderCandidate(GameObject go, Bounds bounds)
    {
        string path = GetPath(go).ToLowerInvariant();
        if (path.Contains("terrain") || path.Contains("ground") || path.Contains("grass") ||
            path.Contains("flower") || path.Contains("tree") || path.Contains("rock") ||
            path.Contains("camera") || path.Contains("player") || path.Contains("npc") ||
            path.Contains("skeleton") || path.Contains("water") || path.Contains("road") ||
            path.Contains("path") || path.Contains("floor") || path.Contains("grid"))
            return false;

        bool buildingName = path.Contains("house") || path.Contains("wall") || path.Contains("log") ||
            path.Contains("beam") || path.Contains("base") || path.Contains("stone") ||
            path.Contains("plinth") || path.Contains("door") || path.Contains("window") ||
            path.Contains("roof") || path.Contains("stair") || path.Contains("step");
        if (!buildingName)
            return false;

        if (bounds.size.y < 0.55f)
            return false;
        if (Mathf.Max(bounds.size.x, bounds.size.z) > 7.5f)
            return false;
        if (bounds.size.x * bounds.size.z > 24f)
            return false;

        return true;
    }

    [MenuItem("RPG/Remove Added BoxColliders")]
    static void RemoveHouseColliders()
    {
        // Removes auto-added colliders (both legacy BoxColliders and current MeshColliders)
        // from Default-layer building objects, leaving manually placed colliders untouched.
        int removed = RemoveGeneratedHouseColliders();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[HouseColliderSetup] Removed {removed} generated colliders.");
    }

    static string GetPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}

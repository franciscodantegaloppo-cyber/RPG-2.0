using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupVillageWalkableSurfaces
{
    private const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    private const string HolderName = "VillageWalkableSurfaces";
    private const int GroundLayerFallback = 11;

    [MenuItem("Tools/RPG/Setup Village Walkable Surfaces")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        VillageLayout layout = Object.FindAnyObjectByType<VillageLayout>();
        if (layout == null)
        {
            Debug.LogWarning("[VillageWalkable] No VillageLayout found.");
            return;
        }

        int houseCount = 0;
        foreach (Transform house in layout.transform)
        {
            if (!house.name.StartsWith("House_", System.StringComparison.Ordinal))
                continue;

            Transform old = house.Find(HolderName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            Transform source = house.Find("Interior") ?? house;
            if (!TryComputeLocalBounds(house, source, out Bounds localBounds))
                continue;

            GameObject holder = new GameObject(HolderName);
            holder.transform.SetParent(house, false);
            holder.layer = GroundLayer();

            AddInteriorFloor(holder.transform, localBounds);
            AddDetectedStairSurfaces(holder.transform, house);
            houseCount++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"[VillageWalkable] Added walkable floor/stair colliders to {houseCount} village houses.");
    }

    private static void AddInteriorFloor(Transform holder, Bounds localBounds)
    {
        float thickness = 0.12f;
        Vector3 size = new Vector3(
            Mathf.Max(0.8f, localBounds.size.x - 0.9f),
            thickness,
            Mathf.Max(0.8f, localBounds.size.z - 0.9f));
        Vector3 center = new Vector3(localBounds.center.x, localBounds.min.y - thickness * 0.5f + 0.04f, localBounds.center.z);
        AddBox(holder, "InteriorFloor_Walkable", center, size);
    }

    private static void AddDetectedStairSurfaces(Transform holder, Transform house)
    {
        foreach (Renderer renderer in house.GetComponentsInChildren<Renderer>(true))
        {
            string lower = renderer.name.ToLowerInvariant();
            if (!lower.Contains("stair") && !lower.Contains("step"))
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 localMin = house.InverseTransformPoint(worldBounds.min);
            Vector3 localMax = house.InverseTransformPoint(worldBounds.max);
            Vector3 min = Vector3.Min(localMin, localMax);
            Vector3 max = Vector3.Max(localMin, localMax);
            Bounds localBounds = new Bounds((min + max) * 0.5f, max - min);

            float thickness = 0.14f;
            Vector3 center = new Vector3(localBounds.center.x, localBounds.max.y - thickness * 0.5f + 0.04f, localBounds.center.z);
            Vector3 size = new Vector3(Mathf.Max(0.5f, localBounds.size.x), thickness, Mathf.Max(0.5f, localBounds.size.z));
            AddBox(holder, renderer.name + "_Walkable", center, size);
        }
    }

    private static void AddBox(Transform holder, string name, Vector3 localCenter, Vector3 localSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(holder, false);
        go.transform.localPosition = localCenter;
        go.layer = holder.gameObject.layer;

        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.size = localSize;
        collider.isTrigger = true;
    }

    private static int GroundLayer()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        return groundLayer >= 0 ? groundLayer : GroundLayerFallback;
    }

    private static bool TryComputeLocalBounds(Transform house, Transform source, out Bounds bounds)
    {
        Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
        bounds = default;
        bool found = false;

        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            for (int z = 0; z <= 1; z++)
            {
                Vector3 corner = new Vector3(
                    x == 0 ? world.min.x : world.max.x,
                    y == 0 ? world.min.y : world.max.y,
                    z == 0 ? world.min.z : world.max.z);
                Vector3 local = house.InverseTransformPoint(corner);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            Bounds rendererLocal = new Bounds((min + max) * 0.5f, max - min);
            if (!found)
            {
                bounds = rendererLocal;
                found = true;
            }
            else
            {
                bounds.Encapsulate(rendererLocal);
            }
        }

        return found && bounds.size.sqrMagnitude > 0.01f;
    }
}

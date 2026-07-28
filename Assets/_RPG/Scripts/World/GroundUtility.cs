using UnityEngine;

// Shared ground-height resolver for NPCs/enemies that snap to the ground each frame
// (GroundSnapOnStart, NPCWander, NPCVisualGroundAligner). Terrain.SampleHeight() alone only
// covers the outdoor terrain mesh - house interiors, raised thresholds, and staircases are
// separate colliders (added by HouseColliderSetup) sitting above or beside the terrain, so an
// NPC that only ever asks the Terrain for its height floats at outdoor ground level the moment
// it walks onto one of those surfaces. This raycasts a tight window around the NPC's current
// position first (so nothing above head height, like a tree canopy, gets mistaken for floor) and
// only falls back to the raw terrain height when nothing closer is hit.
public static class GroundUtility
{
    const float MinWalkableNormalY = 0.45f;
    const float MaxUnmarkedSurfaceAboveProbe = 1.15f;

    public static float GetGroundY(Vector3 position, Transform ignoreRoot = null, float probeAbove = 3f, float probeBelow = 3f)
    {
        return TryGetGround(position, ignoreRoot, out GroundHit ground, probeAbove, probeBelow) ? ground.y : float.NegativeInfinity;
    }

    public static bool TryProjectToGround(Vector3 position, Transform ignoreRoot, out Vector3 groundedPosition, float probeAbove = 4f, float probeBelow = 6f)
    {
        if (TryGetGround(position, ignoreRoot, out GroundHit ground, probeAbove, probeBelow))
        {
            groundedPosition = new Vector3(position.x, ground.y, position.z);
            return true;
        }

        groundedPosition = position;
        return false;
    }

    public static bool TryGetGround(Vector3 position, Transform ignoreRoot, out GroundHit ground, float probeAbove = 3f, float probeBelow = 3f)
    {
        float terrainY = float.NegativeInfinity;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            terrainY = terrain.SampleHeight(position) + terrain.transform.position.y;

        Vector3 origin = new Vector3(position.x, position.y + probeAbove, position.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, probeAbove + probeBelow, ~0, QueryTriggerInteraction.Collide);

        GroundHit bestMarkedWalkable = default;
        bool hasMarkedWalkable = false;
        GroundHit bestPhysicalWalkable = new GroundHit(terrainY, null, terrainY);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider is TerrainCollider)
                continue;
            if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot))
                continue;

            bool markedWalkable = IsWalkableProbeCollider(hit.collider);
            if (hit.collider.isTrigger && !markedWalkable)
                continue;
            if (!markedWalkable && hit.normal.y < MinWalkableNormalY)
                continue;
            if (!markedWalkable && hit.point.y > position.y + MaxUnmarkedSurfaceAboveProbe)
                continue;

            GroundHit candidate = new GroundHit(hit.point.y, hit.collider, terrainY);
            if (markedWalkable)
            {
                if (!hasMarkedWalkable || candidate.y > bestMarkedWalkable.y)
                {
                    bestMarkedWalkable = candidate;
                    hasMarkedWalkable = true;
                }
                continue;
            }

            if (candidate.y > bestPhysicalWalkable.y)
                bestPhysicalWalkable = candidate;
        }

        ground = hasMarkedWalkable ? bestMarkedWalkable : bestPhysicalWalkable;
        return !float.IsNegativeInfinity(ground.y);
    }

    static bool IsWalkableProbeCollider(Collider collider)
    {
        Transform current = collider.transform;
        while (current != null)
        {
            // Temple pavilion platforms are raised far above the terrain. Treat the complete
            // named floor hierarchy as intentional ground, so enemies (including the crab)
            // snap to its top face instead of sampling the terrain below it.
            if (current.name == "VillageWalkableSurfaces" ||
                current.name.IndexOf("Pavilion Floor", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                current.name.IndexOf("Pavilion_Floor", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            current = current.parent;
        }

        return false;
    }

    public readonly struct GroundHit
    {
        public readonly float y;
        public readonly Collider collider;
        public readonly float terrainY;

        public GroundHit(float y, Collider collider, float terrainY)
        {
            this.y = y;
            this.collider = collider;
            this.terrainY = terrainY;
        }

        public bool IsTerrain => collider == null;
    }
}

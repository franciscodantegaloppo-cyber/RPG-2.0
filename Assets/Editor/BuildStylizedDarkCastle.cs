using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildStylizedDarkCastle
{
    private const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    private const string RootName = "StylizedDarkCastle_FarFromSpawn";
    private const string PrefabRoot = "Assets/StylizedDarkCastle/Prefabs/";
    private const string MaterialRoot = "Assets/StylizedDarkCastle/Materials/";

    // Site chosen far from spawn (0,0,0), matching the previous build site
    private const float SiteX = -200f;
    private const float SiteZ = 350f;

    // Fixed plateau height instead of a live terrain sample: sampling the centre of the flatten
    // footprint on every rebuild is not perfectly idempotent (each SetHeights call very slightly
    // perturbs the exact sample point via heightmap resolution rounding), so repeated rebuilds
    // were drifting the whole castle downward a little more each time. A fixed value is stable.
    private const float PlateauY = 48.9f;
    private const string NatureRootName = "StylizedDarkCastle_SurroundingNature";

    private static readonly Dictionary<string, GameObject> PrefabCache = new();
    private static readonly Dictionary<string, Material> MaterialCache = new();

    [MenuItem("RPG/World/Build Stylized Dark Castle")]
    public static void Build()
    {
        var scene = EditorSceneUtility.OpenSceneSafely(ScenePath);

        var existing = GameObject.Find(RootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var existingNature = GameObject.Find(NatureRootName);
        if (existingNature != null)
        {
            Object.DestroyImmediate(existingNature);
        }

        float plateauY = FlattenTerrainPatch(SiteX, SiteZ - 25f, 110f, 75f, 35f);

        var root = new GameObject(RootName);
        root.transform.position = new Vector3(SiteX, plateauY, SiteZ);
        root.transform.rotation = Quaternion.identity;

        var wallsRoot = NewChild(root.transform, "CurtainWalls");
        var towersRoot = NewChild(root.transform, "Towers");
        var floorsRoot = NewChild(root.transform, "MegaKeep_Floors");
        var shellRoot = NewChild(root.transform, "MegaKeep_Shell");
        var stairsRoot = NewChild(root.transform, "MegaKeep_Stairwells");
        var pathRoot = NewChild(root.transform, "Courtyard_Paths");
        var interiorRoot = NewChild(root.transform, "Interior_Furniture");
        var lightRoot = NewChild(root.transform, "Lighting");

        BuildCurtainWall(wallsRoot);
        BuildWallWalkways(wallsRoot); // Adds a gorgeous, solid 5m wide stone walkway on top of all outer walls
        BuildTowers(towersRoot);
        BuildMegaKeep(floorsRoot, shellRoot, stairsRoot, interiorRoot, lightRoot);
        BuildEntryPath(pathRoot);
        BuildOuterLighting(lightRoot);
        BuildSurroundingNature();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"Built {RootName} at {root.transform.position} (plateau Y={plateauY:F2}).");
    }

    // ---------------------------------------------------------------- terrain

    private static float FlattenTerrainPatch(float cx, float cz, float halfWidthX, float halfWidthZ, float falloff)
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("No active terrain found; skipping terrain flatten.");
            return 0f;
        }

        var data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        int res = data.heightmapResolution;
        float sizeX = data.size.x;
        float sizeZ = data.size.z;

        float target = PlateauY;

        float totalHalfX = halfWidthX + falloff;
        float totalHalfZ = halfWidthZ + falloff;

        int xBase = Mathf.Clamp(Mathf.FloorToInt((cx - totalHalfX - terrainPos.x) / sizeX * (res - 1)), 0, res - 1);
        int xEnd = Mathf.Clamp(Mathf.CeilToInt((cx + totalHalfX - terrainPos.x) / sizeX * (res - 1)), 0, res - 1);
        int zBase = Mathf.Clamp(Mathf.FloorToInt((cz - totalHalfZ - terrainPos.z) / sizeZ * (res - 1)), 0, res - 1);
        int zEnd = Mathf.Clamp(Mathf.CeilToInt((cz + totalHalfZ - terrainPos.z) / sizeZ * (res - 1)), 0, res - 1);

        int width = xEnd - xBase + 1;
        int height = zEnd - zBase + 1;
        if (width <= 0 || height <= 0)
        {
            Debug.LogWarning("Flatten region out of terrain bounds.");
            return target;
        }

        var heights = data.GetHeights(xBase, zBase, width, height);
        for (int zi = 0; zi < height; zi++)
        {
            float worldZ = terrainPos.z + ((zBase + zi) / (float)(res - 1)) * sizeZ;
            for (int xi = 0; xi < width; xi++)
            {
                float worldX = terrainPos.x + ((xBase + xi) / (float)(res - 1)) * sizeX;
                float dx = Mathf.Abs(worldX - cx) - halfWidthX;
                float dz = Mathf.Abs(worldZ - cz) - halfWidthZ;
                float outside = Mathf.Max(dx, dz);

                if (outside <= 0f)
                {
                    heights[zi, xi] = (target - terrainPos.y) / data.size.y;
                }
                else if (outside < falloff)
                {
                    float t = outside / falloff;
                    t = t * t * (3f - 2f * t);
                    float original = heights[zi, xi] * data.size.y + terrainPos.y;
                    float blended = Mathf.Lerp(target, original, t);
                    heights[zi, xi] = (blended - terrainPos.y) / data.size.y;
                }
            }
        }

        data.SetHeights(xBase, zBase, heights);
        return target;
    }

    // ---------------------------------------------------------------- helpers

    private static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject LoadPrefab(string relativePath)
    {
        if (PrefabCache.TryGetValue(relativePath, out var cached))
        {
            return cached;
        }

        string fullPath = relativePath.StartsWith("Assets/") ? relativePath : (PrefabRoot + relativePath);
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
        if (go == null)
        {
            Debug.LogError("Missing prefab: " + fullPath);
        }

        PrefabCache[relativePath] = go;
        return go;
    }

    private static Material LoadMat(string matName)
    {
        if (MaterialCache.TryGetValue(matName, out var cached))
        {
            return cached;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + matName + ".mat");
        MaterialCache[matName] = mat;
        return mat;
    }

    private static GameObject Place(Transform parent, string relativePath, Vector3 localPos, float yRot, string name)
    {
        var prefab = LoadPrefab(relativePath);
        if (prefab == null)
        {
            return null;
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        inst.transform.localPosition = localPos;
        inst.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        inst.name = name;
        return inst;
    }

    private static void PlaceDirtTile2x2(Transform parent, float centerX, float centerZ, string name)
    {
        Place(parent, "CastlePartBase/dirt_2x2.prefab", new Vector3(centerX + 2.5f, 0f, centerZ - 2.5f), 0f, name);
    }

    // Creates a solid flat block with unique material tiling (solving the stretched/gigantic texture issue)
    private static void BuildFlatSlab(Transform parent, string name, float cx, float cz, float sizeX, float sizeZ, float y, string matName)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(cx, y, cz);
        go.transform.localScale = new Vector3(sizeX, 0.4f, sizeZ);
        
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        var col = go.AddComponent<BoxCollider>();
        col.size = Vector3.one;

        var renderer = go.GetComponent<Renderer>();
        var mat = LoadMat(matName);
        if (mat != null)
        {
            // Instantiate a copy of the material so setting tiling doesn't affect other objects
            var matCopy = new Material(mat);
            matCopy.name = mat.name + "_TileCopy_" + sizeX + "x" + sizeZ;
            Vector2 tiling = new Vector2(sizeX / 5f, sizeZ / 5f);
            
            if (matCopy.HasProperty("_BaseMap")) matCopy.SetTextureScale("_BaseMap", tiling);
            if (matCopy.HasProperty("_MainTex")) matCopy.SetTextureScale("_MainTex", tiling);
            
            renderer.sharedMaterial = matCopy;
        }
    }

    // Creates a massive solid masonry block (used for Keep stairs foundation to avoid floating look)
    private static void BuildSolidSupportBlock(Transform parent, string name, float cx, float cz, float sizeX, float sizeY, float sizeZ, float cy, string matName)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(cx, cy, cz);
        go.transform.localScale = new Vector3(sizeX, sizeY, sizeZ);

        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        var col = go.AddComponent<BoxCollider>();
        col.size = Vector3.one;

        var renderer = go.GetComponent<Renderer>();
        var mat = LoadMat(matName);
        if (mat != null)
        {
            var matCopy = new Material(mat);
            matCopy.name = mat.name + "_SolidSupport_" + sizeX + "x" + sizeY + "x" + sizeZ;
            Vector2 tiling = new Vector2(sizeX / 5f, sizeZ / 5f);
            
            if (matCopy.HasProperty("_BaseMap")) matCopy.SetTextureScale("_BaseMap", tiling);
            if (matCopy.HasProperty("_MainTex")) matCopy.SetTextureScale("_MainTex", tiling);
            
            renderer.sharedMaterial = matCopy;
        }
    }

    // ---------------------------------------------------------------- curtain wall (outer defensive ring)

    private const float WallXMin = -105f, WallXMax = 105f, WallZMin = -70f, WallZMax = 45f;
    private const float GateX = 0f;

    private static void BuildCurtainWall(Transform wallsRoot)
    {
        Place(wallsRoot, "CastlePartBase/Wall_Corner_Outer_Large.prefab", new Vector3(WallXMax, 0, WallZMax), 0f, "Corner_NE");
        Place(wallsRoot, "CastlePartBase/Wall_Corner_Outer_Large.prefab", new Vector3(WallXMax, 0, WallZMin), 90f, "Corner_SE");
        Place(wallsRoot, "CastlePartBase/Wall_Corner_Outer_Large.prefab", new Vector3(WallXMin, 0, WallZMin), 180f, "Corner_SW");
        Place(wallsRoot, "CastlePartBase/Wall_Corner_Outer_Large.prefab", new Vector3(WallXMin, 0, WallZMax), 270f, "Corner_NW");

        Place(wallsRoot, "CastlePartBase/Battlement_Normal.prefab", new Vector3(WallXMax, 6, WallZMax), 0f, "CornerCap_NE");
        Place(wallsRoot, "CastlePartBase/Battlement_Normal.prefab", new Vector3(WallXMax, 6, WallZMin), 90f, "CornerCap_SE");
        Place(wallsRoot, "CastlePartBase/Battlement_Normal.prefab", new Vector3(WallXMin, 6, WallZMin), 180f, "CornerCap_SW");
        Place(wallsRoot, "CastlePartBase/Battlement_Normal.prefab", new Vector3(WallXMin, 6, WallZMax), 270f, "CornerCap_NW");

        BuildWallRunX(wallsRoot, WallZMax, 0f, "N", float.NaN);
        BuildWallRunX(wallsRoot, WallZMin, 180f, "S", GateX);
        BuildWallRunZ(wallsRoot, WallXMax, 90f, "E");
        BuildWallRunZ(wallsRoot, WallXMin, 270f, "W");

        Place(wallsRoot, "CastlePartBase/Wall_Arch_Left.prefab", new Vector3(GateX - 5f, 0, WallZMin), 180f, "Gate_Arch_Left");
        Place(wallsRoot, "CastlePartBase/Wall_Arch_Right.prefab", new Vector3(GateX + 5f, 0, WallZMin), 180f, "Gate_Arch_Right");
    }

    private static void BuildWallRunX(Transform wallsRoot, float fixedZ, float rot, string tag, float gateAt)
    {
        int idx = 0;
        for (float x = WallXMin + 2.5f; x < WallXMax - 1f; x += 5f)
        {
            // <=2.5 (not <2.5) because gateAt=0 sits exactly between two segment centres on this
            // grid (segments land on ...-2.5, 2.5...) - a strict "<" would match neither and the
            // gate would silently never get placed. This opens both flanking segments, giving a
            // proper 10m-wide entrance instead of a solid wall.
            bool isGate = !float.IsNaN(gateAt) && Mathf.Abs(x - gateAt) <= 2.5f;
            string prefab;
            if (isGate) prefab = "CastlePartBase/Wall_Gate.prefab";
            else if (idx % 7 == 3) prefab = "CastlePartBase/Wall_ArrowSlit.prefab";
            else if (idx % 5 == 2) prefab = "CastlePartBase/Wall_Window.prefab";
            else prefab = "CastlePartBase/Wall_Plain.prefab";

            Place(wallsRoot, prefab, new Vector3(x, 0, fixedZ), rot, isGate ? "MainGate" : $"Wall_{tag}_{idx}");
            if (!isGate)
            {
                Place(wallsRoot, "CastlePartBase/Battlement_Normal.prefab", new Vector3(x, 6, fixedZ), rot, $"Batt_{tag}_{idx}");
            }
            idx++;
        }
    }

    private static void BuildWallRunZ(Transform wallsRoot, float fixedX, float rot, string tag)
    {
        int idx = 0;
        for (float z = WallZMin + 2.5f; z < WallZMax - 1f; z += 5f)
        {
            string prefab = (idx % 7 == 3) ? "CastlePartBase/Wall_ArrowSlit.prefab" :
                             (idx % 5 == 2) ? "CastlePartBase/Wall_Window.prefab" :
                             "CastlePartBase/Wall_Plain.prefab";
            Place(wallsRoot, prefab, new Vector3(fixedX, 0, z), rot, $"Wall_{tag}_{idx}");
            Place(wallsRoot, "CastlePartBase/Battlement_Normal.prefab", new Vector3(fixedX, 6, z), rot, $"Batt_{tag}_{idx}");
            idx++;
        }
    }

    // Fills the top of the outer curtain walls with a solid walkable stone chemin de ronde, eliminating the transparent look
    private static void BuildWallWalkways(Transform wallsRoot)
    {
        // North walkway
        for (float x = WallXMin; x <= WallXMax; x += 5f)
        {
            BuildFlatSlab(wallsRoot, $"Walkway_N_{x}", x, WallZMax, 5f, 5f, 5.8f, "mat_floorTile");
        }
        // South walkway
        for (float x = WallXMin; x <= WallXMax; x += 5f)
        {
            if (Mathf.Abs(x - GateX) >= 5f)
            {
                BuildFlatSlab(wallsRoot, $"Walkway_S_{x}", x, WallZMin, 5f, 5f, 5.8f, "mat_floorTile");
            }
        }
        // East walkway
        for (float z = WallZMin + 5f; z < WallZMax; z += 5f)
        {
            BuildFlatSlab(wallsRoot, $"Walkway_E_{z}", WallXMax, z, 5f, 5f, 5.8f, "mat_floorTile");
        }
        // West walkway
        for (float z = WallZMin + 5f; z < WallZMax; z += 5f)
        {
            BuildFlatSlab(wallsRoot, $"Walkway_W_{z}", WallXMin, z, 5f, 5f, 5.8f, "mat_floorTile");
        }
    }

    // ---------------------------------------------------------------- towers

    private static void BuildTowers(Transform towersRoot)
    {
        // Corner turrets are bigger (15x15) so a real Stairs_Stone flight fits with a walkway
        // beside it - the same proven pattern used for the main keep's stairwells.
        BuildTower(towersRoot, "Turret_NE", ShellXMax, ShellZMax, 7, 180f, 7.5f);
        BuildTower(towersRoot, "Turret_SE", ShellXMax, ShellZMin, 7, 0f, 7.5f);
        BuildTower(towersRoot, "Turret_SW", ShellXMin, ShellZMin, 7, 0f, 7.5f);
        BuildTower(towersRoot, "Turret_NW", ShellXMin, ShellZMax, 7, 180f, 7.5f);

        // Gate towers stay small (10x10) - they only need one flight, using a narrow wooden
        // staircase with a walkway on either side instead.
        BuildTower(towersRoot, "GateTower_Left", -15f, GroundZMin + 5f, 2, 0f, 5f);
        BuildTower(towersRoot, "GateTower_Right", 15f, GroundZMin + 5f, 2, 0f, 5f);
    }

    private static void BuildTower(Transform parent, string name, float rootX, float rootZ, int floors, float doorRot, float half)
    {
        var towerRoot = NewChild(parent, name);
        bool bigTower = half >= 7f;

        for (int floor = 0; floor < floors; floor++)
        {
            float y = floor * 6f;
            bool isTop = floor == floors - 1;
            BuildTowerRing(towerRoot, rootX, rootZ, y, floor == 0 ? doorRot : float.NaN, half);
            BuildTowerFloorSlab(towerRoot, name, rootX, rootZ, y, !isTop, half, bigTower);

            if (!isTop)
            {
                if (bigTower)
                {
                    Place(towerRoot, "CastlePartBase/Stairs_Stone.prefab", new Vector3(rootX + 5f, y, rootZ - 5f), 0f, $"{name}_Stairs_{floor}");
                    Place(towerRoot, "CastlePartBase/Railing_Normal.prefab", new Vector3(rootX - 5f, y + 6f, rootZ - 5f), 0f, $"{name}_Rail_{floor}_A");
                    Place(towerRoot, "CastlePartBase/Railing_Normal.prefab", new Vector3(rootX + 5f, y + 6f, rootZ - 5f), 0f, $"{name}_Rail_{floor}_B");
                }
                else
                {
                    Place(towerRoot, "CastlePartBase/Stairs_Wood.prefab", new Vector3(rootX + 1.25f, y, rootZ + 2.3f), 0f, $"{name}_Stairs_{floor}");
                }
            }
        }

        float topY = floors * 6f;
        // Wall_Merlons_Corner's own content sits 6m ABOVE its pivot (verified by measurement),
        // so the pivot must be placed 6m below the intended crenellation height, not at it.
        float merlonY = topY - 6f;
        Place(towerRoot, "CastlePartBase/Wall_Merlons_Corner.prefab", new Vector3(rootX - half, merlonY, rootZ - half), 180f, $"{name}_Merlon_SW");
        Place(towerRoot, "CastlePartBase/Wall_Merlons_Corner.prefab", new Vector3(rootX + half, merlonY, rootZ - half), 90f, $"{name}_Merlon_SE");
        Place(towerRoot, "CastlePartBase/Wall_Merlons_Corner.prefab", new Vector3(rootX + half, merlonY, rootZ + half), 0f, $"{name}_Merlon_NE");
        Place(towerRoot, "CastlePartBase/Wall_Merlons_Corner.prefab", new Vector3(rootX - half, merlonY, rootZ + half), 270f, $"{name}_Merlon_NW");
        BuildFlatSlab(towerRoot, $"{name}_Roof", rootX, rootZ, half * 2f + 0.2f, half * 2f + 0.2f, topY + 0.15f, "mat_roof");
    }

    private static void BuildTowerRing(Transform parent, float rootX, float rootZ, float y, float doorSide, float half)
    {
        int segs = Mathf.RoundToInt(half * 2f / 5f);
        PlaceTowerWallRun(parent, rootZ - half, rootX, y, 180f, Mathf.Approximately(doorSide, 180f), $"S_{y}", segs, true);
        PlaceTowerWallRun(parent, rootZ + half, rootX, y, 0f, Mathf.Approximately(doorSide, 0f), $"N_{y}", segs, true);
        PlaceTowerWallRun(parent, rootX + half, rootZ, y, 90f, Mathf.Approximately(doorSide, 90f), $"E_{y}", segs, false);
        PlaceTowerWallRun(parent, rootX - half, rootZ, y, 270f, Mathf.Approximately(doorSide, 270f), $"W_{y}", segs, false);

        Place(parent, "CastlePartBase/Wall_Corner_Outer_Small.prefab", new Vector3(rootX - half, y, rootZ - half), 180f, $"TCorner_SW_{y}");
        Place(parent, "CastlePartBase/Wall_Corner_Outer_Small.prefab", new Vector3(rootX + half, y, rootZ - half), 90f, $"TCorner_SE_{y}");
        Place(parent, "CastlePartBase/Wall_Corner_Outer_Small.prefab", new Vector3(rootX + half, y, rootZ + half), 0f, $"TCorner_NE_{y}");
        Place(parent, "CastlePartBase/Wall_Corner_Outer_Small.prefab", new Vector3(rootX - half, y, rootZ + half), 270f, $"TCorner_NW_{y}");
    }

    private static void PlaceTowerWallRun(Transform parent, float fixedCoord, float centerCoord, float y, float rot, bool withDoor, string tag, int segs, bool runsAlongX)
    {
        bool doorPlaced = false;
        int mid = (segs - 1) / 2;
        for (int i = 0; i < segs; i++)
        {
            float offset = (i - (segs - 1) / 2f) * 5f;
            bool isDoor = withDoor && !doorPlaced && i == mid;
            if (isDoor) doorPlaced = true;
            string prefab = isDoor ? "CastlePartBase/Wall_Door_Floor_Mid.prefab" :
                            (i % 2 == 0 ? "CastlePartBase/Wall_Window.prefab" : "CastlePartBase/Wall_Plain.prefab");
            Vector3 pos = runsAlongX ? new Vector3(centerCoord + offset, y, fixedCoord) : new Vector3(fixedCoord, y, centerCoord + offset);
            Place(parent, prefab, pos, rot, $"T{tag}_{i}");
        }
    }

    // Big turrets: a real 10-wide Stairs_Stone flight with a solid landing on either side (matching
    // its true footprint - the earlier version assumed it was only 5m wide, which is what let the
    // side "landings" overlap the stair mesh and block the climb). Small gate towers: a narrow
    // Stairs_Wood flight with a walkway strip flanking it on both sides.
    private static void BuildTowerFloorSlab(Transform parent, string name, float rootX, float rootZ, float y, bool hasHole, float half, bool bigTower)
    {
        if (!hasHole)
        {
            BuildFlatSlab(parent, $"{name}_Floor_{y}", rootX, rootZ, half * 2f - 0.4f, half * 2f - 0.4f, y, "mat_floorTile");
            return;
        }

        if (bigTower)
        {
            BuildFlatSlab(parent, $"{name}_FloorN_{y}", rootX, rootZ + 2.5f, half * 2f - 0.4f, 10f - 0.4f, y, "mat_floorTile");
            BuildFlatSlab(parent, $"{name}_FloorSW_{y}", rootX - 6.25f, rootZ - 5f, 2.3f, 4.6f, y, "mat_floorTile");
            BuildFlatSlab(parent, $"{name}_FloorSE_{y}", rootX + 6.25f, rootZ - 5f, 2.3f, 4.6f, y, "mat_floorTile");
        }
        else
        {
            BuildFlatSlab(parent, $"{name}_FloorW_{y}", rootX - 3.15f, rootZ, 3.3f, half * 2f - 0.4f, y, "mat_floorTile");
            BuildFlatSlab(parent, $"{name}_FloorE_{y}", rootX + 3.15f, rootZ, 3.3f, half * 2f - 0.4f, y, "mat_floorTile");
        }
    }

    // ---------------------------------------------------------------- mega keep (fills the whole bailey, 5 floors)

    private const float ShellXMin = -95f, ShellXMax = 95f, ShellZMin = -57.5f, ShellZMax = 32.5f;
    private const float GroundXMin = -103f, GroundXMax = 103f, GroundZMin = -69f, GroundZMax = 44f;
    private const float StairHoleHalfWidth = 5f; // matches Stairs_Stone's real 10m footprint exactly
    private const float StairZMin = -17.5f, StairZMax = -7.5f;
    private static readonly float[] StairXs = { -55f, 0f, 55f };

    private readonly struct RoomBay
    {
        public readonly float Width;
        public readonly string Tag;
        public readonly string Theme;

        public RoomBay(float width, string tag, string theme)
        {
            Width = width;
            Tag = tag;
            Theme = theme;
        }
    }

    private static void BuildMegaKeep(Transform floorsRoot, Transform shellRoot, Transform stairsRoot, Transform interiorRoot, Transform lightRoot)
    {
        BuildFloorSlab(floorsRoot, 0f, GroundXMin, GroundXMax, GroundZMin, GroundZMax, "Ground");
        for (int level = 1; level <= 4; level++)
        {
            BuildFloorSlab(floorsRoot, level * 6f, ShellXMin, ShellXMax, ShellZMin, ShellZMax, $"Floor{level + 1}");
        }

        BuildFlatSlab(floorsRoot, "RoofCap", (ShellXMin + ShellXMax) / 2f, (ShellZMin + ShellZMax) / 2f,
            ShellXMax - ShellXMin, ShellZMax - ShellZMin, 30.15f, "mat_roof");

        BuildShellCrown(shellRoot);
        BuildStairwells(stairsRoot);

        BuildFloor(shellRoot, interiorRoot, lightRoot, 0, GroundFloorNorth, GroundFloorSouth);
        BuildFloor(shellRoot, interiorRoot, lightRoot, 1, Floor2North, Floor2South);
        BuildFloor(shellRoot, interiorRoot, lightRoot, 2, Floor3North, Floor3South);
        BuildFloor(shellRoot, interiorRoot, lightRoot, 3, Floor4North, Floor4South);
        BuildFloor(shellRoot, interiorRoot, lightRoot, 4, Floor5North, Floor5South);
    }

    private static void BuildFloorSlab(Transform floorsRoot, float y, float xMin, float xMax, float zMin, float zMax, string tag)
    {
        // Room bay floors, north and south of the whole corridor band.
        BuildFlatSlab(floorsRoot, $"{tag}_NorthBays", (xMin + xMax) / 2f, (StairZMax + zMax) / 2f, xMax - xMin, zMax - StairZMax, y, "mat_floorTile");
        BuildFlatSlab(floorsRoot, $"{tag}_SouthBays", (xMin + xMax) / 2f, (zMin + StairZMin) / 2f, xMax - xMin, StairZMin - zMin, y, "mat_floorTile");

        // Two ALWAYS-solid walkway strips flanking the stair band, so the corridor is walkable
        // end-to-end at every floor no matter where the stairwells are - a player never has to
        // step onto a stair to get past this section.
        BuildFlatSlab(floorsRoot, $"{tag}_WalkNorth", (xMin + xMax) / 2f, -8.75f, xMax - xMin, 2.5f, y, "mat_floorTile"); // z -10..-7.5
        BuildFlatSlab(floorsRoot, $"{tag}_WalkSouth", (xMin + xMax) / 2f, -16.25f, xMax - xMin, 2.5f, y, "mat_floorTile"); // z -17.5..-15

        // Stair band itself (z -15..-10, depth 5): solid on the ground floor (nothing below to
        // climb from), and on upper floors solid everywhere EXCEPT the exact 10m footprint of
        // each Stairs_Stone piece - not a wider guess, which is what let the earlier "landings"
        // overlap the stair mesh and block the climb.
        if (y <= 0.01f)
        {
            BuildFlatSlab(floorsRoot, $"{tag}_StairBandSolid", (xMin + xMax) / 2f, -12.5f, xMax - xMin, 5f, y, "mat_floorTile");
        }
        else
        {
            float prevEdge = xMin;
            int seg = 0;
            foreach (var hx in StairXs)
            {
                float holeLeft = hx - StairHoleHalfWidth;
                float holeRight = hx + StairHoleHalfWidth;
                if (holeLeft > prevEdge)
                {
                    BuildFlatSlab(floorsRoot, $"{tag}_StairBandMid_{seg}", (prevEdge + holeLeft) / 2f, -12.5f, holeLeft - prevEdge, 5f, y, "mat_floorTile");
                    seg++;
                }
                prevEdge = holeRight;
            }
            if (xMax > prevEdge)
            {
                BuildFlatSlab(floorsRoot, $"{tag}_StairBandMid_{seg}", (prevEdge + xMax) / 2f, -12.5f, xMax - prevEdge, 5f, y, "mat_floorTile");
            }
        }
    }

    private static void BuildShellCrown(Transform shellRoot)
    {
        for (int level = 0; level < 5; level++)
        {
            BuildShellRing(shellRoot, level * 6f, level);
        }

        // Merlon pivots need the same -6m correction as the towers (see BuildTower).
        Place(shellRoot, "CastlePartBase/Wall_Merlons_Corner.prefab", new Vector3(ShellXMin, 24f, ShellZMin), 180f, "Shell_Merlon_SW");
        Place(shellRoot, "CastlePartBase/Wall_Merlons_Corner.prefab", new Vector3(ShellXMax, 24f, ShellZMin), 90f, "Shell_Merlon_SE");
        Place(shellRoot, "CastlePartBase/Wall_Merlons_Corner.prefab", new Vector3(ShellXMax, 24f, ShellZMax), 0f, "Shell_Merlon_NE");
        Place(shellRoot, "CastlePartBase/Wall_Merlons_Corner.prefab", new Vector3(ShellXMin, 24f, ShellZMax), 270f, "Shell_Merlon_NW");

        // Main entrance: a proper gate opening (not the broken decorative double-door prop),
        // flanked by the same ornamental arches used on the outer curtain gate.
        Place(shellRoot, "CastlePartBase/Wall_Arch_Left.prefab", new Vector3(GateX - 5f, 0f, ShellZMin), 180f, "MainGate_Arch_Left");
        Place(shellRoot, "CastlePartBase/Wall_Arch_Right.prefab", new Vector3(GateX + 5f, 0f, ShellZMin), 180f, "MainGate_Arch_Right");
    }

    private static void BuildShellRing(Transform shellRoot, float y, int level)
    {
        int idx = 0;
        for (float x = ShellXMin + 2.5f; x < ShellXMax - 1f; x += 5f)
        {
            // See BuildWallRunX: GateX=0 sits exactly between two segment centres, so this must
            // be <= (inclusive) or the entrance silently never gets placed.
            bool isEntrance = level == 0 && Mathf.Abs(x - GateX) <= 2.5f;
            string prefab = isEntrance ? "CastlePartBase/Wall_Gate.prefab" :
                             (idx % 2 == 1) ? "CastlePartBase/Wall_Window.prefab" : "CastlePartBase/Wall_Plain.prefab";
            Place(shellRoot, prefab, new Vector3(x, y, ShellZMin), 180f, $"ShellS_{level}_{idx}");
            idx++;
        }

        idx = 0;
        for (float x = ShellXMin + 2.5f; x < ShellXMax - 1f; x += 5f)
        {
            string prefab = (idx % 2 == 1) ? "CastlePartBase/Wall_Window.prefab" : "CastlePartBase/Wall_Plain.prefab";
            Place(shellRoot, prefab, new Vector3(x, y, ShellZMax), 0f, $"ShellN_{level}_{idx}");
            idx++;
        }

        idx = 0;
        for (float z = ShellZMin + 2.5f; z < ShellZMax - 1f; z += 5f)
        {
            string prefab = (idx % 3 == 1) ? "CastlePartBase/Wall_Window.prefab" :
                             (idx % 7 == 3) ? "CastlePartBase/Wall_ArrowSlit.prefab" :
                             "CastlePartBase/Wall_Plain.prefab";
            Place(shellRoot, prefab, new Vector3(ShellXMax, y, z), 90f, $"ShellE_{level}_{idx}");
            Place(shellRoot, prefab, new Vector3(ShellXMin, y, z), 270f, $"ShellW_{level}_{idx}");
            idx++;
        }

        Place(shellRoot, "CastlePartBase/Wall_Corner_Outer_Large.prefab", new Vector3(ShellXMax, y, ShellZMax), 0f, $"ShellCorner_NE_{level}");
        Place(shellRoot, "CastlePartBase/Wall_Corner_Outer_Large.prefab", new Vector3(ShellXMax, y, ShellZMin), 90f, $"ShellCorner_SE_{level}");
        Place(shellRoot, "CastlePartBase/Wall_Corner_Outer_Large.prefab", new Vector3(ShellXMin, y, ShellZMin), 180f, $"ShellCorner_SW_{level}");
        Place(shellRoot, "CastlePartBase/Wall_Corner_Outer_Large.prefab", new Vector3(ShellXMin, y, ShellZMax), 270f, $"ShellCorner_NW_{level}");
    }

    // Builds 3 large stone stairwells rising through all 5 floors, with zero blocking support blocks so they are 100% connected and walkable
    private static void BuildStairwells(Transform stairsRoot)
    {
        float stairCenterZ = (StairZMin + StairZMax) / 2f;
        foreach (var stairX in StairXs)
        {
            for (int flight = 0; flight < 4; flight++)
            {
                float y = flight * 6f;
                Vector3 pivot = new Vector3(stairX + 5f, y, stairCenterZ);
                Place(stairsRoot, "CastlePartBase/Stairs_Stone.prefab", pivot, 0f, $"Stairwell_{stairX}_Flight{flight}");
            }

            for (int level = 1; level <= 4; level++)
            {
                float y = level * 6f;
                Place(stairsRoot, "CastlePartBase/Railing_Normal.prefab", new Vector3(stairX - StairHoleHalfWidth, y, stairCenterZ), 90f, $"Rail_{stairX}_{level}_A");
                Place(stairsRoot, "CastlePartBase/Railing_Normal.prefab", new Vector3(stairX + StairHoleHalfWidth, y, stairCenterZ), 90f, $"Rail_{stairX}_{level}_B");
            }
        }
    }

    // ---------------------------------------------------------------- room layout

    private static readonly RoomBay[] GroundFloorSouth =
    {
        new(70f, "Armory_G", "Armory"),
        new(50f, "EntranceHall", "GreatHall"),
        new(70f, "Stable_G", "Stable"),
    };

    private static readonly RoomBay[] GroundFloorNorth =
    {
        new(60f, "Kitchen_G", "Kitchen"),
        new(70f, "GreatHall_G", "GreatHall"),
        new(60f, "Chapel_G", "Chapel"),
    };

    private static readonly RoomBay[] Floor2South =
    {
        new(50f, "Barracks_2", "Barracks"),
        new(90f, "Hall_2", "GreatHall"),
        new(50f, "Storage_2", "Storage"),
    };

    private static readonly RoomBay[] Floor2North =
    {
        new(55f, "Bedroom_2A", "Bedroom"),
        new(80f, "Study_2", "Study"),
        new(55f, "Bedroom_2B", "Bedroom"),
    };

    private static readonly RoomBay[] Floor3South =
    {
        new(40f, "Storage_3A", "Storage"),
        new(110f, "Hall_3", "GreatHall"),
        new(40f, "Storage_3B", "Storage"),
    };

    private static readonly RoomBay[] Floor3North =
    {
        new(60f, "Library_3", "Library"),
        new(70f, "Study_3", "Study"),
        new(60f, "Bedroom_3", "Bedroom"),
    };

    private static readonly RoomBay[] Floor4South =
    {
        new(50f, "Armory_4", "Armory"),
        new(90f, "Barracks_4", "Barracks"),
        new(50f, "Storage_4", "Storage"),
    };

    private static readonly RoomBay[] Floor4North =
    {
        new(55f, "Bedroom_4A", "Bedroom"),
        new(80f, "Hall_4", "GreatHall"),
        new(55f, "Bedroom_4B", "Bedroom"),
    };

    private static readonly RoomBay[] Floor5South =
    {
        new(60f, "LordBedroom_5", "LordBedroom"),
        new(70f, "ThroneRoom_5", "Throne"),
        new(60f, "Library_5", "Library"),
    };

    private static readonly RoomBay[] Floor5North =
    {
        new(50f, "Study_5", "Study"),
        new(90f, "Hall_5", "GreatHall"),
        new(50f, "Bedroom_5", "Bedroom"),
    };

    private static void BuildFloor(Transform shellRoot, Transform interiorRoot, Transform lightRoot, int level, RoomBay[] northBays, RoomBay[] southBays)
    {
        float y = level * 6f;
        BuildRoomRow(shellRoot, interiorRoot, lightRoot, true, y, northBays);
        BuildRoomRow(shellRoot, interiorRoot, lightRoot, false, y, southBays);
    }

    private static void BuildRoomRow(Transform shellRoot, Transform interiorRoot, Transform lightRoot, bool isNorthRow, float y, RoomBay[] bays)
    {
        float zNear = isNorthRow ? -7.5f : StairZMin;
        float zFar = isNorthRow ? ShellZMax : ShellZMin;
        float doorWallRot = isNorthRow ? 180f : 0f;
        float prevX = ShellXMin;

        foreach (var bay in bays)
        {
            float xStart = prevX;
            float xEnd = prevX + bay.Width;
            float cx = (xStart + xEnd) / 2f;
            float cz = (zNear + zFar) / 2f;
            float halfW = bay.Width / 2f - 1.2f;
            float halfD = Mathf.Abs(zFar - zNear) / 2f - 1.2f;

            if (xStart > ShellXMin + 0.01f)
            {
                BuildPartitionWallZ(shellRoot, xStart, Mathf.Min(zNear, zFar), Mathf.Max(zNear, zFar), y, $"Part_{bay.Tag}");
            }

            BuildDoorWallX(shellRoot, xStart, xEnd, zNear, y, doorWallRot, cx, bay.Tag);
            FurnishRoom(interiorRoot, bay.Theme, cx, cz, halfW, halfD, y, bay.Tag);

            var lightGo = new GameObject($"Light_{bay.Tag}");
            lightGo.transform.SetParent(lightRoot, false);
            lightGo.transform.localPosition = new Vector3(cx, y + 4.5f, cz);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.58f, 0.26f);
            light.intensity = 1.5f;
            light.range = Mathf.Clamp(bay.Width * 0.55f, 8f, 22f);
            light.shadows = LightShadows.None;

            prevX = xEnd;
        }
    }

    private static void BuildPartitionWallZ(Transform parent, float x, float zStart, float zEnd, float y, string tag)
    {
        int idx = 0;
        for (float z = zStart + 2.5f; z < zEnd - 1f; z += 5f)
        {
            Place(parent, "CastlePartBase/Wall_Plain.prefab", new Vector3(x, y, z), 90f, $"{tag}_{idx}");
            idx++;
        }
    }

    // True if x sits over (or right next to) a stairwell hole - a door here would open onto
    // open air / the stairs instead of solid corridor floor.
    private static bool IsNearStairHole(float x)
    {
        foreach (var hx in StairXs)
        {
            if (Mathf.Abs(x - hx) < StairHoleHalfWidth + 2f) return true;
        }
        return false;
    }

    private static void BuildDoorWallX(Transform parent, float xStart, float xEnd, float z, float y, float rot, float doorCenterX, string tag)
    {
        var segments = new List<float>();
        for (float x = xStart + 2.5f; x < xEnd - 1f; x += 5f)
        {
            segments.Add(x);
        }

        // Prefer the segment closest to the room's centre, but never one that opens onto a
        // stairwell hole; if every segment near the centre is unsafe, fall back to the nearest
        // safe one anywhere in the bay so every room still gets exactly one door.
        float doorX = float.NaN;
        float bestDist = float.MaxValue;
        foreach (var seg in segments)
        {
            if (IsNearStairHole(seg)) continue;
            float d = Mathf.Abs(seg - doorCenterX);
            if (d < bestDist)
            {
                bestDist = d;
                doorX = seg;
            }
        }
        if (float.IsNaN(doorX) && segments.Count > 0)
        {
            doorX = segments[segments.Count / 2];
        }

        int idx = 0;
        foreach (var x in segments)
        {
            bool isDoor = !float.IsNaN(doorX) && Mathf.Approximately(x, doorX);
            string prefab = isDoor ? "CastlePartBase/Wall_Door_Floor_Mid.prefab" : "CastlePartBase/Wall_Plain.prefab";
            Place(parent, prefab, new Vector3(x, y, z), rot, $"{tag}_Corridor_{idx}");
            idx++;
        }
    }

    // ---------------------------------------------------------------- room furnishing (High detail with Armory Pack Integration)

    private static void FurnishRoom(Transform interiorRoot, string theme, float cx, float cz, float halfW, float halfD, float y, string tag)
    {
        switch (theme)
        {
            case "Kitchen":
                Place(interiorRoot, "CastleInterior/Cauldron.prefab", new Vector3(cx, y, cz), 0f, $"{tag}_Cauldron");
                Place(interiorRoot, "CastleInterior/Fireplace_Middle.prefab", new Vector3(cx, y, cz + halfD * 0.6f), 180f, $"{tag}_Fireplace");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Oven/Oven.prefab", new Vector3(cx - halfW * 0.5f, y, cz + halfD * 0.5f), 180f, $"{tag}_ArmoryOven");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Table/Table.prefab", new Vector3(cx, y, cz - halfD * 0.2f), 0f, $"{tag}_KitchenTable");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chair/Chair.prefab", new Vector3(cx - 1.5f, y, cz - halfD * 0.2f), 90f, $"{tag}_Chair1");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chair/Chair.prefab", new Vector3(cx + 1.5f, y, cz - halfD * 0.2f), 270f, $"{tag}_Chair2");
                Place(interiorRoot, "CastleInterior/Bucket.prefab", new Vector3(cx + halfW * 0.5f, y, cz - halfD * 0.4f), 0f, $"{tag}_Bucket");
                Place(interiorRoot, "CastleInterior/FireWood.prefab", new Vector3(cx + halfW * 0.5f, y, cz + halfD * 0.3f), 0f, $"{tag}_FireWood");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Bottles/Bottle.001.prefab", new Vector3(cx, y + 0.8f, cz - halfD * 0.2f), 0f, $"{tag}_Bottle1");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Bottles/Bottle.002.prefab", new Vector3(cx + 0.5f, y + 0.8f, cz - halfD * 0.2f), 0f, $"{tag}_Bottle2");
                Place(interiorRoot, "CastleInterior/Shelves_Poor.prefab", new Vector3(cx - halfW * 0.5f, y, cz - halfD * 0.4f), 90f, $"{tag}_Shelves");
                break;

            case "GreatHall":
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Table/Table.prefab", new Vector3(cx, y, cz - halfD * 0.3f), 0f, $"{tag}_ArmoryTable1");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Table/Table.prefab", new Vector3(cx, y, cz + halfD * 0.3f), 0f, $"{tag}_ArmoryTable2");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Table/Tablecloth.prefab", new Vector3(cx, y + 0.05f, cz - halfD * 0.3f), 0f, $"{tag}_Cloth1");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Table/Tablecloth.prefab", new Vector3(cx, y + 0.05f, cz + halfD * 0.3f), 0f, $"{tag}_Cloth2");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chair/Chair.prefab", new Vector3(cx - 2.5f, y, cz - halfD * 0.3f), 90f, $"{tag}_Chair_1");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chair/Chair.prefab", new Vector3(cx + 2.5f, y, cz - halfD * 0.3f), 270f, $"{tag}_Chair_2");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chair/Chair.prefab", new Vector3(cx - 2.5f, y, cz + halfD * 0.3f), 90f, $"{tag}_Chair_3");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chair/Chair.prefab", new Vector3(cx + 2.5f, y, cz + halfD * 0.3f), 270f, $"{tag}_Chair_4");
                Place(interiorRoot, "CastleInterior/Fireplace_Large.prefab", new Vector3(cx - halfW * 0.7f, y, cz), 90f, $"{tag}_Fireplace");
                Place(interiorRoot, "CastleInterior/Chandelier.prefab", new Vector3(cx, y + 5.5f, cz - halfD * 0.3f), 0f, $"{tag}_Chandelier1");
                Place(interiorRoot, "CastleInterior/Chandelier.prefab", new Vector3(cx, y + 5.5f, cz + halfD * 0.3f), 0f, $"{tag}_Chandelier2");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chest/ChestL.001 1.prefab", new Vector3(cx + halfW * 0.7f, y, cz), 270f, $"{tag}_ArmoryChest");
                break;

            case "Bedroom":
                Place(interiorRoot, "CastleInterior/Bed_Single_Wealthy.prefab", new Vector3(cx - halfW * 0.5f, y, cz), 90f, $"{tag}_Bed");
                Place(interiorRoot, "CastleInterior/Cabinet.prefab", new Vector3(cx + halfW * 0.5f, y, cz - halfD * 0.4f), 0f, $"{tag}_Cabinet");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Table/Table.prefab", new Vector3(cx + halfW * 0.4f, y, cz + halfD * 0.4f), 0f, $"{tag}_BedTable");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chair/Chair.prefab", new Vector3(cx + halfW * 0.4f, y, cz + halfD * 0.1f), 180f, $"{tag}_BedChair");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chest/ChestS.001.prefab", new Vector3(cx - halfW * 0.5f, y, cz - halfD * 0.5f), 90f, $"{tag}_SmallChest");
                break;

            case "LordBedroom":
                Place(interiorRoot, "CastleInterior/Bed_Double.prefab", new Vector3(cx - halfW * 0.3f, y, cz), 90f, $"{tag}_Bed");
                Place(interiorRoot, "CastleInterior/Cabinet.prefab", new Vector3(cx + halfW * 0.5f, y, cz - halfD * 0.3f), 0f, $"{tag}_Cabinet");
                Place(interiorRoot, "CastleInterior/BathTub.prefab", new Vector3(cx + halfW * 0.5f, y, cz + halfD * 0.4f), 90f, $"{tag}_BathTub");
                Place(interiorRoot, "CastleInterior/Chandelier.prefab", new Vector3(cx, y + 5.5f, cz), 0f, $"{tag}_Chandelier");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Table/Table 1.prefab", new Vector3(cx, y, cz - halfD * 0.4f), 0f, $"{tag}_Desk");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chair/Chair.prefab", new Vector3(cx, y, cz - halfD * 0.4f + 1.5f), 0f, $"{tag}_DeskChair");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chest/ChestL.001 1.prefab", new Vector3(cx - halfW * 0.3f, y, cz - halfD * 0.6f), 0f, $"{tag}_BigChest");
                break;

            case "Armory":
                // Real medieval weapon smith and storage room
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Combinations/WeaponRack.002.prefab", new Vector3(cx - halfW * 0.4f, y, cz), 90f, $"{tag}_Rack1");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Combinations/WeaponRack.003.prefab", new Vector3(cx + halfW * 0.4f, y, cz), 270f, $"{tag}_Rack2");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Anvil and Airblower/Anvil.prefab", new Vector3(cx - 2f, y, cz - halfD * 0.3f), 45f, $"{tag}_Anvil");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Anvil and Airblower/Airblower.prefab", new Vector3(cx - 3.5f, y, cz - halfD * 0.3f), 45f, $"{tag}_Blower");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Table/Table.prefab", new Vector3(cx + 2f, y, cz - halfD * 0.3f), 0f, $"{tag}_RepairTable");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chest/ChestL.001 1.prefab", new Vector3(cx, y, cz + halfD * 0.4f), 180f, $"{tag}_WeaponChest");
                break;

            case "Storage":
                Place(interiorRoot, "CastleInterior/Barrel_Stack.prefab", new Vector3(cx - halfW * 0.4f, y, cz), 0f, $"{tag}_Barrels");
                Place(interiorRoot, "CastleInterior/Crate.prefab", new Vector3(cx + halfW * 0.4f, y, cz - halfD * 0.3f), 0f, $"{tag}_Crate1");
                Place(interiorRoot, "CastleInterior/Crate.prefab", new Vector3(cx + halfW * 0.4f, y, cz + halfD * 0.3f), 0f, $"{tag}_Crate2");
                Place(interiorRoot, "CastleInterior/Shelves_Poor.prefab", new Vector3(cx, y, cz + halfD * 0.5f), 0f, $"{tag}_Shelves");
                break;

            case "Library":
                Place(interiorRoot, "CastleInterior/Shelves_Wealthy.prefab", new Vector3(cx - halfW * 0.5f, y, cz - halfD * 0.3f), 0f, $"{tag}_Shelves1");
                Place(interiorRoot, "CastleInterior/Shelves_Wealthy.prefab", new Vector3(cx - halfW * 0.5f, y, cz + halfD * 0.3f), 0f, $"{tag}_Shelves2");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Table/Table.prefab", new Vector3(cx + halfW * 0.3f, y, cz), 0f, $"{tag}_ReadingTable");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chair/Chair.prefab", new Vector3(cx + halfW * 0.3f, y, cz - 1.8f), 180f, $"{tag}_Chair");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Books/Book.001.prefab", new Vector3(cx + halfW * 0.3f, y + 0.8f, cz), 0f, $"{tag}_Book1");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Books/Book.002.prefab", new Vector3(cx + halfW * 0.3f + 0.4f, y + 0.8f, cz), 0f, $"{tag}_Book2");
                break;

            case "Chapel":
                Place(interiorRoot, "CastleInterior/Table.prefab", new Vector3(cx, y, cz + halfD * 0.4f), 0f, $"{tag}_Altar");
                Place(interiorRoot, "CastleInterior/Bench.prefab", new Vector3(cx, y, cz - halfD * 0.1f), 0f, $"{tag}_Bench1");
                Place(interiorRoot, "CastleInterior/Bench.prefab", new Vector3(cx, y, cz - halfD * 0.45f), 0f, $"{tag}_Bench2");
                Place(interiorRoot, "CastleInterior/Chandelier.prefab", new Vector3(cx, y + 5.5f, cz), 0f, $"{tag}_Chandelier");
                break;

            case "Study":
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Table/Table.prefab", new Vector3(cx, y, cz), 0f, $"{tag}_StudyTable");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chair/Chair.prefab", new Vector3(cx, y, cz - 1.8f), 180f, $"{tag}_StudyChair");
                Place(interiorRoot, "CastleInterior/WallShelf_Long.prefab", new Vector3(cx - halfW * 0.5f, y + 1.5f, cz), 90f, $"{tag}_Shelf");
                Place(interiorRoot, "CastleInterior/Cabinet.prefab", new Vector3(cx + halfW * 0.5f, y, cz + halfD * 0.3f), 0f, $"{tag}_Cabinet");
                break;

            case "Barracks":
                Place(interiorRoot, "CastleInterior/Bed_Single_Poor.prefab", new Vector3(cx - halfW * 0.4f, y, cz), 0f, $"{tag}_Bed1");
                Place(interiorRoot, "CastleInterior/Bed_Single_Poor.prefab", new Vector3(cx + halfW * 0.4f, y, cz), 0f, $"{tag}_Bed2");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Combat Dummy/CombatDummy.001.prefab", new Vector3(cx, y, cz + halfD * 0.4f), 0f, $"{tag}_Dummy1");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Combat Dummy/CombatDummy.002.prefab", new Vector3(cx - 1.8f, y, cz + halfD * 0.4f), 0f, $"{tag}_Dummy2");
                Place(interiorRoot, "CastleInterior/Fireplace_Small.prefab", new Vector3(cx, y, cz - halfD * 0.4f), 180f, $"{tag}_Fireplace");
                break;

            case "Throne":
                // Create a magnificent stone dais and place the throne chair on it
                BuildFlatSlab(interiorRoot, $"{tag}_Dais", cx, cz + halfD * 0.5f, 4.5f, 4.5f, y + 0.2f, "mat_floorTile");
                Place(interiorRoot, "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chair/Chair.prefab", new Vector3(cx, y + 0.4f, cz + halfD * 0.5f), 180f, $"{tag}_ThroneChair");
                Place(interiorRoot, "CastleInterior/Chandelier.prefab", new Vector3(cx, y + 5.5f, cz), 0f, $"{tag}_Chandelier1");
                Place(interiorRoot, "CastleInterior/Chandelier.prefab", new Vector3(cx - halfW * 0.5f, y + 5.5f, cz - halfD * 0.3f), 0f, $"{tag}_Chandelier2");
                Place(interiorRoot, "CastleInterior/Chandelier.prefab", new Vector3(cx + halfW * 0.5f, y + 5.5f, cz - halfD * 0.3f), 0f, $"{tag}_Chandelier3");
                break;

            case "Stable":
                Place(interiorRoot, "CastleInterior/HorseStall_Gate.prefab", new Vector3(cx - halfW * 0.3f, y, cz - halfD * 0.3f), 0f, $"{tag}_Gate");
                Place(interiorRoot, "CastleInterior/Horse_Food.prefab", new Vector3(cx + halfW * 0.3f, y, cz - halfD * 0.3f), 0f, $"{tag}_Food");
                Place(interiorRoot, "CastleInterior/Horse_Water.prefab", new Vector3(cx, y, cz + halfD * 0.3f), 90f, $"{tag}_Water");
                Place(interiorRoot, "CastleInterior/FireWood.prefab", new Vector3(cx + halfW * 0.4f, y, cz + halfD * 0.3f), 0f, $"{tag}_FireWood");
                break;
        }
    }

    // ---------------------------------------------------------------- entry path (outside the walls)

    private static void BuildEntryPath(Transform pathRoot)
    {
        for (float z = -80; z <= -70; z += 10)
        {
            PlaceDirtTile2x2(pathRoot, GateX, z, $"ApproachRoad_{z}");
        }

        for (float z = -68; z <= -63; z += 5)
        {
            Place(pathRoot, "CastlePartBase/Pillar.prefab", new Vector3(GateX - 6, 0, z), 0f, $"Pillar_L_{z}");
            Place(pathRoot, "CastlePartBase/Pillar.prefab", new Vector3(GateX + 6, 0, z), 0f, $"Pillar_R_{z}");
        }
    }

    // ---------------------------------------------------------------- lighting (outside the room grid)

    private static void BuildOuterLighting(Transform lightRoot)
    {
        var points = new List<Vector3>
        {
            new(GateX, 4, -65),
            new(ShellXMax, 8, ShellZMax), new(ShellXMax, 8, ShellZMin),
            new(ShellXMin, 8, ShellZMin), new(ShellXMin, 8, ShellZMax),
            new(-15, 8, GroundZMin + 6f), new(15, 8, GroundZMin + 6f),
        };

        for (int i = 0; i < points.Count; i++)
        {
            var go = new GameObject($"Torch_Light_{i}");
            go.transform.SetParent(lightRoot, false);
            go.transform.localPosition = points[i];
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.22f);
            light.intensity = 1.6f;
            light.range = 12f;
            light.shadows = LightShadows.Soft;
        }
    }

    // ---------------------------------------------------------------- surrounding nature density
    // Scatters trees/bushes/rocks in the open ground around the castle (never inside the curtain
    // wall footprint, and clear of the entry path), each dropped onto the actual terrain height
    // at its own X/Z so nothing floats.

    private static void BuildSurroundingNature()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            return;
        }

        var natureRoot = new GameObject(NatureRootName);

        string[] treePrefabs =
        {
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Trees/Spruce 1.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Trees/Spruce 2.prefab",
        };
        string[] bushPrefabs = { "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Bush/Bush.prefab" };
        string[] rockPrefabs =
        {
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 1.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 2.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 3.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 1.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 2.prefab",
        };

        float wallXMinWorld = SiteX + WallXMin - 8f;
        float wallXMaxWorld = SiteX + WallXMax + 8f;
        float wallZMinWorld = SiteZ + WallZMin - 8f;
        float wallZMaxWorld = SiteZ + WallZMax + 8f;

        var rng = new System.Random(1234);
        int placed = 0;
        int attempts = 0;
        const int targetCount = 260;
        const int maxAttempts = 2000;

        while (placed < targetCount && attempts < maxAttempts)
        {
            attempts++;
            float ox = (float)(rng.NextDouble() * 2.0 - 1.0) * 230f;
            float oz = (float)(rng.NextDouble() * 2.0 - 1.0) * 230f;
            float wx = SiteX + ox;
            float wz = SiteZ + oz;

            bool insideWall = wx > wallXMinWorld && wx < wallXMaxWorld && wz > wallZMinWorld && wz < wallZMaxWorld;
            if (insideWall)
            {
                continue;
            }

            // Keep the south approach path clear.
            bool onApproach = Mathf.Abs(wx - SiteX) < 15f && wz < SiteZ + WallZMin + 5f && wz > SiteZ + WallZMin - 45f;
            if (onApproach)
            {
                continue;
            }

            double roll = rng.NextDouble();
            string[] pool = roll < 0.55 ? treePrefabs : (roll < 0.8 ? bushPrefabs : rockPrefabs);
            var prefab = LoadPrefab(pool[rng.Next(pool.Length)]);
            if (prefab == null)
            {
                continue;
            }

            float terrainY = terrain.SampleHeight(new Vector3(wx, 0, wz)) + terrain.transform.position.y;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, natureRoot.transform);
            inst.transform.position = new Vector3(wx, terrainY, wz);
            inst.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
            float scale = 0.8f + (float)rng.NextDouble() * 0.7f;
            inst.transform.localScale = Vector3.one * scale;
            inst.name = $"Nature_{placed}";
            placed++;
        }

        Debug.Log($"Scattered {placed} trees/bushes/rocks around the castle grounds.");
    }
}


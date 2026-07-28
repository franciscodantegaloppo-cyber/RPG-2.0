using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class DungeonGenerator
{
    const string PackPrefabPath = "Assets/Gridness Studios/Elementary Dungeon Pack Lite/Prefabs/";
    const string VillageScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string DungeonScenePath = "Assets/_RPG/Scenes/dungeon_1.unity";

    const int GridW = 4;
    const int GridH = 3;
    const int MacroSpacing = 9;
    const int MinRoomSize = 3;
    const int MaxRoomSize = 5;
    const int BossRoomSize = 7;

    static readonly Vector2Int[] Dirs4 = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    class RoomNode
    {
        public int gx, gy, size;
        public bool isBoss, isStart;
        public readonly List<RoomNode> connections = new List<RoomNode>();
        public Vector2Int Center => new Vector2Int(gx * MacroSpacing + MacroSpacing / 2, gy * MacroSpacing + MacroSpacing / 2);
    }

    [MenuItem("RPG/Dungeon/Generate dungeon_1")]
    public static void Generate()
    {
        GameObject floorPrefab = Load("Floor_Flat");
        GameObject wallPrefab = Load("Wall_Chiseled");
        GameObject doorWallPrefab = Load("Wall_DoorStand");
        GameObject doorPrefab = Load("Door_Middle");
        GameObject ceilingPrefab = Load("Ceiling_SquareLarge");
        GameObject columnPrefab = Load("Column_Half");
        GameObject torchPrefab = Load("Torch.001");

        if (floorPrefab == null || wallPrefab == null || doorWallPrefab == null || doorPrefab == null || ceilingPrefab == null)
        {
            Debug.LogError("[DungeonGenerator] Faltan prefabs del Elementary Dungeon Pack Lite. Abortando.");
            return;
        }

        Mesh floorMesh = MeshOf(floorPrefab);
        Mesh wallMesh = MeshOf(wallPrefab);
        float cellSize = Mathf.Max(floorMesh.bounds.size.x, floorMesh.bounds.size.z);
        float wallHeight = wallMesh.bounds.size.y;
        bool wallLongAxisIsX = wallMesh.bounds.size.x >= wallMesh.bounds.size.z;

        // ---------- 1. Maze graph (macro grid) ----------
        var nodes = new RoomNode[GridW, GridH];
        for (int x = 0; x < GridW; x++)
            for (int y = 0; y < GridH; y++)
                nodes[x, y] = new RoomNode { gx = x, gy = y, size = Random.Range(MinRoomSize, MaxRoomSize + 1) };

        RoomNode start = nodes[0, 0];
        start.isStart = true;

        var rng = new System.Random();
        var visited = new bool[GridW, GridH];
        var stack = new Stack<RoomNode>();
        visited[0, 0] = true;
        stack.Push(start);
        var edges = new List<(RoomNode, RoomNode)>();

        while (stack.Count > 0)
        {
            RoomNode cur = stack.Peek();
            var options = UnvisitedNeighbors(cur, nodes, visited);
            if (options.Count == 0) { stack.Pop(); continue; }
            RoomNode next = options[rng.Next(options.Count)];
            visited[next.gx, next.gy] = true;
            cur.connections.Add(next);
            next.connections.Add(cur);
            edges.Add((cur, next));
            stack.Push(next);
        }

        // extra loop connections for bifurcations (~15%)
        for (int x = 0; x < GridW; x++)
            for (int y = 0; y < GridH; y++)
            {
                TryExtraEdge(nodes[x, y], x + 1, y, nodes, rng, edges);
                TryExtraEdge(nodes[x, y], x, y + 1, nodes, rng, edges);
            }

        // boss room = farthest node from start (BFS)
        var dist = Bfs(start, nodes);
        RoomNode boss = dist.OrderByDescending(kv => kv.Value).First().Key;
        boss.isBoss = true;
        boss.size = BossRoomSize;

        // ---------- 2. Carve fine grid ----------
        var floorCells = new HashSet<Vector2Int>();
        var regionOf = new Dictionary<Vector2Int, int>();
        int regionCounter = 0;

        foreach (var node in nodes)
            FillRoom(node, floorCells, regionOf, regionCounter++);

        foreach (var (a, b) in edges)
            CarveCorridor(a, b, floorCells, regionOf, regionCounter++);

        // ---------- 3. Create scene ----------
        Scene dungeonScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        SetupLighting();

        var root = new GameObject("DungeonRoot");
        var floors = NewChild(root, "Floors");
        var walls = NewChild(root, "Walls");
        var ceilings = NewChild(root, "Ceilings");
        var doors = NewChild(root, "Doors");
        var props = NewChild(root, "Props");
        var lights = NewChild(root, "Lights");

        foreach (var cell in floorCells)
        {
            PlaceAlignedFlat(floorPrefab, cell, 0f, cellSize, floorMesh, floors.transform);
            PlaceAlignedFlat(ceilingPrefab, cell, wallHeight, cellSize, MeshOf(ceilingPrefab), ceilings.transform);
        }

        int torchCounter = 0;
        foreach (var cell in floorCells)
        {
            foreach (var dir in Dirs4)
            {
                Vector2Int neighbor = cell + dir;
                if (!floorCells.Contains(neighbor))
                {
                    GameObject wallGO = PlaceWallSegment(wallPrefab, cell, dir, cellSize, wallHeight, wallLongAxisIsX, walls.transform);
                    torchCounter++;
                    if (torchPrefab != null && torchCounter % 3 == 0)
                        PlaceTorch(torchPrefab, wallGO.transform, wallHeight, lights.transform);
                }
                else if (regionOf[cell] != regionOf[neighbor] && (dir == Vector2Int.up || dir == Vector2Int.right))
                {
                    PlaceWallSegment(doorWallPrefab, cell, dir, cellSize, wallHeight, wallLongAxisIsX, doors.transform);
                    GameObject doorGO = PlaceWallSegment(doorPrefab, cell, dir, cellSize, wallHeight, wallLongAxisIsX, doors.transform);
                    // Purely decorative: never block the walkway or the NavMesh bake.
                    // (its MeshCollider is concave, so it can't be a trigger — just remove it.)
                    foreach (var col in doorGO.GetComponentsInChildren<Collider>())
                        Object.DestroyImmediate(col);
                }
            }
        }

        if (columnPrefab != null)
            PlaceCorners(columnPrefab, floorCells, cellSize, wallHeight, props.transform);

        MarkStaticRecursive(root, StaticEditorFlags.BatchingStatic);

        // ---------- 4. Boss room camera trigger ----------
        SetupBossRoom(boss, cellSize, wallHeight, props.transform);

        // ---------- 5. Entrance / Exit (reusing the existing "rendija" crack) ----------
        SetupEntranceExit(start, cellSize, props.transform);

        // ---------- 6. NavMesh ----------
        var navSurfaceGO = new GameObject("NavMeshSurface");
        navSurfaceGO.transform.SetParent(root.transform);
        var surface = navSurfaceGO.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; // trigger colliders (decorative doors) are excluded
        surface.BuildNavMesh();

        // ---------- 7. Save scene + register in build settings ----------
        EditorSceneManager.SaveScene(dungeonScene, DungeonScenePath);
        RegisterSceneInBuildSettings(DungeonScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[DungeonGenerator] dungeon_1 generado: {floorCells.Count} celdas de piso, " +
            $"{edges.Count} conexiones principales, sala de jefe en macro-celda ({boss.gx},{boss.gy}). " +
            "Agregada a Build Settings. Revisá la escena y guardá la Village si movió algo.");
    }

    // ---------------- maze helpers ----------------

    static List<RoomNode> UnvisitedNeighbors(RoomNode cur, RoomNode[,] nodes, bool[,] visited)
    {
        var list = new List<RoomNode>();
        TryAdd(cur.gx + 1, cur.gy);
        TryAdd(cur.gx - 1, cur.gy);
        TryAdd(cur.gx, cur.gy + 1);
        TryAdd(cur.gx, cur.gy - 1);
        return list;

        void TryAdd(int x, int y)
        {
            if (x < 0 || y < 0 || x >= GridW || y >= GridH) return;
            if (visited[x, y]) return;
            list.Add(nodes[x, y]);
        }
    }

    static void TryExtraEdge(RoomNode cur, int nx, int ny, RoomNode[,] nodes, System.Random rng, List<(RoomNode, RoomNode)> edges)
    {
        if (nx < 0 || ny < 0 || nx >= GridW || ny >= GridH) return;
        RoomNode other = nodes[nx, ny];
        if (cur.connections.Contains(other)) return;
        if (rng.NextDouble() > 0.15) return;

        cur.connections.Add(other);
        other.connections.Add(cur);
        edges.Add((cur, other));
    }

    static Dictionary<RoomNode, int> Bfs(RoomNode start, RoomNode[,] nodes)
    {
        var dist = new Dictionary<RoomNode, int> { [start] = 0 };
        var queue = new Queue<RoomNode>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var n in cur.connections)
            {
                if (dist.ContainsKey(n)) continue;
                dist[n] = dist[cur] + 1;
                queue.Enqueue(n);
            }
        }
        return dist;
    }

    static void FillRoom(RoomNode node, HashSet<Vector2Int> floorCells, Dictionary<Vector2Int, int> regionOf, int regionId)
    {
        int half = node.size / 2;
        Vector2Int c = node.Center;
        for (int dx = -half; dx <= half; dx++)
            for (int dy = -half; dy <= half; dy++)
            {
                var cell = new Vector2Int(c.x + dx, c.y + dy);
                floorCells.Add(cell);
                regionOf[cell] = regionId;
            }
    }

    static void CarveCorridor(RoomNode a, RoomNode b, HashSet<Vector2Int> floorCells, Dictionary<Vector2Int, int> regionOf, int regionId)
    {
        Vector2Int ca = a.Center, cb = b.Center;
        if (ca.x == cb.x)
        {
            int y0 = Mathf.Min(ca.y, cb.y), y1 = Mathf.Max(ca.y, cb.y);
            for (int y = y0; y <= y1; y++) Carve(new Vector2Int(ca.x, y));
        }
        else
        {
            int x0 = Mathf.Min(ca.x, cb.x), x1 = Mathf.Max(ca.x, cb.x);
            for (int x = x0; x <= x1; x++) Carve(new Vector2Int(x, ca.y));
        }

        void Carve(Vector2Int cell)
        {
            if (!floorCells.Contains(cell)) regionOf[cell] = regionId;
            floorCells.Add(cell);
        }
    }

    // ---------------- placement helpers ----------------

    static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PackPrefabPath + name + ".prefab");

    static Mesh MeshOf(GameObject prefab) => prefab.GetComponentInChildren<MeshFilter>().sharedMesh;

    static GameObject NewChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        return go;
    }

    static void PlaceAlignedFlat(GameObject prefab, Vector2Int cell, float baseY, float cellSize, Mesh mesh, Transform parent)
    {
        Vector3 desiredMin = new Vector3(cell.x * cellSize, baseY, cell.y * cellSize);
        Vector3 pos = desiredMin - new Vector3(mesh.bounds.min.x, 0f, mesh.bounds.min.z);
        pos.y = baseY - mesh.bounds.min.y;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent);
        instance.transform.position = pos;
    }

    static GameObject PlaceWallSegment(GameObject prefab, Vector2Int cell, Vector2Int dir, float cellSize, float wallHeight, bool wallLongAxisIsX, Transform parent)
    {
        Vector3 cellCenter = new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
        Vector3 boundary = cellCenter + new Vector3(dir.x, 0f, dir.y) * (cellSize * 0.5f);

        bool horizontalEdge = dir == Vector2Int.up || dir == Vector2Int.down; // edge runs along X
        float yaw = 0f;
        if (wallLongAxisIsX)
            yaw = horizontalEdge ? 0f : 90f;
        else
            yaw = horizontalEdge ? 90f : 0f;

        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Mesh mesh = MeshOf(prefab);
        Vector3 localOffset = new Vector3(mesh.bounds.center.x, 0f, mesh.bounds.center.z);
        Vector3 pos = boundary - rot * localOffset;
        pos.y = -mesh.bounds.min.y;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent);
        instance.transform.SetPositionAndRotation(pos, rot);
        return instance;
    }

    static void PlaceTorch(GameObject torchPrefab, Transform wallTransform, float wallHeight, Transform parent)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(torchPrefab);
        instance.transform.SetParent(parent);
        instance.transform.position = wallTransform.position + Vector3.up * (wallHeight * 0.55f) - wallTransform.forward * 0.05f;
        instance.transform.rotation = wallTransform.rotation;

        // Too many shadow-casting point lights tank the shadow atlas; torches are atmosphere, not shadow casters.
        foreach (var light in instance.GetComponentsInChildren<Light>())
            light.shadows = LightShadows.None;
    }

    static void PlaceCorners(GameObject columnPrefab, HashSet<Vector2Int> floorCells, float cellSize, float wallHeight, Transform parent)
    {
        Mesh mesh = MeshOf(columnPrefab);
        var placed = new HashSet<Vector2Int>();

        foreach (var cell in floorCells)
        {
            bool north = !floorCells.Contains(cell + Vector2Int.up);
            bool east = !floorCells.Contains(cell + Vector2Int.right);
            bool south = !floorCells.Contains(cell + Vector2Int.down);
            bool west = !floorCells.Contains(cell + Vector2Int.left);

            TryCorner(north, east, 1, 1);
            TryCorner(north, west, 0, 1);
            TryCorner(south, east, 1, 0);
            TryCorner(south, west, 0, 0);

            void TryCorner(bool sideA, bool sideB, int cx, int cy)
            {
                if (!sideA || !sideB) return;
                var vertex = new Vector2Int(cell.x + cx, cell.y + cy);
                if (!placed.Add(vertex)) return;

                Vector3 pos = new Vector3(vertex.x * cellSize, -mesh.bounds.min.y, vertex.y * cellSize);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(columnPrefab);
                instance.transform.SetParent(parent);
                instance.transform.position = pos;
            }
        }
    }

    static void SetupLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.10f, 0.14f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.03f, 0.03f, 0.05f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.018f;
    }

    static void MarkStaticRecursive(GameObject go, StaticEditorFlags flags)
    {
        GameObjectUtility.SetStaticEditorFlags(go, flags);
        foreach (Transform child in go.transform)
            MarkStaticRecursive(child.gameObject, flags);
    }

    static void SetupBossRoom(RoomNode boss, float cellSize, float wallHeight, Transform parent)
    {
        var trigger = new GameObject("BossRoomCameraTrigger");
        trigger.transform.SetParent(parent);
        Vector2Int c = boss.Center;
        trigger.transform.position = new Vector3(c.x * cellSize, wallHeight * 0.5f, c.y * cellSize);

        var box = trigger.AddComponent<BoxCollider>();
        box.isTrigger = true;
        float span = boss.size * cellSize * 0.95f;
        box.size = new Vector3(span, wallHeight, span);

        trigger.AddComponent<BossRoomCameraTrigger>();
    }

    static void SetupEntranceExit(RoomNode start, float cellSize, Transform propsParent)
    {
        // Find the existing crack ("rendija") object in SpawnVillage and mirror it.
        Scene villageScene = EditorSceneManager.OpenScene(VillageScenePath, OpenSceneMode.Additive);
        DungeonEntrance existingEntrance = Object.FindAnyObjectByType<DungeonEntrance>();

        if (existingEntrance == null)
        {
            Debug.LogWarning("[DungeonGenerator] No se encontró DungeonEntrance en SpawnVillage; no se pudo clonar la rendija.");
            EditorSceneManager.CloseScene(villageScene, true);
            return;
        }

        // --- clone: new entrance inside SpawnVillage pointing at dungeon_1 ---
        GameObject newEntranceGO = Object.Instantiate(existingEntrance.gameObject);
        newEntranceGO.name = "DungeonEntrance_Dungeon1";
        newEntranceGO.transform.position = existingEntrance.transform.position + existingEntrance.transform.right * 3f;
        EditorSceneManager.MoveGameObjectToScene(newEntranceGO, villageScene);

        int newSceneIndex = EditorBuildSettings.scenes.Length; // dungeon_1 will land on this index after registration
        var soEntrance = new SerializedObject(newEntranceGO.GetComponent<DungeonEntrance>());
        soEntrance.FindProperty("dungeonSceneIndex").intValue = newSceneIndex;
        soEntrance.ApplyModifiedProperties();

        // --- clone the crack visual into dungeon_1's start room, wired as the exit back ---
        var crackProp = new SerializedObject(existingEntrance).FindProperty("crackParticles");
        GameObject crackSource = crackProp != null && crackProp.objectReferenceValue != null
            ? ((ParticleSystem)crackProp.objectReferenceValue).gameObject
            : null;

        GameObject exitGO = crackSource != null
            ? Object.Instantiate(crackSource)
            : new GameObject("DungeonExit_Rendija");
        exitGO.name = "DungeonExit_Rendija";

        Vector2Int c = start.Center;
        exitGO.transform.position = new Vector3(c.x * cellSize, 0.05f, c.y * cellSize);
        exitGO.transform.SetParent(propsParent, true);

        var exitScript = exitGO.AddComponent<DungeonExit>();
        var soExit = new SerializedObject(exitScript);
        soExit.FindProperty("villageSceneIndex").intValue = 0;
        soExit.ApplyModifiedProperties();

        var exitCollider = exitGO.GetComponent<Collider>();
        if (exitCollider == null)
        {
            var box = exitGO.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.5f, 2f, 1.5f);
        }

        EditorSceneManager.MarkSceneDirty(villageScene);
        EditorSceneManager.SaveScene(villageScene);
        EditorSceneManager.CloseScene(villageScene, true);
    }

    static void RegisterSceneInBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

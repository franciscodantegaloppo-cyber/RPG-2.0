using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class CircularWaterBoxSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string WaterMaterialPath = "Assets/WaterWorks/Materials/SSR_Water.mat";
    const string DepthMaterialPath = "Assets/_RPG/Materials/RiverWaterDepth.mat";
    const string SurfaceMeshPath = "Assets/_RPG/Generated/CircularWaterSurface.asset";
    const string DepthMeshPath = "Assets/_RPG/Generated/CircularWaterDepth.asset";
    const string PrefabPath = "Assets/_RPG/Prefabs/World/WaterBox_Circular.prefab";
    const string RequestPath = "Assets/_RPG/Generated/CircularWaterBoxSetup.generate";
    const string ObjectName = "WaterBox_Circular";
    const int Segments = 128;
    const float Radius = 10f;
    const float Depth = 7f;

    [InitializeOnLoadMethod]
    static void QueueSetup() => EditorApplication.delayCall += TryRun;

    static void TryRun()
    {
        string request = Path.GetFullPath(RequestPath);
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        File.Delete(request);
        if (File.Exists(request + ".meta")) File.Delete(request + ".meta");
        Setup();
    }

    [MenuItem("RPG/World/Create Perfect Circular Water Box")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CircularWaterBoxSetup] Sali de Play Mode para crear el agua.");
            return;
        }
        EditorSceneUtility.OpenSceneSafely(ScenePath);

        Material water = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
        Material depth = AssetDatabase.LoadAssetAtPath<Material>(DepthMaterialPath);
        if (water == null || depth == null)
        {
            Debug.LogError("[CircularWaterBoxSetup] Faltan los materiales WaterWorks.");
            return;
        }

        Mesh surfaceMesh = BuildSurfaceMesh();
        Mesh depthMesh = BuildDepthMesh();
        GameObject prefab = BuildPrefab(surfaceMesh, depthMesh, water, depth);

        GameObject existing = GameObject.Find(ObjectName);
        if (existing == null)
        {
            existing = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            existing.name = ObjectName;
            existing.transform.position = FindStagingPosition();
            existing.transform.rotation = Quaternion.identity;
            existing.transform.localScale = Vector3.one;
        }

        EditorUtility.SetDirty(existing);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = existing;
        Debug.Log("[CircularWaterBoxSetup] WaterBox_Circular creado: diametro 20 m, profundidad 7 m.");
    }

    static GameObject BuildPrefab(Mesh surfaceMesh, Mesh depthMesh,
        Material water, Material depth)
    {
        GameObject root = new GameObject(ObjectName, typeof(MeshFilter), typeof(MeshRenderer));
        root.GetComponent<MeshFilter>().sharedMesh = surfaceMesh;
        MeshRenderer surfaceRenderer = root.GetComponent<MeshRenderer>();
        surfaceRenderer.sharedMaterial = water;
        surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;
        surfaceRenderer.receiveShadows = true;
        WaterSurfaceVolume volume = root.AddComponent<WaterSurfaceVolume>();
        volume.ConfigureCircular(true);

        GameObject depthObject = new GameObject("CircularWater_Depth",
            typeof(MeshFilter), typeof(MeshRenderer));
        depthObject.transform.SetParent(root.transform, false);
        depthObject.GetComponent<MeshFilter>().sharedMesh = depthMesh;
        MeshRenderer depthRenderer = depthObject.GetComponent<MeshRenderer>();
        depthRenderer.sharedMaterial = depth;
        depthRenderer.shadowCastingMode = ShadowCastingMode.Off;
        depthRenderer.receiveShadows = false;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static Mesh BuildSurfaceMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SurfaceMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "CircularWaterSurface" };
            AssetDatabase.CreateAsset(mesh, SurfaceMeshPath);
        }
        mesh.Clear();

        Vector3[] vertices = new Vector3[Segments + 1];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[Segments * 3];
        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(.5f, .5f);
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            float x = Mathf.Cos(angle) * Radius;
            float z = Mathf.Sin(angle) * Radius;
            vertices[i + 1] = new Vector3(x, 0f, z);
            uvs[i + 1] = new Vector2(x / (Radius * 2f) + .5f,
                z / (Radius * 2f) + .5f);
            int triangle = i * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = (i + 1) % Segments + 1;
            triangles[triangle + 2] = i + 1;
        }
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Mesh BuildDepthMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(DepthMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "CircularWaterDepth" };
            AssetDatabase.CreateAsset(mesh, DepthMeshPath);
        }
        mesh.Clear();

        List<Vector3> vertices = new List<Vector3>(Segments * 2 + 1);
        List<int> triangles = new List<int>(Segments * 9);
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            float x = Mathf.Cos(angle) * Radius;
            float z = Mathf.Sin(angle) * Radius;
            vertices.Add(new Vector3(x, 0f, z));
            vertices.Add(new Vector3(x, -Depth, z));
        }
        int bottomCenter = vertices.Count;
        vertices.Add(new Vector3(0f, -Depth, 0f));

        for (int i = 0; i < Segments; i++)
        {
            int next = (i + 1) % Segments;
            int topA = i * 2;
            int bottomA = topA + 1;
            int topB = next * 2;
            int bottomB = topB + 1;
            triangles.Add(topA);
            triangles.Add(bottomA);
            triangles.Add(bottomB);
            triangles.Add(topA);
            triangles.Add(bottomB);
            triangles.Add(topB);
            triangles.Add(bottomCenter);
            triangles.Add(bottomB);
            triangles.Add(bottomA);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Vector3 FindStagingPosition()
    {
        GameObject river = GameObject.Find("WaterWorks_River_Valley");
        Terrain terrain = Terrain.activeTerrain;
        Vector3 position = river != null && river.transform.childCount > 0
            ? river.transform.GetChild(0).position + new Vector3(24f, 0f, 24f)
            : new Vector3(30f, 0f, 30f);
        if (terrain != null)
            position.y = terrain.SampleHeight(position) +
                         terrain.transform.position.y + .32f;
        return position;
    }
}

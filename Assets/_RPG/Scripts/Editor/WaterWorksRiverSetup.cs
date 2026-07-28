using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Places finite WaterWorks planes along the main river-like valley of the generated terrain.
// It deliberately uses several overlapping short planes rather than the package's default
// 100000-unit ocean, so water remains inside the visible channel and does not cover the map.
public static class WaterWorksRiverSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/WaterWorksRiverSetup.generate";
    const string RootName = "WaterWorks_River_Valley";
    const string WaterPrefabPath = "Assets/WaterWorks/Water_Plane.prefab";

    const int SegmentCount = 7;
    const float StartRadius = 105f;
    const float EndRadius = 390f;
    const float RiverWidth = 19f;
    const float SurfaceOffset = 0.32f;
    const float WaterDepth = 18f;
    const string DepthMaterialPath = "Assets/_RPG/Materials/RiverWaterDepth.mat";
    const string DepthMeshPath = "Assets/_RPG/Generated/RiverWaterDepthVolume.asset";

    [MenuItem("RPG/World/Create WaterWorks River")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[WaterWorksRiverSetup] Sali de Play Mode para crear el río.");
            return;
        }

        EditorSceneUtility.OpenSceneSafely(ScenePath);
        Terrain terrain = Terrain.activeTerrain;
        GameObject waterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPrefabPath);
        if (terrain == null || waterPrefab == null)
        {
            Debug.LogError("[WaterWorksRiverSetup] Falta el Terrain activo o WaterWorks/Water_Plane.prefab.");
            return;
        }

        GameObject oldRoot = GameObject.Find(RootName);
        if (oldRoot != null)
            Object.DestroyImmediate(oldRoot);

        GameObject root = new GameObject(RootName);
        float segmentLength = (EndRadius - StartRadius) / SegmentCount + 8f;
        Material depthMaterial = GetOrCreateDepthMaterial();
        Mesh depthMesh = GetOrCreateDepthMesh();

        for (int i = 0; i < SegmentCount; i++)
        {
            float t = (i + 0.5f) / SegmentCount;
            float radius = Mathf.Lerp(StartRadius, EndRadius, t);
            Vector3 position = RiverPosition(radius);
            Vector3 ahead = RiverPosition(radius + 1f);
            Vector3 tangent = ahead - position;
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.001f)
                tangent = Vector3.forward;

            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + SurfaceOffset;
            GameObject segment = (GameObject)PrefabUtility.InstantiatePrefab(waterPrefab);
            segment.name = "River_Water_" + (i + 1).ToString("00");
            segment.transform.SetParent(root.transform, true);
            segment.transform.position = position;
            segment.transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            // Unity's built-in Plane mesh is 10 x 10 metres.
            segment.transform.localScale = new Vector3(RiverWidth / 10f, 1f, segmentLength / 10f);

            // This is visual water only. The player can enter it when swimming is added later;
            // it must not behave as a solid floor.
            MeshCollider collider = segment.GetComponent<MeshCollider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
            if (segment.GetComponent<WaterSurfaceVolume>() == null)
                segment.AddComponent<WaterSurfaceVolume>();

            CreateDepthVolume(segment.transform, depthMesh, depthMaterial);
        }

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WaterWorksRiverSetup] Río WaterWorks creado con " + SegmentCount + " tramos finitos.");
    }

    static void CreateDepthVolume(Transform segment, Mesh mesh, Material material)
    {
        GameObject volume = new GameObject("Water_Depth_Volume", typeof(MeshFilter), typeof(MeshRenderer));
        volume.transform.SetParent(segment, false);
        // The mesh's open top aligns with the animated WaterWorks surface,
        // leaving the waves visible while filling the river below them.
        volume.transform.localPosition = Vector3.zero;
        volume.transform.localRotation = Quaternion.identity;
        volume.transform.localScale = Vector3.one;
        volume.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = volume.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static Material GetOrCreateDepthMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(DepthMaterialPath);
        if (material != null) return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        material = new Material(shader) { name = "RiverWaterDepth" };
        Color deepWater = new Color(0.025f, 0.27f, 0.43f, 0.54f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", deepWater);
        if (material.HasProperty("_Color")) material.SetColor("_Color", deepWater);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;
        AssetDatabase.CreateAsset(material, DepthMaterialPath);
        return material;
    }

    static Mesh GetOrCreateDepthMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(DepthMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "RiverWaterDepthVolume" };
            AssetDatabase.CreateAsset(mesh, DepthMeshPath);
        }

        float half = 5f;
        mesh.Clear();
        mesh.vertices = new[]
        {
            new Vector3(-half, 0f, -half), new Vector3(half, 0f, -half),
            new Vector3(half, 0f, half), new Vector3(-half, 0f, half),
            new Vector3(-half, -WaterDepth, -half), new Vector3(half, -WaterDepth, -half),
            new Vector3(half, -WaterDepth, half), new Vector3(-half, -WaterDepth, half)
        };
        mesh.triangles = new[]
        {
            0, 4, 5, 0, 5, 1,
            1, 5, 6, 1, 6, 2,
            2, 6, 7, 2, 7, 3,
            3, 7, 4, 3, 4, 0,
            4, 7, 6, 4, 6, 5
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Vector3 RiverPosition(float radius)
    {
        // Matches the first broad, lowered PathBand in DistantMountainExpansionGenerator.
        float angle = (-48f + Mathf.Sin(radius * 0.018f) * 9f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    [InitializeOnLoadMethod]
    static void RunRequestedSetup()
    {
        EditorApplication.delayCall += TryRunRequestedSetup;
    }

    static void TryRunRequestedSetup()
    {
        string request = Path.GetFullPath(RequestPath);
        if (!File.Exists(request))
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRunRequestedSetup;
            return;
        }

        File.Delete(request);
        if (File.Exists(request + ".meta"))
            File.Delete(request + ".meta");
        Setup();
    }
}

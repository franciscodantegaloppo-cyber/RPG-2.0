using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DistantMountainExpansionGenerator
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string OldRootName = "DistantMountainExpansion_200m";
    const string DetailRootName = "DistantSmallDetails_200m";
    const string RequestPath = "Assets/_RPG/Generated/DistantMountains.generate";
    const string TerrainAssetPath = "Assets/_RPG/Generated/SingleProtectedWorldTerrain.asset";
    const string GeneratedLayerFolder = "Assets/_RPG/Generated/TerrainLayers";
    const string TerrainMaterialPath = "Assets/_RPG/Generated/TerrainLayers/Generated_URP_Terrain.mat";

    const int Seed = 92417;
    const float ProtectedRadius = 62f;
    const float BlendEndRadius = 92f;
    const float TerrainSize = 1200f;
    const float TerrainHeight = 520f;

    static readonly string[] TerrainLayerPaths =
    {
        GeneratedLayerFolder + "/TerrainLayer_Generated_Grass.terrainlayer",
        GeneratedLayerFolder + "/TerrainLayer_Generated_Rock.terrainlayer",
        GeneratedLayerFolder + "/TerrainLayer_Generated_Dirt.terrainlayer",
        GeneratedLayerFolder + "/TerrainLayer_Generated_Path.terrainlayer",
        GeneratedLayerFolder + "/TerrainLayer_Generated_Snow.terrainlayer",
    };

    static readonly string[] SmallRockPaths =
    {
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 1.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 2.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 3.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 4.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 5.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Rocks/rpgpp_lt_rocks_tiny_01.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Rocks/rpgpp_lt_rock_small_01.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Rocks/rpgpp_lt_rock_small_02.prefab",
    };

    static readonly string[] NatureDetailPaths =
    {
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Trees/Spruce 1.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Trees/Spruce 2.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Bush/Bush.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Grass/Grass.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Flower/Flower.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Log/Log.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Stump/Stump.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Trees/rpgpp_lt_tree_01.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Trees/rpgpp_lt_tree_02.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Trees/rpgpp_lt_tree_pine_01.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Bushes/rpgpp_lt_bush_01.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Bushes/rpgpp_lt_bush_02.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Flowers/rpgpp_lt_flower_01.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Flowers/rpgpp_lt_flower_02.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Flowers/rpgpp_lt_flower_03.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Grass/rpgpp_lt_grass_small_01a.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Grass/rpgpp_lt_grass_small_01b.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Plants/rpgpp_lt_plant_01.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Plants/rpgpp_lt_plant_02.prefab",
    };

    static readonly Vector4[] MajorPeaks =
    {
        new Vector4(135f, 95f, 90f, 0.62f),
        new Vector4(-170f, 80f, 105f, 0.70f),
        new Vector4(95f, -185f, 100f, 0.68f),
        new Vector4(-120f, -210f, 95f, 0.60f),
        new Vector4(255f, 40f, 135f, 0.82f),
        new Vector4(-285f, -45f, 140f, 0.86f),
        new Vector4(40f, 310f, 155f, 0.90f),
        new Vector4(-70f, -345f, 150f, 0.88f),
        new Vector4(360f, 250f, 170f, 0.96f),
        new Vector4(-390f, 230f, 175f, 1.00f),
        new Vector4(325f, -330f, 170f, 0.94f),
        new Vector4(-360f, -320f, 165f, 0.92f),
    };

    static readonly Vector4[] PlayableClearings =
    {
        new Vector4(185f, 210f, 42f, 0.075f),
        new Vector4(-235f, 185f, 48f, 0.085f),
        new Vector4(260f, -135f, 52f, 0.072f),
        new Vector4(-210f, -265f, 46f, 0.090f),
        new Vector4(420f, 45f, 58f, 0.105f),
        new Vector4(-430f, -70f, 55f, 0.105f),
    };

    static readonly Vector4[] ForestZones =
    {
        new Vector4(345f, -120f, 135f, 1f),
        new Vector4(-320f, 265f, 115f, 1f),
    };

    [MenuItem("RPG/World/Generate Single Terrain Mountains 200m")]
    public static void Generate()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        Random.InitState(Seed);
        RemovePreviousSeparatedExpansion();
        ConfigureSceneLightingAndCameras();

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            var terrainObject = Terrain.CreateTerrainGameObject(new TerrainData());
            terrainObject.name = "World_Terrain_Single";
            terrain = terrainObject.GetComponent<Terrain>();
        }

        TerrainData data = CreateSingleTerrainData();
        SaveOrReplaceTerrainAsset(data);

        terrain.terrainData = data;
        terrain.name = "World_Terrain_Single_ProtectedSpawn";
        terrain.transform.position = new Vector3(-TerrainSize * 0.5f, 0f, -TerrainSize * 0.5f);
        terrain.heightmapPixelError = 1f;
        terrain.basemapDistance = 900f;
        terrain.drawInstanced = false;
        terrain.materialTemplate = EnsureTerrainMaterial();

        var terrainCollider = terrain.GetComponent<TerrainCollider>();
        if (terrainCollider != null)
            terrainCollider.terrainData = data;

        terrain.Flush();

        ScatterSmallDetailsOnly();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[DistantMountainExpansion] Single terrain generated. Spawn radius protected. No separate mountain terrain objects were kept.");
    }

    [InitializeOnLoadMethod]
    static void RunRequestedGeneration()
    {
        EditorApplication.delayCall += TryRunRequestedGeneration;
    }

    [DidReloadScripts]
    static void RunAfterScriptsReload()
    {
        EditorApplication.delayCall += TryRunRequestedGeneration;
    }

    static void TryRunRequestedGeneration()
    {
        string absoluteRequestPath = Path.GetFullPath(RequestPath);
        if (!File.Exists(absoluteRequestPath))
            return;

        File.Delete(absoluteRequestPath);
        string metaPath = absoluteRequestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        Generate();
        AssetDatabase.Refresh();
    }

    static TerrainData CreateSingleTerrainData()
    {
        EnsureGeneratedTerrainLayers();

        var data = new TerrainData
        {
            heightmapResolution = 513,
            alphamapResolution = 512,
            baseMapResolution = 1024,
            size = new Vector3(TerrainSize, TerrainHeight, TerrainSize),
        };

        TerrainLayer[] layers = LoadTerrainLayers();
        if (layers.Length > 0)
            data.terrainLayers = layers;

        data.SetHeights(0, 0, BuildHeights(data.heightmapResolution));
        if (layers.Length > 0)
            data.SetAlphamaps(0, 0, BuildAlphas(data.alphamapWidth, data.alphamapHeight, layers.Length));

        return data;
    }

    static float[,] BuildHeights(int resolution)
    {
        var heights = new float[resolution, resolution];
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Vector2 world = HeightWorldPosition(x, z, resolution);
                heights[z, x] = NormalizedTerrainHeight(world);
            }
        }

        return heights;
    }

    static float NormalizedTerrainHeight(Vector2 world)
    {
        float dist = world.magnitude;
        if (dist <= ProtectedRadius)
            return 0f;

        float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ProtectedRadius, BlendEndRadius, dist));
        float mountainBand = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(78f, 170f, dist));
        float outerRise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(95f, 250f, dist)) * 0.10f;

        float ridgeNoise =
            Mathf.PerlinNoise(world.x * 0.0065f + 12.4f, world.y * 0.0065f + 4.8f) * 0.50f +
            Mathf.PerlinNoise(world.x * 0.014f + 41.3f, world.y * 0.014f + 7.2f) * 0.42f +
            Mathf.PerlinNoise(world.x * 0.035f + 5.1f, world.y * 0.035f + 28.8f) * 0.30f;

        float angle = Mathf.Atan2(world.y, world.x);
        float ridgeLine = Mathf.Pow(Mathf.Sin(angle * 9f + dist * 0.045f) * 0.5f + 0.5f, 3.1f) * 0.16f;
        float peak = Mathf.Pow(Mathf.Max(0f, ridgeNoise - 0.70f), 1.55f) * 0.52f;
        float needle = Mathf.Pow(Mathf.Max(0f, Mathf.PerlinNoise(world.x * 0.018f + 80.2f, world.y * 0.018f + 12.6f) - 0.62f), 2.25f) * 0.34f;
        float foothills = Mathf.PerlinNoise(world.x * 0.025f - 6.5f, world.y * 0.025f + 9.1f) * 0.035f;
        float majorPeaks = MajorPeakField(world);

        float paths = PathMask(world.x, world.y);
        float height = outerRise + mountainBand * (Mathf.Pow(ridgeNoise, 2.2f) * 0.09f + ridgeLine + peak + needle);
        height += mountainBand * majorPeaks;
        height += foothills;
        height *= 1f - paths * 0.36f;

        Vector2 clearing = ClearingBlendAndLevel(world);
        if (clearing.x > 0f)
            height = Mathf.Lerp(height, clearing.y, clearing.x * 0.92f);

        return Mathf.Clamp(height * blend, 0f, 0.92f);
    }

    static float[,,] BuildAlphas(int width, int height, int layerCount)
    {
        var alphas = new float[height, width, layerCount];
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 world = AlphaWorldPosition(x, z, width, height);
                float dist = world.magnitude;
                float protectedBlend = Mathf.InverseLerp(ProtectedRadius, BlendEndRadius, dist);
                float paths = PathMask(world.x, world.y);
                float normalizedHeight = NormalizedTerrainHeight(world);
                float clearing = ClearingBlendAndLevel(world).x;
                float forest = ForestMask(world);
                float rocky = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(82f, 165f, dist));
                rocky += Mathf.PerlinNoise(world.x * 0.022f, world.y * 0.022f) * 0.34f;
                rocky += normalizedHeight * 0.45f;

                float snow = Mathf.SmoothStep(0.42f, 0.72f, normalizedHeight);
                snow += Mathf.SmoothStep(0.28f, 0.50f, MajorPeakField(world)) * 0.22f;
                snow *= protectedBlend;

                float grass = Mathf.Clamp01(1f - rocky * 0.65f - paths * 0.95f - snow * 0.92f);
                grass += forest * 0.25f;
                float rock = Mathf.Clamp01(rocky * protectedBlend * (1f - snow * 0.55f));
                float dirt = Mathf.Clamp01((paths + clearing * 0.85f) * protectedBlend);
                float sand = Mathf.Clamp01(paths * 0.12f * protectedBlend);

                SetAlpha(alphas, z, x, 0, layerCount, grass);
                SetAlpha(alphas, z, x, 1, layerCount, rock);
                SetAlpha(alphas, z, x, 2, layerCount, dirt);
                SetAlpha(alphas, z, x, 3, layerCount, sand);
                SetAlpha(alphas, z, x, 4, layerCount, snow);
                NormalizeAlpha(alphas, z, x, layerCount);
            }
        }

        return alphas;
    }

    static float MajorPeakField(Vector2 world)
    {
        float height = 0f;
        for (int i = 0; i < MajorPeaks.Length; i++)
        {
            Vector4 peak = MajorPeaks[i];
            float distance = Vector2.Distance(world, new Vector2(peak.x, peak.y));
            float cone = Mathf.Clamp01(1f - distance / peak.z);
            float sharpSummit = Mathf.Pow(cone, 2.15f) * peak.w * 0.58f;
            float rockyShoulder = Mathf.Pow(cone, 5.0f) * peak.w * 0.22f;
            height = Mathf.Max(height, sharpSummit + rockyShoulder);
        }

        return height;
    }

    static Vector2 ClearingBlendAndLevel(Vector2 world)
    {
        float bestBlend = 0f;
        float level = 0f;
        for (int i = 0; i < PlayableClearings.Length; i++)
        {
            Vector4 clearing = PlayableClearings[i];
            float distance = Vector2.Distance(world, new Vector2(clearing.x, clearing.y));
            float blend = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(clearing.z * 0.62f, clearing.z, distance));
            if (blend > bestBlend)
            {
                bestBlend = blend;
                level = clearing.w;
            }
        }

        return new Vector2(bestBlend, level);
    }

    static float ForestMask(Vector2 world)
    {
        float mask = 0f;
        for (int i = 0; i < ForestZones.Length; i++)
        {
            Vector4 zone = ForestZones[i];
            float distance = Vector2.Distance(world, new Vector2(zone.x, zone.y));
            mask = Mathf.Max(mask, Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(zone.z * 0.55f, zone.z, distance)));
        }

        return mask;
    }

    static void ScatterSmallDetailsOnly()
    {
        var old = GameObject.Find(DetailRootName);
        if (old != null)
            Object.DestroyImmediate(old);

        var root = new GameObject(DetailRootName);
        var rocks = LoadExisting(SmallRockPaths);
        var nature = LoadExisting(NatureDetailPaths);
        Terrain terrain = Terrain.activeTerrain;

        for (int i = 0; i < 420; i++)
        {
            Vector3 pos = RandomRingPosition(ProtectedRadius + 25f, 575f);
            Vector2 world = new Vector2(pos.x, pos.z);
            if ((PathMask(pos.x, pos.z) > 0.38f || ClearingBlendAndLevel(world).x > 0.25f) && Random.value < 0.82f)
                continue;

            pos = PlaceOnTerrain(terrain, pos);
            float scale = Random.Range(0.14f, 0.38f);
            InstantiateRandom(rocks, root.transform, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), scale);
        }

        for (int i = 0; i < 980; i++)
        {
            Vector3 pos = RandomRingPosition(ProtectedRadius + 18f, 585f);
            Vector2 world = new Vector2(pos.x, pos.z);
            if ((PathMask(pos.x, pos.z) > 0.52f || ClearingBlendAndLevel(world).x > 0.28f) && Random.value < 0.9f)
                continue;

            pos = PlaceOnTerrain(terrain, pos);
            float dist = new Vector2(pos.x, pos.z).magnitude;
            float scale = dist > 360f ? Random.Range(0.8f, 1.7f) : Random.Range(0.55f, 1.15f);
            InstantiateRandom(nature, root.transform, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), scale);
        }

        for (int i = 0; i < 520; i++)
        {
            Vector4 zone = ForestZones[Random.Range(0, ForestZones.Length)];
            Vector2 offset = Random.insideUnitCircle * zone.z;
            Vector3 pos = new Vector3(zone.x + offset.x, 0f, zone.y + offset.y);
            if (new Vector2(pos.x, pos.z).magnitude < ProtectedRadius + 20f || PathMask(pos.x, pos.z) > 0.32f)
                continue;

            pos = PlaceOnTerrain(terrain, pos);
            float scale = Random.Range(0.85f, 1.9f);
            InstantiateRandom(nature, root.transform, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), scale);
        }
    }

    static Vector3 PlaceOnTerrain(Terrain terrain, Vector3 position)
    {
        if (terrain == null)
            return position;

        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        return position;
    }

    static void RemovePreviousSeparatedExpansion()
    {
        var oldRoot = GameObject.Find(OldRootName);
        if (oldRoot != null)
            Object.DestroyImmediate(oldRoot);

        string folder = "Assets/_RPG/Generated/DistantMountains";
        if (AssetDatabase.IsValidFolder(folder))
            AssetDatabase.DeleteAsset(folder);
    }

    static float PathMask(float x, float z)
    {
        float angle = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
        float radius = new Vector2(x, z).magnitude;
        float mask =
            PathBand(angle, radius, -48f, 76f, 410f, 9.0f) +
            PathBand(angle, radius, 36f, 82f, 520f, 9.5f) +
            PathBand(angle, radius, 123f, 105f, 555f, 10f) +
            PathBand(angle, radius, -150f, 120f, 590f, 10f) +
            PathBand(angle, radius, 172f, 170f, 500f, 7.5f) +
            PathBand(angle, radius, -8f, 185f, 470f, 7.5f);
        return Mathf.Clamp01(mask);
    }

    static float PathBand(float angle, float radius, float targetAngle, float minRadius, float maxRadius, float width)
    {
        float angleDelta = Mathf.Abs(Mathf.DeltaAngle(angle, targetAngle + Mathf.Sin(radius * 0.018f) * 9f));
        float radial = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minRadius - 22f, minRadius + 18f, radius));
        radial *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(maxRadius - 18f, maxRadius + 28f, radius));
        return Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(width, width + 9f, angleDelta)) * radial;
    }

    static Vector2 HeightWorldPosition(int x, int z, int resolution)
    {
        float wx = -TerrainSize * 0.5f + (x / (float)(resolution - 1)) * TerrainSize;
        float wz = -TerrainSize * 0.5f + (z / (float)(resolution - 1)) * TerrainSize;
        return new Vector2(wx, wz);
    }

    static Vector2 AlphaWorldPosition(int x, int z, int width, int height)
    {
        float wx = -TerrainSize * 0.5f + (x / (float)(width - 1)) * TerrainSize;
        float wz = -TerrainSize * 0.5f + (z / (float)(height - 1)) * TerrainSize;
        return new Vector2(wx, wz);
    }

    static TerrainLayer[] LoadTerrainLayers()
    {
        var layers = new List<TerrainLayer>();
        foreach (string path in TerrainLayerPaths)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer != null)
                layers.Add(layer);
        }
        return layers.ToArray();
    }

    static void EnsureGeneratedTerrainLayers()
    {
        EnsureGeneratedFolder();
        if (!AssetDatabase.IsValidFolder(GeneratedLayerFolder))
            AssetDatabase.CreateFolder("Assets/_RPG/Generated", "TerrainLayers");

        EnsureTerrainLayer("Grass", new Color(0.18f, 0.30f, 0.16f), new Color(0.34f, 0.42f, 0.25f), 5.5f);
        EnsureTerrainLayer("Rock", new Color(0.28f, 0.28f, 0.26f), new Color(0.48f, 0.47f, 0.42f), 6.5f);
        EnsureTerrainLayer("Dirt", new Color(0.25f, 0.19f, 0.13f), new Color(0.42f, 0.31f, 0.20f), 4.5f);
        EnsureTerrainLayer("Path", new Color(0.36f, 0.29f, 0.20f), new Color(0.54f, 0.45f, 0.31f), 4f);
        EnsureTerrainLayer("Snow", new Color(0.82f, 0.88f, 0.92f), new Color(0.98f, 0.99f, 1f), 6f);
    }

    static Material EnsureTerrainMaterial()
    {
        string defaultTerrainPath = AssetDatabase.GUIDToAssetPath("594ea882c5a793440b60ff72d896021e");
        var defaultTerrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(defaultTerrainPath);
        if (defaultTerrainMaterial != null)
            return defaultTerrainMaterial;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (shader == null)
            shader = Shader.Find("Nature/Terrain/Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            return material;

        if (material == null)
        {
            material = new Material(shader)
            {
                name = "Generated_URP_Terrain"
            };
            AssetDatabase.CreateAsset(material, TerrainMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    static void EnsureTerrainLayer(string name, Color low, Color high, float tileSize)
    {
        string texturePath = GeneratedLayerFolder + "/Terrain_" + name + "_Texture.asset";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, true);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            AssetDatabase.CreateAsset(texture, texturePath);
        }

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float broad = Mathf.PerlinNoise((x + name.Length * 19) * 0.085f, (y + name.Length * 31) * 0.085f);
                float fine = Mathf.PerlinNoise((x + 7) * 0.36f, (y + 13) * 0.36f);
                float dryPatch = Mathf.PerlinNoise((x + 91) * 0.17f, (y + 47) * 0.17f);
                float mix = Mathf.Clamp01(broad * 0.64f + fine * 0.24f + dryPatch * 0.12f);
                Color color = Color.Lerp(low, high, mix);
                if (name == "Grass")
                    color = Color.Lerp(color, new Color(0.30f, 0.27f, 0.16f), Mathf.SmoothStep(0.54f, 0.95f, dryPatch) * 0.28f);
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply();
        EditorUtility.SetDirty(texture);

        string layerPath = GeneratedLayerFolder + "/TerrainLayer_Generated_" + name + ".terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, layerPath);
        }

        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(tileSize, tileSize);
        layer.tileOffset = Vector2.zero;
        layer.specular = Color.black;
        layer.metallic = 0f;
        layer.smoothness = 0f;
        EditorUtility.SetDirty(layer);
    }

    static void ConfigureSceneLightingAndCameras()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.70f, 0.84f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.55f, 0.50f);
        RenderSettings.ambientGroundColor = new Color(0.27f, 0.30f, 0.25f);
        RenderSettings.fog = false;

        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.58f, 0.73f, 0.92f);
            EditorUtility.SetDirty(camera);
        }
    }

    static List<GameObject> LoadExisting(IEnumerable<string> paths)
    {
        var loaded = new List<GameObject>();
        foreach (string path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                loaded.Add(prefab);
        }
        return loaded;
    }

    static void InstantiateRandom(IReadOnlyList<GameObject> prefabs, Transform parent, Vector3 pos, Quaternion rot, float uniformScale)
    {
        if (prefabs == null || prefabs.Count == 0)
            return;

        var prefab = prefabs[Random.Range(0, prefabs.Count)];
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        obj.transform.SetParent(parent, true);
        obj.transform.SetPositionAndRotation(pos, rot);
        obj.transform.localScale = obj.transform.localScale * uniformScale;
        obj.name = prefab.name;
    }

    static Vector3 RandomRingPosition(float minRadius, float maxRadius)
    {
        float radius = Mathf.Sqrt(Random.Range(minRadius * minRadius, maxRadius * maxRadius));
        float angle = Random.Range(-Mathf.PI, Mathf.PI);
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    static void SaveOrReplaceTerrainAsset(TerrainData data)
    {
        EnsureGeneratedFolder();
        AssetDatabase.DeleteAsset(TerrainAssetPath);
        AssetDatabase.CreateAsset(data, TerrainAssetPath);
    }

    static void EnsureGeneratedFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Generated"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Generated");
    }

    static void SetAlpha(float[,,] alphas, int z, int x, int layer, int layerCount, float value)
    {
        if (layer < layerCount)
            alphas[z, x, layer] = Mathf.Max(0f, value);
    }

    static void NormalizeAlpha(float[,,] alphas, int z, int x, int layerCount)
    {
        float total = 0f;
        for (int i = 0; i < layerCount; i++)
            total += alphas[z, x, i];

        if (total <= 0f)
        {
            alphas[z, x, 0] = 1f;
            return;
        }

        for (int i = 0; i < layerCount; i++)
            alphas[z, x, i] /= total;
    }
}

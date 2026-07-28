using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DecorateWorldNature
{
    private const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    private const string DecorRootName = "NaturePack_Decorations";

    private static readonly string[] TreePrefabs = {
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Trees/rpgpp_lt_tree_01.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Trees/rpgpp_lt_tree_02.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Vegetation/Trees/rpgpp_lt_tree_pine_01.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Trees/Spruce 1.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Trees/Spruce 2.prefab"
    };

    private static readonly string[] RockPrefabs = {
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 1.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 2.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 3.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 4.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Rock Cliffs/Rock Cliff 1.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Rock Cliffs/Rock Cliff 2.prefab",
        "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Rock Cliffs/Rock Cliff 3.prefab"
    };

    private const string SkyPrefabPath = "Assets/RPGPP_LT/Prefabs/Nature/Sky/rpgpp_lt_sky_01.prefab";
    private static readonly string[] CloudPrefabs = {
        "Assets/RPGPP_LT/Prefabs/Nature/Sky/rpgpp_lt_cloud_01.prefab",
        "Assets/RPGPP_LT/Prefabs/Nature/Sky/rpgpp_lt_cloud_02.prefab"
    };

    [MenuItem("RPG/World/Decorate World Nature and Lower Mountains")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("No active terrain found in the scene.");
            return;
        }

        // 1. Lower and smooth the mountains
        LowerMountains(terrain);

        // 2. Fix LOD group offsets so they don't float or get buried
        FixLODChildrenLocalPositions();

        // 3. Align ALL genuine existing trees and rocks to prevent floating objects (protecting structural walls)
        AlignExistingObjects(terrain);

        // 4. Convert Legacy/Standard shaders of all nature packs to URP Lit so they have gorgeous textures instead of being pink
        ConvertStandardShadersToURP();

        // 5. Clear old generated decorations
        var oldDecor = GameObject.Find(DecorRootName);
        if (oldDecor != null)
        {
            Object.DestroyImmediate(oldDecor);
        }

        var decorRoot = new GameObject(DecorRootName);
        decorRoot.transform.position = Vector3.zero;

        // 6. Set up the realistic Sky Dome and Cloud system
        SetupSkyAndClouds(decorRoot.transform);

        // 7. Place beautiful protective forest along the path from spawn to armory zone
        BuildProtectivePathForest(decorRoot.transform, terrain);

        // 8. Scatter dense forests and rock formations across the terrain and mountains
        ScatterFoliageAndRocks(decorRoot.transform, terrain);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("Successfully converted standard shaders, lowered mountains, aligned all assets, built the realistic sky dome with clouds, and scattered rich trees and rocks!");
    }

    private static void FixLODChildrenLocalPositions()
    {
        var allGos = Object.FindObjectsByType<GameObject>();
        int resetCount = 0;

        foreach (var go in allGos)
        {
            if (go == null) continue;

            string name = go.name.ToLower();
            if (name.Contains("_lod0") || name.Contains("_lod1") || name.Contains("_lod2") || name.Contains("_lod3"))
            {
                if (go.transform.localPosition != Vector3.zero)
                {
                    go.transform.localPosition = Vector3.zero;
                    resetCount++;
                }
            }
        }
        Debug.Log($"Reset local position of {resetCount} LOD child objects to zero to resolve layout offsets.");
    }

    private static void ConvertStandardShadersToURP()
    {
        var urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null)
        {
            Debug.LogError("URP Lit shader not found.");
            return;
        }

        string[] searchFolders = {
            "Assets/RPGPP_LT",
            "Assets/Proxy Games",
            "Assets/Daniel Mistage",
            "Assets/YughuesFreeNatureMaterials"
        };

        string[] matGuids = AssetDatabase.FindAssets("t:Material", searchFolders);
        int converted = 0;

        foreach (var guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            string shaderName = mat.shader != null ? mat.shader.name : "None";
            if (shaderName.Contains("Standard") || shaderName.Contains("Classic") || shaderName.Contains("Mobile/Diffuse"))
            {
                var tex = mat.mainTexture;
                var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

                mat.shader = urpShader;
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                }
                mat.SetColor("_BaseColor", color);

                string matNameLower = mat.name.ToLower();
                if (matNameLower.Contains("leaf") || matNameLower.Contains("leaves") || matNameLower.Contains("grass") || 
                    matNameLower.Contains("flower") || matNameLower.Contains("foliage") || matNameLower.Contains("cloud") ||
                    matNameLower.Contains("tree") || matNameLower.Contains("branch"))
                {
                    mat.SetFloat("_AlphaClip", 1f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
                }

                EditorUtility.SetDirty(mat);
                converted++;
            }
        }

        // Also scan and convert any scene-used Standard material
        var allRenderers = Object.FindObjectsByType<Renderer>();
        HashSet<Material> processedMats = new HashSet<Material>();
        foreach (var r in allRenderers)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null || processedMats.Contains(mat)) continue;
                processedMats.Add(mat);

                string shaderName = mat.shader != null ? mat.shader.name : "None";
                if (shaderName.Contains("Standard") || shaderName.Contains("Classic") || shaderName.Contains("Mobile/Diffuse"))
                {
                    var tex = mat.mainTexture;
                    var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

                    mat.shader = urpShader;
                    if (tex != null)
                    {
                        mat.SetTexture("_BaseMap", tex);
                    }
                    mat.SetColor("_BaseColor", color);

                    string matNameLower = mat.name.ToLower();
                    if (matNameLower.Contains("leaf") || matNameLower.Contains("leaves") || matNameLower.Contains("grass") || 
                        matNameLower.Contains("flower") || matNameLower.Contains("foliage") || matNameLower.Contains("cloud") ||
                        matNameLower.Contains("tree") || matNameLower.Contains("branch"))
                    {
                        mat.SetFloat("_AlphaClip", 1f);
                        mat.EnableKeyword("_ALPHATEST_ON");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                        if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
                    }

                    EditorUtility.SetDirty(mat);
                    converted++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Converted {converted} Standard materials to Universal Render Pipeline/Lit.");
    }

    private static void LowerMountains(Terrain terrain)
    {
        var data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        int res = data.heightmapResolution;
        float sizeY = data.size.y;

        float[,] heights = data.GetHeights(0, 0, res, res);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float h = heights[y, x];
                float worldY = h * sizeY + terrainPos.y;

                // Keeps the spawn, village, and low plateaus intact (Y < 15m), but dampens higher mountain peaks by 65%
                if (worldY > 15f)
                {
                    float excess = worldY - 15f;
                    float newWorldY = 15f + excess * 0.35f;
                    heights[y, x] = (newWorldY - terrainPos.y) / sizeY;
                }
            }
        }

        data.SetHeights(0, 0, heights);
        terrain.Flush();
        Debug.Log("Mountains successfully lowered and smoothed.");
    }

    private static void AlignExistingObjects(Terrain terrain)
    {
        var terrainPos = terrain.transform.position;
        var allGos = Object.FindObjectsByType<GameObject>();
        int alignedCount = 0;

        foreach (var go in allGos)
        {
            if (go == null) continue;

            string name = go.name.ToLower();
            
            // Skip structural, player, camera, UI, and lighting objects to protect village houses and walls
            if (name.Contains("camera") || name.Contains("light") || name.Contains("manager") || name.Contains("player") || 
                name.Contains("canvas") || name.Contains("hud") || name.Contains("merchant") || name.Contains("tonio") || 
                name.Contains("npc") || name.Contains("spawnvillage") || name.Contains("castle") || name.Contains("house") || 
                name.Contains("wall") || name.Contains("gate") || name.Contains("pillar") || name.Contains("tower") || 
                name.Contains("door") || name.Contains("window") || name.Contains("floor") || name.Contains("roof") || 
                name.Contains("ceiling") || name.Contains("colliders") || name.Contains("structure") || name.Contains("building"))
                continue;

            // Do not align LOD level children individually to prevent breaking the LOD group hierarchy
            if (name.Contains("_lod0") || name.Contains("_lod1") || name.Contains("_lod2") || name.Contains("_lod3"))
                continue;

            // Target nature elements only
            bool isNature = name.Contains("tree") || name.Contains("spruce") || name.Contains("rock") || name.Contains("cliff") || 
                             name.Contains("pino") || name.Contains("arbol") || name.Contains("piedra") || name.Contains("bush") || 
                             name.Contains("grass") || name.Contains("flower") || name.Contains("plant") || 
                             name.Contains("log_") || name.Contains("branch") || name.Contains("stump") || name.Contains("mushroom") ||
                             (go.transform.parent != null && (go.transform.parent.name.Contains("Distant") || 
                                                              go.transform.parent.name.Contains("Scatter") || 
                                                              go.transform.parent.name.Contains("Grass") || 
                                                              go.transform.parent.name.Contains("Forest")));

            if (isNature)
            {
                // Skip sky and clouds
                if (name.Contains("sky") || name.Contains("cloud"))
                    continue;

                Vector3 pos = go.transform.position;
                float groundY = terrain.SampleHeight(pos) + terrainPos.y;
                
                // Align parent perfectly to terrain surface
                go.transform.position = new Vector3(pos.x, groundY, pos.z);
                alignedCount++;
            }
        }

        Debug.Log($"Aligned {alignedCount} root nature elements to the terrain surface perfectly.");
    }

    private static void SetupSkyAndClouds(Transform parent)
    {
        // Place Sky Dome
        var skyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SkyPrefabPath);
        if (skyAsset != null)
        {
            // Destroy any existing sky dome to avoid duplicate sky rendering
            var oldSkies = GameObject.FindObjectsByType<GameObject>();
            foreach (var os in oldSkies)
            {
                if (os.name.Contains("rpgpp_lt_sky_01"))
                {
                    Object.DestroyImmediate(os);
                }
            }

            var skyInstance = (GameObject)PrefabUtility.InstantiatePrefab(skyAsset, parent);
            skyInstance.name = "rpgpp_lt_sky_01_CastleSky";
            skyInstance.transform.position = new Vector3(0f, -5f, 0f);
            skyInstance.transform.localScale = new Vector3(22f, 15f, 22f); // Covers the entire map area beautifully
        }

        // Scatter 40 beautiful real clouds
        var cloudParent = new GameObject("Sky_Clouds").transform;
        cloudParent.SetParent(parent, false);

        for (int i = 0; i < 40; i++)
        {
            string cloudPath = CloudPrefabs[Random.Range(0, CloudPrefabs.Length)];
            var cloudAsset = AssetDatabase.LoadAssetAtPath<GameObject>(cloudPath);
            if (cloudAsset == null) continue;

            float x = Random.Range(-500f, 500f);
            float z = Random.Range(-500f, 600f);
            float y = Random.Range(120f, 170f);

            var cloud = (GameObject)PrefabUtility.InstantiatePrefab(cloudAsset, cloudParent);
            cloud.name = $"Cloud_{i}";
            cloud.transform.position = new Vector3(x, y, z);
            cloud.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            
            float scale = Random.Range(8f, 18f);
            cloud.transform.localScale = new Vector3(scale, scale * 0.7f, scale);
        }
    }

    private static void BuildProtectivePathForest(Transform parent, Terrain terrain)
    {
        var pathParent = new GameObject("Path_ProtectiveForest").transform;
        pathParent.SetParent(parent, false);

        Vector3 spawnPos = new Vector3(0f, 0f, 0f);
        Vector3 armoryPos = new Vector3(-12.25f, 0f, -29.56f);

        // Line the path with beautiful protective trees, keeping a safe distance of 5m to 12m from the pathway center
        for (float t = 0.1f; t <= 1.0f; t += 0.05f)
        {
            Vector3 pathPt = Vector3.Lerp(spawnPos, armoryPos, t);
            Vector3 pathDir = (armoryPos - spawnPos).normalized;
            Vector3 sideOffset = new Vector3(-pathDir.z, 0f, pathDir.x); // Perpendicular to path

            for (int side = -1; side <= 1; side += 2)
            {
                float offsetDist = Random.Range(5.5f, 12f);
                Vector3 treePos = pathPt + sideOffset * side * offsetDist;

                float groundY = terrain.SampleHeight(treePos) + terrain.transform.position.y;
                treePos.y = groundY;

                // Only place outside immediate spawn zone
                if (Vector3.Distance(treePos, spawnPos) > 12f)
                {
                    string treePath = TreePrefabs[Random.Range(0, TreePrefabs.Length)];
                    var treeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(treePath);
                    if (treeAsset != null)
                    {
                        var tree = (GameObject)PrefabUtility.InstantiatePrefab(treeAsset, pathParent);
                        tree.name = "Path_ProtectiveTree";
                        tree.transform.position = treePos;
                        tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        float scale = Random.Range(0.85f, 1.35f);
                        tree.transform.localScale = new Vector3(scale, scale, scale);

                        // Ensure trees have solid physics colliders
                        if (tree.GetComponent<Collider>() == null)
                        {
                            var col = tree.AddComponent<CapsuleCollider>();
                            col.height = 8f;
                            col.radius = 0.8f;
                            col.center = new Vector3(0f, 4f, 0f);
                        }
                    }
                }
            }
        }
    }

    private static void ScatterFoliageAndRocks(Transform parent, Terrain terrain)
    {
        var scatterParent = new GameObject("Terrain_Scatter_Dense").transform;
        scatterParent.SetParent(parent, false);

        Vector3 spawnPos = new Vector3(0f, 0f, 0f);
        Vector3 castlePos = new Vector3(-200f, 48.9f, 350f);

        // Scatter 350 dense forest trees and 150 gorgeous rocks/cliffs
        int placedTrees = 0;
        int placedRocks = 0;

        while (placedTrees < 350 || placedRocks < 150)
        {
            float rx = Random.Range(-550f, 550f);
            float rz = Random.Range(-550f, 550f);
            Vector3 testPos = new Vector3(rx, 0f, rz);

            // Distance guards to protect gameplay spaces
            if (Vector3.Distance(testPos, spawnPos) < 25f) continue;
            if (Vector3.Distance(testPos, castlePos) < 45f && terrain.SampleHeight(testPos) < 40f) continue; // Keep castle courtyard clean

            float groundY = terrain.SampleHeight(testPos) + terrain.transform.position.y;
            testPos.y = groundY;

            bool placeTree = placedTrees < 350 && (placedRocks >= 150 || Random.value > 0.3f);

            if (placeTree)
            {
                string treePath = TreePrefabs[Random.Range(0, TreePrefabs.Length)];
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(treePath);
                if (asset != null)
                {
                    var tree = (GameObject)PrefabUtility.InstantiatePrefab(asset, scatterParent);
                    tree.name = "ScatterTree";
                    tree.transform.position = testPos;
                    tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    float scale = Random.Range(0.8f, 1.6f);
                    tree.transform.localScale = new Vector3(scale, scale, scale);

                    if (tree.GetComponent<Collider>() == null)
                    {
                        var col = tree.AddComponent<CapsuleCollider>();
                        col.height = 8f;
                        col.radius = 0.8f;
                        col.center = new Vector3(0f, 4f, 0f);
                    }
                    placedTrees++;
                }
            }
            else
            {
                string rockPath = RockPrefabs[Random.Range(0, RockPrefabs.Length)];
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(rockPath);
                if (asset != null)
                {
                    var rock = (GameObject)PrefabUtility.InstantiatePrefab(asset, scatterParent);
                    rock.name = "ScatterRock";
                    rock.transform.position = testPos;
                    // Slightly tilt rocks randomly to look organic and natural
                    rock.transform.rotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-5f, 5f));
                    float scale = Random.Range(0.6f, 2.5f);
                    rock.transform.localScale = new Vector3(scale, scale, scale);

                    if (rock.GetComponent<Collider>() == null)
                    {
                        var col = rock.AddComponent<BoxCollider>();
                        col.size = Vector3.one * 2f;
                    }
                    placedRocks++;
                }
            }
        }
    }
}

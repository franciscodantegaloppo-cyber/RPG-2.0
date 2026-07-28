using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class TerrainMountainizer
{
    [MenuItem("RPG/Terrain/Make Mountains Around Spawn")]
    public static void MakeMountains()
    {
        string scenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        // Open scene
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        
        // Find terrain
        Terrain terrain = Object.FindAnyObjectByType<Terrain>(FindObjectsInactive.Exclude);
        if (terrain == null)
        {
            EditorUtility.DisplayDialog("Error", "No Terrain found in scene.", "OK");
            return;
        }
        
        TerrainData terrainData = terrain.terrainData;
        if (terrainData == null)
        {
            EditorUtility.DisplayDialog("Error", "Terrain has no terrain data.", "OK");
            return;
        }
        
        // Get terrain position and size
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;
        
        // Find house and spawn objects
        List<Vector3> protectedPoints = new List<Vector3>();
        
        // Houses: tag "House" or name contains "House"
        var houseObjects = GameObject.FindGameObjectsWithTag("House");
        foreach (var go in houseObjects)
        {
            protectedPoints.Add(go.transform.position);
        }
        // Also search by name
        var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        foreach (var t in transforms)
        {
            if (t.name.Contains("House") && !protectedPoints.Contains(t.position))
            {
                protectedPoints.Add(t.position);
            }
        }
        
        // Spawn: tag "Spawn" or name contains "Spawn"
        var spawnObjects = GameObject.FindGameObjectsWithTag("Spawn");
        foreach (var go in spawnObjects)
        {
            protectedPoints.Add(go.transform.position);
        }
        foreach (var t in transforms)
        {
            if (t.name.Contains("Spawn") && !protectedPoints.Contains(t.position))
            {
                protectedPoints.Add(t.position);
            }
        }
        
        if (protectedPoints.Count == 0)
        {
            EditorUtility.DisplayDialog("Warning", "No protected points (houses/spawn) found. Will modify entire terrain.", "OK");
        }
        
        // Parameters
        float innerRadius = 15f; // keep flat around houses/spawn
        float outerRadius = 40f; // blend zone
        float mountainHeight = 80f; // max height addition
        float noiseFrequency = 0.05f;
        int octaves = 3;
        float persistence = 0.5f;
        float lacunarity = 2.0f;
        
        int width = terrainData.heightmapResolution;
        int height = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, width, height);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Normalized coordinates
                float nx = (float)x / (width - 1);
                float ny = (float)y / (height - 1);
                
                // World position on terrain
                Vector3 worldPos = terrainPos + new Vector3(nx * terrainSize.x, 0, ny * terrainSize.z);
                
                // Find distance to nearest protected point
                float minDist = float.MaxValue;
                foreach (var p in protectedPoints)
                {
                    float d = Vector3.Distance(worldPos, p);
                    if (d < minDist) minDist = d;
                }
                
                float heightFactor = 0f; // 0 = keep original, 1 = full mountain
                if (protectedPoints.Count > 0)
                {
                    if (minDist < innerRadius)
                    {
                        heightFactor = 0f;
                    }
                    else if (minDist < outerRadius)
                    {
                        // blend
                        float t = (minDist - innerRadius) / (outerRadius - innerRadius);
                        heightFactor = Mathf.SmoothStep(0f, 1f, t);
                    }
                    else
                    {
                        heightFactor = 1f;
                    }
                }
                else
                {
                    // no protected points, apply full mountain everywhere
                    heightFactor = 1f;
                }
                
                // Original height (normalized 0-1)
                float originalHeight = heights[y, x];
                
                // Compute mountain height using fractal noise
                float n = 0f;
                float amplitude = 1f;
                float frequency = noiseFrequency;
                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = worldPos.x * frequency;
                    float sampleZ = worldPos.z * frequency;
                    float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f; // -1 to 1
                    n += perlin * amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }
                // Normalize n to 0-1 range (approx)
                float maxAmplitude = 1f / (1f - persistence);
                n = (n / maxAmplitude + 1f) * 0.5f; // 0 to 1
                
                // Mountain height offset (in world units)
                float mountainOffset = n * mountainHeight;
                
                // Convert mountain offset to normalized height (0-1) based on terrain terrain size y
                float mountainHeightNorm = mountainOffset / terrainSize.y;
                
                // Blend
                float newHeight = Mathf.Lerp(originalHeight, mountainHeightNorm, heightFactor);
                
                // Clamp to 0-1
                newHeight = Mathf.Clamp01(newHeight);
                
                heights[y, x] = newHeight;
            }
        }
        
        terrainData.SetHeights(0, 0, heights);
        
        // Mark dirty
        EditorUtility.SetDirty(terrain);
        EditorUtility.SetDirty(terrainData);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Success", "Terrain modified successfully.", "OK");
    }
}

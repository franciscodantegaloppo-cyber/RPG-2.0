using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GrassFlowersTerrainSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string SourceFolder = "Assets/ALP_Assets/GrassFlowersFREE/Textures/GrassFlowers";
    const string RequestPath = "Assets/_RPG/Generated/GrassFlowersTerrainSetup.generate";

    [InitializeOnLoadMethod]
    static void Queue() => EditorApplication.delayCall += TryRun;

    static void TryRun()
    {
        if (!File.Exists(Path.GetFullPath(RequestPath)) ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        Setup();
    }

    [MenuItem("RPG/World/Add GrassFlowers to Terrain Paint Details")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        Terrain terrain = Terrain.activeTerrain ??
            Object.FindAnyObjectByType<Terrain>(FindObjectsInactive.Include);
        if (terrain == null)
        {
            Debug.LogError("[GrassFlowersTerrainSetup] No se encontró el Terrain.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SourceFolder });
        List<DetailPrototype> prototypes =
            new List<DetailPrototype>(terrain.terrainData.detailPrototypes);
        int added = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!Path.GetFileNameWithoutExtension(path)
                    .StartsWith("grass", System.StringComparison.OrdinalIgnoreCase))
                continue;
            ConfigureTexture(path);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || prototypes.Exists(p => p.prototypeTexture == texture))
                continue;

            bool flower = Path.GetFileNameWithoutExtension(path)
                .Contains("flower", System.StringComparison.OrdinalIgnoreCase);
            DetailPrototype prototype = new DetailPrototype
            {
                prototypeTexture = texture,
                renderMode = DetailRenderMode.GrassBillboard,
                usePrototypeMesh = false,
                minWidth = flower ? .18f : .22f,
                maxWidth = flower ? .42f : .58f,
                minHeight = flower ? .22f : .3f,
                maxHeight = flower ? .52f : .72f,
                noiseSpread = .08f,
                healthyColor = Color.white,
                dryColor = new Color(.72f, .72f, .72f, 1f)
            };
            prototypes.Add(prototype);
            added++;
        }

        terrain.terrainData.detailPrototypes = prototypes.ToArray();
        terrain.detailObjectDensity = 1f;
        EditorUtility.SetDirty(terrain.terrainData);
        EditorUtility.SetDirty(terrain);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        string request = Path.GetFullPath(RequestPath);
        if (File.Exists(request)) File.Delete(request);
        if (File.Exists(request + ".meta")) File.Delete(request + ".meta");
        AssetDatabase.Refresh();
        Debug.Log("[GrassFlowersTerrainSetup] " + added +
                  " variantes agregadas a Paint Details sin borrar el pasto pintado.");
    }

    static void ConfigureTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }
}

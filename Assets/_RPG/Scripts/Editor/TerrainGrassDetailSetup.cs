using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TerrainGrassDetailSetup
{
    const string GrassTexturePath =
        "Assets/TENKOKU - DYNAMIC SKY/_DEMO/TERRAIN/GRASS/tex_grass1.png";

    static TerrainGrassDetailSetup()
    {
        EditorApplication.delayCall += AddGrassToOpenScene;
    }

    [MenuItem("RPG/World/Add Paintable Grass Detail")]
    public static void AddGrassToOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        ConfigureTextureImporter();

        Texture2D grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTexturePath);
        if (grassTexture == null)
        {
            Debug.LogError("[TerrainGrassDetailSetup] No se encontró la textura de césped: " +
                           GrassTexturePath);
            return;
        }

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

        if (terrains.Length == 0)
            return;

        var processedData = new HashSet<TerrainData>();
        int changedTerrains = 0;
        int removedBrokenEntries = 0;

        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain != null ? terrain.terrainData : null;
            if (data == null || !processedData.Add(data))
                continue;

            var details = new List<DetailPrototype>(data.detailPrototypes);
            int brokenOnThisTerrain = 0;

            // An empty detail prototype is rendered as a white/invalid entry in
            // Paint Details and cannot paint anything useful.
            brokenOnThisTerrain = details.RemoveAll(detail =>
                detail == null ||
                (detail.prototypeTexture == null && detail.prototype == null));
            removedBrokenEntries += brokenOnThisTerrain;

            int correctIndex = details.FindIndex(detail =>
                detail != null && detail.prototypeTexture == grassTexture);
            int whiteLegacyIndex = details.FindIndex(IsWhiteLegacyGrass);
            bool changed = brokenOnThisTerrain > 0;

            Undo.RegisterCompleteObjectUndo(data, "Repair paintable grass detail");

            // A Terrain created without a useful detail map accepts brush input in the editor,
            // but stores no visible grass. Allocate a practical map before the player paints.
            // The world uses one very large Terrain. At 1024 each detail cell covers too much
            // ground and individual grass cards look isolated even at full brush opacity.
            // 2048 halves that spacing while remaining considerably lighter than scene prefabs.
            if (data.detailResolution < 2048)
            {
                data.SetDetailResolution(2048, 32);
                changed = true;
            }

            // Coverage mode may still produce a single isolated card in a large world cell.
            // Instance Count stores an actual number of grass cards (up to 16) per painted
            // sample, so a full-strength brush creates a continuous patch instead of dots.
            if (data.detailScatterMode != DetailScatterMode.InstanceCountMode)
            {
                data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
                changed = true;
            }

            if (whiteLegacyIndex >= 0)
            {
                // Keep the old layer position so any density already painted in
                // that slot is preserved, but replace its unusable white texture.
                details[whiteLegacyIndex] = CreateGrassPrototype(grassTexture);
                changed = true;

                if (correctIndex >= 0 && correctIndex != whiteLegacyIndex)
                {
                    details.RemoveAt(correctIndex);
                }
            }
            else if (correctIndex < 0)
            {
                details.Add(CreateGrassPrototype(grassTexture));
                changed = true;
            }
            else if (NeedsGrassRepair(details[correctIndex], grassTexture))
            {
                details[correctIndex] = CreateGrassPrototype(grassTexture);
                changed = true;
            }

            if (!terrain.drawTreesAndFoliage ||
                terrain.detailObjectDistance < 120f ||
                terrain.detailObjectDensity < .95f)
            {
                Undo.RecordObject(terrain, "Enable terrain grass rendering");
                terrain.drawTreesAndFoliage = true;
                terrain.detailObjectDistance = Mathf.Max(terrain.detailObjectDistance, 120f);
                terrain.detailObjectDensity = Mathf.Max(terrain.detailObjectDensity, 1f);
                EditorUtility.SetDirty(terrain);
                changed = true;
            }

            if (changed)
            {
                data.detailPrototypes = details.ToArray();
                data.wavingGrassStrength = .24f;
                data.wavingGrassSpeed = .32f;
                data.wavingGrassAmount = .18f;
                data.wavingGrassTint = new Color(.72f, .82f, .63f, 1f);
                EditorUtility.SetDirty(data);
                changedTerrains++;
            }
        }

        if (changedTerrains <= 0)
            return;

        AssetDatabase.SaveAssets();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.isLoaded)
            EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[TerrainGrassDetailSetup] Césped pintable agregado a {changedTerrains} Terrain(s). " +
                  $"Entradas rotas eliminadas: {removedBrokenEntries}. " +
                  "Disponible en Paint Terrain > Paint Details.");
    }

    static DetailPrototype CreateGrassPrototype(Texture2D texture)
    {
        return new DetailPrototype
        {
            prototypeTexture = texture,
            usePrototypeMesh = false,
            renderMode = DetailRenderMode.Grass,
            minWidth = 0.68f,
            maxWidth = 1.08f,
            minHeight = 0.72f,
            maxHeight = 1.24f,
            noiseSpread = 0.1f,
            positionJitter = 1f,
            useDensityScaling = true,
            healthyColor = new Color(0.68f, 0.86f, 0.58f, 1f),
            dryColor = new Color(0.48f, 0.62f, 0.34f, 1f)
        };
    }

    static bool NeedsGrassRepair(DetailPrototype detail, Texture2D texture)
    {
        return detail == null ||
               detail.prototypeTexture != texture ||
               detail.usePrototypeMesh ||
               detail.renderMode != DetailRenderMode.Grass ||
               Mathf.Abs(detail.minWidth - .68f) > .01f ||
               Mathf.Abs(detail.maxWidth - 1.08f) > .01f ||
               Mathf.Abs(detail.minHeight - .72f) > .01f ||
               Mathf.Abs(detail.maxHeight - 1.24f) > .01f ||
               Mathf.Abs(detail.noiseSpread - .1f) > .01f ||
               Mathf.Abs(detail.positionJitter - 1f) > .01f ||
               !detail.useDensityScaling;
    }

    static bool IsWhiteLegacyGrass(DetailPrototype detail)
    {
        if (detail == null || detail.prototypeTexture == null)
            return false;

        string path = AssetDatabase.GetAssetPath(detail.prototypeTexture);
        return path.Replace('\\', '/')
            .Equals("Assets/Proxy Games/Stylized Nature Kit Lite/Textures/Grass.png",
                System.StringComparison.OrdinalIgnoreCase);
    }

    static void ConfigureTextureImporter()
    {
        if (AssetImporter.GetAtPath(GrassTexturePath) is not TextureImporter importer)
            return;

        bool changed = false;

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (importer.textureType != TextureImporterType.Default)
        {
            importer.textureType = TextureImporterType.Default;
            changed = true;
        }

        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }
}

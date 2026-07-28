using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ApplyCastleTextures
{
    private const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    private const string CastleObjectName = "Generated_Medieval_Castle_LowPoly_FarFromSpawn";
    private const string TextureFolder = "Assets/Generated/MedievalCastleLowPoly/Textures";
    private const string MaterialFolder = "Assets/Generated/MedievalCastleLowPoly/Materials";

    private static readonly Dictionary<string, string> TextureByMaterial = new()
    {
        ["Stone"] = "Stone_Texture",
        ["StoneTrim"] = "StoneTrim_Texture",
        ["StoneInterior"] = "StoneInterior_Texture",
        ["StoneFloor"] = "StoneFloor_Texture",
        ["Cobble"] = "Cobble_Texture",
        ["Wood"] = "Wood_Texture",
        ["WoodDark"] = "WoodDark_Texture",
        ["Iron"] = "Iron_Texture",
        ["RoofSlate"] = "RoofSlate_Texture",
        ["Thatch"] = "Thatch_Texture",
        ["Water"] = "Water_Texture",
        ["Grass"] = "Grass_Texture",
        ["Dirt"] = "Dirt_Texture",
        ["Fire"] = "Fire_Texture",
        ["Rushes"] = "Thatch_Texture",
        ["ClothRed"] = "ClothRed_Texture",
        ["ClothTan"] = "ClothTan_Texture",
        ["Shadow"] = "StoneInterior_Texture",
    };

    private static readonly Dictionary<string, Color> ColorByMaterial = new()
    {
        ["Stone"] = new Color(0.55f, 0.55f, 0.52f),
        ["StoneTrim"] = new Color(0.72f, 0.70f, 0.63f),
        ["StoneInterior"] = new Color(0.45f, 0.45f, 0.43f),
        ["StoneFloor"] = new Color(0.47f, 0.45f, 0.40f),
        ["Cobble"] = new Color(0.42f, 0.42f, 0.38f),
        ["Wood"] = new Color(0.63f, 0.40f, 0.20f),
        ["WoodDark"] = new Color(0.38f, 0.20f, 0.10f),
        ["Iron"] = new Color(0.30f, 0.30f, 0.30f),
        ["RoofSlate"] = new Color(0.32f, 0.38f, 0.45f),
        ["Thatch"] = new Color(0.78f, 0.62f, 0.28f),
        ["Water"] = new Color(0.28f, 0.58f, 0.75f, 0.72f),
        ["Grass"] = new Color(0.32f, 0.58f, 0.25f),
        ["Dirt"] = new Color(0.45f, 0.31f, 0.18f),
        ["Fire"] = new Color(1.0f, 0.45f, 0.08f),
        ["Rushes"] = new Color(0.55f, 0.45f, 0.20f),
        ["ClothRed"] = new Color(0.65f, 0.08f, 0.07f),
        ["ClothTan"] = new Color(0.68f, 0.56f, 0.39f),
        ["Shadow"] = new Color(0.06f, 0.06f, 0.06f),
    };

    [MenuItem("Tools/Generated Assets/Apply Medieval Castle Textures")]
    public static void Apply()
    {
        if (!Directory.Exists(MaterialFolder))
        {
            Directory.CreateDirectory(MaterialFolder);
        }

        AssetDatabase.Refresh();
        var createdMaterials = CreateOrUpdateMaterials();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var castle = GameObject.Find(CastleObjectName);
        if (castle == null)
        {
            Debug.LogError($"Castle object not found in scene: {CastleObjectName}");
            return;
        }

        var changedSlots = 0;
        foreach (var renderer in castle.GetComponentsInChildren<Renderer>(true))
        {
            var shared = renderer.sharedMaterials;
            for (var i = 0; i < shared.Length; i++)
            {
                var key = NormalizeMaterialName(shared[i] != null ? shared[i].name : string.Empty);
                if (createdMaterials.TryGetValue(key, out var replacement))
                {
                    shared[i] = replacement;
                    changedSlots++;
                }
            }

            renderer.sharedMaterials = shared;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Applied medieval castle textures to {CastleObjectName}. Material slots updated: {changedSlots}.");
    }

    private static Dictionary<string, Material> CreateOrUpdateMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        var result = new Dictionary<string, Material>();
        foreach (var pair in TextureByMaterial)
        {
            var materialPath = $"{MaterialFolder}/{pair.Key}_Textured.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = shader;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{pair.Value}.png");
            if (texture != null)
            {
                material.mainTexture = texture;
            }

            if (ColorByMaterial.TryGetValue(pair.Key, out var color))
            {
                material.color = color;
            }

            if (pair.Key == "Water")
            {
                material.SetFloat("_Smoothness", 0.65f);
                material.SetFloat("_Metallic", 0f);
            }
            else if (pair.Key == "Iron")
            {
                material.SetFloat("_Smoothness", 0.38f);
                material.SetFloat("_Metallic", 0.75f);
            }
            else if (pair.Key == "Fire")
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.04f) * 1.6f);
            }
            else
            {
                material.SetFloat("_Smoothness", 0.18f);
                material.SetFloat("_Metallic", 0f);
            }

            EditorUtility.SetDirty(material);
            result[pair.Key] = material;
        }

        return result;
    }

    private static string NormalizeMaterialName(string rawName)
    {
        var name = rawName.Replace(" (Instance)", string.Empty);
        name = name.Replace("_Textured", string.Empty);
        var spaceIndex = name.IndexOf(' ');
        if (spaceIndex > 0)
        {
            name = name[..spaceIndex];
        }

        return name;
    }
}

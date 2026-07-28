using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UpgradeCastleRealism
{
    private const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    private const string CastleObjectName = "Generated_Medieval_Castle_LowPoly_FarFromSpawn";
    private const string UpgradeRootName = "Realism_Upgrade_v2";
    private const string MaterialFolder = "Assets/Generated/MedievalCastleLowPoly/RealisticMaterials";

    [MenuItem("Tools/Generated Assets/Upgrade Medieval Castle Realism")]
    public static void Apply()
    {
        if (!Directory.Exists(MaterialFolder))
        {
            Directory.CreateDirectory(MaterialFolder);
        }

        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var castle = GameObject.Find(CastleObjectName);
        if (castle == null)
        {
            Debug.LogError($"Castle object not found: {CastleObjectName}");
            return;
        }

        var materials = CreateRealisticMaterials();
        ApplyMaterialsToCastle(castle, materials);
        HideStaticDoorMeshes(castle);

        var old = castle.transform.Find(UpgradeRootName);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        var root = new GameObject(UpgradeRootName);
        root.transform.SetParent(castle.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        AddMoatAndEntryConnections(root.transform, materials);
        AddInteriorDetailPass(root.transform, materials);
        AddInteractiveDoors(root.transform, materials);
        AddLighting(root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("Upgraded medieval castle realism: realistic materials, connected moat entry, interior details, interactive hinged doors, and lights applied.");
    }

    private static Dictionary<string, Material> CreateRealisticMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var map = new Dictionary<string, Material>
        {
            ["Stone"] = NewMat("Real_WeatheredCastleStone", shader, "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_StonesRough_d.tga", "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_StonesRough_n.tga", "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_StonesRough_s.tga", new Color(0.62f, 0.60f, 0.55f), 0.28f),
            ["StoneTrim"] = NewMat("Real_CutLimestoneTrim", shader, "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_Blocs_d.tga", "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_Blocs_n.tga", "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_Blocs_s.tga", new Color(0.76f, 0.73f, 0.66f), 0.24f),
            ["StoneInterior"] = NewMat("Real_DampInteriorStone", shader, "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_Rough04_d.tga", "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_Rough04_n.tga", "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_Rough04_s.tga", new Color(0.48f, 0.47f, 0.43f), 0.20f),
            ["StoneFloor"] = NewMat("Real_WornStoneFloor", shader, "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_StonesFlat_d.tga", "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_StonesFlat_n.tga", "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_StonesFlat_s.tga", new Color(0.58f, 0.55f, 0.50f), 0.23f),
            ["Cobble"] = NewMat("Real_CourtyardCobble", shader, "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_StonesSmooth_d.tga", "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_StonesSmooth_n.tga", "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_StonesSmooth_s.tga", new Color(0.58f, 0.56f, 0.51f), 0.25f),
            ["RoofSlate"] = NewMat("Real_DarkSlateRoof", shader, "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_Slates_d.tga", "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_Slates_n.tga", "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_Slates_s.tga", new Color(0.36f, 0.40f, 0.45f), 0.31f),
            ["Dirt"] = NewMat("Real_Dirt", shader, "Assets/_RPG/Textures/RealDirt.png", null, null, Color.white, 0.18f),
            ["Grass"] = NewMat("Real_Grass", shader, "Assets/_RPG/Textures/RealGrass.png", null, null, Color.white, 0.18f),
            ["Water"] = LoadExisting("Real_MoatWater", "Assets/Proxy Games/Stylized Nature Kit Lite/Materials/Water.mat"),
            ["Wood"] = LoadExisting("Real_MedievalWood", "Assets/Advance Studios/Medieval Castle/Art/Model/Materials/BridgeTexture.mat"),
            ["WoodDark"] = LoadExisting("Real_DoorWoodIron", "Assets/Advance Studios/Medieval Castle/Art/Model/Materials/DoorsWindowsTexture.mat"),
            ["Iron"] = NewMat("Real_DarkIron", shader, "Assets/YughuesFreeNatureMaterials/Textures/T_YFNM_Coal_e.tga", "Assets/YughuesFreeNatureMaterials/Textures/T_YFNM_Coal_n.tga", "Assets/YughuesFreeNatureMaterials/Textures/T_YFNM_Coal_s.tga", new Color(0.28f, 0.27f, 0.25f), 0.42f),
            ["Thatch"] = LoadExisting("Real_ThatchRoof", "Assets/Nicrom/3D_PolyArt/Medieval_Village_Demo/Assets/Materials/Roof/MVD_Material_Roof_A_1a.mat"),
            ["Fire"] = LoadExisting("Real_Fire", "Assets/Nicrom/3D_PolyArt/Medieval_Village_Demo/Assets/Materials/MVD_Material_CandleFire.mat"),
            ["ClothRed"] = NewSolid("Real_DarkRedCloth", shader, new Color(0.40f, 0.03f, 0.025f), 0.35f),
            ["ClothTan"] = NewSolid("Real_CanvasCloth", shader, new Color(0.62f, 0.52f, 0.37f), 0.35f),
            ["Shadow"] = NewSolid("Real_DarkPassage", shader, new Color(0.025f, 0.024f, 0.022f), 0.1f)
        };

        map["Rushes"] = map["Thatch"];
        return map;
    }

    private static Material NewMat(string name, Shader shader, string albedoPath, string normalPath, string specPath, Color color, float smoothness)
    {
        var path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        mat.color = color;
        SetTexture(mat, "_BaseMap", albedoPath);
        SetTexture(mat, "_MainTex", albedoPath);
        SetTexture(mat, "_BumpMap", normalPath);
        SetTexture(mat, "_SpecGlossMap", specPath);
        if (normalPath != null)
        {
            mat.EnableKeyword("_NORMALMAP");
        }

        if (specPath != null)
        {
            mat.EnableKeyword("_SPECGLOSSMAP");
        }

        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material NewSolid(string name, Shader shader, Color color, float smoothness)
    {
        var path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        mat.color = color;
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material LoadExisting(string name, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        return NewSolid(name, Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"), Color.white, 0.2f);
    }

    private static void SetTexture(Material mat, string property, string path)
    {
        if (path == null || !mat.HasProperty(property))
        {
            return;
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture != null)
        {
            mat.SetTexture(property, texture);
            mat.SetTextureScale(property, new Vector2(2.5f, 2.5f));
        }
    }

    private static void ApplyMaterialsToCastle(GameObject castle, Dictionary<string, Material> materials)
    {
        foreach (var renderer in castle.GetComponentsInChildren<Renderer>(true))
        {
            var shared = renderer.sharedMaterials;
            for (var i = 0; i < shared.Length; i++)
            {
                var key = NormalizeMaterialName(shared[i] != null ? shared[i].name : string.Empty);
                if (materials.TryGetValue(key, out var replacement))
                {
                    shared[i] = replacement;
                }
            }

            renderer.sharedMaterials = shared;
        }
    }

    private static string NormalizeMaterialName(string rawName)
    {
        var name = rawName.Replace(" (Instance)", string.Empty).Replace("_Textured", string.Empty);
        if (name.StartsWith("Real_"))
        {
            return string.Empty;
        }

        var spaceIndex = name.IndexOf(' ');
        return spaceIndex > 0 ? name[..spaceIndex] : name;
    }

    private static void HideStaticDoorMeshes(GameObject castle)
    {
        var keywords = new[]
        {
            "Puerta_Madera", "Gatehouse_Puerta", "Rastrillo_Hierro", "Puerta_Torre_Homenaje",
            "Puerta_Armeria", "Puerta_Herreria", "Puerta_Establos", "Puerta_Cuartel", "Puerta_Sirvientes",
            "Rejas"
        };

        foreach (var t in castle.GetComponentsInChildren<Transform>(true))
        {
            foreach (var keyword in keywords)
            {
                if (t.name.Contains(keyword))
                {
                    t.gameObject.SetActive(false);
                    break;
                }
            }
        }
    }

    private static void AddMoatAndEntryConnections(Transform root, Dictionary<string, Material> materials)
    {
        AddBox(root, "Stone_Abutment_South_Drawbridge", new Vector3(0, 0.55f, -68.5f), new Vector3(18, 1.1f, 4), materials["StoneTrim"]);
        AddBox(root, "Packed_Dirt_Ramp_To_Drawbridge", new Vector3(0, 0.22f, -77), new Vector3(20, 0.45f, 20), materials["Dirt"]);
        AddBox(root, "Stone_Threshold_After_Drawbridge", new Vector3(0, 0.62f, -47.5f), new Vector3(15, 0.75f, 4), materials["StoneFloor"]);
        AddStairs(root, "Foso_Escalera_Oeste", new Vector3(-13.5f, 0.15f, -61f), 3.2f, 14f, 3.2f, 8, materials["StoneTrim"], 0f);
        AddStairs(root, "Foso_Escalera_Este", new Vector3(13.5f, 0.15f, -61f), 3.2f, 14f, 3.2f, 8, materials["StoneTrim"], 0f);

        for (int i = 0; i < 7; i++)
        {
            AddBox(root, $"Drawbridge_Chain_Link_Left_{i}", new Vector3(-6.8f, 2.4f + i * 0.55f, -52.5f - i * 0.8f), new Vector3(0.18f, 0.45f, 0.18f), materials["Iron"]);
            AddBox(root, $"Drawbridge_Chain_Link_Right_{i}", new Vector3(6.8f, 2.4f + i * 0.55f, -52.5f - i * 0.8f), new Vector3(0.18f, 0.45f, 0.18f), materials["Iron"]);
        }
    }

    private static void AddInteriorDetailPass(Transform root, Dictionary<string, Material> materials)
    {
        AddBox(root, "GranSalon_Rug_WornRed", new Vector3(-15f, 0.52f, 16f), new Vector3(11f, 0.08f, 12f), materials["ClothRed"]);
        AddBox(root, "SalaTrono_Runner_Red", new Vector3(16f, 9.18f, 14f), new Vector3(4f, 0.08f, 16f), materials["ClothRed"]);
        AddBox(root, "Biblioteca_Lectern", new Vector3(-14f, 9.85f, 18f), new Vector3(1.3f, 1.2f, 1.0f), materials["WoodDark"]);
        AddBox(root, "Biblioteca_OpenBook", new Vector3(-14f, 10.55f, 18f), new Vector3(1.4f, 0.08f, 0.9f), materials["ClothTan"]);
        AddBox(root, "Cocina_HangingPot", new Vector3(-16f, 2.6f, -1.7f), new Vector3(1.2f, 1.0f, 1.2f), materials["Iron"]);
        AddBox(root, "Capilla_StoneFont", new Vector3(20f, 1.05f, 0.6f), new Vector3(1.4f, 1.4f, 1.4f), materials["StoneTrim"]);
        AddBox(root, "Mazmorra_TortureTable", new Vector3(0f, -5.05f, 3f), new Vector3(5.5f, 0.65f, 1.7f), materials["WoodDark"]);
        AddBox(root, "Armeria_ShieldRack", new Vector3(-44f, 2.8f, 16f), new Vector3(0.4f, 3.0f, 8f), materials["WoodDark"]);
        AddBox(root, "Herreria_CoolingTrough", new Vector3(32f, 0.8f, 15f), new Vector3(4.5f, 0.9f, 1.4f), materials["Water"]);

        for (int i = 0; i < 10; i++)
        {
            AddBox(root, $"Interior_Wall_Torch_Sconce_{i}", new Vector3(-20.8f + (i % 2) * 41.6f, 3.4f, -2f + (i / 2) * 6f), new Vector3(0.25f, 0.8f, 0.25f), materials["Iron"]);
            AddBox(root, $"Interior_Torch_Flame_{i}", new Vector3(-20.8f + (i % 2) * 41.6f, 4.0f, -2f + (i / 2) * 6f), new Vector3(0.45f, 0.75f, 0.45f), materials["Fire"]);
        }
    }

    private static void AddInteractiveDoors(Transform root, Dictionary<string, Material> materials)
    {
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer < 0)
        {
            interactableLayer = 10;
        }

        AddDoor(root, "Puerta_Interactiva_Gatehouse_Izquierda", new Vector3(-2.4f, 4.15f, -48.05f), new Vector3(4.3f, 7.4f, 0.45f), materials["WoodDark"], interactableLayer, new Vector3(-2.15f, 0f, 0f), 105f, "[E] Abrir porton", "[E] Cerrar porton");
        AddDoor(root, "Puerta_Interactiva_Gatehouse_Derecha", new Vector3(2.4f, 4.15f, -48.05f), new Vector3(4.3f, 7.4f, 0.45f), materials["WoodDark"], interactableLayer, new Vector3(2.15f, 0f, 0f), -105f, "[E] Abrir porton", "[E] Cerrar porton");
        AddDoor(root, "Puerta_Interactiva_Torre_Homenaje", new Vector3(0f, 3.4f, -8.95f), new Vector3(6.2f, 6.4f, 0.45f), materials["WoodDark"], interactableLayer, new Vector3(-3.1f, 0f, 0f), 95f, "[E] Abrir puerta del homenaje", "[E] Cerrar puerta del homenaje");
        AddDoor(root, "Puerta_Interactiva_Armeria", new Vector3(-37f, 2.7f, 10.35f), new Vector3(4.5f, 5.0f, 0.35f), materials["WoodDark"], interactableLayer, new Vector3(-2.25f, 0f, 0f), 95f, "[E] Abrir armeria", "[E] Cerrar armeria");
        AddDoor(root, "Puerta_Interactiva_Herreria", new Vector3(38f, 2.7f, 13.25f), new Vector3(4.5f, 5.0f, 0.35f), materials["WoodDark"], interactableLayer, new Vector3(-2.25f, 0f, 0f), 95f, "[E] Abrir herreria", "[E] Cerrar herreria");
        AddDoor(root, "Puerta_Interactiva_Establos", new Vector3(37f, 2.6f, -25.2f), new Vector3(6.4f, 4.8f, 0.35f), materials["WoodDark"], interactableLayer, new Vector3(-3.2f, 0f, 0f), 95f, "[E] Abrir establos", "[E] Cerrar establos");
        AddDoor(root, "Puerta_Interactiva_Cuartel", new Vector3(-37f, 2.7f, -17.25f), new Vector3(5.4f, 5.0f, 0.35f), materials["WoodDark"], interactableLayer, new Vector3(-2.7f, 0f, 0f), 95f, "[E] Abrir cuartel", "[E] Cerrar cuartel");
        AddDoor(root, "Puerta_Interactiva_Sirvientes", new Vector3(0f, 2.55f, -24.25f), new Vector3(4.4f, 4.7f, 0.35f), materials["WoodDark"], interactableLayer, new Vector3(-2.2f, 0f, 0f), 95f, "[E] Abrir alojamiento", "[E] Cerrar alojamiento");

        AddDoor(root, "Pasadizo_Secreto_Puerta_Biblioteca", new Vector3(-22.15f, 11.5f, 10f), new Vector3(0.35f, 4.8f, 5.2f), materials["WoodDark"], interactableLayer, new Vector3(0f, 0f, -2.6f), -90f, "[E] Abrir pasadizo secreto", "[E] Cerrar pasadizo secreto");

        AddDoor(root, "Puente_Levadizo_Interactivo", new Vector3(0f, 0.92f, -58f), new Vector3(13.4f, 0.42f, 19.5f), materials["Wood"], interactableLayer, new Vector3(0f, 0f, 9.75f), -68f, "[E] Subir puente levadizo", "[E] Bajar puente levadizo", CastleHingedInteractable.HingeAxis.LocalX);

        for (int i = 0; i < 6; i++)
        {
            AddDoor(root, $"Mazmorra_Reja_Interactiva_{i}", new Vector3(-15f + i * 6f, -3.2f, 13.05f), new Vector3(4.1f, 4.5f, 0.25f), materials["Iron"], interactableLayer, new Vector3(-2.05f, 0f, 0f), 90f, "[E] Abrir celda", "[E] Cerrar celda");
        }
    }

    private static void AddLighting(Transform root)
    {
        var points = new[]
        {
            new Vector3(0f, 5.5f, -39f), new Vector3(-16f, 4.5f, 16f), new Vector3(16f, 4.5f, 15f),
            new Vector3(16f, 13f, 21f), new Vector3(-16f, 13f, 15f), new Vector3(-16f, 3.5f, -2f),
            new Vector3(38f, 3.8f, 19f), new Vector3(-37f, 3.8f, 17f), new Vector3(0f, -3.2f, 10f)
        };

        for (int i = 0; i < points.Length; i++)
        {
            var go = new GameObject($"Castle_Torch_Light_{i}");
            go.transform.SetParent(root, false);
            go.transform.localPosition = points[i];
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.58f, 0.25f);
            light.intensity = i == 8 ? 2.2f : 1.4f;
            light.range = i == 8 ? 9f : 7f;
            light.shadows = LightShadows.Soft;
        }
    }

    private static void AddDoor(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, int layer, Vector3 hingeOffset, float angle, string closedText, string openText, CastleHingedInteractable.HingeAxis axis = CastleHingedInteractable.HingeAxis.LocalY)
    {
        var door = AddBox(parent, name, pos, scale, mat);
        door.layer = layer;
        var collider = door.GetComponent<BoxCollider>();
        collider.isTrigger = false;

        var hinge = door.AddComponent<CastleHingedInteractable>();
        SetPrivate(hinge, "localHingeOffset", hingeOffset);
        SetPrivate(hinge, "hingeAxis", axis);
        SetPrivate(hinge, "openAngle", angle);
        SetPrivate(hinge, "closedText", closedText);
        SetPrivate(hinge, "openText", openText);

        AddBox(parent, $"{name}_IronBands_Top", pos + new Vector3(0, scale.y * 0.25f, 0.03f), new Vector3(scale.x * 0.92f, 0.18f, 0.12f), mat);
        AddBox(parent, $"{name}_IronBands_Bottom", pos + new Vector3(0, -scale.y * 0.25f, 0.03f), new Vector3(scale.x * 0.92f, 0.18f, 0.12f), mat);
    }

    private static GameObject AddBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return go;
    }

    private static void AddStairs(Transform parent, string prefix, Vector3 start, float width, float depth, float height, int steps, Material material, float yRotation)
    {
        for (int i = 0; i < steps; i++)
        {
            float t = (i + 0.5f) / steps;
            var step = AddBox(parent, $"{prefix}_Step_{i:00}", start + new Vector3(0f, height * t, depth * t), new Vector3(width, height / steps, depth / steps), material);
            step.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
        }
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }
}

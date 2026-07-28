using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SixthMissionWorldSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string ModelPath =
        "Assets/_RPG/Scripts/Combat/red_diamond/Meshy_AI_Quiero_que_hagas_un_d_0724001651_texture.fbx";
    const string AlbedoPath =
        "Assets/_RPG/Scripts/Combat/red_diamond/Meshy_AI_Quiero_que_hagas_un_d_0724001651_texture.png";
    const string NormalPath =
        "Assets/_RPG/Scripts/Combat/red_diamond/Meshy_AI_Quiero_que_hagas_un_d_0724001651_texture_normal.png";
    const string MetallicPath =
        "Assets/_RPG/Scripts/Combat/red_diamond/Meshy_AI_Quiero_que_hagas_un_d_0724001651_texture_metallic.png";
    const string EmissionPath =
        "Assets/_RPG/Scripts/Combat/red_diamond/Meshy_AI_Quiero_que_hagas_un_d_0724001651_texture_emission.png";
    const string AuraPath = "Assets/_RPG/Scripts/Combat/aura_oscura.jpg";
    const string MaterialPath = "Assets/_RPG/Materials/RedDiamond_URP.mat";
    const string PrefabPath = "Assets/_RPG/Prefabs/Quests/RedDiamond.prefab";
    const string ItemPath = "Assets/_RPG/Resources/Items/RedDiamond.asset";
    const string DiamondReferencePrefabPath =
        "Assets/_RPG/Prefabs/Items/DiamondEnhancer.prefab";
    const string StoneMaterialPath = "Assets/_RPG/Materials/AnomalyDemonStatueStone.mat";
    const string RequestPath = "Assets/_RPG/Generated/SixthMissionWorldSetup.generate";
    const string CrabBossPrefabPath = "Assets/_RPG/Prefabs/Enemies/CrabDemonBoss.prefab";
    const string RuntimeCrabBossPrefabPath =
        "Assets/_RPG/Resources/Enemies/CrabDemonBoss.prefab";

    [InitializeOnLoadMethod]
    static void QueueRequestedSetup()
    {
        EditorApplication.delayCall += TryRunRequestedSetup;
    }

    static void TryRunRequestedSetup()
    {
        string request = Path.GetFullPath(RequestPath);
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        File.Delete(request);
        if (File.Exists(request + ".meta")) File.Delete(request + ".meta");
        Setup();
    }

    [MenuItem("RPG/Quests/Setup Sixth Mission World")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[SixthMissionWorldSetup] Sali de Play Mode para ejecutar.");
            return;
        }
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        EnsureFolder("Assets/_RPG/Prefabs", "Quests");
        EnsureFolder("Assets/_RPG/Resources", "Enemies");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CrabBossPrefabPath) != null)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeCrabBossPrefabPath) != null)
                AssetDatabase.DeleteAsset(RuntimeCrabBossPrefabPath);
            AssetDatabase.CopyAsset(CrabBossPrefabPath, RuntimeCrabBossPrefabPath);
        }
        ConfigureTexture(NormalPath, TextureImporterType.NormalMap);
        ConfigureAuraSprite();
        Material redMaterial = EnsureRedDiamondMaterial();
        ItemData item = EnsureItemAsset();
        GameObject prefab = EnsurePrefab(redMaterial, item);
        item.equipmentPrefab = prefab;
        EditorUtility.SetDirty(item);
        AssignItemToPrefab(item);

        GameObject worldDiamond = EnsureSceneDiamond(prefab);
        GameObject controllerObject = GameObject.Find("SixthMissionSequence");
        if (controllerObject == null)
            controllerObject = new GameObject("SixthMissionSequence");
        SixthMissionSequence sequence =
            controllerObject.GetComponent<SixthMissionSequence>();
        if (sequence == null)
            sequence = controllerObject.AddComponent<SixthMissionSequence>();

        SerializedObject serialized = new SerializedObject(sequence);
        serialized.FindProperty("redDiamondTarget").objectReferenceValue =
            worldDiamond != null ? worldDiamond.transform : null;
        serialized.FindProperty("darkAuraIndicatorSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(AuraPath);
        serialized.FindProperty("stoneMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>(StoneMaterialPath);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SixthMissionWorldSetup] Sexta mision, Diamante Rojo y aura configurados.");
    }

    static Material EnsureRedDiamondMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = "RedDiamond_URP" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
        Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(1f, .46f, .46f, 1f));
        if (material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        if (material.HasProperty("_MetallicGlossMap"))
        {
            material.SetTexture("_MetallicGlossMap", metallic);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        if (material.HasProperty("_EmissionMap"))
        {
            material.SetTexture("_EmissionMap", emission);
            material.SetColor("_EmissionColor", new Color(3.4f, .025f, .035f, 1f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static ItemData EnsureItemAsset()
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(ItemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, ItemPath);
        }
        item.itemID = "red_diamond_quest";
        item.itemName = "Diamante Rojo";
        item.itemType = ItemType.Diamond;
        item.description =
            "El coraz\u00f3n carmes\u00ed cuyo latido atrae y libera la oscuridad.";
        item.stackable = true;
        item.maxStack = 1;
        ItemData normalDiamond =
            AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_RPG/Resources/Items/Diamond.asset");
        item.icon = normalDiamond != null ? normalDiamond.icon : null;
        EditorUtility.SetDirty(item);
        return item;
    }

    static GameObject EnsurePrefab(Material material, ItemData item)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (source == null)
        {
            Debug.LogError("[SixthMissionWorldSetup] No se encontro el modelo red_diamond.");
            return null;
        }

        GameObject root = new GameObject("RedDiamond");
        GameObject visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (visual == null) visual = Object.Instantiate(source);
        visual.name = "RedDiamond_Mesh";
        visual.transform.SetParent(root.transform, false);

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = material;
        NormalizeVisual(visual.transform, GetDiamondReferenceHeight());

        Bounds bounds = GetBounds(root.transform);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = root.transform.InverseTransformPoint(bounds.center);
        collider.size = bounds.size + Vector3.one * .12f;
        collider.isTrigger = false;

        GameObject lightObject = new GameObject("RedGlow");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = Vector3.up * .55f;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, .015f, .025f);
        light.intensity = 5.2f;
        light.range = 9f;
        light.shadows = LightShadows.Soft;

        RedDiamondPickup pickup = root.AddComponent<RedDiamondPickup>();
        SerializedObject serialized = new SerializedObject(pickup);
        serialized.FindProperty("redDiamondItem").objectReferenceValue = item;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SetLayerRecursive(root, LayerMask.NameToLayer("Interactable"));

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void AssignItemToPrefab(ItemData item)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        RedDiamondPickup pickup = contents.GetComponent<RedDiamondPickup>();
        if (pickup != null)
        {
            SerializedObject serialized = new SerializedObject(pickup);
            serialized.FindProperty("redDiamondItem").objectReferenceValue = item;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
    }

    static GameObject EnsureSceneDiamond(GameObject prefab)
    {
        if (prefab == null) return null;
        GameObject existing = GameObject.Find("red_diamond") ??
                              GameObject.Find("Red_Diamond_Quest");
        if (existing == null || existing.GetComponent<RedDiamondPickup>() == null)
        {
            if (existing != null) Object.DestroyImmediate(existing);
            existing = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            existing.name = "Red_Diamond_Quest";
            existing.transform.position = FindDiamondPosition();
            existing.transform.rotation = Quaternion.identity;
        }
        return existing;
    }

    static Vector3 FindDiamondPosition()
    {
        KingGoblinPlagueStatue statue =
            Object.FindAnyObjectByType<KingGoblinPlagueStatue>();
        GameObject spawn = GameObject.Find("SpawnPoint") ??
                           GameObject.Find("PlayerSpawnPoint");
        Vector3 statuePosition = statue != null
            ? statue.transform.position
            : new Vector3(-35f, 0f, -35f);
        Vector3 spawnPosition = spawn != null ? spawn.transform.position : Vector3.zero;
        Vector3 outward = Vector3.ProjectOnPlane(
            statuePosition - spawnPosition, Vector3.up).normalized;
        if (outward.sqrMagnitude < .01f) outward = Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, outward);
        Vector3 position = statuePosition + outward * 34f + side * 7f;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            position.y = terrain.SampleHeight(position) +
                         terrain.transform.position.y + .58f;
        return position;
    }

    static void NormalizeVisual(Transform visual, float targetHeight)
    {
        Bounds before = GetBounds(visual);
        float scale = before.size.y > .001f ? targetHeight / before.size.y : 1f;
        visual.localScale *= scale;
        Bounds after = GetBounds(visual);
        visual.position += visual.parent.position -
                           new Vector3(after.center.x, after.min.y, after.center.z);
    }

    static float GetDiamondReferenceHeight()
    {
        GameObject reference =
            AssetDatabase.LoadAssetAtPath<GameObject>(DiamondReferencePrefabPath);
        if (reference == null) return .75f;
        GameObject instance = PrefabUtility.InstantiatePrefab(reference) as GameObject;
        if (instance == null) instance = Object.Instantiate(reference);
        instance.hideFlags = HideFlags.HideAndDontSave;
        float height = GetBounds(instance.transform).size.y;
        Object.DestroyImmediate(instance);
        return height > .05f ? height : .75f;
    }

    static Bounds GetBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers.Length > 0
            ? renderers[0].bounds
            : new Bounds(root.position, Vector3.one);
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void ConfigureAuraSprite()
    {
        TextureImporter importer = AssetImporter.GetAtPath(AuraPath) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    static void ConfigureTexture(string path, TextureImporterType type)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || importer.textureType == type) return;
        importer.textureType = type;
        importer.SaveAndReimport();
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    static void SetLayerRecursive(GameObject root, int layer)
    {
        if (layer < 0) return;
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}

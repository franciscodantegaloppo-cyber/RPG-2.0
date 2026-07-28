using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class KingGoblinSwordCraftingSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string SwordModelPath = "Assets/ExplosiveLLC/Espada del rey goblin/Meshy_AI_Crea_una_espada_de_ki_0711191752_texture.fbx";
    const string SwordTexturePath = "Assets/ExplosiveLLC/Espada del rey goblin/Meshy_AI_Crea_una_espada_de_ki_0711191752_texture.png";
    const string SwordEmissionPath = "Assets/ExplosiveLLC/Espada del rey goblin/Meshy_AI_Crea_una_espada_de_ki_0711191752_texture_emission.png";
    const string SwordNormalPath = "Assets/ExplosiveLLC/Espada del rey goblin/Meshy_AI_Crea_una_espada_de_ki_0711191752_texture_normal.png";

    const string DiamondModelPath = "Assets/(P&W)Temple_Edition/Diamond/Meshy_AI_Crea_un_diamante_low__0711184725_texture.fbx";
    const string DiamondTexturePath = "Assets/(P&W)Temple_Edition/Diamond/Meshy_AI_Crea_un_diamante_low__0711184725_texture.png";
    const string DiamondEmissionPath = "Assets/(P&W)Temple_Edition/Diamond/Meshy_AI_Crea_un_diamante_low__0711184725_texture_emission.png";
    const string DiamondNormalPath = "Assets/(P&W)Temple_Edition/Diamond/Meshy_AI_Crea_un_diamante_low__0711184725_texture_normal.png";

    const string RuneModelPath = "Assets/Daniel Mistage/Runa FG/Meshy_AI_haz_una_runa_que_en_e_0711192142_texture.fbx";
    const string RuneTexturePath = "Assets/Daniel Mistage/Runa FG/Meshy_AI_haz_una_runa_que_en_e_0711192142_texture.png";
    const string RuneEmissionPath = "Assets/Daniel Mistage/Runa FG/Meshy_AI_haz_una_runa_que_en_e_0711192142_texture_emission.png";
    const string RuneNormalPath = "Assets/Daniel Mistage/Runa FG/Meshy_AI_haz_una_runa_que_en_e_0711192142_texture_normal.png";

    const string CraftingTablePath = "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Extra Content/Fantasy Workshops and Crafting Vol2/SFWC2_Crafting_Table.prefab";
    const string CraftingGridPath = "Assets/Bitszer/Crafting table grid.png";

    const string PrefabFolder = "Assets/_RPG/Prefabs/Items";
    const string ItemFolder = "Assets/_RPG/ScriptableObjects/Items";
    const string WeaponFolder = "Assets/_RPG/ScriptableObjects/Weapons";
    const string MaterialFolder = "Assets/_RPG/Materials";

    const string SwordPrefabPath = PrefabFolder + "/KingGoblinSword.prefab";
    const string DiamondPrefabPath = PrefabFolder + "/DiamondEnhancer.prefab";
    const string RunePrefabPath = PrefabFolder + "/RunaFG.prefab";
    const string SwordItemPath = ItemFolder + "/KingGoblinSword.asset";
    const string DiamondItemPath = ItemFolder + "/DiamondEnhancer.asset";
    // Points at the same canonical rune ItemData that EnemyStats.DropRune() loads via
    // Resources.Load("Items/Rune") - keeping a second "RunaFG" ItemData asset around made the
    // dropped rune and the crafted rune show up as two different (if identically-named) items.
    const string RuneItemPath = "Assets/_RPG/Resources/Items/Rune.asset";
    const string SwordWeaponPath = WeaponFolder + "/KingGoblinSwordWeapon.asset";

    const string SwordWorldName = "KingGoblinSwordPickup_NearSpawn";
    const string CraftingTableName = "CraftingTable_NearSpawn";
    const string DiamondPickupName = "DiamondEnhancerPickup_NearSpawn";
    const string RunePickupName = "RunaFGPickup_NearSpawn";

    [MenuItem("RPG/Items/Setup King Goblin Sword And Crafting")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[KingGoblinSwordCraftingSetup] Sali de Play Mode para armar los objetos.");
            return;
        }

        EnsureFolders();
        CopyCraftingGridToResources();

        GameObject swordPrefab = BuildVisualPrefab(SwordModelPath, SwordPrefabPath, "KingGoblinSwordVisual",
            CreateMaterial("KingGoblinSword_AngelBlue.mat", SwordTexturePath, SwordNormalPath, SwordEmissionPath, new Color(0.05f, 0.75f, 1f), 4.5f),
            new Vector3(0f, 0f, 0f), Vector3.one, 1.85f);

        GameObject diamondPrefab = BuildVisualPrefab(DiamondModelPath, DiamondPrefabPath, "DiamondEnhancerVisual",
            CreateMaterial("DiamondEnhancer_URP.mat", DiamondTexturePath, DiamondNormalPath, DiamondEmissionPath, new Color(0.1f, 0.85f, 1f), 2.4f),
            Vector3.zero, Vector3.one, 0.75f);

        GameObject runePrefab = BuildVisualPrefab(RuneModelPath, RunePrefabPath, "RunaFGVisual",
            CreateMaterial("RunaFG_URP.mat", RuneTexturePath, RuneNormalPath, RuneEmissionPath, new Color(0.75f, 0.2f, 1f), 1.8f),
            Vector3.zero, Vector3.one, 0.9f);

        WeaponData weapon = LoadOrCreate<WeaponData>(SwordWeaponPath);
        weapon.weaponID = "king_goblin_sword";
        weapon.weaponName = "Espada del Rey Goblin";
        weapon.weaponType = WeaponType.Sword2H;
        weapon.weaponPrefab = swordPrefab;
        weapon.baseDamage = 0f;
        weapon.attackRange = 2.2f;
        weapon.attackCooldown = 0.42f;
        weapon.useCustomEquippedVisual = false;
        // This mesh runs diagonally through its own local axes, so the bounding-box grip anchor
        // (used by every other weapon) lands mid-blade instead of at the handle.
        weapon.useVertexGripAnchor = true;
        // Its farthest-apart vertices also land on the handle/tip in the opposite order from
        // every other sword so far - without this the grip anchors at the blade tip instead.
        weapon.invertGripAxis = true;
        weapon.customHandPositionOffset = new Vector3(0.105f, 0.045f, 0.075f);
        weapon.customHandRotationOffset = new Vector3(289.52f, 32.80f, 107.87f);
        weapon.customGripAnchorPercent = 0.16f;
        weapon.customMaxEquippedWeaponSize = 1.45f;
        weapon.hasIntrinsicGlow = true;
        weapon.intrinsicGlowColor = new Color(0.05f, 0.78f, 1f);
        weapon.intrinsicGlowStrength = 4.2f;
        EditorUtility.SetDirty(weapon);

        ItemData swordItem = LoadOrCreate<ItemData>(SwordItemPath);
        swordItem.itemID = "king_goblin_sword";
        swordItem.itemName = "Espada del Rey Goblin";
        swordItem.itemType = ItemType.Weapon;
        swordItem.description = "Espada angelical de brillo celeste fuerte. Ataque +2000.";
        swordItem.attackBonus = 2000f;
        swordItem.stackable = false;
        swordItem.maxStack = 1;
        swordItem.weaponData = weapon;
        swordItem.equipmentPrefab = swordPrefab;
        EditorUtility.SetDirty(swordItem);

        ItemData diamondItem = LoadOrCreate<ItemData>(DiamondItemPath);
        diamondItem.itemID = "diamond_excellence";
        diamondItem.itemName = "Diamond";
        diamondItem.itemType = ItemType.Diamond;
        diamondItem.description = "Mejora excelencia: +50% a un atributo aleatorio, con riesgo de destruir el item.";
        diamondItem.stackable = true;
        diamondItem.maxStack = 100;
        diamondItem.equipmentPrefab = diamondPrefab;
        EditorUtility.SetDirty(diamondItem);

        ItemData runeItem = LoadOrCreate<ItemData>(RuneItemPath);
        runeItem.itemID = "rune";
        runeItem.itemName = "Runa FG";
        runeItem.itemType = ItemType.Rune;
        runeItem.description = "Click derecho: +1% a una propiedad aleatoria. En mesa de crafting: +2%.";
        runeItem.stackable = true;
        runeItem.maxStack = 99;
        runeItem.equipmentPrefab = runePrefab;
        EditorUtility.SetDirty(runeItem);

        PlaceSceneObjects(swordItem, diamondItem, runeItem);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[KingGoblinSwordCraftingSetup] Espada, diamond, Runa FG y crafting table creados/colocados cerca del spawn.");
    }

    static void PlaceSceneObjects(ItemData swordItem, ItemData diamondItem, ItemData runeItem)
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        DestroyIfExists(SwordWorldName);
        DestroyIfExists(CraftingTableName);
        DestroyIfExists(DiamondPickupName);
        DestroyIfExists(RunePickupName);

        Vector3 spawn = FindSpawnCenter();
        PlacePickup(SwordPrefabPath, SwordWorldName, swordItem, spawn + new Vector3(4f, 0f, 2f), Quaternion.Euler(0f, 25f, 78f), 1.2f, true);
        PlacePickup(DiamondPrefabPath, DiamondPickupName, diamondItem, spawn + new Vector3(2.8f, 0f, 4.2f), Quaternion.Euler(0f, 15f, 0f), 0.9f, false);
        PlacePickup(RunePrefabPath, RunePickupName, runeItem, spawn + new Vector3(1.6f, 0f, 4.2f), Quaternion.Euler(0f, -25f, 0f), 0.9f, false);

        GameObject tablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CraftingTablePath);
        if (tablePrefab != null)
        {
            GameObject table = (GameObject)PrefabUtility.InstantiatePrefab(tablePrefab);
            table.name = CraftingTableName;
            table.transform.position = spawn + new Vector3(5.8f, 0f, 4.5f);
            table.transform.rotation = Quaternion.Euler(0f, -35f, 0f);
            table.transform.localScale = Vector3.one * 1.15f;
            SnapToGround(table.transform);
            EnsureCollider(table);
            table.AddComponent<CraftingTableInteractable>();
            SetInteractableLayer(table.transform);
            EditorUtility.SetDirty(table);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    static void PlacePickup(string prefabPath, string objectName, ItemData item, Vector3 position, Quaternion rotation, float scale, bool equipOnPickup)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return;

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = objectName;
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.transform.localScale = Vector3.one * scale;
        SnapToGround(go.transform);
        LiftRenderableAboveOrigin(go.transform, 0.06f);
        EnsureCollider(go);
        AddPickupComponent(go, item, equipOnPickup);
        SetInteractableLayer(go.transform);

        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = equipOnPickup ? new Color(0.1f, 0.75f, 1f) : item.itemType == ItemType.Rune ? new Color(0.75f, 0.2f, 1f) : new Color(0.1f, 0.85f, 1f);
        light.range = equipOnPickup ? 5.5f : 2.8f;
        light.intensity = equipOnPickup ? 2.5f : 1.15f;

        EditorUtility.SetDirty(go);
    }

    static void AddPickupComponent(GameObject go, ItemData item, bool equipOnPickup)
    {
        WorldItemPickup pickup = go.AddComponent<WorldItemPickup>();
        SerializedObject so = new SerializedObject(pickup);
        so.FindProperty("item").objectReferenceValue = item;
        so.FindProperty("equipOnPickup").boolValue = equipOnPickup;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject BuildVisualPrefab(string modelPath, string prefabPath, string name, Material material, Vector3 localEuler, Vector3 localScale, float targetMaxSize)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            Debug.LogWarning("[KingGoblinSwordCraftingSetup] No se encontro modelo: " + modelPath);
            return null;
        }

        GameObject root = new GameObject(name);
        GameObject child = (GameObject)PrefabUtility.InstantiatePrefab(model);
        child.transform.SetParent(root.transform, false);
        child.transform.localRotation = Quaternion.Euler(localEuler);
        child.transform.localScale = localScale;
        ApplyMaterial(child, material);
        EnableRenderers(child);
        StripColliders(child);
        NormalizeVisual(child.transform, targetMaxSize);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static Material CreateMaterial(string fileName, string albedoPath, string normalPath, string emissionPath, Color emissionColor, float emissionStrength)
    {
        string path = MaterialFolder + "/" + fileName;
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (shader != null)
        {
            mat.shader = shader;
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(emissionPath);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
        if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
        if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", emission);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor * emissionStrength);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
        mat.EnableKeyword("_NORMALMAP");
        mat.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void ApplyMaterial(GameObject root, Material material)
    {
        if (material == null)
            return;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = material;
            renderer.sharedMaterials = mats;
        }
    }

    static void EnableRenderers(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    static void NormalizeVisual(Transform visual, float targetMaxSize)
    {
        Bounds bounds = ComputeBounds(visual);
        float maxSize = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxSize > 0.001f)
        {
            float scale = targetMaxSize / maxSize;
            visual.localScale *= scale;
        }

        bounds = ComputeBounds(visual);
        visual.position += visual.root.position - bounds.center;

        bounds = ComputeBounds(visual);
        visual.position += Vector3.up * -bounds.min.y;
    }

    static void StripColliders(GameObject root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
    }

    static void EnsureCollider(GameObject root)
    {
        if (root.GetComponentInChildren<Collider>() != null)
            return;
        BoxCollider box = root.AddComponent<BoxCollider>();
        Bounds bounds = ComputeBounds(root.transform);
        box.center = root.transform.InverseTransformPoint(bounds.center);
        box.size = Vector3.Scale(bounds.size, InvertScale(root.transform.lossyScale)) * 1.25f;
        if (box.size.sqrMagnitude < 0.1f)
            box.size = Vector3.one;
    }

    static void SetInteractableLayer(Transform root)
    {
        int layer = LayerMask.NameToLayer("Interactable");
        if (layer < 0)
            return;
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetInteractableLayer(child);
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void EnsureFolders()
    {
        EnsureFolder("Assets/_RPG", "Prefabs");
        EnsureFolder("Assets/_RPG/Prefabs", "Items");
        EnsureFolder("Assets/_RPG", "ScriptableObjects");
        EnsureFolder("Assets/_RPG/ScriptableObjects", "Items");
        EnsureFolder("Assets/_RPG/ScriptableObjects", "Weapons");
        EnsureFolder("Assets/_RPG", "Materials");
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "UI");
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }

    static void CopyCraftingGridToResources()
    {
        string target = "Assets/Resources/UI/Crafting table grid.png";
        if (!File.Exists(Path.GetFullPath(CraftingGridPath)))
            return;
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(target) == null)
            AssetDatabase.CopyAsset(CraftingGridPath, target);
    }

    static void DestroyIfExists(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
            Object.DestroyImmediate(existing);
    }

    static Vector3 FindSpawnCenter()
    {
        VillageLayout layout = Object.FindAnyObjectByType<VillageLayout>();
        if (layout != null)
            return layout.transform.position + new Vector3(0f, 0f, 3.5f);
        GameObject spawn = GameObject.FindWithTag("SpawnPoint");
        if (spawn != null)
            return spawn.transform.position;
        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform.position : Vector3.zero;
    }

    static void SnapToGround(Transform transform)
    {
        Vector3 pos = transform.position;
        if (GroundUtility.TryProjectToGround(pos + Vector3.up * 2f, null, out Vector3 grounded, 8f, 18f))
            pos.y = grounded.y;
        else if (Terrain.activeTerrain != null)
            pos.y = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y;
        transform.position = pos;
    }

    static void LiftRenderableAboveOrigin(Transform root, float clearance)
    {
        Bounds bounds = ComputeBounds(root);
        float targetMinY = root.position.y + clearance;
        if (bounds.min.y < targetMinY)
            root.position += Vector3.up * (targetMinY - bounds.min.y);
    }

    static Vector3 InvertScale(Vector3 scale)
    {
        return new Vector3(
            scale.x != 0f ? 1f / scale.x : 1f,
            scale.y != 0f ? 1f / scale.y : 1f,
            scale.z != 0f ? 1f / scale.z : 1f);
    }

    static Bounds ComputeBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FifthMissionWorldSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/FifthMissionWorld.generate";
    const string ChestName = "FifthMission_ForgottenSwordChest";
    const string ChestPrefabPath = "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/Chest/ChestL.001 1.prefab";
    const string OldSwordPrefabPath = "Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 1 COLOR 3.prefab";
    const string DarkSaberPrefabPath = "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Extra Content/Chemtech-Biopunk Scimitar/SFCL_Scimitar_001 Variant.prefab";
    const string DarkSaberModelPath = "Assets/Daniel Mistage/Stylized Fantasy Armory/FBX/Chemtech-Biopunk Scimitar/SFCL_Scimitar_001.fbx";
    const string BoneSwordPrefabPath = "Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 3 COLOR 1.prefab";
    const string ClaymorePrefabPath = "Assets/URP GanzSe Free Modular Character Pack/Prefabs/GREAT SWORDS/FREE GREAT SWORD 3 COLOR 2.prefab";
    const string DoorPrefabPath = "Assets/Gridness Studios/Elementary Dungeon Pack Lite/Prefabs/Door_Middle.prefab";
    const string CombatControllerPath = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animation Controller/RPG-Character-Animation-Controller.controller";
    const string TonioWeaponPath = "Assets/_RPG/ScriptableObjects/Weapons/KingGoblinSwordWeapon.asset";
    const string OldSwordWeaponPath = "Assets/_RPG/ScriptableObjects/Weapons/OldSwordRewardWeapon.asset";
    const string OldSwordItemPath = "Assets/_RPG/Resources/Items/OldSwordReward.asset";
    const string DarkSaberWeaponPath = "Assets/_RPG/ScriptableObjects/Weapons/DarkSaberRewardWeapon.asset";
    const string DarkSaberItemPath = "Assets/_RPG/Resources/Items/DarkSaberReward.asset";
    const string PalettePath = "Assets/URP GanzSe Free Modular Character Pack/Material/Base Palette Material URP.mat";

    [MenuItem("RPG/Quests/Setup Fifth Mission And NPC Improvements")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        ItemData darkSaber = EnsureDarkSaberAssets();
        SetupMissionChest(darkSaber);
        SetupTonioAppearance();
        SetupNpcBehavior();
        SetupDungeonFloorDoor();

        // Entering Play can be requested while an asset reimport is finishing. In that narrow
        // window the scene APIs reject saving; defer the idempotent setup instead of throwing.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Setup;
            return;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[FifthMissionWorldSetup] Misi\u00f3n 5 actualizada: crab boss, Sable Oscuro exe +7 y encantamiento de Nahue.");
    }

    static ItemData EnsureDarkSaberAssets()
    {
        EnsureDarkSaberModelReadable();

        WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(DarkSaberWeaponPath);
        if (weapon == null)
        {
            weapon = ScriptableObject.CreateInstance<WeaponData>();
            AssetDatabase.CreateAsset(weapon, DarkSaberWeaponPath);
        }
        weapon.weaponID = "dark_saber_reward";
        weapon.weaponName = "Sable Oscuro";
        weapon.weaponType = WeaponType.Sword1H;
        weapon.weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DarkSaberPrefabPath);
        weapon.baseDamage = 32f;
        weapon.attackRange = 1.7f;
        weapon.attackCooldown = .58f;
        weapon.attackSpeedMultiplier = 2f;
        weapon.staminaMultiplier = 2f;
        weapon.useCustomEquippedVisual = true;
        weapon.customHandPositionOffset = new Vector3(.075f, .025f, .045f);
        weapon.customHandRotationOffset = new Vector3(289.52f, 32.8f, 107.87f);
        weapon.customGripAnchorPercent = .11f;
        weapon.customMaxEquippedWeaponSize = 1.25f;
        weapon.useVertexGripAnchor = true;
        weapon.invertGripAxis = true;
        weapon.hasIntrinsicGlow = true;
        weapon.intrinsicGlowColor = new Color(.28f, .015f, .48f);
        weapon.intrinsicGlowStrength = .72f;
        EditorUtility.SetDirty(weapon);

        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(DarkSaberItemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, DarkSaberItemPath);
        }
        item.itemID = "dark_saber_reward";
        item.itemName = "Sable Oscuro";
        item.itemType = ItemType.Weapon;
        item.description = "Sable exe +7 recuperado tras derrotar al Insectoid Crab Boss. Nahue puede encantarlo con energ\u00eda oscura.";
        item.attackBonus = 32f;
        item.stackable = false;
        item.maxStack = 1;
        item.weaponData = weapon;
        EditorUtility.SetDirty(item);
        return item;
    }

    static void EnsureDarkSaberModelReadable()
    {
        if (AssetImporter.GetAtPath(DarkSaberModelPath) is not ModelImporter importer ||
            importer.isReadable)
            return;

        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    static void SetupMissionChest(ItemData darkSaber)
    {
        GameObject chest = GameObject.Find(ChestName);
        if (chest == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath);
            chest = prefab != null ? PrefabUtility.InstantiatePrefab(prefab) as GameObject :
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.name = ChestName;
            Transform spawn = FindSpawn();
            Vector3 position = spawn != null
                ? spawn.position + spawn.right * 4.5f + spawn.forward * 5.5f
                : new Vector3(6f, 0f, 6f);
            chest.transform.SetPositionAndRotation(position, spawn != null ? spawn.rotation : Quaternion.identity);
            SnapToTerrain(chest.transform);
        }

        SetLayerRecursive(chest.transform, LayerMask.NameToLayer("Default"));
        EnsureSolidCollider(chest);
        Transform oldTrigger = chest.transform.Find("InteractionZone");
        if (oldTrigger != null) Object.DestroyImmediate(oldTrigger.gameObject);
        GameObject trigger = new GameObject("InteractionZone");
        trigger.transform.SetParent(chest.transform, false);
        trigger.layer = LayerMask.NameToLayer("Interactable");
        BoxCollider interaction = trigger.AddComponent<BoxCollider>();
        FitCollider(chest.transform, interaction);
        interaction.size += Vector3.one * .65f;
        interaction.isTrigger = true;

        ChestContainer container = chest.GetComponent<ChestContainer>();
        if (container == null) container = chest.AddComponent<ChestContainer>();
        SerializedObject chestData = new SerializedObject(container);
        chestData.FindProperty("chestName").stringValue = "Cofre del Sable Oscuro";
        chestData.FindProperty("startingItems").arraySize = 0;
        chestData.ApplyModifiedPropertiesWithoutUndo();

        FifthMissionSwordChest questChest = chest.GetComponent<FifthMissionSwordChest>();
        if (questChest == null) questChest = chest.AddComponent<FifthMissionSwordChest>();
        SerializedObject questData = new SerializedObject(questChest);
        questData.FindProperty("darkSaber").objectReferenceValue = darkSaber;
        questData.FindProperty("excellenceLevel").intValue = 7;
        questData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(chest);
    }

    static void SetupTonioAppearance()
    {
        TonioQuestGiver tonio = Object.FindAnyObjectByType<TonioQuestGiver>(FindObjectsInactive.Include);
        if (tonio == null) return;
        Transform ganz = tonio.transform.Find("GanzMerchant");
        Material palette = AssetDatabase.LoadAssetAtPath<Material>(PalettePath);
        if (ganz == null || palette == null) return;
        HashSet<string> visible = new HashSet<string>
        {
            "Base Character Mesh", "Chest Armor Type 5 Color 3", "Arm Armor Type 5 Color 3",
            "Belt Armor Type 5 Color 3", "Legs Armor Type 5 Color 3", "Feet Armor Type 5 Color 3",
            "Hair Type 5 Color 5", "Face Hair Type 5 Color 5", "Eyes Type 1 Color 1",
            "Eyebrow Type 5 Color 5", "Nose Type 2", "Ears Type 1"
        };
        foreach (SkinnedMeshRenderer renderer in ganz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            renderer.sharedMaterial = palette;
            bool active = visible.Contains(renderer.name);
            renderer.gameObject.SetActive(active);
            renderer.enabled = active;
        }
        EditorUtility.SetDirty(tonio.gameObject);
    }

    static void SetupNpcBehavior()
    {
        RuntimeAnimatorController combat = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CombatControllerPath);
        TonioQuestGiver tonio = Object.FindAnyObjectByType<TonioQuestGiver>(FindObjectsInactive.Include);
        NPCHerrero blacksmith = Object.FindAnyObjectByType<NPCHerrero>(FindObjectsInactive.Include);
        NahueQuestGiver nahue = Object.FindAnyObjectByType<NahueQuestGiver>(FindObjectsInactive.Include);

        GameObject tonioSword = AssetDatabase.LoadAssetAtPath<GameObject>(OldSwordPrefabPath);
        ConfigureDefender(tonio != null ? tonio.gameObject : null, combat,
            null, tonioSword, WeaponType.Sword1H, 55f);
        ConfigureDefender(blacksmith != null ? blacksmith.gameObject : null, combat, null,
            AssetDatabase.LoadAssetAtPath<GameObject>(ClaymorePrefabPath), WeaponType.Sword2H, 76f);
        ConfigureDefender(nahue != null ? nahue.gameObject : null, combat, null,
            AssetDatabase.LoadAssetAtPath<GameObject>(BoneSwordPrefabPath), WeaponType.Sword1H, 36f);

        if (tonio != null)
        {
            TonioRetaliation retaliation = tonio.GetComponent<TonioRetaliation>();
            WeaponData oldSword = AssetDatabase.LoadAssetAtPath<WeaponData>(OldSwordWeaponPath);
            if (retaliation != null && oldSword != null)
            {
                SerializedObject retaliationData = new SerializedObject(retaliation);
                retaliationData.FindProperty("swordWeaponData").objectReferenceValue = oldSword;
                retaliationData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(retaliation);
            }
        }

        if (blacksmith != null)
        {
            NPCAmbientSpeech speech = blacksmith.GetComponent<NPCAmbientSpeech>();
            if (speech == null) speech = blacksmith.gameObject.AddComponent<NPCAmbientSpeech>();
            speech.Configure("\u00bfSe te ofrece algo?", "Tengo las mejores espadas del poblado.");
            EditorUtility.SetDirty(speech);
        }
        if (nahue != null)
        {
            NPCAmbientSpeech speech = nahue.GetComponent<NPCAmbientSpeech>();
            if (speech == null) speech = nahue.gameObject.AddComponent<NPCAmbientSpeech>();
            speech.Configure("El silencio eterno de tus ojos es la clave; m\u00edrame a m\u00ed, yo ya venc\u00ed.");
            EditorUtility.SetDirty(speech);
        }
    }

    static void ConfigureDefender(GameObject npc, RuntimeAnimatorController combat, WeaponData weaponData,
        GameObject weaponPrefab, WeaponType type, float damage)
    {
        if (npc == null) return;
        NPCMeleeDefender defender = npc.GetComponent<NPCMeleeDefender>();
        if (defender == null) defender = npc.AddComponent<NPCMeleeDefender>();
        SerializedObject data = new SerializedObject(defender);
        data.FindProperty("combatController").objectReferenceValue = combat;
        data.FindProperty("weaponDataTemplate").objectReferenceValue = weaponData;
        data.FindProperty("weaponPrefab").objectReferenceValue = weaponPrefab;
        data.FindProperty("weaponType").enumValueIndex = (int)type;
        data.FindProperty("attackDamage").floatValue = damage;
        data.FindProperty("detectionRadius").floatValue = 10f;
        data.FindProperty("attackRange").floatValue = type == WeaponType.Sword2H ? 2.9f : 2.45f;
        data.FindProperty("attackCooldown").floatValue = type == WeaponType.Sword2H ? 1.7f : 1.35f;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(npc);
    }

    static void SetupDungeonFloorDoor()
    {
        DungeonEntrance entrance = Object.FindAnyObjectByType<DungeonEntrance>(FindObjectsInactive.Include);
        if (entrance == null) return;
        Transform old = entrance.transform.Find("DungeonFloorDoorVisual");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
        if (prefab == null) return;
        GameObject door = PrefabUtility.InstantiatePrefab(prefab, entrance.gameObject.scene) as GameObject;
        door.name = "DungeonFloorDoorVisual";
        door.transform.SetParent(entrance.transform, false);
        door.transform.localPosition = new Vector3(0f, .06f, 0f);
        door.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        NormalizeSize(door, 2.7f);
        foreach (Collider collider in door.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
    }

    static Transform FindSpawn()
    {
        try
        {
            GameObject spawn = GameObject.FindWithTag("SpawnPoint");
            if (spawn != null) return spawn.transform;
        }
        catch (UnityException) { }
        return null;
    }

    static void NormalizeSize(GameObject root, float target)
    {
        Bounds bounds = RendererBounds(root.transform);
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (size > .001f) root.transform.localScale *= target / size;
    }

    static void EnsureSolidCollider(GameObject root)
    {
        Collider collider = root.GetComponent<Collider>();
        if (collider != null && !collider.isTrigger) return;
        BoxCollider box = root.AddComponent<BoxCollider>();
        FitCollider(root.transform, box);
        box.isTrigger = false;
    }

    static void FitCollider(Transform root, BoxCollider collider)
    {
        Bounds bounds = RendererBounds(root);
        collider.center = root.InverseTransformPoint(bounds.center);
        Vector3 scale = root.lossyScale;
        collider.size = new Vector3(bounds.size.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(.001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(.001f, Mathf.Abs(scale.z)));
    }

    static Bounds RendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void SetLayerRecursive(Transform root, int layer)
    {
        if (layer < 0) return;
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursive(child, layer);
    }

    static void SnapToTerrain(Transform target)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;
        Vector3 p = target.position;
        p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        target.position = p;
    }

    [InitializeOnLoadMethod]
    static void Queue() => EditorApplication.delayCall += TryRun;

    static void TryRun()
    {
        string request = Path.GetFullPath(RequestPath);
        if (!File.Exists(request)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }
        File.Delete(request);
        if (File.Exists(request + ".meta")) File.Delete(request + ".meta");
        Setup();
    }
}

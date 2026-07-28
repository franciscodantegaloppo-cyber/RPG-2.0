using System.IO;
using ithappy.Animals_FREE;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AnimalSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/AnimalSetup.generate";
    const string OutputFolder = "Assets/_RPG/Prefabs/Animals";

    struct AnimalConfig
    {
        public string prefab;
        public string name;
        public AnimalKind kind;
        public AnimalTemperament temperament;
        public float health;
        public float attack;
        public float detection;
        public float flee;
        public float wander;
        public float step;
        public float walk;
        public float run;
        public bool canAttack;
        public Vector3 scenePosition;
    }

    [MenuItem("RPG/Animals/Setup Animals")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[AnimalSetup] Sali de Play Mode para generar animales.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        EnsureFolders();
        foreach (AnimalConfig config in Configs())
        {
            GameObject prefab = BuildAnimalPrefab(config);
            PlaceSceneAnimal(prefab, config);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AnimalSetup] Animales RPG creados con vida, poder, animacion y merodeo.");
    }

    [InitializeOnLoadMethod]
    static void RunRequestedSetup()
    {
        EditorApplication.delayCall += TryRunRequestedSetup;
    }

    static void TryRunRequestedSetup()
    {
        string absolutePath = Path.GetFullPath(RequestPath);
        if (!File.Exists(absolutePath) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        File.Delete(absolutePath);
        string metaPath = absolutePath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        Setup();
    }

    static GameObject BuildAnimalPrefab(AnimalConfig config)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(config.prefab);
        if (source == null)
        {
            Debug.LogError("[AnimalSetup] No encontre prefab animal: " + config.prefab);
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = config.name;
        ConfigureAnimal(instance, config);
        string path = OutputFolder + "/" + config.name + ".prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return saved;
    }

    static void ConfigureAnimal(GameObject animal, AnimalConfig config)
    {
        animal.tag = "Enemy";
        SetLayerRecursive(animal, LayerMask.NameToLayer("Enemy"));

        MovePlayerInput playerInput = animal.GetComponent<MovePlayerInput>();
        if (playerInput != null)
            Object.DestroyImmediate(playerInput);

        CreatureMover mover = animal.GetComponent<CreatureMover>();
        if (mover == null)
            mover = animal.AddComponent<CreatureMover>();

        SetSerialized(mover, "m_WalkSpeed", config.walk);
        SetSerialized(mover, "m_RunSpeed", config.run);
        SetSerialized(mover, "m_RotateSpeed", 220f);
        SetSerialized(mover, "m_Space", Space.World);

        CharacterController cc = animal.GetComponent<CharacterController>();
        if (cc == null)
            cc = animal.AddComponent<CharacterController>();
        cc.stepOffset = Mathf.Min(cc.height * 0.45f, 0.45f);
        cc.slopeLimit = 45f;

        EnemyStats stats = animal.GetComponent<EnemyStats>();
        if (stats == null)
            stats = animal.AddComponent<EnemyStats>();
        SetSerialized(stats, "maxHealth", config.health);
        SetSerialized(stats, "attack", config.attack);
        SetSerialized(stats, "xpReward", Mathf.RoundToInt(config.health * 0.35f));
        SetSerialized(stats, "minGoldDrop", config.canAttack ? 12 : 1);
        SetSerialized(stats, "maxGoldDrop", config.canAttack ? 28 : 6);

        AnimalAI ai = animal.GetComponent<AnimalAI>();
        if (ai == null)
            ai = animal.AddComponent<AnimalAI>();
        SetSerialized(ai, "animalKind", config.kind);
        SetSerialized(ai, "temperament", config.temperament);
        SetSerialized(ai, "detectionRadius", config.detection);
        SetSerialized(ai, "fleeRadius", config.flee);
        SetSerialized(ai, "leashRadius", Mathf.Max(config.detection + 6f, 16f));
        SetSerialized(ai, "wanderRadius", config.wander);
        SetSerialized(ai, "stepDistance", config.step);
        SetSerialized(ai, "attackRadius", config.kind == AnimalKind.Tiger ? 1.75f : 1.25f);
        SetSerialized(ai, "attackCooldown", config.kind == AnimalKind.Tiger ? 1.45f : 2.2f);
        SetSerialized(ai, "lungeSeconds", config.kind == AnimalKind.Tiger ? 0.38f : 0.25f);
        SetSerialized(ai, "canAttack", config.canAttack);
        SetSerialized(ai, "playerMask", new LayerMask { value = LayerMask.GetMask("Player") });

        foreach (Collider collider in animal.GetComponentsInChildren<Collider>(true))
            collider.isTrigger = false;

        EditorUtility.SetDirty(animal);
    }

    static void PlaceSceneAnimal(GameObject prefab, AnimalConfig config)
    {
        if (prefab == null)
            return;

        GameObject existing = GameObject.Find(config.name);
        if (existing != null)
        {
            ConfigureAnimal(existing, config);
            existing.transform.position = Grounded(config.scenePosition);
            EditorUtility.SetDirty(existing);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = config.name;
        instance.transform.SetPositionAndRotation(Grounded(config.scenePosition), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        ConfigureAnimal(instance, config);
        EditorUtility.SetDirty(instance);
    }

    static Vector3 Grounded(Vector3 position)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 0.05f;
        return position;
    }

    static AnimalConfig[] Configs()
    {
        return new[]
        {
            new AnimalConfig
            {
                prefab = "Assets/ithappy/Animals_FREE/Prefabs/Tiger_001.prefab",
                name = "Animal_Tiger",
                kind = AnimalKind.Tiger,
                temperament = AnimalTemperament.Aggressive,
                health = 150f,
                attack = 26f,
                detection = 13f,
                flee = 0f,
                wander = 16f,
                step = 5f,
                walk = 4.2f,
                run = 14f,
                canAttack = true,
                scenePosition = new Vector3(20f, 0f, 24f)
            },
            new AnimalConfig
            {
                prefab = "Assets/ithappy/Animals_FREE/Prefabs/Horse_001.prefab",
                name = "Animal_Horse",
                kind = AnimalKind.Horse,
                temperament = AnimalTemperament.Timid,
                health = 100f,
                attack = 4f,
                detection = 8f,
                flee = 4.8f,
                wander = 12f,
                step = 5f,
                walk = 3.4f,
                run = 11f,
                canAttack = false,
                scenePosition = new Vector3(9f, 0f, 13f)
            },
            new AnimalConfig
            {
                prefab = "Assets/ithappy/Animals_FREE/Prefabs/Deer_001.prefab",
                name = "Animal_Deer",
                kind = AnimalKind.Deer,
                temperament = AnimalTemperament.Timid,
                health = 50f,
                attack = 3f,
                detection = 8f,
                flee = 5.5f,
                wander = 13f,
                step = 4f,
                walk = 3f,
                run = 10f,
                canAttack = false,
                scenePosition = new Vector3(13f, 0f, 20f)
            },
            new AnimalConfig
            {
                prefab = "Assets/ithappy/Animals_FREE/Prefabs/Dog_001.prefab",
                name = "Animal_Dog",
                kind = AnimalKind.Dog,
                temperament = AnimalTemperament.Passive,
                health = 45f,
                attack = 6f,
                detection = 6f,
                flee = 0f,
                wander = 8f,
                step = 3f,
                walk = 2.6f,
                run = 8f,
                canAttack = false,
                scenePosition = new Vector3(1.5f, 0f, 9f)
            },
            new AnimalConfig
            {
                prefab = "Assets/ithappy/Animals_FREE/Prefabs/Kitty_001.prefab",
                name = "Animal_Kitty",
                kind = AnimalKind.Kitty,
                temperament = AnimalTemperament.Timid,
                health = 25f,
                attack = 2f,
                detection = 5f,
                flee = 3.2f,
                wander = 6f,
                step = 2f,
                walk = 2.2f,
                run = 6.5f,
                canAttack = false,
                scenePosition = new Vector3(-1.5f, 0f, 6f)
            },
            new AnimalConfig
            {
                prefab = "Assets/ithappy/Animals_FREE/Prefabs/Chicken_001.prefab",
                name = "Animal_Chicken",
                kind = AnimalKind.Chicken,
                temperament = AnimalTemperament.Timid,
                health = 15f,
                attack = 1f,
                detection = 4.5f,
                flee = 3.5f,
                wander = 5f,
                step = 1.6f,
                walk = 1.8f,
                run = 5f,
                canAttack = false,
                scenePosition = new Vector3(3f, 0f, 8f)
            },
            new AnimalConfig
            {
                prefab = "Assets/ithappy/Animals_FREE/Prefabs/Pinguin_001.prefab",
                name = "Animal_Pinguin",
                kind = AnimalKind.Pinguin,
                temperament = AnimalTemperament.Timid,
                health = 28f,
                attack = 2f,
                detection = 5f,
                flee = 3f,
                wander = 6f,
                step = 2f,
                walk = 1.8f,
                run = 4.8f,
                canAttack = false,
                scenePosition = new Vector3(6f, 0f, 17f)
            }
        };
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Prefabs"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Prefabs");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/_RPG/Prefabs", "Animals");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Generated"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Generated");
    }

    static void SetLayerRecursive(GameObject root, int layer)
    {
        if (layer < 0)
            return;

        root.layer = layer;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    static void SetSerialized(Object target, string propertyName, float value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, int value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, bool value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized<T>(Object target, string propertyName, T value) where T : System.Enum
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.enumValueIndex = System.Convert.ToInt32(value);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, LayerMask value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.intValue = value.value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

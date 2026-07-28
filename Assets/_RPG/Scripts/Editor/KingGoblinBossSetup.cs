using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class KingGoblinBossSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/KingGoblinBossSetup.generate";
    const string Root = "Assets/Daniel Mistage/King goblin_1/Meshy_AI_Quiero_que_crees_un_r_biped";
    const string ModelPath = Root + "/Meshy_AI_Quiero_que_crees_un_r_biped_Character_output.fbx";
    const string TexturePath = Root + "/Meshy_AI_Quiero_que_crees_un_r_biped_texture_0.png";
    const string NormalPath = Root + "/Meshy_AI_Quiero_que_crees_un_r_biped_texture_0_normal.png";
    const string RoughnessPath = Root + "/Meshy_AI_Quiero_que_crees_un_r_biped_texture_0_roughness.png";
    const string MetallicPath = Root + "/Meshy_AI_Quiero_que_crees_un_r_biped_texture_0_metallic.png";
    const string HealthBarTexturePath = "Assets/_Recovery/king goblin health bar/Meshy_AI_Quiero_que_hagas_una__0710213526_texture.png";
    const string MaterialPath = "Assets/_RPG/Materials/KingGoblinBoss.mat";
    const string ControllerPath = "Assets/_RPG/Animations/KingGoblinBoss.controller";
    const string PrefabPath = "Assets/_RPG/Prefabs/Enemies/KingGoblinBoss.prefab";
    const string SceneObjectName = "KingGoblinBoss";

    [MenuItem("RPG/Enemies/Setup King Goblin Boss")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[KingGoblinBossSetup] Ignorado durante Play Mode.");
            return;
        }

        EditorSceneUtility.OpenSceneSafely(ScenePath);
        EnsureFolders();

        Material material = EnsureMaterial();
        RuntimeAnimatorController controller = EnsureController();
        GameObject prefab = BuildPrefab(material, controller);
        PlaceSceneInstance(prefab);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[KingGoblinBossSetup] King goblin creado con 5000 vida, 500 dano, barra especial y animaciones.");
    }

    [InitializeOnLoadMethod]
    static void RunRequestedSetup()
    {
        EditorApplication.delayCall += TryRunRequestedSetup;
    }

    static void TryRunRequestedSetup()
    {
        string absoluteRequestPath = Path.GetFullPath(RequestPath);
        if (!File.Exists(absoluteRequestPath))
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRunRequestedSetup;
            return;
        }

        File.Delete(absoluteRequestPath);
        string metaPath = absoluteRequestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);
        Setup();
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Prefabs"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Prefabs/Enemies"))
            AssetDatabase.CreateFolder("Assets/_RPG/Prefabs", "Enemies");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Animations"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Animations");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Materials"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Generated"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Generated");
    }

    static Material EnsureMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
        Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(RoughnessPath);

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
        if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
        if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", metallic);
        if (mat.HasProperty("_SmoothnessTextureChannel")) mat.SetFloat("_SmoothnessTextureChannel", 0f);
        if (mat.HasProperty("_GlossMapScale")) mat.SetFloat("_GlossMapScale", roughness != null ? 0.35f : 0.25f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject BuildPrefab(Material material, RuntimeAnimatorController controller)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (source == null)
        {
            Debug.LogError("[KingGoblinBossSetup] No se encontro el modelo: " + ModelPath);
            return null;
        }

        GameObject boss = (GameObject)PrefabUtility.InstantiatePrefab(source);
        boss.name = SceneObjectName;
        ConfigureBoss(boss, material, controller);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(boss, PrefabPath);
        Object.DestroyImmediate(boss);
        return saved;
    }

    static void ConfigureBoss(GameObject boss, Material material, RuntimeAnimatorController controller)
    {
        boss.tag = "Enemy";
        SetLayerRecursive(boss, LayerMask.NameToLayer("Enemy"));
        boss.transform.localScale = Vector3.one * 1.45f;

        foreach (Renderer renderer in boss.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = material;

        Animator animator = boss.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = boss.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        CapsuleCollider capsule = boss.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = boss.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 1.55f, 0f);
        capsule.height = 3.1f;
        capsule.radius = 0.65f;

        Rigidbody rb = boss.GetComponent<Rigidbody>();
        if (rb == null)
            rb = boss.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        EnemyAI oldAi = boss.GetComponent<EnemyAI>();
        if (oldAi != null)
            Object.DestroyImmediate(oldAi);

        EnemyStats stats = boss.GetComponent<EnemyStats>();
        if (stats == null)
            stats = boss.AddComponent<EnemyStats>();
        SetSerialized(stats, "maxHealth", 5000f);
        SetSerialized(stats, "attack", 500f);
        SetSerialized(stats, "armor", 30f);
        SetSerialized(stats, "creatureType", (int)CreatureType.Boss);
        SetSerialized(stats, "xpReward", 1000);

        KingGoblinBossAI ai = boss.GetComponent<KingGoblinBossAI>();
        if (ai == null)
            ai = boss.AddComponent<KingGoblinBossAI>();
        SetSerialized(ai, "walkSpeed", 1.35f);
        SetSerialized(ai, "runSpeed", 4.6f);
        SetSerialized(ai, "superRunSpeed", 7f);
        SetSerialized(ai, "walkDistance", 6f);
        // Reference speeds are the clip's own native pace (walkAnimSpeed still equals walkSpeed,
        // but runSpeed/superRunSpeed above are now deliberately faster than their references) -
        // KingGoblinBossAI rescales animator.speed at runtime by actualSpeed/reference, so the
        // faster movement makes the leg-cycle speed up proportionally instead of the stride
        // visually stretching, which is what read as sliding/surfing before this was fixed.
        SetSerialized(ai, "walkAnimSpeed", 1.35f);
        SetSerialized(ai, "runAnimSpeed", 3.05f);
        SetSerialized(ai, "superRunAnimSpeed", 4.65f);
        SetSerialized(ai, "jumpForwardSpeed", 2.1f);

        KingGoblinVisualRootLock rootLock = boss.GetComponent<KingGoblinVisualRootLock>();
        if (rootLock != null)
            Object.DestroyImmediate(rootLock);

        KingGoblinAnimationRootStabilizer stabilizer = boss.GetComponent<KingGoblinAnimationRootStabilizer>();
        if (stabilizer == null)
            stabilizer = boss.AddComponent<KingGoblinAnimationRootStabilizer>();

        KingGoblinHealthBar bar = boss.GetComponent<KingGoblinHealthBar>();
        if (bar == null)
            bar = boss.AddComponent<KingGoblinHealthBar>();
        var barSerialized = new SerializedObject(bar);
        barSerialized.FindProperty("frameTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(HealthBarTexturePath);
        barSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(boss);
    }

    static RuntimeAnimatorController EnsureController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack1", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack3", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idle = AddState(sm, "Idle", Clip("Idle_03"), new Vector3(220f, 80f, 0f));
        AnimatorState walk = AddState(sm, "Walk", Clip("Walking"), new Vector3(220f, 180f, 0f));
        AnimatorState run = AddState(sm, "Run", Clip("Running"), new Vector3(220f, 280f, 0f));
        AnimatorState fast = AddState(sm, "SuperRun", Clip("RunFast"), new Vector3(220f, 380f, 0f));
        AnimatorState jump = AddState(sm, "Jump", Clip("Regular_Jump"), new Vector3(560f, 80f, 0f));
        AnimatorState attack1 = AddState(sm, "SwordSlash", Clip("Right_Hand_Sword_Slash"), new Vector3(560f, 180f, 0f));
        AnimatorState attack2 = AddState(sm, "LeftHook", Clip("Left_Hook_from_Guard"), new Vector3(560f, 280f, 0f));
        AnimatorState attack3 = AddState(sm, "HighKick", Clip("Step_in_High_Kick"), new Vector3(560f, 380f, 0f));
        AnimatorState hit = AddState(sm, "Hit", Clip("Idle_03"), new Vector3(880f, 180f, 0f));
        AnimatorState die = AddState(sm, "Death", Clip("Jump_Over_Obstacle_2"), new Vector3(880f, 280f, 0f));

        sm.defaultState = idle;
        AddFloatTransition(idle, walk, "Speed", AnimatorConditionMode.Greater, 0.15f);
        AddFloatTransition(walk, idle, "Speed", AnimatorConditionMode.Less, 0.15f);
        AddFloatTransition(walk, run, "Speed", AnimatorConditionMode.Greater, 0.75f);
        AddFloatTransition(run, walk, "Speed", AnimatorConditionMode.Less, 0.75f);
        AddFloatTransition(run, fast, "Speed", AnimatorConditionMode.Greater, 1.5f);
        AddFloatTransition(fast, run, "Speed", AnimatorConditionMode.Less, 1.5f);
        AddFloatTransition(idle, run, "Speed", AnimatorConditionMode.Greater, 0.75f);
        AddFloatTransition(idle, fast, "Speed", AnimatorConditionMode.Greater, 1.5f);

        AddTriggerTransition(sm, attack1, "Attack1");
        AddTriggerTransition(sm, attack2, "Attack2");
        AddTriggerTransition(sm, attack3, "Attack3");
        AddTriggerTransition(sm, jump, "Jump");
        AddTriggerTransition(sm, hit, "Hit");
        AddTriggerTransition(sm, die, "Die");

        AddReturnTransition(attack1, idle, 0.85f);
        AddReturnTransition(attack2, idle, 0.85f);
        AddReturnTransition(attack3, idle, 0.85f);
        AddReturnTransition(jump, idle, 0.9f);
        AddReturnTransition(hit, idle, 0.55f);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimationClip Clip(string token)
    {
        string[] guids = AssetDatabase.FindAssets(token + " t:AnimationClip", new[] { Root });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object asset in assets)
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview", System.StringComparison.OrdinalIgnoreCase))
                    return clip;
        }
        return null;
    }

    static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion, Vector3 pos)
    {
        AnimatorState state = sm.AddState(name, pos);
        state.motion = motion;
        return state;
    }

    static void AddFloatTransition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, float threshold)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.12f;
        transition.AddCondition(mode, threshold, parameter);
    }

    static void AddTriggerTransition(AnimatorStateMachine sm, AnimatorState to, string parameter)
    {
        AnimatorStateTransition transition = sm.AddAnyStateTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.08f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
    }

    static void AddReturnTransition(AnimatorState from, AnimatorState to, float exitTime)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = 0.12f;
    }

    static void PlaceSceneInstance(GameObject prefab)
    {
        if (prefab == null)
            return;

        GameObject existing = GameObject.Find(SceneObjectName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        boss.name = SceneObjectName;
        Vector3 position = new Vector3(-34f, 0f, 44f);
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        boss.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 150f, 0f));
        EditorUtility.SetDirty(boss);
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
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, int value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

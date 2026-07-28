using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class GoblinEnemySetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string FbxPath = "Assets/Goblins -characters with animations/Goblins -characters with animations/models/goblin.fbx";
    const string CombatControllerPath = "Assets/_RPG/Animations/GoblinEnemyCombat.controller";
    const string RequestPath = "Assets/_RPG/Generated/GoblinEnemySetup.generate";

    struct Variant
    {
        public string SourcePrefabPath;
        public string EnemyPrefabPath;
        public string Name;
        public Vector3 ColliderCenter;
        public float ColliderHeight;
        public float ColliderRadius;
        public Vector3 ScenePosition;
        public float SceneYaw;
    }

    // Goblins are weaker but faster than the skeleton (maxHealth 55 / attack 9 / chaseRunSpeed
    // 4.15 there) - lower health/attack/armor, higher wander/chase speeds and a shorter attack
    // cooldown to match their quick attack_1/attack_2 clips (0.76s vs the sword's longer swing).
    static readonly Variant[] Variants = new Variant[]
    {
        new Variant
        {
            SourcePrefabPath = "Assets/Goblins -characters with animations/Goblins -characters with animations/models/prefab/goblin.prefab",
            EnemyPrefabPath = "Assets/_RPG/Prefabs/Enemies/GoblinEnemy1.prefab",
            Name = "GoblinEnemy1",
            ColliderCenter = new Vector3(0f, 0.88f, 0f),
            ColliderHeight = 1.75f,
            ColliderRadius = 0.34f,
            ScenePosition = new Vector3(-16f, 0f, 24f),
            SceneYaw = 140f,
        },
        new Variant
        {
            SourcePrefabPath = "Assets/Goblins -characters with animations/Goblins -characters with animations/models/prefab/goblin_2.prefab",
            EnemyPrefabPath = "Assets/_RPG/Prefabs/Enemies/GoblinEnemy2.prefab",
            Name = "GoblinEnemy2",
            ColliderCenter = new Vector3(0f, 0.80f, 0f),
            ColliderHeight = 1.6f,
            ColliderRadius = 0.34f,
            ScenePosition = new Vector3(-22f, 0f, 30f),
            SceneYaw = 95f,
        },
        new Variant
        {
            SourcePrefabPath = "Assets/Goblins -characters with animations/Goblins -characters with animations/models/prefab/goblin_3.prefab",
            EnemyPrefabPath = "Assets/_RPG/Prefabs/Enemies/GoblinEnemy3.prefab",
            Name = "GoblinEnemy3",
            ColliderCenter = new Vector3(0f, 0.80f, 0f),
            ColliderHeight = 1.6f,
            ColliderRadius = 0.33f,
            ScenePosition = new Vector3(-10f, 0f, 32f),
            SceneYaw = 210f,
        },
    };

    [MenuItem("RPG/Enemies/Setup Goblin Enemies")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[GoblinEnemySetup] Ignorado durante Play Mode. Sali de Play para ejecutar el setup.");
            return;
        }

        EditorSceneUtility.OpenSceneSafely(ScenePath);

        EnsureFolders();
        RuntimeAnimatorController controller = EnsureCombatController();

        foreach (Variant variant in Variants)
        {
            GameObject prefab = BuildPrefab(variant, controller);
            PlaceSceneInstance(variant, prefab, controller);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[GoblinEnemySetup] 3 goblin variants configured and placed.");
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
            return;

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
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Generated"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Generated");
    }

    static GameObject BuildPrefab(Variant variant, RuntimeAnimatorController controller)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(variant.SourcePrefabPath);
        if (source == null)
        {
            Debug.LogError("[GoblinEnemySetup] Source prefab not found: " + variant.SourcePrefabPath);
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = variant.Name;
        ConfigureEnemyObject(instance, variant, controller);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, variant.EnemyPrefabPath);
        Object.DestroyImmediate(instance);
        return saved;
    }

    static void ConfigureEnemyObject(GameObject enemy, Variant variant, RuntimeAnimatorController controller)
    {
        enemy.tag = "Enemy";
        SetLayerRecursive(enemy, LayerMask.NameToLayer("Enemy"));

        Animator animator = enemy.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            if (controller != null)
                animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            if (animator.gameObject.GetComponent<NPCAnimationEvents>() == null)
                animator.gameObject.AddComponent<NPCAnimationEvents>();
        }

        // Sword/shield/club are already skinned meshes built into the goblin rig (unlike the
        // skeleton, which needed an external weapon prefab attached to its hand bone).
        CapsuleCollider capsule = enemy.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = enemy.AddComponent<CapsuleCollider>();
        capsule.center = variant.ColliderCenter;
        capsule.height = variant.ColliderHeight;
        capsule.radius = variant.ColliderRadius;
        capsule.isTrigger = false;

        Rigidbody body = enemy.GetComponent<Rigidbody>();
        if (body == null)
            body = enemy.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null)
            Object.DestroyImmediate(agent);

        EnemyStats stats = enemy.GetComponent<EnemyStats>();
        if (stats == null)
            stats = enemy.AddComponent<EnemyStats>();
        SetSerialized(stats, "maxHealth", 34f);
        SetSerialized(stats, "attack", 6f);
        SetSerialized(stats, "armor", 4f);
        SetSerialized(stats, "creatureType", (int)CreatureType.Normal);
        SetSerialized(stats, "xpReward", 16);

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai == null)
            ai = enemy.AddComponent<EnemyAI>();
        SetSerialized(ai, "detectionRadius", 13f);
        SetSerialized(ai, "leashRadius", 20f);
        SetSerialized(ai, "attackRadius", 1.35f);
        SetSerialized(ai, "playerMask", new LayerMask { value = LayerMask.GetMask("Player") });
        SetSerialized(ai, "wanderRadius", 7f);
        SetSerialized(ai, "wanderSpeed", 1.6f);
        SetSerialized(ai, "chaseWalkSpeed", 2.6f);
        SetSerialized(ai, "chaseRunSpeed", 5f);
        SetSerialized(ai, "runDistance", 6f);
        SetSerialized(ai, "attackCooldown", 1.05f);
        SetSerialized(ai, "attackDamageDelay", 0.3f);
        SetSerialized(ai, "obstacleProbeDistance", 0.9f);
        SetSerialized(ai, "obstacleSideStep", 1.35f);
        SetSerialized(ai, "obstacleMask", new LayerMask { value = ~LayerMask.GetMask("Enemy", "Player", "Interactable") });

        // The goblin walk/run clips have zero baked root motion (pure in-place loops) - without
        // this, EnemyAI's code-driven translation and the animation's own fixed playback pace
        // don't match and the feet visibly slide. Matches wanderSpeed/chaseRunSpeed above exactly
        // so Run (always exactly chaseRunSpeed) plays at its natural 1x pace, and Walk (shared by
        // wanderSpeed and chaseWalkSpeed) is calibrated to wander and auto-compensates when
        // chase-walking faster.
        SetSerialized(ai, "walkAnimSpeed", 1.6f);
        SetSerialized(ai, "runAnimSpeed", 5f);

        // Generic procedural attack/hit/death FX helper - not skeleton-specific despite the name,
        // it only needs an Animator (+ optional NavMeshAgent). useProceduralMotion defaults off.
        SkeletonEnemyAnimatorFX fx = enemy.GetComponent<SkeletonEnemyAnimatorFX>();
        if (fx == null)
            fx = enemy.AddComponent<SkeletonEnemyAnimatorFX>();
        // The goblin walk/run clips have zero baked motion (see EnsureCombatController comment) -
        // enable the synthetic walk-bob so locomotion reads as walking instead of gliding/surfing.
        SetSerialized(fx, "enableWalkBob", true);

        EditorUtility.SetDirty(enemy);
    }

    static RuntimeAnimatorController EnsureCombatController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CombatControllerPath);
        if (controller != null)
            AssetDatabase.DeleteAsset(CombatControllerPath);

        controller = AnimatorController.CreateAnimatorControllerAtPath(CombatControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Damage", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("LeftAttack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("RightAttack", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimationClip idle = LoadClip("idle");
        AnimationClip walk = LoadClip("walk");
        AnimationClip run = LoadClip("run");
        AnimationClip attack = LoadClip("attack_1");
        AnimationClip attackAlt = LoadClip("attack_2");
        AnimationClip hit = LoadClip("hit");
        AnimationClip death = LoadClip("dead");

        AnimatorState idleState = sm.AddState("Idle", new Vector3(250f, 80f, 0f));
        idleState.motion = idle;
        sm.defaultState = idleState;

        AnimatorState walkState = sm.AddState("Walk", new Vector3(250f, 180f, 0f));
        walkState.motion = walk != null ? walk : idle;
        AnimatorState runState = sm.AddState("Run", new Vector3(250f, 280f, 0f));
        runState.motion = run != null ? run : walkState.motion;
        AnimatorState attackState = sm.AddState("Attack", new Vector3(560f, 80f, 0f));
        attackState.motion = attack;
        AnimatorState attackAltState = sm.AddState("AttackAlt", new Vector3(560f, 180f, 0f));
        attackAltState.motion = attackAlt != null ? attackAlt : attack;
        AnimatorState hitState = sm.AddState("Hit", new Vector3(560f, 280f, 0f));
        hitState.motion = hit;
        AnimatorState deathState = sm.AddState("Death", new Vector3(560f, 380f, 0f));
        deathState.motion = death != null ? death : hit;

        AddFloatTransition(idleState, walkState, "Speed", AnimatorConditionMode.Greater, 0.15f);
        AddFloatTransition(walkState, idleState, "Speed", AnimatorConditionMode.Less, 0.15f);
        AddFloatTransition(walkState, runState, "Speed", AnimatorConditionMode.Greater, 0.75f);
        AddFloatTransition(runState, walkState, "Speed", AnimatorConditionMode.Less, 0.75f);
        AddFloatTransition(idleState, runState, "Speed", AnimatorConditionMode.Greater, 0.75f);
        AddFloatTransition(runState, idleState, "Speed", AnimatorConditionMode.Less, 0.15f);

        AddTriggerTransition(sm, attackState, "Attack");
        AddTriggerTransition(sm, attackState, "RightAttack");
        AddTriggerTransition(sm, attackAltState, "LeftAttack");
        AddTriggerTransition(sm, hitState, "Hit");
        AddTriggerTransition(sm, hitState, "Damage");
        AddTriggerTransition(sm, deathState, "Die");
        AddTriggerTransition(sm, deathState, "Death");

        AddReturnTransition(attackState, idleState, 0.85f);
        AddReturnTransition(attackAltState, idleState, 0.85f);
        AddReturnTransition(hitState, idleState, 0.8f);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static AnimationClip LoadClip(string preferredName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip && clip.name == preferredName)
                return clip;
        }

        Debug.LogWarning("[GoblinEnemySetup] Animation clip not found: " + preferredName);
        return null;
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

    static void PlaceSceneInstance(Variant variant, GameObject prefab, RuntimeAnimatorController controller)
    {
        if (prefab == null)
            return;

        GameObject existing = FindSceneInstance(variant.Name);
        if (existing != null)
        {
            ConfigureEnemyObject(existing, variant, controller);
            existing.name = variant.Name;
            EditorUtility.SetDirty(existing);
            return;
        }

        Vector3 position = variant.ScenePosition;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;

        GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        enemy.name = variant.Name;
        enemy.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, variant.SceneYaw, 0f));
        EditorUtility.SetDirty(enemy);
    }

    static GameObject FindSceneInstance(string name)
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (GameObject obj in allObjects)
        {
            if (!obj.scene.IsValid())
                continue;
            if (obj.name == name && obj.GetComponent<EnemyAI>() != null)
                return obj;
        }

        return null;
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

    static void SetSerialized(Object target, string propertyName, LayerMask value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value.value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, bool value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

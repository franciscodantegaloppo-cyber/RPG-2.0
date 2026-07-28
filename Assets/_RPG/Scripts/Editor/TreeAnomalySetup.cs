using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TreeAnomalySetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string SourceFolder = "Assets/_RPG/Imported/Anomalia del arbol/";
    const string CharacterFbx = SourceFolder + "Meshy_AI_Quiero_que_hagas_una__biped_Character_output.fbx";
    const string ControllerPath = "Assets/_RPG/Prefabs/Enemies/TreeAnomaly.controller";
    const string PrefabPath = "Assets/_RPG/Prefabs/Enemies/TreeAnomaly.prefab";
    const string MaterialPath = "Assets/_RPG/Materials/TreeAnomaly_URP.mat";

    [MenuItem("RPG/Enemies/Setup Tree Anomaly")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[TreeAnomalySetup] Ignorado durante Play Mode.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        EnsureFolders();
        ConfigureAnimationRigs();
        GameObject prefab = BuildPrefab();
        if (prefab == null) return;

        PlaceSceneInstance(prefab);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[TreeAnomalySetup] Anomalia del Arbol lista en la escena.");
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Prefabs"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Prefabs/Enemies"))
            AssetDatabase.CreateFolder("Assets/_RPG/Prefabs", "Enemies");
    }

    static GameObject BuildPrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbx);
        if (source == null)
        {
            Debug.LogError("[TreeAnomalySetup] No se encontro " + CharacterFbx);
            return null;
        }

        // Physics and AI live on a stable outer root. Generic Meshy clips are
        // allowed to animate the visual hierarchy, but can never overwrite the
        // enemy's real world position or terrain grounding.
        GameObject instance = new GameObject("TreeAnomaly");
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
        visual.name = "TreeAnomaly_Visual";
        visual.transform.SetParent(instance.transform, false);

        Animator animator = visual.GetComponent<Animator>();
        if (animator == null) animator = visual.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.avatar = null;
        animator.runtimeAnimatorController = BuildController();

        instance.tag = "Enemy";
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            instance.layer = enemyLayer;
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = enemyLayer;
        }

        // The FBX's embedded material is not reliable after prefab instantiation. Apply the
        // project material explicitly so spawned anomalies keep their Meshy texture.
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;

        CapsuleCollider capsule = instance.GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = instance.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.95f, 0f);
        capsule.height = 1.9f;
        capsule.radius = 0.4f;

        EnemyStats stats = instance.GetComponent<EnemyStats>();
        if (stats == null) stats = instance.AddComponent<EnemyStats>();
        SetSerialized(stats, "maxHealth", 80f);
        SetSerialized(stats, "attack", 10f);
        SetSerialized(stats, "xpReward", 22);
        SetSerialized(stats, "minGoldDrop", 5);
        SetSerialized(stats, "maxGoldDrop", 12);

        // NavMeshAgent removed, same as SkeletonEnemy/goblins - this project's enemies drive
        // movement through EnemyAI's own manual-move + obstacle-avoidance path, not a baked
        // NavMesh, so an agent component would just sit there unused (or fight EnemyAI's own
        // transform writes if it happened to be on a mesh).
        NavMeshAgentRemoveIfPresent(instance);

        EnemyAI ai = instance.GetComponent<EnemyAI>();
        if (ai == null) ai = instance.AddComponent<EnemyAI>();
        SetSerialized(ai, "detectionRadius", 10f);
        SetSerialized(ai, "leashRadius", 15f);
        SetSerialized(ai, "attackRadius", 1.7f);
        SetSerialized(ai, "playerMask", LayerMask.GetMask("Player"));
        SetSerialized(ai, "wanderRadius", 6f);
        SetSerialized(ai, "wanderSpeed", 1.2f);
        SetSerialized(ai, "chaseWalkSpeed", 1.9f);
        SetSerialized(ai, "chaseRunSpeed", 3.6f);
        // Native leg-cycle pace of the Walking/Running clips at anim.speed=1 - without these,
        // EnemyAI.SetAnimatorSpeed() leaves anim.speed locked at 1x regardless of actual travel
        // speed, causing the same skating/sliding bug King Goblin and the goblins had before.
        SetSerialized(ai, "walkAnimSpeed", 1.2f);
        SetSerialized(ai, "runAnimSpeed", 2.4f);
        SetSerialized(ai, "runDistance", 6f);
        SetSerialized(ai, "attackCooldown", 1.6f);
        SetSerialized(ai, "attackDamageDelay", 0.5f);
        SetSerialized(ai, "obstacleMask", ~LayerMask.GetMask("Enemy", "Player", "Interactable"));

        Rigidbody rb = instance.GetComponent<Rigidbody>();
        if (rb == null) rb = instance.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        return saved;
    }

    static void NavMeshAgentRemoveIfPresent(GameObject go)
    {
        var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) Object.DestroyImmediate(agent);
    }

    static RuntimeAnimatorController BuildController()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("RightAttack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("LeftAttack", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimationClip idle = LoadClip("Animation_Idle_03_withSkin", "Armature|Armature|Idle_03|baselayer");
        AnimationClip walk = LoadClip("Animation_Walking_withSkin", "Armature|Armature|walking_man|baselayer");
        AnimationClip run = LoadClip("Animation_Running_withSkin", "Armature|Armature|running|baselayer");
        AnimationClip swordSlash = LoadClip("Animation_Right_Hand_Sword_Slash_withSkin", "Armature|Armature|Right_Hand_Sword_Slash|baselayer");
        AnimationClip chargedSlash = LoadClip("Animation_Charged_Upward_Slash_withSkin", "Armature|Armature|Charged_Upward_Slash|baselayer");

        AnimatorState idleState = sm.AddState("Idle", new Vector3(240f, 0f, 0f));
        idleState.motion = idle;
        sm.defaultState = idleState;

        AnimatorState walkState = sm.AddState("Walk", new Vector3(240f, 80f, 0f));
        walkState.motion = walk;
        AnimatorState runState = sm.AddState("Run", new Vector3(240f, 160f, 0f));
        runState.motion = run;

        AnimatorState rightAttackState = sm.AddState("RightAttack", new Vector3(520f, 40f, 0f));
        rightAttackState.motion = swordSlash;
        AnimatorState leftAttackState = sm.AddState("LeftAttack", new Vector3(520f, 120f, 0f));
        leftAttackState.motion = chargedSlash;

        AddFloatTransition(idleState, walkState, "Speed", AnimatorConditionMode.Greater, 0.15f);
        AddFloatTransition(walkState, idleState, "Speed", AnimatorConditionMode.Less, 0.15f);
        AddFloatTransition(walkState, runState, "Speed", AnimatorConditionMode.Greater, 0.75f);
        AddFloatTransition(runState, walkState, "Speed", AnimatorConditionMode.Less, 0.75f);
        AddFloatTransition(idleState, runState, "Speed", AnimatorConditionMode.Greater, 0.75f);
        AddFloatTransition(runState, idleState, "Speed", AnimatorConditionMode.Less, 0.15f);

        AddTriggerTransition(sm, rightAttackState, "RightAttack");
        AddTriggerTransition(sm, leftAttackState, "LeftAttack");
        AddReturnTransition(rightAttackState, idleState, 0.85f);
        AddReturnTransition(leftAttackState, idleState, 0.85f);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static AnimationClip LoadClip(string fileSuffix, string preferredName)
    {
        string path = SourceFolder + "Meshy_AI_Quiero_que_hagas_una__biped_" + fileSuffix + ".fbx";
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
            if (asset is AnimationClip clip && clip.name == preferredName)
                return clip;

        foreach (Object asset in assets)
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;

        Debug.LogWarning("[TreeAnomalySetup] Clip not found in " + path);
        return null;
    }

    static void ConfigureAnimationRigs()
    {
        ConfigureGenericModel(CharacterFbx, false);
        ConfigureGenericClip("Animation_Idle_03_withSkin", true);
        ConfigureGenericClip("Animation_Walking_withSkin", true);
        ConfigureGenericClip("Animation_Running_withSkin", true);
        ConfigureGenericClip("Animation_Right_Hand_Sword_Slash_withSkin", false);
        ConfigureGenericClip("Animation_Charged_Upward_Slash_withSkin", false);
    }

    static void ConfigureGenericClip(string fileSuffix, bool loop)
    {
        string path = SourceFolder + "Meshy_AI_Quiero_que_hagas_una__biped_" + fileSuffix + ".fbx";
        ConfigureGenericModel(path, loop);
    }

    static void ConfigureGenericModel(string path, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;

        bool importerChanged = importer.animationType != ModelImporterAnimationType.Generic ||
            importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.sourceAvatar = null;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        bool changed = importerChanged;
        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation clip = clips[i];
            if (clip.loopTime != loop)
            {
                clip.loopTime = loop;
                clips[i] = clip;
                changed = true;
            }
        }
        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
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
        transition.duration = 0.15f;
    }

    static void PlaceSceneInstance(GameObject prefab)
    {
        GameObject existing = GameObject.Find("TreeAnomaly");
        Vector3 position = existing != null ? existing.transform.position : FindSpawnPosition();

        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "TreeAnomaly";
        instance.transform.SetPositionAndRotation(position, Quaternion.identity);
        EditorUtility.SetDirty(instance);
    }

    static Vector3 FindSpawnPosition()
    {
        Vector3 position = new Vector3(18f, 0f, -12f);
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        return position;
    }

    static void SetSerialized(Object target, string propertyName, float value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop != null) prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, int value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop != null) prop.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, LayerMask value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop != null) prop.intValue = value.value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

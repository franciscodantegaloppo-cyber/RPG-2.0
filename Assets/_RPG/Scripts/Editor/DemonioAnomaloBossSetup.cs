using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Converts the imported scene object named "demonio_anomalo" into a reusable boss prefab.
// It intentionally reuses CrabDemonBossAI: that controller is the project's battle-tested
// capsule movement/obstacle avoidance implementation and continuously matches animation pace
// to actual travel speed, preventing the skating/surfing caused by root-motion mismatches.
public static class DemonioAnomaloBossSetup
{
    const string ControllerPath = "Assets/_RPG/Prefabs/Enemies/DemonioAnomaloBoss.controller";
    const string PrefabPath = "Assets/_RPG/Prefabs/Enemies/DemonioAnomaloBoss.prefab";
    const string WalkAnimationPath = "Assets/MeshyImports/Meshy_Model_20260716_192032/Meshy_AI_Crea_una_animalia_hum_biped_Animation_Walking_withSkin.fbx";
    const string OriginalMaterialPath = "Assets/MeshyImports/Meshy_Model_20260716_192032/Material_1.mat";

    [MenuItem("RPG/Enemies/Setup Demonio Anomalo Boss")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[DemonioAnomaloBossSetup] Sal de Play Mode antes de configurarlo.");
            return;
        }

        GameObject boss = FindBossObject();
        if (boss == null)
        {
            Debug.LogError("[DemonioAnomaloBossSetup] Selecciona el objeto 'demonio_anomalo' en la jerarquia y vuelve a ejecutar RPG > Enemies > Setup Demonio Anomalo Boss.");
            return;
        }

        ConfigureBoss(boss);
        PrefabUtility.SaveAsPrefabAsset(boss, PrefabPath);
        EditorSceneManager.MarkSceneDirty(boss.scene);
        EditorSceneManager.SaveScene(boss.scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[DemonioAnomaloBossSetup] Demonio Anomalo configurado: 1000 vida, 100 ataque, deteccion de 15 m y bolas de fuego rojas gigantes.");
    }

    static GameObject FindBossObject()
    {
        if (Selection.activeGameObject != null)
            return Selection.activeGameObject;

        GameObject namedObject = GameObject.Find("demonio_anomalo");
        if (namedObject != null)
            return namedObject;
        return GameObject.Find("DemonioAnomalo");
    }

    static void ConfigureBoss(GameObject boss)
    {
        boss.name = "DemonioAnomaloBoss";
        boss.tag = "Enemy";
        SetLayerRecursively(boss.transform, LayerMask.NameToLayer("Enemy"));

        // Keep the exact scale placed in the map. The encounter configuration must never resize
        // an authored scene model, so the boss's collision capsule is fitted to that scale below.

        RemoveIncompatibleAI(boss);

        Animator animator = boss.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = boss.AddComponent<Animator>();
        // The anomaly is a Generic Meshy rig, so Humanoid clips used by the crab boss could not
        // animate it (its Animator had no humanoid Avatar). Its own matching Generic walking
        // clip has the same bone hierarchy and therefore plays correctly on this model.
        animator.runtimeAnimatorController = BuildMovementController();
        animator.applyRootMotion = false;
        RestoreOriginalMaterial(boss);

        CapsuleCollider capsule = boss.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = boss.AddComponent<CapsuleCollider>();
        FitCapsuleToVisuals(boss, capsule);

        EnemyStats stats = boss.GetComponent<EnemyStats>();
        if (stats == null)
            stats = boss.AddComponent<EnemyStats>();
        Set(stats, "maxHealth", 1000f);
        Set(stats, "attack", 100f);
        Set(stats, "creatureType", (int)CreatureType.Boss);
        Set(stats, "xpReward", 750);
        Set(stats, "minGoldDrop", 100);
        Set(stats, "maxGoldDrop", 200);

        CrabDemonBossAI ai = boss.GetComponent<CrabDemonBossAI>();
        if (ai == null)
            ai = boss.AddComponent<CrabDemonBossAI>();
        Set(ai, "detectionRadius", 15f);
        Set(ai, "loseDetectionRadius", 50f);
        Set(ai, "attackRadius", 2.4f);
        // CrabDemonBossAI applies the Anomaly's 2x scale at runtime, keeping the serialized
        // values as its heavy base stride and avoiding a second multiplication on re-setup.
        Set(ai, "walkSpeed", 0.65f);
        Set(ai, "runSpeed", 1.55f);
        Set(ai, "walkAnimSpeed", 0.65f);
        Set(ai, "runAnimSpeed", 1.55f);
        Set(ai, "ignoreTerrainInObstacleProbe", true);
        Set(ai, "forceTransformMovement", true);
        Set(ai, "directRushChance", 0.22f);
        Set(ai, "directRushDecisionInterval", 3.5f);
        Set(ai, "allowMeleeAttacks", true);
        Set(ai, "fireballMinRange", 8f);
        // Heavy projectiles remain slow, but the demon pressures the player consistently.
        Set(ai, "fireballCooldown", 1.8f);
        Set(ai, "fireballVisualScale", 2.8f);
        Set(ai, "fireballSpeed", 5.25f);
        Set(ai, "fireballHitRadius", 2.1f);
        Set(ai, "fireballLeavesGroundFire", true);
        Set(ai, "groundFireDuration", 5f);
        Set(ai, "groundFireDamagePerTick", 25f);
        Set(ai, "allowJumping", false);

        Transform spawn = boss.transform.Find("FireballSpawn");
        if (spawn == null)
        {
            GameObject point = new GameObject("FireballSpawn");
            spawn = point.transform;
            spawn.SetParent(boss.transform, false);
        }
        Bounds bounds = GetVisualBounds(boss);
        spawn.position = bounds.center + Vector3.up * (bounds.extents.y * 0.3f) + boss.transform.forward * (capsule.radius + 0.7f);
        Set(ai, "fireballSpawnPoint", spawn);

        SkeletonEnemyAnimatorFX bob = boss.GetComponent<SkeletonEnemyAnimatorFX>();
        if (bob == null)
            bob = boss.AddComponent<SkeletonEnemyAnimatorFX>();
        Set(bob, "enableWalkBob", true);
        Set(bob, "walkBobAmplitude", 0.09f);
        Set(bob, "walkBobCyclesPerSecond", 1.05f);

        DemonioAnomaloFootsteps footsteps = boss.GetComponent<DemonioAnomaloFootsteps>();
        if (footsteps == null)
            footsteps = boss.AddComponent<DemonioAnomaloFootsteps>();
        Set(footsteps, "stepDistance", 1.4f);
        Set(footsteps, "shakeStrength", 0.075f);
        Set(footsteps, "shakeDuration", 0.11f);

        if (boss.GetComponent<DemonioAnomaloEncounterEffects>() == null)
            boss.AddComponent<DemonioAnomaloEncounterEffects>();
    }

    static RuntimeAnimatorController BuildMovementController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        foreach (AnimatorControllerParameter parameter in controller.parameters.Clone() as AnimatorControllerParameter[])
            controller.RemoveParameter(parameter);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState state in stateMachine.states.Clone() as ChildAnimatorState[])
            stateMachine.RemoveState(state.state);
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.Clone() as AnimatorStateTransition[])
            stateMachine.RemoveAnyStateTransition(transition);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        AnimationClip walk = LoadWalkClip();
        AnimatorState idle = stateMachine.AddState("Idle", new Vector3(220f, 80f, 0f));
        AnimatorState walking = stateMachine.AddState("Walk", new Vector3(440f, 80f, 0f));
        walking.motion = walk;
        stateMachine.defaultState = idle;

        AnimatorStateTransition toWalk = idle.AddTransition(walking);
        toWalk.hasExitTime = false;
        toWalk.duration = 0.1f;
        toWalk.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");
        AnimatorStateTransition toIdle = walking.AddTransition(idle);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.12f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimationClip LoadWalkClip()
    {
        ModelImporter importer = AssetImporter.GetAtPath(WalkAnimationPath) as ModelImporter;
        if (importer != null)
        {
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = true;
                clips[i].loopPose = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(WalkAnimationPath))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;

        Debug.LogError("[DemonioAnomaloBossSetup] No se encontro la animacion de caminata del demonio.");
        return null;
    }

    static void RemoveIncompatibleAI(GameObject boss)
    {
        foreach (MonoBehaviour behaviour in boss.GetComponents<MonoBehaviour>())
        {
            if (behaviour == null || behaviour is EnemyStats || behaviour is CrabDemonBossAI)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName == "EnemyAI" || typeName == "KingGoblinBossAI" || typeName == "SuperiorTreeAnomalyAI" ||
                typeName == "TreeAnomalyAI" || typeName == "AnimalAI")
                Object.DestroyImmediate(behaviour);
        }
    }

    static Bounds GetVisualBounds(GameObject boss)
    {
        Renderer[] renderers = boss.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(boss.transform.position + Vector3.up * 2f, new Vector3(2f, 4f, 2f));

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    public static void RestoreOriginalMaterial(GameObject boss)
    {
        Material original = AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterialPath);
        if (original == null)
        {
            Debug.LogWarning("[DemonioAnomalo] No se encontro el material original del modelo.");
            return;
        }
        foreach (Renderer renderer in boss.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer) continue;
            renderer.sharedMaterial = original;
        }
    }

    static void FitCapsuleToVisuals(GameObject boss, CapsuleCollider capsule)
    {
        Bounds bounds = GetVisualBounds(boss);
        float scale = Mathf.Max(0.01f, boss.transform.lossyScale.y);
        capsule.center = boss.transform.InverseTransformPoint(bounds.center);
        capsule.height = Mathf.Max(2f, bounds.size.y / scale);
        capsule.radius = Mathf.Max(0.5f, Mathf.Min(bounds.size.x, bounds.size.z) / scale * 0.3f);
        capsule.isTrigger = true;
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        if (layer < 0) return;
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }

    static void Set(Object target, string propertyName, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Set(Object target, string propertyName, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            if (property.propertyType == SerializedPropertyType.Enum) property.enumValueIndex = value;
            else property.intValue = value;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Set(Object target, string propertyName, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Set(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

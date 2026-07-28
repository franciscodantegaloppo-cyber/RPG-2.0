using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class SkeletonEnemySetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string SourcePrefabPath = "Assets/Skeleton/Prefab/Skeleton.prefab";
    const string SourceControllerPath = "Assets/Skeleton/FBX/Skeleton.controller";
    const string SourceTexturePath = "Assets/Skeleton/Textures/SceletonVersion3.tga";
    const string CombatControllerPath = "Assets/_RPG/Animations/SkeletonEnemyCombat.controller";
    const string SkeletonSwordPath = "Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 3 COLOR 1.prefab";
    const string EnemyPrefabPath = "Assets/_RPG/Prefabs/Enemies/SkeletonEnemy.prefab";
    const string EnemyMaterialPath = "Assets/_RPG/Materials/SkeletonEnemy_URP.mat";
    const string RequestPath = "Assets/_RPG/Generated/SkeletonEnemySetup.generate";
    const string CleanupRequestPath = "Assets/_RPG/Generated/CleanupDuplicateSkeletonEnemies.generate";

    [MenuItem("RPG/Enemies/Setup Skeleton Enemy")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[SkeletonEnemySetup] Ignorado durante Play Mode. Sali de Play para ejecutar el setup.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        EnsureFolders();
        // The combat clips are Humanoid, so the rendered skeleton and all of its native
        // locomotion clips must share one valid Humanoid avatar. Converting only the
        // rendered model (the previous broken setup) made the Generic walk curves fail.
        const string skeletonModelPath = "Assets/Skeleton/FBX/Skeleton.fbx";
        EnsureHumanoidRig(skeletonModelPath);
        Avatar skeletonAvatar = LoadAvatar(skeletonModelPath);
        EnsureHumanoidRig("Assets/Skeleton/FBX/IDLE.fbx", skeletonAvatar);
        EnsureHumanoidRig("Assets/Skeleton/FBX/Walk.fbx", skeletonAvatar);
        EnsureHumanoidRig("Assets/Skeleton/FBX/Run.fbx", skeletonAvatar);
        EnsureCombatController();
        Material material = EnsureMaterial();
        GameObject prefab = BuildPrefab(material);
        CleanupDuplicateSceneSkeletons();
        PlaceSceneInstance(prefab);
        ConfigurePlayerCombatMask();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SkeletonEnemySetup] Skeleton enemy prefab, sword, drops and scene instance configured.");
    }

    [InitializeOnLoadMethod]
    static void RunRequestedSetup()
    {
        EditorApplication.delayCall += TryRunRequestedSetup;
        EditorApplication.delayCall += TryRunRequestedCleanup;
    }

    static void TryRunRequestedSetup()
    {
        string absoluteRequestPath = Path.GetFullPath(RequestPath);
        if (!File.Exists(absoluteRequestPath))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += TryRunRequestedSetup;
            return;
        }

        File.Delete(absoluteRequestPath);
        string metaPath = absoluteRequestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        Setup();
    }

    [MenuItem("RPG/Enemies/Cleanup Duplicate Skeleton Enemies")]
    public static void CleanupDuplicateSceneSkeletons()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        System.Collections.Generic.List<GameObject> skeletons = new System.Collections.Generic.List<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (!IsSceneSkeletonEnemy(obj))
                continue;

            Transform parent = obj.transform.parent;
            bool hasSkeletonParent = false;
            while (parent != null)
            {
                if (IsSceneSkeletonEnemy(parent.gameObject))
                {
                    hasSkeletonParent = true;
                    break;
                }
                parent = parent.parent;
            }

            if (!hasSkeletonParent)
                skeletons.Add(obj);
        }

        if (skeletons.Count <= 1)
        {
            Debug.Log("[SkeletonEnemySetup] Skeleton cleanup: " + skeletons.Count + " scene skeleton enemy found.");
            return;
        }

        GameObject keep = null;
        int bestScore = int.MinValue;
        foreach (GameObject skeleton in skeletons)
        {
            int score = ScoreSkeletonEnemy(skeleton);
            if (score > bestScore)
            {
                bestScore = score;
                keep = skeleton;
            }
        }

        int removed = 0;
        foreach (GameObject skeleton in skeletons)
        {
            if (skeleton == keep)
                continue;

            Debug.Log("[SkeletonEnemySetup] Removing duplicate skeleton enemy: " + GetPath(skeleton) + " score=" + ScoreSkeletonEnemy(skeleton));
            Object.DestroyImmediate(skeleton);
            removed++;
        }

        if (keep != null)
        {
            keep.name = "SkeletonEnemy";
            EditorUtility.SetDirty(keep);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SkeletonEnemySetup] Skeleton cleanup kept: " + (keep != null ? GetPath(keep) : "none") + " removed=" + removed);
    }

    static void TryRunRequestedCleanup()
    {
        string absoluteRequestPath = Path.GetFullPath(CleanupRequestPath);
        if (!File.Exists(absoluteRequestPath))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += TryRunRequestedCleanup;
            return;
        }

        File.Delete(absoluteRequestPath);
        string metaPath = absoluteRequestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        CleanupDuplicateSceneSkeletons();
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Prefabs"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Prefabs/Enemies"))
            AssetDatabase.CreateFolder("Assets/_RPG/Prefabs", "Enemies");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Materials"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Animations"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Animations");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Generated"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Generated");
    }

    static Material EnsureMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(EnemyMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (material == null)
        {
            material = new Material(shader);
            material.name = "SkeletonEnemy_URP";
            AssetDatabase.CreateAsset(material, EnemyMaterialPath);
        }
        else if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(SourceTexturePath);
        if (texture != null)
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(0.86f, 0.82f, 0.72f, 1f));
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.18f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(material);
        return material;
    }

    static GameObject BuildPrefab(Material material)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (source == null)
        {
            Debug.LogError("[SkeletonEnemySetup] Source skeleton prefab not found: " + SourcePrefabPath);
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = "SkeletonEnemy";
        ConfigureEnemyObject(instance, material);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, EnemyPrefabPath);
        Object.DestroyImmediate(instance);
        return saved;
    }

    static void ConfigureEnemyObject(GameObject enemy, Material material)
    {
        enemy.tag = "Enemy";
        SetLayerRecursive(enemy, LayerMask.NameToLayer("Enemy"));

        Animator animator = enemy.GetComponentInChildren<Animator>(true);
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CombatControllerPath);
        if (animator != null)
        {
            if (controller != null)
                animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            if (animator.gameObject.GetComponent<NPCAnimationEvents>() == null)
                animator.gameObject.AddComponent<NPCAnimationEvents>();
        }

        foreach (Renderer renderer in enemy.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.GetComponentInParent<SkeletonWeaponMarker>() != null)
                continue;
            renderer.sharedMaterial = material;
        }

        EnsureSword(enemy, animator);

        CapsuleCollider capsule = enemy.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = enemy.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.95f, 0f);
        capsule.height = 1.9f;
        capsule.radius = 0.38f;
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
        SetSerialized(stats, "maxHealth", 55f);
        SetSerialized(stats, "attack", 9f);
        SetSerialized(stats, "armor", 20f);
        SetSerialized(stats, "creatureType", (int)CreatureType.Undead);
        SetSerialized(stats, "xpReward", 30);

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai == null)
            ai = enemy.AddComponent<EnemyAI>();
        SetSerialized(ai, "detectionRadius", 12f);
        SetSerialized(ai, "leashRadius", 20f);
        SetSerialized(ai, "attackRadius", 1.55f);
        SetSerialized(ai, "playerMask", new LayerMask { value = LayerMask.GetMask("Player") });
        SetSerialized(ai, "wanderRadius", 7f);
        SetSerialized(ai, "wanderSpeed", 1.35f);
        SetSerialized(ai, "chaseWalkSpeed", 2.15f);
        SetSerialized(ai, "chaseRunSpeed", 4.15f);
        SetSerialized(ai, "runDistance", 7f);
        SetSerialized(ai, "attackCooldown", 1.45f);
        SetSerialized(ai, "attackDamageDelay", 0.42f);
        SetSerialized(ai, "walkAnimSpeed", 1.35f);
        SetSerialized(ai, "runAnimSpeed", 4.15f);
        SetSerialized(ai, "obstacleProbeDistance", 0.9f);
        SetSerialized(ai, "obstacleSideStep", 1.35f);
        SetSerialized(ai, "obstacleMask", new LayerMask { value = ~LayerMask.GetMask("Enemy", "Player", "Interactable") });

        if (enemy.GetComponent<SkeletonEnemyAnimatorFX>() == null)
            enemy.AddComponent<SkeletonEnemyAnimatorFX>();

        // The native skeleton clips move the rendered rig slightly below its root.
        // Keep the AI root grounded and compensate only the visual child in LateUpdate,
        // which avoids both underground feet and the old root-motion surfing regression.
        NPCVisualGroundAligner groundAligner =
            enemy.GetComponent<NPCVisualGroundAligner>();
        if (groundAligner == null)
            groundAligner = enemy.AddComponent<NPCVisualGroundAligner>();
        SetSerialized(groundAligner, "groundOffset", .025f);
        SetSerialized(groundAligner, "maxCorrectionPerFrame", .65f);

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
        AnimationClip idle = LoadClip("Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations/2Hand-Sword/RPG-Character@2Hand-Sword-Idle.FBX", "2Hand-Sword-Idle");
        AnimationClip walk = LoadClip("Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations/2Hand-Sword/RPG-Character@2Hand-Sword-Walk-Slow.FBX", "2Hand-Sword-Walk-Slow");
        AnimationClip run = LoadClip("Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations/2Hand-Sword/RPG-Character@2Hand-Sword-Run-Forward.FBX", "2Hand-Sword-Run-Forward");
        AnimationClip attack = LoadClip("Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations/2Hand-Sword/RPG-Character@2Hand-Sword-Attack1.FBX", "2Hand-Sword-Attack1");
        AnimationClip attackAlt = LoadClip("Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations/2Hand-Sword/RPG-Character@2Hand-Sword-Attack3.FBX", "2Hand-Sword-Attack3");
        AnimationClip hit = LoadClip("Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations/2Hand-Sword/RPG-Character@2Hand-Sword-GetHit-F1.FBX", "2Hand-Sword-GetHit-F1");
        AnimationClip death = LoadClip("Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations/2Hand-Sword/RPG-Character@2Hand-Sword-Knockdown1.FBX", "2Hand-Sword-Knockdown1");

        AnimatorState idleState = sm.AddState("Idle", new Vector3(250f, 80f, 0f));
        idleState.motion = idle;
        sm.defaultState = idleState;

        AnimatorState walkState = sm.AddState("Walk", new Vector3(250f, 180f, 0f));
        walkState.motion = walk;
        AnimatorState runState = sm.AddState("Run", new Vector3(250f, 280f, 0f));
        runState.motion = run;
        AnimatorState attackState = sm.AddState("Attack", new Vector3(560f, 80f, 0f));
        attackState.motion = attack;
        attackState.speed = 1.05f;
        AnimatorState attackAltState = sm.AddState("AttackAlt", new Vector3(560f, 180f, 0f));
        attackAltState.motion = attackAlt != null ? attackAlt : attack;
        attackAltState.speed = 1.05f;
        AnimatorState hitState = sm.AddState("Hit", new Vector3(560f, 280f, 0f));
        hitState.motion = hit;
        hitState.speed = 1.1f;
        AnimatorState deathState = sm.AddState("Death", new Vector3(560f, 380f, 0f));
        deathState.motion = death != null ? death : hit;
        deathState.speed = 0.95f;

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

        AddReturnTransition(attackState, idleState, 0.82f);
        AddReturnTransition(attackAltState, idleState, 0.82f);
        AddReturnTransition(hitState, idleState, 0.78f);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static AnimationClip LoadClip(string path, string preferredName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip && clip.name == preferredName)
                return clip;
        }

        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                return clip;
        }

        Debug.LogWarning("[SkeletonEnemySetup] Animation clip not found: " + path);
        return null;
    }

    static Avatar LoadAvatar(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Avatar avatar)
                return avatar;
        }

        return null;
    }

    static void EnsureHumanoidRig(string path, Avatar sourceAvatar = null)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return;

        bool alreadyConfigured = importer.animationType == ModelImporterAnimationType.Human &&
                                 (sourceAvatar == null ||
                                  (importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther &&
                                   importer.sourceAvatar == sourceAvatar));
        if (alreadyConfigured)
            return;

        importer.animationType = ModelImporterAnimationType.Human;
        if (sourceAvatar != null)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;
        }
        else
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }
        importer.SaveAndReimport();
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

    static void EnsureSword(GameObject enemy, Animator animator)
    {
        RemoveChildByName(enemy.transform, "Skeleton_2Hand_Sword");
        RemoveChildByName(enemy.transform, "Skeleton_Bone_Sword");

        GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonSwordPath);
        if (swordPrefab == null)
            return;

        Transform hand = null;
        if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null)
            hand = FindChildByName(enemy.transform, "Wrist_R") ??
                   FindChildByName(enemy.transform, "B_R_Hand") ??
                   FindChildByName(enemy.transform, "RightHand");
        if (hand == null)
            hand = enemy.transform;

        GameObject sword = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab);
        sword.name = "Skeleton_Bone_Sword";
        if (sword.GetComponent<SkeletonWeaponMarker>() == null)
            sword.AddComponent<SkeletonWeaponMarker>();
        sword.transform.SetParent(hand, false);

        // Put the hilt in the palm from the actual rig geometry rather than relying on an
        // arbitrary fixed offset. On this skeleton the finger base is a child of Wrist_R;
        // 62% of that segment lands in the closed palm and keeps the handle in the hand after
        // Humanoid retargeting as well.
        Transform middleFinger = animator != null && animator.avatar != null && animator.avatar.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal)
            : null;
        if (middleFinger == null)
            middleFinger = FindChildByName(hand, "MiddleFinger1_R");
        sword.transform.position = middleFinger != null
            ? Vector3.Lerp(hand.position, middleFinger.position, .62f)
            : hand.TransformPoint(new Vector3(-.102f, .008f, .002f));

        // The imported sword's long blade axis is local +Z. Keep that axis vertical in the
        // relaxed pose (tip/edge upward) while its broad face follows the skeleton's forward
        // direction. Because it remains a direct child of RightHand/Wrist_R, both attack clips
        // then carry the complete wrist arc instead of a script forcing a static rotation.
        sword.transform.rotation = Quaternion.LookRotation(Vector3.up, enemy.transform.forward);
        sword.transform.localScale = Vector3.one * 0.7f;

        sword.SetActive(true);
        foreach (Renderer renderer in sword.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            renderer.gameObject.SetActive(true);
        }

        foreach (Collider collider in sword.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
    }

    static void RemoveChildByName(Transform root, string contains)
    {
        Transform child = FindChildByName(root, contains);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }

    static Transform FindChildByName(Transform root, string contains)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return child;
        }

        return null;
    }

    static void PlaceSceneInstance(GameObject prefab)
    {
        if (prefab == null)
            return;

        GameObject existing = FindBestSceneSkeletonEnemy();
        if (existing != null)
        {
            ConfigureEnemyObject(existing, EnsureMaterial());
            existing.name = "SkeletonEnemy";
            EditorUtility.SetDirty(existing);
            return;
        }

        Vector3 position = new Vector3(16f, 0f, 18f);
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;

        GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        enemy.name = "SkeletonEnemy";
        enemy.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 210f, 0f));
        ConfigureEnemyObject(enemy, EnsureMaterial());
        EditorUtility.SetDirty(enemy);
    }

    static GameObject FindBestSceneSkeletonEnemy()
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        GameObject best = null;
        int bestScore = int.MinValue;
        foreach (GameObject obj in allObjects)
        {
            if (!IsSceneSkeletonEnemy(obj))
                continue;

            int score = ScoreSkeletonEnemy(obj);
            if (score > bestScore)
            {
                best = obj;
                bestScore = score;
            }
        }

        return best;
    }

    static bool IsSceneSkeletonEnemy(GameObject obj)
    {
        if (obj == null || !obj.scene.IsValid())
            return false;

        if (obj.GetComponent<EnemyAI>() != null && obj.name.IndexOf("Skeleton", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (obj.name.IndexOf("SkeletonEnemy", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    static int ScoreSkeletonEnemy(GameObject enemy)
    {
        int score = 0;
        if (enemy == null)
            return score;

        if (enemy.activeInHierarchy) score += 10;
        if (enemy.GetComponent<EnemyAI>() != null) score += 25;
        if (enemy.GetComponent<EnemyStats>() != null) score += 20;
        if (enemy.GetComponent<SkeletonEnemyAnimatorFX>() != null) score += 15;
        if (enemy.GetComponentInChildren<NPCAnimationEvents>(true) != null) score += 8;
        if (FindChildByName(enemy.transform, "Skeleton_Bone_Sword") != null || FindChildByName(enemy.transform, "Skeleton_2Hand_Sword") != null) score += 18;

        Animator animator = enemy.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            score += 8;
            if (animator.runtimeAnimatorController != null)
            {
                score += 18;
                string controllerPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                if (controllerPath == CombatControllerPath)
                    score += 35;
            }
        }

        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(enemy);
        if (source != null)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (sourcePath == EnemyPrefabPath)
                score += 30;
            if (sourcePath == SourcePrefabPath)
                score -= 20;
        }

        return score;
    }

    static string GetPath(GameObject obj)
    {
        if (obj == null)
            return "";

        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    static void ConfigurePlayerCombatMask()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        player.layer = LayerMask.NameToLayer("Player");
        CombatSystem combat = player.GetComponent<CombatSystem>();
        if (combat == null)
            return;

        SetSerialized(combat, "enemyMask", new LayerMask { value = LayerMask.GetMask("Enemy") });
        EditorUtility.SetDirty(combat);
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
}

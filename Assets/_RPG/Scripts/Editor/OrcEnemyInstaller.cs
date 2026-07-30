#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class OrcWarriorEnemySetup
{
    // Self-installs only while SpawnVillage is the active edit-mode scene.
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string ModelPath =
        "Assets/Guerrero_orco/" +
        "Meshy_AI_Warrior_Orc_King_biped_Character_output.fbx";
    const string AnimationPath =
        "Assets/Guerrero_orco/" +
        "Meshy_AI_Warrior_Orc_King_biped_Meshy_AI_Meshy_Merged_Animations.fbx";
    const string TexturePath =
        "Assets/Guerrero_orco/" +
        "Meshy_AI_Warrior_Orc_King_biped_texture_0.png";
    const string BoneSwordPath =
        "Assets/URP GanzSe Free Modular Character Pack/Prefabs/" +
        "ONE-HANDED SWORDS/FREE ONE HANDED SWORD 3 COLOR 1.prefab";
    const string MaterialPath =
        "Assets/_RPG/Materials/OrcWarrior_URP.mat";
    const string ControllerPath =
        "Assets/_RPG/Generated/Animations/OrcWarrior.controller";
    const string PrefabPath =
        "Assets/_RPG/Prefabs/Enemies/OrcWarriorEnemy.prefab";
    const string SceneObjectName = "Guerrero_Orco";
    const string MarkerName = "_OrcWarrior_Configured";
    const string SwordName = "Orc_Bone_Sword";
    const float DesiredHeight = 2.45f;
    static bool configuringImporters;

    static OrcWarriorEnemySetup()
    {
        EditorApplication.delayCall += EnsureInstalled;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += EnsureInstalled;
        };
    }

    [MenuItem("RPG/Enemies/Crear Guerrero Orco")]
    public static void EnsureInstalled()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            scene.path != ScenePath)
            return;

        GameObject source =
            AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (source == null)
        {
            Debug.LogWarning("[OrcWarrior] Modelo aun no importado: " +
                             ModelPath);
            return;
        }

        EnsureLoopingClips(AnimationPath);
        EnsureLoopingClips(ModelPath);
        EnsureFolders();
        Material material = EnsureMaterial();
        AnimatorController controller = EnsureController();
        if (material == null || controller == null)
            return;

        GameObject prefab = EnsurePrefab(source, material, controller);
        if (prefab == null)
            return;

        GameObject instance = FindSceneRoot(scene, SceneObjectName);
        if (instance == null)
        {
            instance =
                PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                instance = Object.Instantiate(prefab);
            instance.name = SceneObjectName;
            PlaceNearSpawn(instance);
            Ground(instance);
        }
        else
        {
            ConfigureRoot(instance, material, controller);
            if (instance.transform.Find(MarkerName) == null)
            {
                AddMarker(instance);
                Ground(instance);
            }
        }

        EditorUtility.SetDirty(instance);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = instance;
        SceneView.lastActiveSceneView?.FrameSelected();

        Animator animator = instance.GetComponentInChildren<Animator>(true);
        int clipCount = LoadImportedClips().Count;
        Debug.Log("[OrcWarrior] Instalado en SpawnVillage. Clips " +
                  "importados=" + clipCount +
                  ", rig=" +
                  (animator != null && animator.isHuman
                      ? "Humanoid"
                      : "Generic") +
                  ", posicion=" + instance.transform.position +
                  ". Deteccion a 20 m, persecucion persistente, golpes " +
                  "sincronizados, bloqueo y reacciones activos.");
    }

    static void EnsureFolders()
    {
        EnsureFolder("Assets/_RPG/Generated");
        EnsureFolder("Assets/_RPG/Generated/Animations");
        EnsureFolder("Assets/_RPG/Prefabs");
        EnsureFolder("Assets/_RPG/Prefabs/Enemies");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        string name = path.Substring(path.LastIndexOf('/') + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static Material EnsureMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[OrcWarrior] No se encontro URP/Lit.");
            return null;
        }

        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "OrcWarrior_URP"
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        Texture2D baseMap =
            AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        material.SetTexture("_BaseMap", baseMap);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", .08f);
        material.SetFloat("_Smoothness", .3f);
        material.enableInstancing = true;
        material.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    static AnimatorController EnsureController()
    {
        AnimatorController existing =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(
                ControllerPath);
        if (existing != null)
            return existing;

        List<AnimationClip> imported = LoadImportedClips();
        Debug.Log("[OrcWarrior] Animaciones encontradas: " +
                  (imported.Count > 0
                      ? string.Join(", ", imported.Select(c => c.name))
                      : "ninguna"));

        AnimationClip idle = FindClip(imported, "idle", "stand");
        AnimationClip walk = FindClip(imported, "walk");
        AnimationClip run = FindClip(imported, "run", "sprint");
        List<AnimationClip> attacks = FindClips(imported,
            "attack", "slash", "swing", "strike");
        AnimationClip hit = FindClip(imported,
            "damage", "gethit", "hurt", "impact", "reaction");
        AnimationClip death = FindClip(imported,
            "death", "die", "dying", "knockdown",
            "shotandfall", "fallbackward", "fallforward");
        AnimationClip block = FindClip(imported,
            "block", "guard", "parry");
        AnimationClip dodge = FindClip(imported,
            "dodge", "roll", "evade", "backstep",
            "backjump");

        idle ??= LoadFirstClip(
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/" +
            "Animations/2Hand-Sword/" +
            "RPG-Character@2Hand-Sword-Idle.FBX");
        walk ??= LoadFirstClip(
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/" +
            "Animations/2Hand-Sword/" +
            "RPG-Character@2Hand-Sword-Walk-Slow.FBX");
        run ??= LoadFirstClip(
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/" +
            "Animations/2Hand-Sword/" +
            "RPG-Character@2Hand-Sword-Run-Forward.FBX");
        AnimationClip attack = attacks.FirstOrDefault() ??
            LoadFirstClip(
                "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/" +
                "Animations/2Hand-Sword/" +
                "RPG-Character@2Hand-Sword-Attack1.FBX");
        AnimationClip attackAlt = attacks.Skip(1).FirstOrDefault() ??
            LoadFirstClip(
                "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/" +
                "Animations/2Hand-Sword/" +
                "RPG-Character@2Hand-Sword-Attack3.FBX") ??
            attack;
        hit ??= LoadFirstClip(
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/" +
            "Animations/2Hand-Sword/" +
            "RPG-Character@2Hand-Sword-GetHit-F1.FBX");
        death ??= LoadFirstClip(
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/" +
            "Animations/2Hand-Sword/" +
            "RPG-Character@2Hand-Sword-Knockdown1.FBX");
        dodge ??= LoadFirstClip(
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/" +
            "Animations/2Hand-Sword/" +
            "RPG-Character@2Hand-Sword-DiveRoll-Forward1.max.FBX");
        block ??= hit;

        if (idle == null || walk == null || run == null ||
            attack == null || death == null)
        {
            Debug.LogError("[OrcWarrior] Faltan clips esenciales para " +
                           "construir el controlador.");
            return null;
        }

        AnimatorController controller =
            AnimatorController.CreateAnimatorControllerAtPath(
                ControllerPath);
        controller.AddParameter("Speed",
            AnimatorControllerParameterType.Float);
        controller.AddParameter("Combat",
            AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack",
            AnimatorControllerParameterType.Trigger);
        controller.AddParameter("AttackAlt",
            AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit",
            AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die",
            AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Block",
            AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dodge",
            AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idleState = AddState(sm, "Idle", idle, 240f, 80f);
        AnimatorState walkState = AddState(sm, "Walk", walk, 240f, 180f);
        AnimatorState runState = AddState(sm, "Run", run, 240f, 280f);
        AnimatorState attackState =
            AddState(sm, "Attack", attack, 560f, 80f);
        AnimatorState attackAltState =
            AddState(sm, "AttackAlt", attackAlt, 560f, 160f);
        AnimatorState hitState =
            AddState(sm, "Hit", hit, 560f, 240f);
        AnimatorState blockState =
            AddState(sm, "Block", block, 560f, 320f);
        AnimatorState dodgeState =
            AddState(sm, "Dodge", dodge, 560f, 400f);
        AnimatorState deathState =
            AddState(sm, "Death", death, 560f, 480f);

        sm.defaultState = idleState;
        AddSpeedTransition(idleState, walkState,
            AnimatorConditionMode.Greater, .15f);
        AddSpeedTransition(walkState, idleState,
            AnimatorConditionMode.Less, .15f);
        AddSpeedTransition(walkState, runState,
            AnimatorConditionMode.Greater, 3f);
        AddSpeedTransition(runState, walkState,
            AnimatorConditionMode.Less, 3f);
        AddSpeedTransition(idleState, runState,
            AnimatorConditionMode.Greater, 3f);
        AddSpeedTransition(runState, idleState,
            AnimatorConditionMode.Less, .15f);

        AddAnyTrigger(sm, attackState, "Attack", .06f);
        AddAnyTrigger(sm, attackAltState, "AttackAlt", .06f);
        AddAnyTrigger(sm, hitState, "Hit", .05f);
        AddAnyTrigger(sm, blockState, "Block", .04f);
        AddAnyTrigger(sm, dodgeState, "Dodge", .05f);
        AddAnyTrigger(sm, deathState, "Die", .04f);

        AddReturn(attackState, idleState, .82f);
        AddReturn(attackAltState, idleState, .84f);
        AddReturn(hitState, idleState, .78f);
        AddReturn(blockState, idleState, .82f);
        AddReturn(dodgeState, idleState, .9f);

        // Preserve every extra imported animation inside the controller as
        // an authored state. This keeps the full pack available for later
        // tactical extensions without losing the current reliable graph.
        int extraIndex = 0;
        HashSet<AnimationClip> used = new HashSet<AnimationClip>
        {
            idle, walk, run, attack, attackAlt, hit, death, block, dodge
        };
        foreach (AnimationClip clip in imported)
        {
            if (clip == null || used.Contains(clip))
                continue;
            AddState(sm, "Imported_" + SafeName(clip.name), clip,
                820f + (extraIndex / 8) * 190f,
                70f + (extraIndex % 8) * 70f);
            extraIndex++;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static GameObject EnsurePrefab(GameObject source,
        Material material, AnimatorController controller)
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            GameObject root = new GameObject("OrcWarriorEnemy");
            GameObject visual = Object.Instantiate(source);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            NormalizeVisual(root, visual);
            ConfigureRoot(root, material, controller);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        ConfigureRoot(contents, material, controller);
        PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    static void NormalizeVisual(GameObject root, GameObject visual)
    {
        Bounds bounds = VisualBounds(root);
        if (bounds.size.y <= .01f)
            return;
        visual.transform.localScale *= DesiredHeight / bounds.size.y;
        bounds = VisualBounds(root);
        visual.transform.position += Vector3.up * -bounds.min.y;
    }

    static void ConfigureRoot(GameObject root, Material material,
        RuntimeAnimatorController controller)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0)
            enemyLayer = 0;
        root.tag = "Enemy";
        foreach (Transform child in
                 root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = enemyLayer;

        foreach (Renderer renderer in
                 root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            if (slots == null || slots.Length == 0)
                slots = new Material[1];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = material;
            renderer.sharedMaterials = slots;
            renderer.enabled = true;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            // Meshy exports this model without an Animator component. The
            // imported clips are bound from Armature/... and char1, so the
            // Animator must live on Visual rather than on the enemy wrapper.
            Transform visual = root.transform.Find("Visual");
            GameObject animatorHost =
                visual != null ? visual.gameObject : root;
            animator = animatorHost.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = controller;
        Avatar avatar = LoadModelAvatar();
        if (avatar != null && avatar.isValid)
            animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode =
            AnimatorCullingMode.CullUpdateTransforms;
        EnsureBoneSword(root, animator);

        CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 1.18f, 0f);
        capsule.height = 2.35f;
        capsule.radius = .55f;
        capsule.isTrigger = false;

        Rigidbody body = root.GetComponent<Rigidbody>();
        if (body == null)
            body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = root.AddComponent<NavMeshAgent>();
        agent.radius = .52f;
        agent.height = 2.35f;
        agent.baseOffset = 0f;
        agent.speed = 4.25f;
        agent.angularSpeed = 520f;
        agent.acceleration = 16f;
        agent.stoppingDistance = 1.7f;
        agent.autoBraking = true;

        EnemyAI genericAI = root.GetComponent<EnemyAI>();
        if (genericAI != null)
            Object.DestroyImmediate(genericAI);

        EnemyStats stats = root.GetComponent<EnemyStats>();
        if (stats == null)
            stats = root.AddComponent<EnemyStats>();
        SetFloat(stats, "maxHealth", 185f);
        SetFloat(stats, "attack", 22f);
        SetFloat(stats, "armor", 12f);
        SetInt(stats, "creatureType", (int)CreatureType.Normal);
        SetInt(stats, "xpReward", 95);
        SetInt(stats, "minGoldDrop", 16);
        SetInt(stats, "maxGoldDrop", 28);
        SetFloat(stats, "runeDropChance", .28f);

        OrcWarriorAI ai = root.GetComponent<OrcWarriorAI>();
        if (ai == null)
            ai = root.AddComponent<OrcWarriorAI>();
        SetFloat(ai, "detectionRadius", 20f);

        NPCVisualGroundAligner aligner =
            root.GetComponent<NPCVisualGroundAligner>();
        if (aligner == null)
            aligner = root.AddComponent<NPCVisualGroundAligner>();

        AddMarker(root);
        EditorUtility.SetDirty(root);
    }

    static void AddMarker(GameObject root)
    {
        if (root.transform.Find(MarkerName) != null)
            return;
        GameObject marker = new GameObject(MarkerName);
        marker.transform.SetParent(root.transform, false);
        marker.hideFlags =
            HideFlags.HideInHierarchy | HideFlags.NotEditable;
    }

    static void PlaceNearSpawn(GameObject enemy)
    {
        Transform spawn = FindSpawn();
        Vector3 position = spawn != null
            ? spawn.position - spawn.right * 24f +
              spawn.forward * 31f
            : new Vector3(-24f, 0f, 31f);
        enemy.transform.SetPositionAndRotation(position,
            Quaternion.Euler(0f, 145f, 0f));
    }

    static Transform FindSpawn()
    {
        try
        {
            GameObject tagged =
                GameObject.FindGameObjectWithTag("SpawnPoint");
            if (tagged != null)
                return tagged.transform;
        }
        catch (UnityException) { }

        GameObject named = GameObject.Find("SpawnPoint") ??
                           GameObject.Find("PlayerSpawn");
        if (named != null)
            return named.transform;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    static void Ground(GameObject enemy)
    {
        Bounds bounds = VisualBounds(enemy);
        if (GroundUtility.TryProjectToGround(
                bounds.center, enemy.transform,
                out Vector3 grounded, 8f, 18f))
        {
            enemy.transform.position += Vector3.up *
                (grounded.y - bounds.min.y + .02f);
            return;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
            return;
        float ground = terrain.SampleHeight(enemy.transform.position) +
                       terrain.transform.position.y;
        enemy.transform.position += Vector3.up *
            (ground - bounds.min.y + .02f);
    }

    static List<AnimationClip> LoadImportedClips()
    {
        List<AnimationClip> clips = new List<AnimationClip>();
        AddClips(AnimationPath, clips);
        AddClips(ModelPath, clips);
        return clips
            .Where(c => c != null &&
                        !c.name.StartsWith("__preview__",
                            StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
    }

    static Avatar LoadModelAvatar()
    {
        foreach (Object asset in
                 AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            if (asset is Avatar avatar)
                return avatar;
        return null;
    }

    static void EnsureLoopingClips(string assetPath)
    {
        if (configuringImporters)
            return;
        ModelImporter importer =
            AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            return;

        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            string name = Normalized(clip.name);
            bool locomotion =
                name.Contains("idle") ||
                name.Contains("walk") ||
                name.Contains("running") ||
                name.Contains("runfast") ||
                name.Contains("runfast2");
            if (!locomotion || clip.loopTime)
                continue;
            clip.loopTime = true;
            clip.loopPose = true;
            changed = true;
        }
        if (!changed)
            return;

        configuringImporters = true;
        try
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
        finally
        {
            configuringImporters = false;
        }
    }

    static void EnsureBoneSword(GameObject root, Animator animator)
    {
        Transform existing = FindChild(root.transform, SwordName);
        if (existing != null)
            return;

        GameObject swordPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(BoneSwordPath);
        if (swordPrefab == null)
        {
            Debug.LogWarning(
                "[OrcWarrior] No se encontro la Espada de Hueso: " +
                BoneSwordPath);
            return;
        }

        Transform hand = null;
        if (animator != null && animator.isHuman)
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        hand ??= FindChild(root.transform, "RightHand");
        if (hand == null)
        {
            Debug.LogWarning(
                "[OrcWarrior] No se encontro RightHand para equipar " +
                "la Espada de Hueso.");
            return;
        }

        GameObject sword =
            PrefabUtility.InstantiatePrefab(swordPrefab) as GameObject;
        if (sword == null)
            sword = Object.Instantiate(swordPrefab);
        sword.name = SwordName;
        sword.transform.SetParent(hand, false);
        sword.transform.position =
            hand.TransformPoint(new Vector3(.018f, .055f, .005f));
        // This sword prefab uses local +Z as its blade axis.
        sword.transform.rotation =
            Quaternion.LookRotation(Vector3.up, root.transform.forward);
        sword.transform.localScale = Vector3.one * .82f;
        sword.SetActive(true);

        foreach (Renderer renderer in
                 sword.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            renderer.gameObject.SetActive(true);
        }
        foreach (Collider collider in
                 sword.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        foreach (Transform child in
                 sword.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = root.layer;
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in
                 root.GetComponentsInChildren<Transform>(true))
            if (string.Equals(child.name, name,
                    StringComparison.OrdinalIgnoreCase))
                return child;
        return null;
    }

    static void AddClips(string path, ICollection<AnimationClip> clips)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip clip)
                clips.Add(clip);
    }

    static AnimationClip FindClip(
        IEnumerable<AnimationClip> clips, params string[] terms)
    {
        return clips.FirstOrDefault(clip =>
        {
            string name = Normalized(clip.name);
            return terms.Any(term =>
                name.Contains(Normalized(term)));
        });
    }

    static List<AnimationClip> FindClips(
        IEnumerable<AnimationClip> clips, params string[] terms)
    {
        return clips.Where(clip =>
        {
            string name = Normalized(clip.name);
            return terms.Any(term =>
                name.Contains(Normalized(term)));
        }).ToList();
    }

    static string Normalized(string value)
    {
        return value.ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
    }

    static string SafeName(string value)
    {
        return new string(value.Select(character =>
            char.IsLetterOrDigit(character) || character == '_'
                ? character
                : '_').ToArray());
    }

    static AnimationClip LoadFirstClip(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is AnimationClip clip &&
                !clip.name.StartsWith("__preview__",
                    StringComparison.OrdinalIgnoreCase))
                return clip;
        }
        return null;
    }

    static AnimatorState AddState(AnimatorStateMachine sm,
        string name, Motion motion, float x, float y)
    {
        AnimatorState state = sm.AddState(name,
            new Vector3(x, y, 0f));
        state.motion = motion;
        state.speed = 1f;
        return state;
    }

    static void AddSpeedTransition(AnimatorState from,
        AnimatorState to, AnimatorConditionMode mode, float threshold)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = .14f;
        transition.AddCondition(mode, threshold, "Speed");
    }

    static void AddAnyTrigger(AnimatorStateMachine sm,
        AnimatorState target, string parameter, float duration)
    {
        AnimatorStateTransition transition =
            sm.AddAnyStateTransition(target);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(
            AnimatorConditionMode.If, 0f, parameter);
    }

    static void AddReturn(AnimatorState from,
        AnimatorState to, float exitTime)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = .12f;
    }

    static Bounds VisualBounds(GameObject root)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static GameObject FindSceneRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root != null && root.name == name)
                return root;
        return null;
    }

    static void SetFloat(Object target, string property, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty field = serialized.FindProperty(property);
        if (field != null)
            field.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetInt(Object target, string property, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty field = serialized.FindProperty(property);
        if (field != null)
            field.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

}
#endif

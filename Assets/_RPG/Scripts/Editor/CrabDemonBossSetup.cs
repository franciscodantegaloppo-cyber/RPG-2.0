using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Rebuilt to use the real Meshy asset the user provided (Assets/_RPG/Imported/Demonio cangrejo
// boss/), following the same reliable FBX-Humanoid-import pipeline as TreeAnomalySetup.cs,
// instead of the old fully-AI-generated GLB + manually-built AvatarBuilder avatar. That manual
// avatar (uncalibrated HumanLimits/twist values on an unknown bone hierarchy) is the likely cause
// of "hace animaciones de manera aleatoria" - a properly imported FBX Humanoid avatar (calibrated
// from the model's own T-pose by Unity's importer) retargets cleanly instead.
public static class CrabDemonBossSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string SourceFolder = "Assets/_RPG/Imported/Demonio cangrejo boss/";
    const string FilePrefix = "Meshy_AI_Demonic_Crab_Warrior_biped_";
    const string CharacterFbx = SourceFolder + FilePrefix + "Character_output.fbx";
    const string TexturePath = SourceFolder + FilePrefix + "texture_0.png";
    const string NormalPath = SourceFolder + FilePrefix + "texture_0_normal.png";
    const string MetallicPath = SourceFolder + FilePrefix + "texture_0_metallic.png";
    const string RoughnessPath = SourceFolder + FilePrefix + "texture_0_roughness.png";
    const string MaterialPath = "Assets/_RPG/Materials/CrabDemonBoss_URP.mat";
    const string ControllerPath = "Assets/_RPG/Prefabs/Enemies/CrabDemonBoss.controller";
    const string PrefabPath = "Assets/_RPG/Prefabs/Enemies/CrabDemonBoss.prefab";
    const string SceneObjectName = "CrabDemonBoss";

    [MenuItem("RPG/Enemies/Setup Crab Demon Boss")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CrabDemonBossSetup] Ignorado durante Play Mode.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        EnsureFolders();
        Material material = EnsureMaterial();
        RuntimeAnimatorController controller = BuildController();
        GameObject prefab = BuildPrefab(material, controller);
        if (prefab == null) return;

        PlaceSceneInstance(prefab);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CrabDemonBossSetup] Crab Demon boss reconstruido con el modelo real en la escena.");
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Prefabs"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Prefabs/Enemies"))
            AssetDatabase.CreateFolder("Assets/_RPG/Prefabs", "Enemies");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Materials"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Materials");
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
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbx);
        if (source == null)
        {
            Debug.LogError("[CrabDemonBossSetup] No se encontro el modelo: " + CharacterFbx);
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = SceneObjectName;

        // Fully disconnect from the FBX's Model Prefab before changing anything - leaving it
        // connected makes every field we set (Animator.controller, tag, scale, capsule, etc.)
        // get recorded as a nested-PrefabInstance override instead of a normal serialized value.
        // Those override records looked correct in the saved .prefab's YAML (right GUID, right
        // fileID) but Unity silently failed to resolve the Animator.controller override at both
        // edit-time and real Play Mode runtime - unpacking avoids that override machinery
        // entirely, producing a plain flat prefab like every other enemy in this project.
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        instance.tag = "Enemy";
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            instance.layer = enemyLayer;
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = enemyLayer;
        }

        // Real authored FBX imports at proper human-adjacent scale (~2.5m tall combined bounds) -
        // no scale hacks needed like the old AI-generated GLB required. A modest boost still gives
        // it boss presence over the ~1.8m player.
        instance.transform.localScale = Vector3.one * 1.2f;

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = material;

        Animator animator = instance.GetComponentInChildren<Animator>(true);
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        CapsuleCollider capsule = instance.GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = instance.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 1.6f, 0f);
        capsule.height = 3.1f;
        capsule.radius = 0.75f;

        EnemyStats stats = instance.GetComponent<EnemyStats>();
        if (stats == null) stats = instance.AddComponent<EnemyStats>();
        SetSerialized(stats, "maxHealth", 2000f);
        SetSerialized(stats, "attack", 150f);
        SetSerialized(stats, "creatureType", (int)CreatureType.Boss);
        SetSerialized(stats, "xpReward", 1000);
        SetSerialized(stats, "minGoldDrop", 200);
        SetSerialized(stats, "maxGoldDrop", 400);

        CrabDemonBossAI bossAi = instance.GetComponent<CrabDemonBossAI>();
        if (bossAi == null)
            bossAi = instance.AddComponent<CrabDemonBossAI>();
        SetSerialized(bossAi, "attackRadius", 0.8f);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        return saved;
    }

    static RuntimeAnimatorController BuildController()
    {
        // Rebuilding in-place (instead of deleting and recreating at the same path within one
        // execution) - a delete+immediate-recreate on the same asset path in a single script run
        // previously corrupted references silently (the Animator's runtimeAnimatorController
        // ended up null after SaveAsPrefabAsset, even though the controller asset itself looked
        // fine) - keeping the same AnimatorController object/GUID avoids that entirely.
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else
            ClearController(controller);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack1", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack3", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack4", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack5", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack6", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Push", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Block", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("FireballCast", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("VictoryDance", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        ConfigureLocomotionLoop("Walking");
        ConfigureLocomotionLoop("Running");

        AnimationClip idle = LoadClip("Idle_03", "Armature|Armature|Idle_03|baselayer");
        AnimationClip walk = LoadClip("Walking", "Armature|Armature|walking_man|baselayer");
        AnimationClip run = LoadClip("Running", "Armature|Armature|running|baselayer");
        AnimationClip jump = LoadClip("Regular_Jump", "Armature|Armature|Regular_Jump|baselayer");
        AnimationClip push = LoadClip("Boxing_Guard_Step_Knee_Strike", "Armature|Armature|Boxing_Guard_Step_Knee_Strike|baselayer");
        AnimationClip attack1 = LoadClip("Right_Hand_Sword_Slash", "Armature|Armature|Right_Hand_Sword_Slash|baselayer");
        AnimationClip attack2 = LoadClip("Charged_Upward_Slash", "Armature|Armature|Charged_Upward_Slash|baselayer");
        AnimationClip attack3 = LoadClip("Punch_Combo_1", "Armature|Armature|Punch_Combo_1|baselayer");
        AnimationClip attack4 = LoadClip("Spartan_Kick", "Armature|Armature|Spartan_Kick|baselayer");
        AnimationClip attack5 = LoadClip("High_Kick", "Armature|Armature|High_Kick|baselayer");
        AnimationClip attack6 = LoadClip("Sweeping_Kick", "Armature|Armature|Sweeping_Kick|baselayer");
        // Sword_Parry_Backward_4 is a real block/parry animation - directly fixes "no se cubre
        // cuando le voy a pegar" (the old controller had a Block state but no real parry clip).
        AnimationClip block = LoadClip("Sword_Parry_Backward_4", "Armature|Armature|Sword_Parry_Backward_4|baselayer");
        AnimationClip hit = LoadClip("Slap_Reaction", "Armature|Armature|Slap_Reaction|baselayer");
        AnimationClip fireball = LoadClip("mage_soell_cast_3", "Armature|Armature|mage_soell_cast_3|baselayer");
        AnimationClip dance = LoadClip("Superlove_Pop_Dance", "Armature|Armature|Superlove_Pop_Dance|baselayer");
        AnimationClip death = LoadClip("dying_backwards", "Armature|Armature|dying_backwards|baselayer");

        AnimatorState idleState = sm.AddState("Idle", new Vector3(240f, 0f, 0f));
        idleState.motion = idle;
        sm.defaultState = idleState;

        AnimatorState walkState = sm.AddState("Walk", new Vector3(240f, 80f, 0f));
        walkState.motion = walk;
        AnimatorState runState = sm.AddState("Run", new Vector3(240f, 160f, 0f));
        runState.motion = run;

        AnimatorState jumpState = sm.AddState("Jump", new Vector3(520f, 0f, 0f));
        jumpState.motion = jump;
        AnimatorState pushState = sm.AddState("Push", new Vector3(520f, 80f, 0f));
        pushState.motion = push;
        AnimatorState attack1State = sm.AddState("Attack1", new Vector3(520f, 160f, 0f));
        attack1State.motion = attack1;
        AnimatorState attack2State = sm.AddState("Attack2", new Vector3(520f, 240f, 0f));
        attack2State.motion = attack2;
        AnimatorState attack3State = sm.AddState("Attack3", new Vector3(800f, 0f, 0f));
        attack3State.motion = attack3;
        AnimatorState attack4State = sm.AddState("Attack4", new Vector3(800f, 80f, 0f));
        attack4State.motion = attack4;
        AnimatorState attack5State = sm.AddState("Attack5", new Vector3(800f, 160f, 0f));
        attack5State.motion = attack5;
        AnimatorState attack6State = sm.AddState("Attack6", new Vector3(800f, 240f, 0f));
        attack6State.motion = attack6;
        AnimatorState blockState = sm.AddState("Block", new Vector3(520f, 320f, 0f));
        blockState.motion = block;
        AnimatorState hitState = sm.AddState("Hit", new Vector3(520f, 400f, 0f));
        hitState.motion = hit;
        AnimatorState fireballState = sm.AddState("FireballCast", new Vector3(520f, 480f, 0f));
        fireballState.motion = fireball;
        AnimatorState danceState = sm.AddState("VictoryDance", new Vector3(520f, 560f, 0f));
        danceState.motion = dance;
        AnimatorState deathState = sm.AddState("Death", new Vector3(520f, 640f, 0f));
        deathState.motion = death;

        AddFloatTransition(idleState, walkState, "Speed", AnimatorConditionMode.Greater, 0.15f);
        AddFloatTransition(walkState, idleState, "Speed", AnimatorConditionMode.Less, 0.15f);
        AddFloatTransition(walkState, runState, "Speed", AnimatorConditionMode.Greater, 0.75f);
        AddFloatTransition(runState, walkState, "Speed", AnimatorConditionMode.Less, 0.75f);
        AddFloatTransition(idleState, runState, "Speed", AnimatorConditionMode.Greater, 0.75f);
        AddFloatTransition(runState, idleState, "Speed", AnimatorConditionMode.Less, 0.15f);

        AddTriggerTransition(sm, jumpState, "Jump");
        AddTriggerTransition(sm, pushState, "Push");
        AddTriggerTransition(sm, attack1State, "Attack1");
        AddTriggerTransition(sm, attack2State, "Attack2");
        AddTriggerTransition(sm, attack3State, "Attack3");
        AddTriggerTransition(sm, attack4State, "Attack4");
        AddTriggerTransition(sm, attack5State, "Attack5");
        AddTriggerTransition(sm, attack6State, "Attack6");
        AddTriggerTransition(sm, blockState, "Block");
        AddTriggerTransition(sm, hitState, "Hit");
        AddTriggerTransition(sm, fireballState, "FireballCast");
        AddTriggerTransition(sm, danceState, "VictoryDance");
        AddTriggerTransition(sm, deathState, "Death");

        AddReturnTransition(jumpState, idleState, 0.85f);
        AddReturnTransition(pushState, idleState, 0.85f);
        AddReturnTransition(attack1State, idleState, 0.85f);
        AddReturnTransition(attack2State, idleState, 0.85f);
        AddReturnTransition(attack3State, idleState, 0.85f);
        AddReturnTransition(attack4State, idleState, 0.85f);
        AddReturnTransition(attack5State, idleState, 0.85f);
        AddReturnTransition(attack6State, idleState, 0.85f);
        AddReturnTransition(blockState, idleState, 0.8f);
        AddReturnTransition(hitState, idleState, 0.8f);
        // The projectile is released very early in this source clip.  Do not retain its long
        // recovery tail if the controller is rebuilt; runtime also exits it at the release.
        AddReturnTransition(fireballState, idleState, 0.55f);
        // VictoryDance and Death intentionally have no return transition - terminal states for
        // that encounter (dance loops per its clip's own loop flag; death freezes and the
        // GameObject is destroyed shortly after by CrabDemonBossAI.HandleDeath).

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static void ClearController(AnimatorController controller)
    {
        foreach (AnimatorControllerParameter p in controller.parameters.Clone() as AnimatorControllerParameter[])
            controller.RemoveParameter(p);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in sm.states.Clone() as ChildAnimatorState[])
            sm.RemoveState(child.state);
        foreach (AnimatorStateTransition t in sm.anyStateTransitions.Clone() as AnimatorStateTransition[])
            sm.RemoveAnyStateTransition(t);
    }

    static AnimationClip LoadClip(string fileSuffix, string preferredName)
    {
        string path = SourceFolder + FilePrefix + "Animation_" + fileSuffix + "_withSkin.fbx";
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
            if (asset is AnimationClip clip && clip.name == preferredName)
                return clip;

        foreach (Object asset in assets)
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;

        Debug.LogWarning("[CrabDemonBossSetup] Clip not found in " + path);
        return null;
    }

    static void ConfigureLocomotionLoop(string fileSuffix)
    {
        string path = SourceFolder + FilePrefix + "Animation_" + fileSuffix + "_withSkin.fbx";
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation clip = clips[i];
            if (!clip.loopTime)
            {
                clip.loopTime = true;
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
        GameObject existing = Object.FindAnyObjectByType<CrabDemonBossAI>(FindObjectsInactive.Include)?.gameObject;
        Vector3 position = existing != null ? existing.transform.position : FindSpawnPosition();
        Quaternion rotation = existing != null ? existing.transform.rotation : Quaternion.identity;

        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = SceneObjectName;
        instance.transform.SetPositionAndRotation(position, rotation);
        EditorUtility.SetDirty(instance);
    }

    static Vector3 FindSpawnPosition()
    {
        Vector3 position = new Vector3(-25f, 0f, 30f);
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
        if (prop != null)
        {
            if (prop.propertyType == SerializedPropertyType.Enum) prop.enumValueIndex = value;
            else prop.intValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SuperiorTreeAnomalySetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/SuperiorTreeAnomalySetup.generate";
    const string CharacterPath = "Assets/MeshyImports/Meshy_Model_20260715_154844/Meshy_AI_Crea_una_animalia_hum_biped_Character_output.fbx";
    const string NewKickPath = "Assets/MeshyImports/Meshy_Model_20260715_154837/Meshy_AI_Crea_una_animalia_hum_biped_Animation_Boxing_Guard_Right_Straight_Kick_withSkin.fbx";
    const string OldRoot = "Assets/_RPG/Imported/Anomalia del arbol/Meshy_AI_Quiero_que_hagas_una__biped_Animation_";
    const string OldCharacterPath = "Assets/_RPG/Imported/Anomalia del arbol/Meshy_AI_Quiero_que_hagas_una__biped_Character_output.fbx";
    const string CharacterMaterialPath = "Assets/MeshyImports/Meshy_Model_20260715_154844/Material_1.mat";
    const string ControllerPath = "Assets/_RPG/Prefabs/Enemies/SuperiorTreeAnomaly.controller";
    const string PrefabPath = "Assets/_RPG/Prefabs/Enemies/SuperiorTreeAnomaly.prefab";
    const string DeathClipPath = "Assets/_RPG/Prefabs/Enemies/SuperiorTreeAnomaly_Death.anim";

    [MenuItem("RPG/Enemies/Setup Superior Tree Anomaly")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorSceneUtility.OpenSceneSafely(ScenePath);
        EnsureFolder("Assets/_RPG/Prefabs");
        EnsureFolder("Assets/_RPG/Prefabs/Enemies");
        ConfigureGenericRig();

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
        if (source == null) { Debug.LogError("[SuperiorTreeAnomaly] No se encontro el modelo importado de Meshy."); return; }
        // Keep physics/AI on a stable outer root. Generic Meshy clips animate the
        // transform they are attached to; putting the Animator on the visual child
        // prevents those curves from resetting the actor's world position each frame.
        GameObject instance = new GameObject("Superior_tree_anomaly");
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
        visual.name = "Superior_tree_anomaly_Visual";
        visual.transform.SetParent(instance.transform, false);
        ApplyOriginalMaterial(visual);
        int layer = LayerMask.NameToLayer("Enemy");
        foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer >= 0 ? layer : child.gameObject.layer;
        instance.tag = "Enemy";

        Animator animator = visual.GetComponent<Animator>();
        if (animator == null) animator = visual.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.avatar = null;
        animator.runtimeAnimatorController = BuildController();

        // Unity imported models can retain an empty ("missing") component slot.
        // Do not use ?? here: it does not recognize Unity's pseudo-null objects.
        CharacterController controller = instance.GetComponent<CharacterController>();
        if (controller == null) controller = instance.AddComponent<CharacterController>();
        controller.center = new Vector3(0f, 1.05f, 0f);
        controller.height = 2.1f;
        controller.radius = 0.42f;
        controller.slopeLimit = 45f;
        controller.stepOffset = 0.35f;
        controller.enabled = true;

        CapsuleCollider capsule = instance.GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = instance.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 1.05f, 0f);
        capsule.height = 2.1f;
        capsule.radius = 0.42f;
        capsule.isTrigger = true;

        EnemyStats stats = instance.GetComponent<EnemyStats>();
        if (stats == null) stats = instance.AddComponent<EnemyStats>();
        Set(stats, "maxHealth", 260f); Set(stats, "attack", 28f); Set(stats, "xpReward", 95); Set(stats, "minGoldDrop", 28); Set(stats, "maxGoldDrop", 55);
        if (instance.GetComponent<SuperiorTreeAnomalyAI>() == null) instance.AddComponent<SuperiorTreeAnomalyAI>();
        if (instance.GetComponent<WaterEnemyEffects>() == null) instance.AddComponent<WaterEnemyEffects>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        Place(prefab);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    static RuntimeAnimatorController BuildController()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Melee", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Cast", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimationClip idle = Clip(OldRoot + "Idle_03_withSkin.fbx");
        AnimationClip walk = Clip(OldRoot + "Walking_withSkin.fbx");
        AnimationClip run = Clip(OldRoot + "Running_withSkin.fbx");
        AnimationClip melee = Clip(OldRoot + "Spartan_Kick_withSkin.fbx") ?? Clip(NewKickPath);
        AnimationClip cast = Clip(OldRoot + "Charged_Upward_Slash_withSkin.fbx");
        AnimationClip death = BuildDeathClip();
        AnimatorState idleState = sm.AddState("Idle"); idleState.motion = idle; sm.defaultState = idleState;
        AnimatorState walkState = sm.AddState("Walk"); walkState.motion = walk;
        AnimatorState runState = sm.AddState("Run"); runState.motion = run;
        AnimatorState meleeState = sm.AddState("Melee"); meleeState.motion = melee;
        AnimatorState castState = sm.AddState("CastFireball"); castState.motion = cast;
        AnimatorState deathState = sm.AddState("Death"); deathState.motion = death;
        // Separate enter/exit thresholds provide hysteresis, preventing a visual
        // flicker when the AI makes tiny speed corrections near its target range.
        FloatTransition(idleState, walkState, AnimatorConditionMode.Greater, 0.10f);
        FloatTransition(walkState, idleState, AnimatorConditionMode.Less, 0.035f);
        FloatTransition(walkState, runState, AnimatorConditionMode.Greater, 0.78f);
        FloatTransition(runState, walkState, AnimatorConditionMode.Less, 0.62f);
        TriggerTransition(sm, meleeState, "Melee"); TriggerTransition(sm, castState, "Cast");
        TriggerTransition(sm, deathState, "Death");
        ReturnTransition(meleeState, idleState); ReturnTransition(castState, idleState);
        return controller;
    }

    static AnimationClip Clip(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path)) if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
        return null;
    }
    static AnimationClip BuildDeathClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "SuperiorTreeAnomaly_Death", legacy = false };
            AssetDatabase.CreateAsset(clip, DeathClipPath);
        }
        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.35f, 22f), new Keyframe(1.2f, 86f)));
        clip.SetCurve("", typeof(Transform), "localPosition.y", new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.8f, 0.04f), new Keyframe(1.2f, 0f)));
        EditorUtility.SetDirty(clip);
        return clip;
    }
    static void ConfigureGenericRig()
    {
        SetGenericRig(CharacterPath);
        SetGenericRig(OldRoot + "Idle_03_withSkin.fbx");
        SetGenericRig(OldRoot + "Walking_withSkin.fbx");
        SetGenericRig(OldRoot + "Running_withSkin.fbx");
        SetGenericRig(OldRoot + "Spartan_Kick_withSkin.fbx");
        SetGenericRig(OldRoot + "Charged_Upward_Slash_withSkin.fbx");
        SetGenericRig(NewKickPath);
    }
    static void SetGenericRig(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null || importer.animationType == ModelImporterAnimationType.Generic) return;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.SaveAndReimport();
    }
    static void ApplyOriginalMaterial(GameObject root)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CharacterMaterialPath);
        if (material == null) return;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = material;
    }
    static Bounds VisualBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
    static void FloatTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold) { AnimatorStateTransition t = from.AddTransition(to); t.duration = .1f; t.hasExitTime = false; t.AddCondition(mode, threshold, "Speed"); }
    static void TriggerTransition(AnimatorStateMachine sm, AnimatorState to, string trigger) { AnimatorStateTransition t = sm.AddAnyStateTransition(to); t.duration = .06f; t.hasExitTime = false; t.AddCondition(AnimatorConditionMode.If, 0f, trigger); }
    static void ReturnTransition(AnimatorState from, AnimatorState to) { AnimatorStateTransition t = from.AddTransition(to); t.hasExitTime = true; t.exitTime = .94f; t.duration = .08f; }
    static void Place(GameObject prefab)
    {
        GameObject previous = GameObject.Find("Superior_tree_anomaly"); if (previous != null) Object.DestroyImmediate(previous);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab); instance.name = "Superior_tree_anomaly";
        Vector3 p = new Vector3(29f, 0f, -18f); Terrain terrain = Terrain.activeTerrain; if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y; instance.transform.position = p;
    }
    static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace('\\', '/'); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
    static void Set(Object target, string name, float value) { SerializedObject so = new SerializedObject(target); SerializedProperty p = so.FindProperty(name); if (p != null) p.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void Set(Object target, string name, int value) { SerializedObject so = new SerializedObject(target); SerializedProperty p = so.FindProperty(name); if (p != null) p.intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    [InitializeOnLoadMethod] static void StartRequest() => EditorApplication.delayCall += RunRequest;
    static void RunRequest() { string path = Path.GetFullPath(RequestPath); if (!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode) return; File.Delete(path); if (File.Exists(path + ".meta")) File.Delete(path + ".meta"); Setup(); }
}

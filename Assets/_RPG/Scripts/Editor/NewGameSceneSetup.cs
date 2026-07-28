using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class NewGameSceneSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/NewGame.unity";
    const string RequestPath = "Assets/_RPG/Generated/NewGameSceneSetup.generate";
    const string CharacterPath =
        "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Modular Character/GanzSe Free Modular Character Update 1_1.prefab";
    const string IdlePath =
        "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animations/Unarmed/RPG-Character@Unarmed-Idle.FBX";
    const string ControllerPath =
        "Assets/_RPG/Animations/MainMenuCharacter.controller";
    const string PedestalMaterialPath =
        "Assets/_RPG/Materials/MainMenuPedestal.mat";
    const string SkyboxPath =
        "Assets/Fantasy Skybox FREE/Panoramics/FS017/FS017_Sunset.mat";

    [InitializeOnLoadMethod]
    static void Queue()
    {
        EditorApplication.delayCall += () =>
        {
            ConfigurePlayModeStartScene();
            TryRun();
        };
    }

    static void ConfigurePlayModeStartScene()
    {
        SceneAsset menuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (menuScene != null && EditorSceneManager.playModeStartScene != menuScene)
            EditorSceneManager.playModeStartScene = menuScene;
    }

    static void TryRun()
    {
        if (!File.Exists(Path.GetFullPath(RequestPath)) ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        Setup();
    }

    [MenuItem("RPG/UI/Create New Game Scene")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorSceneManager.SaveOpenScenes();
        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "NewGame";

        Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.28f, .24f, .34f);
        RenderSettings.ambientEquatorColor = new Color(.15f, .09f, .13f);
        RenderSettings.ambientGroundColor = new Color(.035f, .018f, .025f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = .012f;
        RenderSettings.fogColor = new Color(.12f, .055f, .08f);

        CreateCamera();
        CreateLights();
        CreatePedestal();
        CreateCharacter();
        CreateEmbers();
        CreateEventSystem();
        new GameObject("NewGameMainMenu").AddComponent<NewGameMainMenu>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        ConfigureBuildSettings();
        ConfigurePlayModeStartScene();
        AssetDatabase.SaveAssets();
        string request = Path.GetFullPath(RequestPath);
        if (File.Exists(request)) File.Delete(request);
        if (File.Exists(request + ".meta")) File.Delete(request + ".meta");
        AssetDatabase.Refresh();
        Debug.Log("[NewGameSceneSetup] NewGame creada como escena inicial y conectada a SpawnVillage.");
    }

    static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera",
            typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 35f;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 500f;
        camera.allowHDR = true;
        cameraObject.transform.position = new Vector3(0f, 1.55f, -6.2f);
        cameraObject.transform.rotation = Quaternion.LookRotation(
            new Vector3(2.15f, 1.25f, 0f) - cameraObject.transform.position);
    }

    static void CreateLights()
    {
        GameObject key = new GameObject("Warm Key Light", typeof(Light));
        Light keyLight = key.GetComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.color = new Color(1f, .64f, .38f);
        keyLight.intensity = 1.4f;
        keyLight.shadows = LightShadows.Soft;
        key.transform.rotation = Quaternion.Euler(36f, -32f, 0f);

        GameObject rim = new GameObject("Violet Rim Light", typeof(Light));
        Light rimLight = rim.GetComponent<Light>();
        rimLight.type = LightType.Point;
        rimLight.color = new Color(.45f, .16f, 1f);
        rimLight.intensity = 7f;
        rimLight.range = 8f;
        rimLight.shadows = LightShadows.None;
        rim.transform.position = new Vector3(3.4f, 2.5f, 1.3f);

        GameObject fill = new GameObject("Soft Fill Light", typeof(Light));
        Light fillLight = fill.GetComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.color = new Color(.26f, .48f, 1f);
        fillLight.intensity = 3.2f;
        fillLight.range = 7f;
        fillLight.shadows = LightShadows.None;
        fill.transform.position = new Vector3(.2f, 1.8f, -2.1f);
    }

    static void CreatePedestal()
    {
        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "CharacterPedestal";
        pedestal.transform.position = new Vector3(2.15f, -.08f, 0f);
        pedestal.transform.localScale = new Vector3(1.1f, .12f, 1.1f);
        Object.DestroyImmediate(pedestal.GetComponent<Collider>());
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            PedestalMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = "MainMenuPedestal" };
            material.SetColor("_BaseColor", new Color(.055f, .025f, .07f));
            material.SetFloat("_Smoothness", .62f);
            material.SetFloat("_Metallic", .72f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(.14f, .025f, .3f) * 2f);
            AssetDatabase.CreateAsset(material, PedestalMaterialPath);
        }
        pedestal.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static void CreateCharacter()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
        if (prefab == null)
        {
            Debug.LogError("[NewGameSceneSetup] Falta el personaje Ganz.");
            return;
        }
        GameObject character = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        character.name = "CharacterPreview";
        character.transform.position = new Vector3(2.15f, .06f, 0f);
        character.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        ConfigureCharacterParts(character);
        Animator animator = character.GetComponent<Animator>() ??
            character.AddComponent<Animator>();
        animator.runtimeAnimatorController = EnsureIdleController();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    static void ConfigureCharacterParts(GameObject character)
    {
        string[] visibleParts =
        {
            "base character mesh", "eyes type 1 color 1",
            "eyebrow type 1 color 1", "nose type 1", "ears type 1",
            "hair type 1 color 1", "chest armor type 1 color 1",
            "arm armor type 1 color 1", "legs armor type 1 color 1",
            "feet armor type 1 color 1", "belt armor type 1 color 1"
        };
        foreach (SkinnedMeshRenderer renderer in
                 character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string normalized = renderer.gameObject.name
                .Replace(" Part", "").ToLowerInvariant();
            bool visible = false;
            foreach (string part in visibleParts)
                if (normalized == part || normalized.Contains(part))
                {
                    visible = true;
                    break;
                }
            renderer.enabled = visible;
        }
    }

    static RuntimeAnimatorController EnsureIdleController()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null) return controller;
        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimationClip idle = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(IdlePath))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                idle = clip;
                break;
            }
        if (idle != null)
        {
            AnimatorState state =
                controller.layers[0].stateMachine.AddState("Idle");
            state.motion = idle;
            controller.layers[0].stateMachine.defaultState = state;
        }
        return controller;
    }

    static void CreateEmbers()
    {
        GameObject root = new GameObject("MenuEmbers");
        root.transform.position = new Vector3(2.15f, .9f, 0f);
        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.06f, .22f);
        main.startSize = new ParticleSystem.MinMaxCurve(.012f, .04f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, .2f, .03f, .12f),
            new Color(1f, .72f, .18f, .55f));
        main.maxParticles = 75;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 15f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(2.7f, 2.5f, 2.7f);
        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = .18f;
        noise.frequency = .3f;
        ParticleSystemRenderer renderer =
            root.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.8f;
        renderer.material = WindVisualEffect.CreateWindStreakMaterial();
    }

    static void CreateEventSystem()
    {
        GameObject eventSystem = new GameObject("EventSystem",
            typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>()
            .AssignDefaultActions();
    }

    static void ConfigureBuildSettings()
    {
        string[] paths =
        {
            ScenePath,
            "Assets/_RPG/Scenes/SpawnVillage.unity",
            "Assets/_RPG/Scenes/Dungeon.unity",
            "Assets/_RPG/Scenes/dungeon_1.unity"
        };
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in paths)
            scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

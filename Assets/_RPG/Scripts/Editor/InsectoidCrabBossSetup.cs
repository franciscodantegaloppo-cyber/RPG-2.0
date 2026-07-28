using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class InsectoidCrabBossSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/InsectoidCrabBoss.generate";
    const string BossName = "InsectoidCrabBoss";
    const string SourcePrefab = "Assets/LB3D/CrabMonster/Prefabs/CrabMonster 1.prefab";
    const string SourceController = "Assets/LB3D/CrabMonster/Rikayon.controller";
    const string BodyMaterialPath = "Assets/_RPG/Materials/InsectoidCrabBoss_Body_URP.mat";
    const string EyesMaterialPath = "Assets/_RPG/Materials/InsectoidCrabBoss_Eyes_URP.mat";

    [MenuItem("RPG/Bosses/Setup Insectoid Crab Boss")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject old = GameObject.Find(BossName);
        if (old != null)
            Object.DestroyImmediate(old);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
        if (prefab == null)
        {
            Debug.LogError("[InsectoidCrabBossSetup] No se encontro el prefab del pack.");
            return;
        }

        GameObject boss = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        boss.name = BossName;
        Rikayon demo = boss.GetComponent<Rikayon>();
        if (demo != null)
            Object.DestroyImmediate(demo);

        Animator animator = boss.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SourceController);
            animator.applyRootMotion = false;
        }

        Material bodyMaterial = CreateUrpMaterial(BodyMaterialPath, "Insectoid Crab Body",
            "Assets/LB3D/CrabMonster/Textures/crab-monster_Rikayon_AlbedoTransparency.png",
            "Assets/LB3D/CrabMonster/Textures/crab-monster_Rikayon_Normal.png",
            "Assets/LB3D/CrabMonster/Textures/crab-monster_Rikayon_MetallicSmoothness.png",
            new Color(.78f, .82f, .86f));
        Material eyesMaterial = CreateUrpMaterial(EyesMaterialPath, "Insectoid Crab Eyes",
            "Assets/LB3D/CrabMonster/Textures/crab-monster_Eyes_AlbedoTransparency.png",
            "Assets/LB3D/CrabMonster/Textures/crab-monster_Eyes_Normal.png",
            "Assets/LB3D/CrabMonster/Textures/crab-monster_Eyes_MetallicSmoothness.png",
            new Color(.75f, .12f, .05f));
        ApplyMaterials(boss, bodyMaterial, eyesMaterial);

        NormalizeSize(boss, 4.2f);
        Vector3 position = FindSpawnPosition();
        boss.transform.position = position;
        float groundY = TerrainHeight(position);
        Bounds bounds = RendererBounds(boss.transform);
        boss.transform.position += Vector3.up * (groundY - bounds.min.y + .08f);
        bounds = RendererBounds(boss.transform);

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            SetLayerRecursive(boss.transform, enemyLayer);

        CapsuleCollider capsule = boss.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = boss.AddComponent<CapsuleCollider>();
        ConfigureCapsule(boss.transform, bounds, capsule);

        Rigidbody rigidbody = boss.GetComponent<Rigidbody>();
        if (rigidbody == null)
            rigidbody = boss.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        EnemyStats stats = boss.GetComponent<EnemyStats>();
        if (stats == null)
            stats = boss.AddComponent<EnemyStats>();
        SerializedObject statsData = new SerializedObject(stats);
        statsData.FindProperty("maxHealth").floatValue = 700f;
        statsData.FindProperty("attack").floatValue = 50f;
        statsData.FindProperty("armor").floatValue = 12f;
        statsData.FindProperty("creatureType").enumValueIndex = (int)CreatureType.Boss;
        statsData.FindProperty("xpReward").intValue = 1400;
        statsData.FindProperty("minGoldDrop").intValue = 180;
        statsData.FindProperty("maxGoldDrop").intValue = 320;
        statsData.FindProperty("runeDropChance").floatValue = .75f;
        statsData.ApplyModifiedPropertiesWithoutUndo();

        InsectoidCrabBossAI ai = boss.GetComponent<InsectoidCrabBossAI>();
        if (ai == null)
            ai = boss.AddComponent<InsectoidCrabBossAI>();
        SerializedObject aiData = new SerializedObject(ai);
        aiData.FindProperty("moveSpeed").floatValue = 3.8f;
        aiData.FindProperty("groundOffset").floatValue = boss.transform.position.y - groundY;
        aiData.ApplyModifiedPropertiesWithoutUndo();

        CrabDemonHealthBar bar = boss.GetComponent<CrabDemonHealthBar>();
        if (bar == null)
            bar = boss.AddComponent<CrabDemonHealthBar>();
        SerializedObject barData = new SerializedObject(bar);
        barData.FindProperty("localOffset").vector3Value =
            new Vector3(0f, Mathf.Max(3.2f, bounds.size.y + .55f), 0f);
        barData.FindProperty("barSize").vector2Value = new Vector2(2.8f, .34f);
        barData.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = boss;
        Debug.Log("[InsectoidCrabBossSetup] Boss creado con 700 HP, 50 de ataque, Sleep y adherencia al terreno.");
    }

    [MenuItem("RPG/Bosses/Test Insectoid Crab Near Player")]
    static void TestNearPlayer()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[InsectoidCrabBossSetup] Esta prueba solo funciona en Play Mode.");
            return;
        }
        GameObject boss = GameObject.Find(BossName);
        GameObject player = GameObject.FindWithTag("Player");
        if (boss == null || player == null)
            return;
        Vector3 position = player.transform.position + player.transform.forward * 7.5f;
        position.y = TerrainHeight(position);
        boss.transform.position = position;
        boss.transform.rotation = Quaternion.LookRotation(-player.transform.forward, Vector3.up);
    }

    static Material CreateUrpMaterial(string path, string materialName, string albedoPath,
        string normalPath, string metallicPath, Color tint)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (material == null)
        {
            material = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (shader != null)
            material.shader = shader;

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath);
        material.SetTexture("_BaseMap", albedo);
        material.SetColor("_BaseColor", tint);
        material.SetTexture("_BumpMap", normal);
        material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap", metallic);
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetFloat("_Metallic", .35f);
        material.SetFloat("_Smoothness", .58f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ApplyMaterials(GameObject boss, Material body, Material eyes)
    {
        foreach (Renderer renderer in boss.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string materialName = materials[i] != null ? materials[i].name : string.Empty;
                materials[i] = materialName.IndexOf("eye", System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? eyes : body;
            }
            renderer.sharedMaterials = materials;
        }
    }

    static Vector3 FindSpawnPosition()
    {
        GameObject spawn = null;
        try { spawn = GameObject.FindWithTag("SpawnPoint"); }
        catch (UnityException) { }
        if (spawn == null)
            return new Vector3(24f, 0f, 24f);

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
            return spawn.transform.position - spawn.transform.right * 30f;

        Vector3 origin = spawn.transform.position;
        float originHeight = TerrainHeight(origin);
        Vector3 best = origin - spawn.transform.right * 32f;
        float bestScore = float.PositiveInfinity;
        for (float radius = 28f; radius <= 58f; radius += 6f)
        {
            for (int step = 0; step < 24; step++)
            {
                float angle = step * 15f * Mathf.Deg2Rad;
                Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                float height = TerrainHeight(candidate);
                Vector3 local = candidate - terrain.transform.position;
                Vector3 normal = terrain.terrainData.GetInterpolatedNormal(
                    Mathf.Clamp01(local.x / terrain.terrainData.size.x),
                    Mathf.Clamp01(local.z / terrain.terrainData.size.z));
                if (normal.y < .94f || Mathf.Abs(height - originHeight) > 4f)
                    continue;

                candidate.y = height;
                Collider[] nearby = Physics.OverlapSphere(candidate + Vector3.up * 1.8f, 4.5f,
                    ~0, QueryTriggerInteraction.Ignore);
                float score = radius * .008f + Mathf.Abs(height - originHeight) * .35f;
                bool invalid = false;
                foreach (Collider collider in nearby)
                {
                    if (collider is TerrainCollider)
                        continue;
                    string objectName = collider.transform.root.name;
                    if (objectName.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        objectName.IndexOf("river", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        invalid = true;
                        break;
                    }
                    score += 8f;
                }
                if (!invalid && score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
        }
        return best;
    }

    static float TerrainHeight(Vector3 position)
    {
        Terrain terrain = Terrain.activeTerrain;
        return terrain != null
            ? terrain.SampleHeight(position) + terrain.transform.position.y
            : position.y;
    }

    static void NormalizeSize(GameObject root, float targetMaxSize)
    {
        Bounds bounds = RendererBounds(root.transform);
        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largest > .001f)
            root.transform.localScale *= targetMaxSize / largest;
    }

    static void ConfigureCapsule(Transform root, Bounds worldBounds, CapsuleCollider capsule)
    {
        Vector3 scale = root.lossyScale;
        Vector3 localSize = new Vector3(
            worldBounds.size.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            worldBounds.size.y / Mathf.Max(.001f, Mathf.Abs(scale.y)),
            worldBounds.size.z / Mathf.Max(.001f, Mathf.Abs(scale.z)));
        capsule.center = root.InverseTransformPoint(worldBounds.center);
        capsule.direction = 1;
        capsule.radius = Mathf.Max(localSize.x, localSize.z) * .32f;
        capsule.height = Mathf.Max(localSize.y * .9f, capsule.radius * 2.05f);
    }

    static Bounds RendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursive(child, layer);
    }

    [InitializeOnLoadMethod]
    static void Queue() => EditorApplication.delayCall += TryRun;

    static void TryRun()
    {
        string request = Path.GetFullPath(RequestPath);
        if (!File.Exists(request))
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }
        File.Delete(request);
        if (File.Exists(request + ".meta"))
            File.Delete(request + ".meta");
        Setup();
    }
}

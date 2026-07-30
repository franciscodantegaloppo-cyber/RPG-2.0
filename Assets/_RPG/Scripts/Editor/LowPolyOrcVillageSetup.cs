#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class LowPolyOrcVillageSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string BuildingPath =
        "Assets/Low Poly Orc DEMO/Prefabs/Building3.prefab";
    const string OrcPath =
        "Assets/Low Poly Orc DEMO/Prefabs/Orc8.prefab";
    const string MacePath =
        "Assets/Low Poly Orc DEMO/Prefabs/Mace2.prefab";
    const string BaseMapPath =
        "Assets/Low Poly Orc DEMO/Materials/Base Color.png";
    const string EmissionPath =
        "Assets/Low Poly Orc DEMO/Materials/Emission.png";
    const string ControllerPath =
        "Assets/_RPG/Generated/Animations/OrcWarrior.controller";
    const string MaterialPath =
        "Assets/_RPG/Materials/LowPolyOrcVillage_URP.mat";
    const string EnemyPrefabPath =
        "Assets/_RPG/Prefabs/Enemies/LowPolyOrcEnemy.prefab";
    const string RootName = "LowPoly_Orc_Village_Staging";
    const string MarkerName = "_LowPolyOrc_Configured_v2";
    const string VillageMarkerName = "_LowPolyOrcVillage_v2";
    const float OrcHeight = 2.15f;

    static LowPolyOrcVillageSetup()
    {
        EditorApplication.delayCall += InstallWhenReady;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += InstallWhenReady;
        };
    }

    [MenuItem("RPG/World/Agregar aldea Low Poly Orc completa")]
    public static void InstallWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            scene.path != ScenePath)
            return;

        GameObject existing = FindSceneRoot(scene, RootName);
        if (existing != null)
        {
            if (existing.transform.Find(VillageMarkerName) != null)
            {
                Selection.activeGameObject = existing;
                return;
            }
            Object.DestroyImmediate(existing);
        }

        GameObject building =
            AssetDatabase.LoadAssetAtPath<GameObject>(BuildingPath);
        GameObject orc =
            AssetDatabase.LoadAssetAtPath<GameObject>(OrcPath);
        GameObject mace =
            AssetDatabase.LoadAssetAtPath<GameObject>(MacePath);
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                ControllerPath);
        if (building == null || orc == null || mace == null ||
            controller == null)
        {
            Debug.LogWarning(
                "[LowPolyOrcVillage] Esperando a que Unity termine de " +
                "importar los modelos y el controlador de animacion.");
            return;
        }

        EnsureFolder("Assets/_RPG/Materials");
        EnsureFolder("Assets/_RPG/Prefabs/Enemies");
        Material material = EnsureMaterial();
        GameObject enemyPrefab =
            EnsureEnemyPrefab(orc, mace, material, controller);
        if (material == null || enemyPrefab == null)
            return;

        GameObject root = new GameObject(RootName);
        Vector3 center = FindDistantTerrainPoint();
        root.transform.position = center;
        GameObject villageMarker = new GameObject(VillageMarkerName);
        villageMarker.transform.SetParent(root.transform, false);
        villageMarker.hideFlags =
            HideFlags.HideInHierarchy | HideFlags.NotEditable;

        Transform buildings = NewGroup("Buildings", root.transform);
        Transform enemies = NewGroup("Orc_Enemies", root.transform);
        Transform props = NewGroup("Props", root.transform);

        Vector3[] houseOffsets =
        {
            new Vector3(-13f, 0f, -9f),
            new Vector3(0f, 0f, -14f),
            new Vector3(13f, 0f, -9f),
            new Vector3(-16f, 0f, 5f),
            new Vector3(16f, 0f, 5f),
            new Vector3(-9f, 0f, 16f),
            new Vector3(9f, 0f, 16f)
        };
        float[] houseRotations = { 28f, 0f, -28f, 78f, -78f, 152f, -152f };
        for (int i = 0; i < houseOffsets.Length; i++)
        {
            GameObject house = InstantiatePrefab(building, buildings);
            house.name = "Orc_House_" + (i + 1).ToString("00");
            house.transform.position = center + houseOffsets[i];
            house.transform.rotation =
                Quaternion.Euler(0f, houseRotations[i], 0f);
            ApplyMaterial(house, material);
            EnsureBuildingColliders(house);
            GroundVisual(house);
            MarkStatic(house.transform);
        }

        Vector3[] orcOffsets =
        {
            new Vector3(-5f, 0f, -3f),
            new Vector3(5f, 0f, -2f),
            new Vector3(-7f, 0f, 8f),
            new Vector3(7f, 0f, 9f)
        };
        for (int i = 0; i < orcOffsets.Length; i++)
        {
            GameObject enemy = InstantiatePrefab(enemyPrefab, enemies);
            enemy.name = "LowPoly_Orc_Enemy_" +
                         (i + 1).ToString("00");
            enemy.transform.position = center + orcOffsets[i];
            enemy.transform.rotation =
                Quaternion.Euler(0f, 180f + i * 45f, 0f);
            GroundVisual(enemy);
        }

        // The demo contains no village props besides its weapon. Display a
        // spare mace by the central gathering space without inventing assets
        // that are not part of the imported package.
        GameObject spareMace = InstantiatePrefab(mace, props);
        spareMace.name = "Orc_Village_Spare_Mace";
        spareMace.transform.position = center + new Vector3(1.2f, 0f, 2f);
        spareMace.transform.rotation = Quaternion.Euler(0f, 35f, 90f);
        ApplyMaterial(spareMace, material);
        EnsureSimpleCollider(spareMace);
        GroundVisual(spareMace);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log(
            "[LowPolyOrcVillage] Asentamiento agregado fuera del spawn: " +
            "7 casas modulares, 4 orcos Humanoid con IA tactica, maza " +
            "equipada, colisiones, vida, dano, separacion y barra de vida. " +
            "El paquete DEMO no incluia una aldea prearmada ni clips propios " +
            "de combate, por eso se reutilizo su edificio modular y el " +
            "controlador de combate fiable del proyecto.");
    }

    static Material EnsureMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError(
                "[LowPolyOrcVillage] No se encontro el shader URP/Lit.");
            return null;
        }

        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "LowPolyOrcVillage_URP"
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        Texture2D baseMap =
            AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMapPath);
        Texture2D emission =
            AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);
        material.SetTexture("_BaseMap", baseMap);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", .22f);
        material.enableInstancing = true;
        if (emission != null)
        {
            material.SetTexture("_EmissionMap", emission);
            material.SetColor("_EmissionColor",
                new Color(.34f, .22f, .09f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static GameObject EnsureEnemyPrefab(GameObject orc, GameObject mace,
        Material material, RuntimeAnimatorController controller)
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (prefab == null)
        {
            GameObject root = new GameObject("LowPolyOrcEnemy");
            GameObject visual = Object.Instantiate(orc);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            NormalizeVisual(root, visual);
            ConfigureEnemy(root, mace, material, controller);
            prefab = PrefabUtility.SaveAsPrefabAsset(
                root, EnemyPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        GameObject contents =
            PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        ConfigureEnemy(contents, mace, material, controller);
        PrefabUtility.SaveAsPrefabAsset(contents, EnemyPrefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
        return AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
    }

    static void ConfigureEnemy(GameObject root, GameObject mace,
        Material material, RuntimeAnimatorController controller)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0)
            enemyLayer = 0;
        foreach (Transform child in
                 root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = enemyLayer;

        ApplyMaterial(root, material);
        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Debug.LogError(
                "[LowPolyOrcVillage] Orc8 no contiene Animator.");
            return;
        }
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        if (root.transform.Find(MarkerName) == null)
        {
            foreach (Transform child in
                     root.GetComponentsInChildren<Transform>(true))
                if (child != null && child.name == "Equipped_Mace")
                    Object.DestroyImmediate(child.gameObject);
            AttachMace(root, animator, mace, material);
            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(root.transform, false);
            marker.hideFlags =
                HideFlags.HideInHierarchy | HideFlags.NotEditable;
        }

        CapsuleCollider capsule = GetOrAdd<CapsuleCollider>(root);
        capsule.center = new Vector3(0f, 1.04f, 0f);
        capsule.height = 2.08f;
        capsule.radius = .48f;
        capsule.isTrigger = false;

        Rigidbody body = GetOrAdd<Rigidbody>(root);
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        NavMeshAgent agent = GetOrAdd<NavMeshAgent>(root);
        agent.radius = .46f;
        agent.height = 2.08f;
        agent.baseOffset = 0f;
        agent.speed = 3.85f;
        agent.angularSpeed = 500f;
        agent.acceleration = 15f;
        agent.stoppingDistance = 1.65f;
        agent.autoBraking = true;

        EnemyStats stats = GetOrAdd<EnemyStats>(root);
        SetFloat(stats, "maxHealth", 135f);
        SetFloat(stats, "attack", 18f);
        SetFloat(stats, "armor", 7f);
        SetInt(stats, "creatureType", (int)CreatureType.Normal);
        SetInt(stats, "xpReward", 65);
        SetInt(stats, "minGoldDrop", 8);
        SetInt(stats, "maxGoldDrop", 18);
        SetFloat(stats, "runeDropChance", .14f);

        OrcWarriorAI ai = GetOrAdd<OrcWarriorAI>(root);
        SetFloat(ai, "detectionRadius", 14f);
        SetFloat(ai, "homeLeashRadius", 28f);
        SetFloat(ai, "pursueSpeed", 3.85f);
        SetFloat(ai, "walkSpeed", 1.55f);
        SetFloat(ai, "attackCooldown", 1.65f);
        SetLayerMask(ai, "sightMask",
            ~LayerMask.GetMask("Enemy", "Ignore Raycast"));

        GetOrAdd<NPCVisualGroundAligner>(root);
        EditorUtility.SetDirty(root);
    }

    static void AttachMace(GameObject root, Animator animator,
        GameObject macePrefab, Material material)
    {
        Transform hand = animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.RightHand)
            : null;
        hand ??= FindBone(animator.transform,
            "hand.R", "RightHand", "Hand_R", "R_Hand",
            "mixamorig:RightHand");
        if (hand == null)
        {
            Debug.LogWarning(
                "[LowPolyOrcVillage] No se encontro la mano derecha de " +
                "Orc8; la maza quedo disponible en el prefab para ajuste.");
            hand = animator.transform;
        }

        GameObject weapon = Object.Instantiate(macePrefab);
        weapon.name = "Equipped_Mace";
        weapon.transform.SetParent(hand, false);
        weapon.transform.localPosition = new Vector3(.02f, .02f, .01f);
        weapon.transform.localRotation =
            Quaternion.Euler(0f, 0f, -90f);
        weapon.transform.localScale = Vector3.one;
        ApplyMaterial(weapon, material);
        foreach (Collider collider in
                 weapon.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        foreach (Transform child in
                 weapon.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = root.layer;
    }

    static void NormalizeVisual(GameObject root, GameObject visual)
    {
        Bounds bounds = VisualBounds(root);
        if (bounds.size.y <= .01f)
            return;
        visual.transform.localScale *= OrcHeight / bounds.size.y;
        bounds = VisualBounds(root);
        visual.transform.position += Vector3.up * -bounds.min.y;
    }

    static void ApplyMaterial(GameObject root, Material material)
    {
        if (material == null)
            return;
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
    }

    static void EnsureBuildingColliders(GameObject root)
    {
        foreach (MeshFilter filter in
                 root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null ||
                filter.GetComponent<Collider>() != null)
                continue;
            MeshCollider collider =
                filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
        }
    }

    static void EnsureSimpleCollider(GameObject root)
    {
        if (root.GetComponentInChildren<Collider>(true) != null)
            return;
        Bounds bounds = VisualBounds(root);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center =
            root.transform.InverseTransformPoint(bounds.center);
        Vector3 scale = root.transform.lossyScale;
        collider.size = new Vector3(
            bounds.size.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(.001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(.001f, Mathf.Abs(scale.z)));
    }

    static Vector3 FindDistantTerrainPoint()
    {
        Transform spawn = FindSpawn();
        Vector3 spawnPosition =
            spawn != null ? spawn.position : Vector3.zero;
        Terrain terrain = ClosestTerrain(spawnPosition);
        if (terrain == null)
            return spawnPosition + new Vector3(130f, 0f, 95f);

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float margin = Mathf.Min(38f, Mathf.Min(size.x, size.z) * .12f);
        Vector3[] candidates =
        {
            new Vector3(origin.x + margin, 0f, origin.z + margin),
            new Vector3(origin.x + size.x - margin, 0f, origin.z + margin),
            new Vector3(origin.x + margin, 0f, origin.z + size.z - margin),
            new Vector3(origin.x + size.x - margin, 0f,
                origin.z + size.z - margin)
        };
        Vector3 best = candidates[0];
        float bestDistance = -1f;
        foreach (Vector3 candidate in candidates)
        {
            float distance =
                (candidate - spawnPosition).sqrMagnitude;
            if (distance <= bestDistance)
                continue;
            best = candidate;
            bestDistance = distance;
        }
        best.y = terrain.SampleHeight(best) + origin.y;
        return best;
    }

    static Terrain ClosestTerrain(Vector3 position)
    {
        Terrain best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
                continue;
            Bounds bounds = new Bounds(
                terrain.transform.position +
                terrain.terrainData.size * .5f,
                terrain.terrainData.size);
            float distance =
                bounds.SqrDistance(position);
            if (distance >= bestDistance)
                continue;
            best = terrain;
            bestDistance = distance;
        }
        return best;
    }

    static void GroundVisual(GameObject root)
    {
        Bounds bounds = VisualBounds(root);
        Terrain terrain = ClosestTerrain(bounds.center);
        if (terrain == null)
            return;
        Vector3 position = root.transform.position;
        float ground = terrain.SampleHeight(position) +
                       terrain.transform.position.y;
        root.transform.position += Vector3.up *
            (ground - bounds.min.y + .015f);
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

    static Transform FindBone(Transform root, params string[] names)
    {
        foreach (Transform child in
                 root.GetComponentsInChildren<Transform>(true))
        {
            foreach (string candidate in names)
                if (string.Equals(child.name, candidate,
                        StringComparison.OrdinalIgnoreCase))
                    return child;
        }
        return null;
    }

    static void MarkStatic(Transform root)
    {
        foreach (Transform child in
                 root.GetComponentsInChildren<Transform>(true))
            child.gameObject.isStatic = true;
    }

    static Transform NewGroup(string name, Transform parent)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    static GameObject InstantiatePrefab(GameObject prefab,
        Transform parent)
    {
        GameObject instance =
            PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            instance = Object.Instantiate(prefab);
        instance.transform.SetParent(parent, true);
        return instance;
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

    static T GetOrAdd<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        return component != null ? component : root.AddComponent<T>();
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

    static void SetLayerMask(Object target,
        string property, LayerMask value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty field = serialized.FindProperty(property);
        if (field != null)
            field.intValue = value.value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int split = path.LastIndexOf('/');
        string parent = path.Substring(0, split);
        string name = path.Substring(split + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif

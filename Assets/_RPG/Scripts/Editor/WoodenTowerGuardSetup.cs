#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class WoodenTowerGuardSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/WoodenTowerGuardSetup.generate";
    const string ModelPath = "Assets/MeshyImports/Meshy_Model_20260719_143442/Meshy_AI_haz_una_torre_simple__0719173408_texture.fbx";
    const string SourceMaterialPath = "Assets/MeshyImports/Meshy_Model_20260719_143442/Material.001.mat";
    const string MaterialPath = "Assets/_RPG/Materials/WoodenTower_Meshy_URP.mat";

    [MenuItem("RPG/World/Create Wooden Tower and Guard")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError("[WoodenTower] No se encontro el FBX Meshy: " + ModelPath);
            return;
        }

        GameObject previousTower = FindSceneObject("woodentower");
        bool preservedPlacement = previousTower != null;
        Vector3 position = previousTower != null ? previousTower.transform.position : Vector3.zero;
        Quaternion rotation = previousTower != null ? previousTower.transform.rotation : Quaternion.identity;
        if (previousTower != null) Object.DestroyImmediate(previousTower);

        GameObject tower = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (tower == null) tower = Object.Instantiate(model);
        tower.name = "wooden_tower";
        tower.SetActive(true);
        tower.transform.localScale = Vector3.one;
        if (preservedPlacement) tower.transform.SetPositionAndRotation(position, rotation);
        else PlaceNearSpawn(tower);

        NormalizeHeight(tower, 12f);
        GroundOnTerrain(tower);
        Material material = GetOrCreateMaterial();
        int rendererCount = ApplyMaterial(tower, material);
        BoxCollider towerCollider = FitBoxCollider(tower);
        SetLayerRecursively(tower, LayerMask.NameToLayer("Default"));
        SetStaticRecursively(tower);

        GameObject oldGuard = FindSceneObject("npcvigia1");
        if (oldGuard != null) Object.DestroyImmediate(oldGuard);
        GameObject guard = CreateGuard(tower);

        EditorUtility.SetDirty(tower);
        EditorUtility.SetDirty(guard);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = tower;

        Debug.Log("[WoodenTower] Torre y npc_vigia_1 listos. Torre=" + tower.transform.position +
                  ", escala=" + tower.transform.lossyScale + ", renderers=" + rendererCount +
                  ", colliderMundo=" + towerCollider.bounds.size + ", vigia=" + guard.transform.position +
                  ", deteccion=10m, movimiento=bloqueado.");
    }

    static GameObject CreateGuard(GameObject tower)
    {
        TonioQuestGiver tonio = Object.FindAnyObjectByType<TonioQuestGiver>(FindObjectsInactive.Include);
        if (tonio == null) throw new System.InvalidOperationException("No se encontro a Tonio para crear el vigia.");

        GameObject guard = Object.Instantiate(tonio.gameObject);
        guard.name = "npc_vigia_1";
        Remove(guard.GetComponent<TonioQuestGiver>());
        Remove(guard.GetComponent<TonioRetaliation>());
        Remove(guard.GetComponent<EnemyStats>());
        Remove(guard.GetComponent<EnemyHealthBar>());
        Remove(guard.GetComponent<WeaponSocket>());
        Remove(guard.GetComponent<NPCWander>());
        Remove(guard.GetComponent<NavMeshAgent>());

        Transform combatHitbox = guard.transform.Find("CombatHitbox");
        if (combatHitbox != null) Object.DestroyImmediate(combatHitbox.gameObject);
        Rigidbody body = guard.GetComponent<Rigidbody>();
        if (body != null) { body.isKinematic = true; body.useGravity = false; }

        Bounds towerBounds = GetVisualBounds(tower);
        Vector3 station = new Vector3(towerBounds.center.x, towerBounds.max.y + .12f, towerBounds.center.z);
        guard.transform.SetPositionAndRotation(station, tower.transform.rotation);
        AlignFeetToHeight(guard, station.y);

        if (guard.GetComponent<OldManFireballCombat>() == null) guard.AddComponent<OldManFireballCombat>();
        if (guard.GetComponent<TowerGuardStationary>() == null) guard.AddComponent<TowerGuardStationary>();

        int layer = LayerMask.NameToLayer("Interactable");
        if (layer < 0) layer = 0;
        SetLayerRecursively(guard, layer);
        return guard;
    }

    static Material GetOrCreateMaterial()
    {
        Material result = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
        if (result == null)
        {
            result = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            result.name = "WoodenTower_Meshy_URP";
            AssetDatabase.CreateAsset(result, MaterialPath);
        }
        else if (source != null)
        {
            result.shader = source.shader;
            result.CopyPropertiesFromMaterial(source);
        }

        result.enableInstancing = true;
        if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", Color.white);
        if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", .28f);
        if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic", .04f);
        if (result.HasProperty("_EmissionColor")) result.SetColor("_EmissionColor", Color.black);
        result.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(result);
        return result;
    }

    static int ApplyMaterial(GameObject root, Material material)
    {
        int count = 0;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            if (slots == null || slots.Length == 0) slots = new Material[1];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
            renderer.enabled = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            count++;
        }
        return count;
    }

    static void PlaceNearSpawn(GameObject tower)
    {
        TonioQuestGiver tonio = Object.FindAnyObjectByType<TonioQuestGiver>(FindObjectsInactive.Include);
        if (tonio != null)
        {
            Vector3 position = tonio.transform.position + tonio.transform.right * 20f + tonio.transform.forward * 14f;
            tower.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-tonio.transform.forward));
        }
        else tower.transform.position = new Vector3(18f, 0f, 14f);
    }

    static void NormalizeHeight(GameObject root, float desiredHeight)
    {
        Bounds bounds = GetVisualBounds(root);
        if (bounds.size.y < .001f) return;
        root.transform.localScale *= desiredHeight / bounds.size.y;
    }

    static void GroundOnTerrain(GameObject root)
    {
        Bounds bounds = GetVisualBounds(root);
        Terrain terrain = Terrain.activeTerrain;
        float ground = terrain != null
            ? terrain.SampleHeight(bounds.center) + terrain.transform.position.y
            : root.transform.position.y;
        root.transform.position += Vector3.up * (ground - bounds.min.y + .02f);
    }

    static void AlignFeetToHeight(GameObject character, float desiredFeetY)
    {
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        character.transform.position += Vector3.up * (desiredFeetY - bounds.min.y);
    }

    static BoxCollider FitBoxCollider(GameObject root)
    {
        foreach (Collider existing in root.GetComponentsInChildren<Collider>(true))
            if (existing.gameObject != root) Object.DestroyImmediate(existing);
        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null) collider = root.AddComponent<BoxCollider>();
        collider.isTrigger = false;

        Bounds world = GetVisualBounds(root);
        Vector3[] corners = BoundsCorners(world);
        Vector3 min = root.transform.InverseTransformPoint(corners[0]);
        Vector3 max = min;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 point = root.transform.InverseTransformPoint(corners[i]);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }
        collider.center = (min + max) * .5f;
        collider.size = max - min;
        return collider;
    }

    static Vector3[] BoundsCorners(Bounds b)
    {
        Vector3[] result = new Vector3[8];
        int i = 0;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            result[i++] = b.center + Vector3.Scale(b.extents, new Vector3(x, y, z));
        return result;
    }

    static Bounds GetVisualBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static GameObject FindSceneObject(string normalizedName)
    {
        foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (!candidate.gameObject.scene.IsValid()) continue;
            string name = candidate.name.ToLowerInvariant().Replace("_", "").Replace(" ", "").Replace("-", "");
            if (name == normalizedName || name.Contains(normalizedName)) return candidate.gameObject;
        }
        return null;
    }

    static void Remove(Object component)
    {
        if (component != null) Object.DestroyImmediate(component);
    }

    static void SetLayerRecursively(GameObject root, int layer)
    {
        if (layer < 0) layer = 0;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
    }

    static void SetStaticRecursively(GameObject root)
    {
        StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                  StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
    }

    [InitializeOnLoadMethod]
    static void QueueSetup()
    {
        EditorApplication.delayCall += TryRun;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += TryRun;
        };
    }

    static void TryRun()
    {
        string absolute = Path.GetFullPath(RequestPath);
        if (!File.Exists(absolute) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(absolute);
        if (File.Exists(absolute + ".meta")) File.Delete(absolute + ".meta");
        Setup();
    }
}
#endif

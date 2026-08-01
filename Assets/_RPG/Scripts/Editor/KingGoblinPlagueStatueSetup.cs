using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class KingGoblinPlagueStatueSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/KingGoblinPlagueStatue.generate";
    const string ModelPath = "Assets/Daniel Mistage/King goblin_1/Meshy_AI_Quiero_que_crees_un_r_biped/Meshy_AI_Quiero_que_crees_un_r_biped_Character_output.fbx";
    const string StoneMaterialPath = "Assets/_RPG/Materials/AnomalyDemonStatueStone.mat";
    static readonly string[] GoblinPaths =
    {
        "Assets/_RPG/Prefabs/Enemies/GoblinEnemy1.prefab",
        "Assets/_RPG/Prefabs/Enemies/GoblinEnemy2.prefab",
        "Assets/_RPG/Prefabs/Enemies/GoblinEnemy3.prefab"
    };

    [MenuItem("RPG/Quests/Setup King Goblin Plague Statue")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject statue = GameObject.Find("KingGoblinPlagueStatue");
        if (statue == null)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) { Debug.LogError("[PlagueStatue] No se encontr\u00f3 el modelo del King Goblin."); return; }
            statue = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (statue == null) statue = Object.Instantiate(model);
            statue.name = "KingGoblinPlagueStatue";
            PlaceOutsideVillage(statue.transform);
            NormalizeHeight(statue, 3.8f);
            SnapToTerrain(statue.transform);
        }

        foreach (MonoBehaviour behaviour in statue.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(behaviour is KingGoblinPlagueStatue) && !(behaviour is QuestNpcAttentionIcon))
                Object.DestroyImmediate(behaviour);
        foreach (Collider collider in statue.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        foreach (Animator animator in statue.GetComponentsInChildren<Animator>(true)) animator.enabled = false;

        Material stone = AssetDatabase.LoadAssetAtPath<Material>(StoneMaterialPath);
        if (stone != null)
            foreach (Renderer renderer in statue.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < materials.Length; i++) materials[i] = stone;
                renderer.sharedMaterials = materials;
            }

        // Keep physical collision separate from the interaction detector. Putting the only
        // collider on Interactable allowed the player's collision matrix/controller to ignore it.
        SetLayerRecursively(statue.transform, LayerMask.NameToLayer("Default"));

        GameObject solidObject = new GameObject("KingGoblinStatue_SolidCollider");
        solidObject.transform.SetParent(statue.transform, false);
        solidObject.layer = LayerMask.NameToLayer("Default");
        BoxCollider solid = solidObject.AddComponent<BoxCollider>();
        FitCollider(statue.transform, solid);
        solid.isTrigger = false;

        GameObject interactionObject = new GameObject("KingGoblinStatue_InteractionZone");
        interactionObject.transform.SetParent(statue.transform, false);
        interactionObject.layer = LayerMask.NameToLayer("Interactable");
        BoxCollider interactionTrigger = interactionObject.AddComponent<BoxCollider>();
        FitCollider(statue.transform, interactionTrigger);
        interactionTrigger.size += new Vector3(.8f, .25f, .8f);
        interactionTrigger.isTrigger = true;

        KingGoblinPlagueStatue interaction = statue.GetComponent<KingGoblinPlagueStatue>();
        if (interaction == null) interaction = statue.AddComponent<KingGoblinPlagueStatue>();
        SerializedObject serialized = new SerializedObject(interaction);
        SerializedProperty prefabs = serialized.FindProperty("goblinPrefabs");
        prefabs.arraySize = GoblinPaths.Length;
        for (int i = 0; i < GoblinPaths.Length; i++)
            prefabs.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPaths[i]);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(statue);
        EditorSceneManager.MarkSceneDirty(statue.scene);
        EditorSceneManager.SaveScene(statue.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = statue;
        Debug.Log("[PlagueStatue] Estatua del Rey Goblin colocada fuera del pueblo, con piedra, collider y prueba de la peste.");
    }

    static void PlaceOutsideVillage(Transform statue)
    {
        GameObject spawn = null;
        try { spawn = GameObject.FindWithTag("SpawnPoint"); } catch (UnityException) { }
        if (spawn == null) { statue.position = new Vector3(22f, 0f, 22f); return; }
        Vector3 forward = Vector3.ProjectOnPlane(spawn.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
        statue.position = spawn.transform.position + forward * 24f + spawn.transform.right * 13f;
        statue.rotation = Quaternion.LookRotation(-forward, Vector3.up);
    }

    static void NormalizeHeight(GameObject root, float targetHeight)
    {
        Bounds bounds = GetBounds(root.transform);
        if (bounds.size.y > .001f) root.transform.localScale *= Mathf.Clamp(targetHeight / bounds.size.y, .01f, 100f);
    }

    static void FitCollider(Transform root, BoxCollider collider)
    {
        Bounds bounds = GetBounds(root);
        collider.center = root.InverseTransformPoint(bounds.center);
        Vector3 scale = root.lossyScale;
        collider.size = new Vector3(bounds.size.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(.001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(.001f, Mathf.Abs(scale.z))) + new Vector3(.12f, .04f, .12f);
    }

    static Bounds GetBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void SnapToTerrain(Transform target)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;
        Vector3 position = target.position;
        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        target.position = position;
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        if (layer < 0) return;
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }

    [InitializeOnLoadMethod]
    static void QueueRequestedSetup() => EditorApplication.delayCall += TryRun;

    static void TryRun()
    {
        string request = Path.GetFullPath(RequestPath);
        if (!File.Exists(request)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += TryRun; return; }
        File.Delete(request);
        Setup();
    }
}

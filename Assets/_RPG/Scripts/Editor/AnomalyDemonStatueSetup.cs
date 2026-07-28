#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AnomalyDemonStatueSetup
{
    const string PrefabPath = "Assets/_RPG/Prefabs/Enemies/DemonioAnomaloBoss.prefab";
    const string MaterialPath = "Assets/_RPG/Materials/AnomalyDemonStatueStone.mat";

    [MenuItem("RPG/World/Add Anomaly Demon Statue")]
    public static void AddToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "SpawnVillage") return;
        GameObject existing = GameObject.Find("AnomalyDemonStatue");
        if (existing != null)
        {
            SetLayerRecursively(existing.transform, LayerMask.NameToLayer("Interactable"));
            EnsureInteractionCollider(existing);
            Selection.activeGameObject = existing;
            EditorSceneManager.MarkSceneDirty(existing.scene);
            return;
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject spawn = GameObject.FindWithTag("SpawnPoint");
        if (prefab == null || spawn == null) return;

        GameObject statue = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        statue.name = "AnomalyDemonStatue";
        SetLayerRecursively(statue.transform, LayerMask.NameToLayer("Interactable"));
        Vector3 forward = spawn.transform.forward; forward.y = 0f;
        if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
        statue.transform.position = spawn.transform.position + forward.normalized * 8f + spawn.transform.right * 5f;
        statue.transform.rotation = Quaternion.LookRotation(-forward.normalized, Vector3.up);

        foreach (MonoBehaviour behaviour in statue.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
        foreach (Collider collider in statue.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (Animator animator in statue.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        Material stone = GetStoneMaterial();
        foreach (Renderer renderer in statue.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = stone;

        EnsureInteractionCollider(statue);
        statue.AddComponent<AnomalyDemonStatue>();
        Selection.activeGameObject = statue;
        EditorSceneManager.MarkSceneDirty(statue.scene);
    }

    static Material GetStoneMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.name = "AnomalyDemonStatueStone";
        Texture2D stone = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float noise = Mathf.PerlinNoise(x * .14f, y * .14f) * .35f + Mathf.PerlinNoise(x * .045f + 7f, y * .045f + 3f) * .65f;
            Color baseStone = Color.Lerp(new Color(.075f, .065f, .09f), new Color(.42f, .39f, .47f), noise);
            stone.SetPixel(x, y, baseStone);
        }
        stone.Apply();
        AssetDatabase.CreateAsset(stone, "Assets/_RPG/Materials/AnomalyDemonStatueStoneTexture.asset");
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", stone);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .16f);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    static Bounds GetBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void EnsureInteractionCollider(GameObject statue)
    {
        Bounds bounds = GetBounds(statue);
        BoxCollider trigger = statue.GetComponent<BoxCollider>();
        if (trigger == null) trigger = statue.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = statue.transform.InverseTransformPoint(bounds.center);
        // Collider.size is local-space; converting world bounds prevents the statue's large
        // imported scale from creating an interaction zone far larger than its visible body.
        Vector3 localSize = statue.transform.InverseTransformVector(bounds.size);
        trigger.size = new Vector3(
            Mathf.Abs(localSize.x) + .25f,
            Mathf.Abs(localSize.y) + .08f,
            Mathf.Abs(localSize.z) + .25f);
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        if (layer < 0) return;
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }
}

[InitializeOnLoad]
static class AnomalyDemonStatueSetupOnLoad
{
    static AnomalyDemonStatueSetupOnLoad()
    {
        EditorApplication.delayCall += AnomalyDemonStatueSetup.AddToScene;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += AnomalyDemonStatueSetup.AddToScene; };
    }
}
#endif

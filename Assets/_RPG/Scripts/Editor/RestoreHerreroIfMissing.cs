using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RestoreHerreroIfMissing
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/RestoreHerrero.generate";
    const string ControllerPath = "Assets/_RPG/Animations/MerchantWalk2.controller";
    const string BlacksmithMaterialPath = "Assets/_RPG/Materials/NPC-Herrero-Blacksmith.mat";
    const string BaseMaterialPath = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Materials/RPG-Character.mat";

    [MenuItem("RPG/Restore Herrero If Missing")]
    public static void Restore()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        NPCHerrero existing = Object.FindAnyObjectByType<NPCHerrero>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            ConfigureHerrero(existing.gameObject);
            PositionHerrero(existing.transform);
            Save();
            Debug.Log("[RestoreHerrero] NPCHerrero existente reactivado y reposicionado.");
            return;
        }

        NPCMerchant merchant = Object.FindAnyObjectByType<NPCMerchant>(FindObjectsInactive.Include);
        if (merchant == null)
        {
            Debug.LogError("[RestoreHerrero] No hay NPCMerchant para clonar como base del herrero.");
            return;
        }

        GameObject herrero = Object.Instantiate(merchant.gameObject);
        herrero.name = "NPCHerrero";

        NPCMerchant merchantComponent = herrero.GetComponent<NPCMerchant>();
        if (merchantComponent != null)
            Object.DestroyImmediate(merchantComponent);

        if (herrero.GetComponent<NPCHerrero>() == null)
            herrero.AddComponent<NPCHerrero>();
        if (herrero.GetComponent<NPCWander>() == null)
            herrero.AddComponent<NPCWander>();

        ConfigureHerrero(herrero);
        PositionHerrero(herrero.transform);
        Save();

        Debug.Log("[RestoreHerrero] NPCHerrero restaurado en la escena.");
    }

    [InitializeOnLoadMethod]
    static void RunRequestedRestore()
    {
        EditorApplication.delayCall += TryRunRequestedRestore;
    }

    static void TryRunRequestedRestore()
    {
        string absoluteRequestPath = Path.GetFullPath(RequestPath);
        if (!File.Exists(absoluteRequestPath))
            return;

        File.Delete(absoluteRequestPath);
        string metaPath = absoluteRequestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        Restore();
    }

    static void ConfigureHerrero(GameObject herrero)
    {
        herrero.tag = "Interactable";
        SetLayerRecursive(herrero, LayerMask.NameToLayer("Interactable"));

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        Material material = EnsureBlacksmithMaterial();

        Animator animator = herrero.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            if (controller != null)
                animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        foreach (SkinnedMeshRenderer renderer in herrero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            renderer.sharedMaterials = materials;
        }

        foreach (MeshRenderer renderer in herrero.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.transform.name.StartsWith("Blacksmith", System.StringComparison.Ordinal))
                continue;
            renderer.sharedMaterial = material;
        }

        NPCWander wander = herrero.GetComponent<NPCWander>();
        if (wander != null)
            wander.Configure(speed: 0.75f, radius: 5f, step: 2.1f, minWait: 2.5f, maxWait: 5.5f);

        if (herrero.GetComponent<GroundSnapOnStart>() == null)
            herrero.AddComponent<GroundSnapOnStart>();

        NPCHerrero blacksmith = herrero.GetComponent<NPCHerrero>();
        if (blacksmith != null)
        {
            var serialized = new SerializedObject(blacksmith);
            SerializedProperty walkController = serialized.FindProperty("walkController");
            if (walkController != null)
                walkController.objectReferenceValue = controller;
            SerializedProperty blacksmithMaterial = serialized.FindProperty("blacksmithMaterial");
            if (blacksmithMaterial != null)
                blacksmithMaterial.objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (Collider collider in herrero.GetComponentsInChildren<Collider>(true))
            collider.gameObject.layer = LayerMask.NameToLayer("Interactable");
    }

    static Material EnsureBlacksmithMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BlacksmithMaterialPath);
        Material baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        if (material == null)
        {
            material = baseMaterial != null ? new Material(baseMaterial) : new Material(shader);
            material.name = "NPC-Herrero-Blacksmith";
            AssetDatabase.CreateAsset(material, BlacksmithMaterialPath);
        }

        if (shader != null)
            material.shader = shader;

        Texture texture = baseMaterial != null ? baseMaterial.mainTexture : null;
        if (texture != null)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        }

        Color leather = new Color(0.45f, 0.29f, 0.16f, 1f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", leather);
        if (material.HasProperty("_Color")) material.SetColor("_Color", leather);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.05f);

        EditorUtility.SetDirty(material);
        return material;
    }

    static void PositionHerrero(Transform herrero)
    {
        herrero.position = new Vector3(3f, 0.05f, 5f);
        herrero.rotation = Quaternion.Euler(0f, 180f, 0f);

        GroundSnapOnStart snapper = herrero.GetComponent<GroundSnapOnStart>();
        if (snapper != null)
            snapper.SnapNow();

        EditorUtility.SetDirty(herrero.gameObject);
    }

    static void SetLayerRecursive(GameObject root, int layer)
    {
        if (layer < 0)
            return;

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            transform.gameObject.layer = layer;
    }

    static void Save()
    {
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}

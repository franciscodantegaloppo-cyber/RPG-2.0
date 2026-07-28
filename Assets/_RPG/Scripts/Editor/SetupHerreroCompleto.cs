using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupHerreroCompleto
{
    const string CONTROLLER_PATH = "Assets/_RPG/Animations/MerchantWalk2.controller";
    const string MAT_PATH        = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Materials/RPG-Character.mat";

    [MenuItem("RPG/Setup Herrero Completo")]
    static void Run()
    {
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CONTROLLER_PATH);
        var baseMat    = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);

        if (controller == null) { Debug.LogError("No encontré el controller: " + CONTROLLER_PATH); return; }
        if (baseMat    == null) { Debug.LogError("No encontré el material: " + MAT_PATH); return; }

        // 1. Crear material URP para el herrero (tinte cobre)
        string herreroMatPath = "Assets/_RPG/Materials/NPC-Herrero-Blacksmith.mat";
        System.IO.Directory.CreateDirectory("Assets/_RPG/Materials");
        var herreroMat = AssetDatabase.LoadAssetAtPath<Material>(herreroMatPath);
        if (herreroMat == null)
        {
            herreroMat = new Material(baseMat);
            AssetDatabase.CreateAsset(herreroMat, herreroMatPath);
        }

        // Fuerza URP/Lit independientemente del shader actual
        var urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null) urpShader = Shader.Find("Lit");
        if (urpShader != null)
        {
            // Guarda textura del material original antes de cambiar shader
            var originalTex = baseMat.mainTexture;
            herreroMat.shader = urpShader;
            if (originalTex != null)
            {
                herreroMat.SetTexture("_BaseMap", originalTex);
                Debug.Log("[Herrero] Textura aplicada: " + originalTex.name);
            }
            herreroMat.SetColor("_BaseColor", new Color(0.45f, 0.29f, 0.16f, 1f));
            if (herreroMat.HasProperty("_Color")) herreroMat.SetColor("_Color", new Color(0.45f, 0.29f, 0.16f, 1f));
            if (herreroMat.HasProperty("_Smoothness")) herreroMat.SetFloat("_Smoothness", 0.18f);
        }
        else
        {
            Debug.LogWarning("[Herrero] No encontré URP/Lit, usando material base tal cual.");
            herreroMat = baseMat;
        }
        EditorUtility.SetDirty(herreroMat);
        AssetDatabase.SaveAssets();

        // 2. Borrar herreros viejos
        foreach (var old in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (old.GetType().Name == "NPCHerrero")
                Undo.DestroyObjectImmediate(old.gameObject);
        }

        // 3. Clonar el merchant como base
        NPCMerchant merchant = Object.FindAnyObjectByType<NPCMerchant>();
        if (merchant == null) { Debug.LogError("No hay NPCMerchant en la escena."); return; }

        var herrero = Object.Instantiate(merchant.gameObject);
        herrero.name = "NPCHerrero";
        Undo.RegisterCreatedObjectUndo(herrero, "Spawn NPCHerrero");

        // 4. Intercambiar componente merchant → herrero
        var oldComp = herrero.GetComponent<NPCMerchant>();
        if (oldComp != null) Object.DestroyImmediate(oldComp);
        var herreroComp = herrero.AddComponent<NPCHerrero>();
        var wander = herrero.GetComponent<NPCWander>();
        if (wander == null)
            wander = herrero.AddComponent<NPCWander>();
        wander.Configure(speed: 0.75f, radius: 5f, step: 2.1f, minWait: 2.5f, maxWait: 5.5f);

        // 5. Aplicar material con tinte cobre a todos los renderers
        foreach (var smr in herrero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = herreroMat;
            smr.sharedMaterials = mats;
        }

        // 6. Asignar controller y desactivar root motion
        var anim = herrero.GetComponentInChildren<Animator>(true);
        if (anim != null)
        {
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            Debug.Log("[Herrero] Animator controller asignado: " + controller.name);
        }
        else Debug.LogWarning("[Herrero] No encontré Animator en el herrero.");

        var so = new SerializedObject(herreroComp);
        so.FindProperty("walkController").objectReferenceValue = controller;
        so.FindProperty("blacksmithMaterial").objectReferenceValue = herreroMat;
        so.ApplyModifiedProperties();

        // 7. Posicionar dentro de la primera casa disponible
        Vector3 housePos = FindHouseInterior();
        herrero.transform.position = housePos;
        herrero.transform.rotation = Quaternion.identity;
        Debug.Log("[Herrero] Posicionado en: " + housePos);

        // 8. Layer Interactable (10)
        herrero.layer = 10;
        foreach (var col in herrero.GetComponentsInChildren<Collider>())
            col.gameObject.layer = 10;

        EditorUtility.SetDirty(herrero);
        EditorSceneManager.MarkAllScenesDirty();

        Selection.activeGameObject = herrero;
        Debug.Log("[Herrero] Setup completo. Guardá con Ctrl+S.");
        EditorUtility.DisplayDialog("Herrero listo",
            "NPCHerrero creado con textura y animaciones.\n" +
            "Posicionado en: " + housePos + "\n\n" +
            "Agregá ítems en NPCHerrero → Shop Items y guardá con Ctrl+S.", "OK");
    }

    static Vector3 FindHouseInterior()
    {
        // Busca cualquier objeto que parezca una casa
        string[] houseKeywords = { "house", "casa", "building", "inn", "shop", "tavern", "hut" };
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);

        foreach (var go in allGOs)
        {
            string nameLower = go.name.ToLower();
            foreach (var kw in houseKeywords)
            {
                if (nameLower.Contains(kw) && go.transform.parent == null)
                {
                    // Posición interior: centro del objeto + 0.5 altura + 1m adelante
                    var bounds = GetBounds(go);
                    Vector3 interior = bounds.center;
                    interior.y = bounds.min.y + 0.05f; // piso
                    Debug.Log("[Herrero] Casa encontrada: " + go.name + " bounds=" + bounds);
                    return interior;
                }
            }
        }

        // Fallback: cerca del merchant pero desplazado
        var merchant = Object.FindAnyObjectByType<NPCMerchant>();
        if (merchant != null)
            return merchant.transform.position + new Vector3(8f, 0f, 0f);

        return new Vector3(5f, 0f, 5f);
    }

    static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }
}

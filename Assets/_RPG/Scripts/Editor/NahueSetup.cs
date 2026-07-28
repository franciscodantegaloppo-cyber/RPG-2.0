using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class NahueSetup
{
    const string MAT_PATH = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Materials/RPG-Character.mat";
    const string WALK_CONTROLLER_PATH = "Assets/_RPG/Animations/MerchantWalk2.controller";
    const string GANZ_PALETTE_MAT_PATH = "Assets/URP GanzSe Free Modular Character Pack/Material/Base Palette Material URP.mat";

    // Same bug as Tonio/Herrero (see TonioSetup.FixTonioArmor / BlacksmithSetup.FixHerreroArmor):
    // Nahue's GanzSe model has every part renderer's material overridden to a flat green-tinted
    // "NPC-Nahue" material instead of the pack's own textured "Base Palette Material URP". The
    // already-active part selection (Chest/Arm/Belt/Legs/Feet Armor Type 1, same set as Herrero)
    // is already a good non-overlapping armor look once the real texture is restored - confirmed
    // via in-editor camera capture - so only the material needs fixing here.
    [MenuItem("RPG/Fix Nahue Armor (GanzSe Low Poly)")]
    static void FixNahueArmor()
    {
        NahueQuestGiver nahue = Object.FindAnyObjectByType<NahueQuestGiver>(FindObjectsInactive.Include);
        if (nahue == null)
        {
            Debug.LogError("[NahueSetup] No hay NPC Nahue en la escena.");
            return;
        }

        GameObject go = nahue.gameObject;
        Transform ganz = go.transform.Find("GanzMerchant");
        if (ganz == null)
        {
            Debug.LogError("[NahueSetup] Nahue no tiene un hijo 'GanzMerchant' - este fix es para el modelo GanzSe.");
            return;
        }

        Material palette = AssetDatabase.LoadAssetAtPath<Material>(GANZ_PALETTE_MAT_PATH);
        if (palette == null)
        {
            Debug.LogError("[NahueSetup] No se encontro " + GANZ_PALETTE_MAT_PATH);
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(ganz.gameObject, "Fix Nahue Armor");
        int total = 0;
        foreach (var smr in ganz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.sharedMaterial = palette;
            total++;
        }

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkAllScenesDirty();

        EditorUtility.DisplayDialog("Nahue: armadura arreglada",
            $"Se restauro la textura real del pack GanzSe en las {total} piezas del modelo " +
            "(el set de armadura ya activo era correcto, solo tenia el tinte verde plano encima).\n\n" +
            "Ctrl+S para guardar la escena.", "OK");
    }

    [MenuItem("RPG/Spawn NPC Nahue in Scene")]
    static void SpawnNahue()
    {
        // Borra cualquier Nahue viejo que este en la escena
        foreach (var old in Object.FindObjectsByType<NahueQuestGiver>(FindObjectsInactive.Include))
            Undo.DestroyObjectImmediate(old.gameObject);

        // Clona al merchant existente - ya tiene modelo, textura, animator, CC y grounding
        // funcionando, mismo patron que TonioSetup.cs.
        var merchant = Object.FindAnyObjectByType<NPCMerchant>();
        if (merchant == null)
        {
            Debug.LogError("[NahueSetup] No hay NPCMerchant en la escena. Necesitamos clonarlo como base.");
            return;
        }

        var nahue = Object.Instantiate(merchant.gameObject);
        nahue.name = "NPCNahue";
        Undo.RegisterCreatedObjectUndo(nahue, "Spawn NPC Nahue");

        // Posicion: al lado opuesto del merchant y separado de Tonio, para no superponerse
        nahue.transform.position = merchant.transform.position + merchant.transform.right * 8f;
        nahue.transform.rotation = merchant.transform.rotation;

        var oldMerchant = nahue.GetComponent<NPCMerchant>();
        if (oldMerchant != null) Object.DestroyImmediate(oldMerchant);

        if (nahue.GetComponent<NahueQuestGiver>() == null)
            Undo.AddComponent<NahueQuestGiver>(nahue);
        if (nahue.GetComponent<NPCWander>() == null)
            Undo.AddComponent<NPCWander>(nahue);

        // Material distinto (tinte verdoso) para distinguirlo del resto de NPCs
        string nahueMatPath = "Assets/_RPG/Materials/NPC-Nahue.mat";
        System.IO.Directory.CreateDirectory("Assets/_RPG/Materials");

        var baseMat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
        var nahueMat = AssetDatabase.LoadAssetAtPath<Material>(nahueMatPath);
        if (nahueMat == null && baseMat != null)
        {
            nahueMat = new Material(baseMat);
            Color tint = new Color(0.24f, 0.42f, 0.22f, 1f);
            nahueMat.SetColor("_BaseColor", tint);
            if (nahueMat.HasProperty("_Color")) nahueMat.SetColor("_Color", tint);
            if (nahueMat.HasProperty("_Smoothness")) nahueMat.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(nahueMat, nahueMatPath);
            AssetDatabase.SaveAssets();
        }

        if (nahueMat != null)
        {
            foreach (var smr in nahue.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = new Material[smr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = nahueMat;
                smr.sharedMaterials = mats;
            }
            foreach (var mr in nahue.GetComponentsInChildren<MeshRenderer>(true))
                mr.sharedMaterial = nahueMat;
        }

        // Layer Interactable (10), igual que el resto de NPCs con dialogo
        nahue.layer = 10;
        foreach (var col in nahue.GetComponentsInChildren<Collider>())
            col.gameObject.layer = 10;

        var wander = nahue.GetComponent<NPCWander>();
        wander.Configure(speed: 0.65f, radius: 4f, step: 1.8f, minWait: 3f, maxWait: 6f);

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WALK_CONTROLLER_PATH);
        var anim = nahue.GetComponentInChildren<Animator>(true);
        if (anim != null && controller != null)
        {
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
        }

        Selection.activeGameObject = nahue;
        EditorSceneManager.MarkAllScenesDirty();

        EditorUtility.DisplayDialog("NPC Nahue creado",
            "Se clono el merchant como base de Elnahue (mismo modelo/animator/CC/grounding, tinte verdoso).\n\n" +
            "Su mision (cazar goblins) se desbloquea recien despues de completar la mision de los " +
            "10 esqueletos de Tonio.\n\n" +
            "Pasos:\n" +
            "1. Reposicionalo donde quieras en el mapa (Scene view)\n" +
            "2. Ctrl+S para guardar la escena", "OK");
    }
}

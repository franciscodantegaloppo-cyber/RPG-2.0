using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public static class TonioSetup
{
    const string MAT_PATH = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Materials/RPG-Character.mat";
    const string WALK_CONTROLLER_PATH = "Assets/_RPG/Animations/MerchantWalk2.controller";
    const string GANZ_PALETTE_MAT_PATH = "Assets/URP GanzSe Free Modular Character Pack/Material/Base Palette Material URP.mat";

    // Tonio's actual visual model (the "GanzMerchant" child) is the GanzSe modular character
    // pack, not the RPG-Character mesh this file's older SpawnTonio() targets - some earlier setup
    // pass left EVERY one of its 216 part renderers (every armor type/color, every hair/face
    // variant) enabled at once, all sharing one flat blue-tinted "NPC-Tonio" material instead of
    // the pack's own textured "Base Palette Material URP". That reads as a shapeless blue blob
    // with z-fighting armor plates layered on top of each other, not "painted blue armor" - there
    // was no real armor selected at all, just every option overlapping with no real texture.
    // Type 3 turned out to be a bare-chest collar/pauldron piece - almost no torso coverage, so
    // it barely read as "armor" once the flat blue override was gone. Type 5 is a full jacket +
    // pauldron + gauntlets + matching leg/boot plates (checked side-by-side via in-editor camera
    // captures of each type), Color 3 for a grounded dark/teel tone instead of Color 1's neon
    // blue-purple.
    static readonly List<string> GoodArmorSet = new List<string>
    {
        "Base Character Mesh",
        "Chest Armor Type 5 Color 3",
        "Arm Armor Type 5 Color 3",
        "Belt Armor Type 5 Color 3",
        "Legs Armor Type 5 Color 3",
        "Feet Armor Type 5 Color 3",
        "Hair Type 5 Color 5",
        "Face Hair Type 5 Color 5",
        "Eyes Type 1 Color 1",
        "Eyebrow Type 1 Color 1",
        "Nose Type 2",
        "Ears Type 1",
    };

    [MenuItem("RPG/Fix Tonio Armor (GanzSe Low Poly)")]
    static void FixTonioArmor()
    {
        TonioQuestGiver tonio = Object.FindAnyObjectByType<TonioQuestGiver>(FindObjectsInactive.Include);
        if (tonio == null)
        {
            Debug.LogError("[TonioSetup] No hay NPC Tonio en la escena.");
            return;
        }

        GameObject go = tonio.gameObject;
        Transform ganz = go.transform.Find("GanzMerchant");
        if (ganz == null)
        {
            Debug.LogError("[TonioSetup] Tonio no tiene un hijo 'GanzMerchant' - este fix es para el modelo GanzSe.");
            return;
        }

        Material palette = AssetDatabase.LoadAssetAtPath<Material>(GANZ_PALETTE_MAT_PATH);
        if (palette == null)
        {
            Debug.LogError("[TonioSetup] No se encontro " + GANZ_PALETTE_MAT_PATH);
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(ganz.gameObject, "Fix Tonio Armor");

        int enabledCount = 0;
        foreach (var smr in ganz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.sharedMaterial = palette;
            smr.enabled = true;
            // Visibility is gated by the GameObject's own active state, not Renderer.enabled -
            // the pack's part GameObjects ship inactive by default and toggling only .enabled
            // left them invisible even though this method reported them as "enabled".
            bool keep = GoodArmorSet.Contains(smr.name);
            smr.gameObject.SetActive(keep);
            if (keep) enabledCount++;
        }

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkAllScenesDirty();

        EditorUtility.DisplayDialog("Tonio: armadura arreglada",
            $"Se restauro la textura real del pack GanzSe y se dejo un set de armadura completo activo " +
            $"({enabledCount} piezas), en vez de las 216 variantes superpuestas con tinte azul plano.\n\n" +
            "Ctrl+S para guardar la escena.", "OK");
    }

    [MenuItem("RPG/Spawn NPC Tonio in Scene")]
    static void SpawnTonio()
    {
        // Borra cualquier Tonio viejo que este en la escena
        foreach (var old in Object.FindObjectsByType<TonioQuestGiver>(FindObjectsInactive.Include))
            Undo.DestroyObjectImmediate(old.gameObject);

        // Clona al merchant existente - ya tiene modelo, textura, animator, CC y grounding
        // funcionando (NPCWander se encarga de mantenerlo pegado al terreno, igual que el
        // player y los animals).
        var merchant = Object.FindAnyObjectByType<NPCMerchant>();
        if (merchant == null)
        {
            Debug.LogError("[TonioSetup] No hay NPCMerchant en la escena. Necesitamos clonarlo como base.");
            return;
        }

        var tonio = Object.Instantiate(merchant.gameObject);
        tonio.name = "NPCTonio";
        Undo.RegisterCreatedObjectUndo(tonio, "Spawn NPC Tonio");

        // Posicion: al lado opuesto del merchant, para no superponerse
        tonio.transform.position = merchant.transform.position - merchant.transform.right * 8f;
        tonio.transform.rotation = merchant.transform.rotation;

        var oldMerchant = tonio.GetComponent<NPCMerchant>();
        if (oldMerchant != null) Object.DestroyImmediate(oldMerchant);

        if (tonio.GetComponent<TonioQuestGiver>() == null)
            Undo.AddComponent<TonioQuestGiver>(tonio);
        if (tonio.GetComponent<NPCWander>() == null)
            Undo.AddComponent<NPCWander>(tonio);

        // Material distinto (tinte azulado) para distinguirlo del merchant/herrero
        string tonioMatPath = "Assets/_RPG/Materials/NPC-Tonio.mat";
        System.IO.Directory.CreateDirectory("Assets/_RPG/Materials");

        var baseMat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
        var tonioMat = AssetDatabase.LoadAssetAtPath<Material>(tonioMatPath);
        if (tonioMat == null && baseMat != null)
        {
            tonioMat = new Material(baseMat);
            Color tint = new Color(0.22f, 0.32f, 0.46f, 1f);
            tonioMat.SetColor("_BaseColor", tint);
            if (tonioMat.HasProperty("_Color")) tonioMat.SetColor("_Color", tint);
            if (tonioMat.HasProperty("_Smoothness")) tonioMat.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(tonioMat, tonioMatPath);
            AssetDatabase.SaveAssets();
        }

        if (tonioMat != null)
        {
            foreach (var smr in tonio.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = new Material[smr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = tonioMat;
                smr.sharedMaterials = mats;
            }
            foreach (var mr in tonio.GetComponentsInChildren<MeshRenderer>(true))
                mr.sharedMaterial = tonioMat;
        }

        // Layer Interactable (10), igual que el resto de NPCs con dialogo
        tonio.layer = 10;
        foreach (var col in tonio.GetComponentsInChildren<Collider>())
            col.gameObject.layer = 10;

        var wander = tonio.GetComponent<NPCWander>();
        wander.Configure(speed: 0.55f, radius: 4f, step: 1.8f, minWait: 3f, maxWait: 6f);

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WALK_CONTROLLER_PATH);
        var anim = tonio.GetComponentInChildren<Animator>(true);
        if (anim != null && controller != null)
        {
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
        }

        Selection.activeGameObject = tonio;
        EditorSceneManager.MarkAllScenesDirty();

        EditorUtility.DisplayDialog("NPC Tonio creado",
            "Se clono el merchant como base de Tonio (mismo modelo/animator/CC/grounding, tinte azulado).\n\n" +
            "Pasos:\n" +
            "1. Reposicionalo donde quieras en el mapa (Scene view)\n" +
            "2. Jugá y presioná E para hablarle y aceptar la mision de los esqueletos\n" +
            "3. Ctrl+S para guardar la escena", "OK");
    }
}

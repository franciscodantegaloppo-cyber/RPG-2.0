using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class BlacksmithSetup
{
    const string NPC_PREFAB_PATH = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Prefabs/Character/RPG-Character-NPC.prefab";
    const string MAT_PATH       = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Materials/RPG-Character.mat";
    const string GANZ_PALETTE_MAT_PATH = "Assets/URP GanzSe Free Modular Character Pack/Material/Base Palette Material URP.mat";

    // Same bug as Tonio (see TonioSetup.FixTonioArmor): NPCHerrero's actual visual model is the
    // GanzSe modular character pack ("GanzMerchant" child), but every one of its part renderers -
    // including the already-active, already-sensible Chest/Arm/Belt/Legs/Feet Armor Type 1 set -
    // got its material overridden to a flat brown-tinted "NPC-Herrero-Blacksmith" material instead
    // of the pack's own textured "Base Palette Material URP", so the whole body read as a solid
    // brown color instead of real armor detail. Unlike Tonio, the active part selection here was
    // already a good non-overlapping set (confirmed via in-editor camera capture) - only the
    // material needed restoring, no part selection changes.
    [MenuItem("RPG/Fix Herrero Armor (GanzSe Low Poly)")]
    static void FixHerreroArmor()
    {
        var herrero = Object.FindAnyObjectByType<NPCHerrero>(FindObjectsInactive.Include);
        if (herrero == null) { Debug.LogError("[BlacksmithSetup] No hay NPCHerrero en la escena."); return; }

        GameObject go = herrero.gameObject;
        Transform ganz = go.transform.Find("GanzMerchant");
        if (ganz == null)
        {
            Debug.LogError("[BlacksmithSetup] Herrero no tiene un hijo 'GanzMerchant' - este fix es para el modelo GanzSe.");
            return;
        }

        Material palette = AssetDatabase.LoadAssetAtPath<Material>(GANZ_PALETTE_MAT_PATH);
        if (palette == null)
        {
            Debug.LogError("[BlacksmithSetup] No se encontro " + GANZ_PALETTE_MAT_PATH);
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(ganz.gameObject, "Fix Herrero Armor");
        int total = 0;
        foreach (var smr in ganz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.sharedMaterial = palette;
            total++;
        }

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkAllScenesDirty();

        EditorUtility.DisplayDialog("Herrero: armadura arreglada",
            $"Se restauro la textura real del pack GanzSe en las {total} piezas del modelo " +
            "(el set de armadura ya activo era correcto, solo tenia el tinte marron plano encima).\n\n" +
            "Ctrl+S para guardar la escena.", "OK");
    }

    [MenuItem("RPG/Spawn NPCHerrero in Scene")]
    static void SpawnHerrero()
    {
        // Borra cualquier NPCHerrero viejo que esté en la escena
        foreach (var old in Object.FindObjectsByType<NPCHerrero>(FindObjectsInactive.Include))
            Undo.DestroyObjectImmediate(old.gameObject);

        // Clona al merchant existente — ya tiene modelo, textura, animator y CC funcionando
        var merchant = Object.FindAnyObjectByType<NPCMerchant>();
        if (merchant == null)
        {
            Debug.LogError("[BlacksmithSetup] No hay NPCMerchant en la escena. Necesitamos clonarlo como base.");
            return;
        }

        var herrero = Object.Instantiate(merchant.gameObject);
        herrero.name = "NPCHerrero";
        Undo.RegisterCreatedObjectUndo(herrero, "Spawn NPCHerrero");

        // Posición: a 8m a la derecha del merchant
        herrero.transform.position = merchant.transform.position + merchant.transform.right * 8f;
        herrero.transform.rotation = merchant.transform.rotation;

        // Quita componentes del merchant, agrega los del herrero
        var oldMerchant = herrero.GetComponent<NPCMerchant>();
        if (oldMerchant != null) Object.DestroyImmediate(oldMerchant);

        if (herrero.GetComponent<NPCHerrero>() == null)
            Undo.AddComponent<NPCHerrero>(herrero);
        if (herrero.GetComponent<NPCWander>() == null)
            Undo.AddComponent<NPCWander>(herrero);

        // Material diferente: tinte rojizo/cobre para distinguirlo del merchant
        string herreroMatPath = "Assets/_RPG/Materials/NPC-Herrero-Blacksmith.mat";
        System.IO.Directory.CreateDirectory("Assets/_RPG/Materials");

        var baseMat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
        var herreroMat = AssetDatabase.LoadAssetAtPath<Material>(herreroMatPath);
        if (herreroMat == null && baseMat != null)
        {
            herreroMat = new Material(baseMat);
            // URP usa _BaseColor, no .color
            herreroMat.SetColor("_BaseColor", new Color(0.45f, 0.29f, 0.16f, 1f));
            if (herreroMat.HasProperty("_Color")) herreroMat.SetColor("_Color", new Color(0.45f, 0.29f, 0.16f, 1f));
            if (herreroMat.HasProperty("_Smoothness")) herreroMat.SetFloat("_Smoothness", 0.18f);
            AssetDatabase.CreateAsset(herreroMat, herreroMatPath);
            AssetDatabase.SaveAssets();
        }

        if (herreroMat != null)
        {
            foreach (var smr in herrero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = new Material[smr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = herreroMat;
                smr.sharedMaterials = mats;
            }
            foreach (var mr in herrero.GetComponentsInChildren<MeshRenderer>(true))
                mr.sharedMaterial = herreroMat;
        }

        // Layer Interactable (10)
        herrero.layer = 10;
        foreach (var col in herrero.GetComponentsInChildren<Collider>())
            col.gameObject.layer = 10;

        ApplyBlacksmithRuntimeReferences(herrero, herreroMat);

        Selection.activeGameObject = herrero;
        EditorSceneManager.MarkAllScenesDirty();

        EditorUtility.DisplayDialog("NPCHerrero creado",
            "Se clonó el merchant como base del herrero.\n" +
            "Tiene el mismo modelo, textura (tinte oscuro) y animaciones.\n\n" +
            "Pasos:\n" +
            "1. Posicionalo en el mapa\n" +
            "2. Inspector → NPCHerrero → Shop Items: agregá armas con precio\n" +
            "3. RPG > Setup Blacksmith Shop UI (si no existe el panel)\n" +
            "4. Ctrl+S para guardar", "OK");
    }

    [MenuItem("RPG/Fix NPCHerrero Visuals")]
    static void FixHerrero()
    {
        var herrero = Object.FindAnyObjectByType<NPCHerrero>();
        if (herrero == null) { Debug.LogError("No hay NPCHerrero en la escena."); return; }

        var go = herrero.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(go, "Fix NPCHerrero Visuals");

        // 1. Busca el Animator Controller del pack
        string controllerPath = "Assets/_RPG/Animations/MerchantWalk2.controller";
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

        var anim = go.GetComponentInChildren<Animator>(true);
        if (anim != null && controller != null)
        {
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            Debug.Log("[Fix] Animator Controller asignado.");
        }
        else
        {
            Debug.LogWarning($"[Fix] anim={anim}, controller={controller}");
        }

        // 2. Aplica el material base (RPG-Character.mat) — textura original del pack
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_RPG/Materials/NPC-Herrero-Blacksmith.mat");
        if (mat != null)
        {
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = new Material[smr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                smr.sharedMaterials = mats;
                Debug.Log($"[Fix] Material aplicado a {smr.name}");
            }
        }
        else Debug.LogError("[Fix] No se encontro NPC-Herrero-Blacksmith.mat.");

        ApplyBlacksmithRuntimeReferences(go, mat);

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[BlacksmithSetup] NPCHerrero visuals arreglados. Guardá con Ctrl+S.");
    }

    [MenuItem("RPG/Move Herrero to Spawn Area")]
    static void MoveHerreroToSpawn()
    {
        var herrero = Object.FindAnyObjectByType<NPCHerrero>();
        if (herrero == null) { Debug.LogError("No NPCHerrero in scene."); return; }
        Undo.RegisterFullObjectHierarchyUndo(herrero.gameObject, "Move Herrero to Spawn");
        herrero.transform.position = new Vector3(3f, 0.05f, 5f);
        herrero.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        EditorUtility.SetDirty(herrero.gameObject);
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[BlacksmithSetup] Herrero movido a (3, 0.05, 5). Guardá con Ctrl+S.");
    }

    [MenuItem("RPG/Apply Herrero Dark Tint")]
    static void ApplyHerreroDarkTint()
    {
        var herrero = Object.FindAnyObjectByType<NPCHerrero>();
        if (herrero == null) { Debug.LogError("No NPCHerrero en escena."); return; }

        string matPath = "Assets/_RPG/Materials/NPC-Herrero-Dark.mat";
        System.IO.Directory.CreateDirectory("Assets/_RPG/Materials");

        var baseMat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
        var darkMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (darkMat == null)
        {
            darkMat = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            darkMat.name = "NPC-Herrero-Dark";
            AssetDatabase.CreateAsset(darkMat, matPath);
        }
        // Dark charcoal tint — makes herrero look like he works with fire/metal
        Color tint = new Color(0.35f, 0.28f, 0.22f, 1f);
        if (darkMat.HasProperty("_BaseColor")) darkMat.SetColor("_BaseColor", tint);
        if (darkMat.HasProperty("_Color"))     darkMat.SetColor("_Color", tint);
        if (darkMat.HasProperty("_Smoothness")) darkMat.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(darkMat);
        AssetDatabase.SaveAssets();

        Undo.RegisterFullObjectHierarchyUndo(herrero.gameObject, "Apply Herrero Dark Tint");
        foreach (var smr in herrero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = darkMat;
            smr.sharedMaterials = mats;
        }
        EditorUtility.SetDirty(herrero.gameObject);
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[BlacksmithSetup] Tinte oscuro carbón aplicado al herrero. Guardá con Ctrl+S.");
    }

    // Built-in sprite that Unity UI uses by default — required in URP or Image renders magenta
    static Sprite GetUISprite() =>
        AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    static Image AddImage(GameObject go, Color color)
    {
        var img = go.AddComponent<Image>();
        img.sprite = GetUISprite();
        img.type   = Image.Type.Sliced;
        img.color  = color;
        return img;
    }

    static void ApplyBlacksmithRuntimeReferences(GameObject go, Material herreroMat)
    {
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_RPG/Animations/MerchantWalk2.controller");
        var anim = go.GetComponentInChildren<Animator>(true);
        if (anim != null && controller != null)
        {
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
        }

        var wander = go.GetComponent<NPCWander>();
        if (wander == null)
            wander = Undo.AddComponent<NPCWander>(go);
        wander.Configure(speed: 0.75f, radius: 5f, step: 2.1f, minWait: 2.5f, maxWait: 5.5f);

        var herrero = go.GetComponent<NPCHerrero>();
        if (herrero != null)
        {
            var so = new SerializedObject(herrero);
            so.FindProperty("walkController").objectReferenceValue = controller;
            so.FindProperty("blacksmithMaterial").objectReferenceValue = herreroMat;
            so.ApplyModifiedProperties();
        }
    }

    [MenuItem("RPG/Setup Blacksmith Shop UI")]
    static void Run()
    {
        var existing = Object.FindObjectsByType<BlacksmithShopPanel>(FindObjectsInactive.Include);
        if (existing.Length > 0)
        {
            Debug.Log("[BlacksmithSetup] BlacksmithShopPanel ya existe en la escena: " + existing[0].name);
            return;
        }

        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("No hay Canvas en la escena."); return; }

        // ── Panel raíz ────────────────────────────────────────────────
        var panelGO = new GameObject("BlacksmithShopPanel");
        Undo.RegisterCreatedObjectUndo(panelGO, "Create Blacksmith Shop Panel");
        panelGO.transform.SetParent(canvas.transform, false);

        var panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.1f, 0.1f);
        panelRect.anchorMax = new Vector2(0.9f, 0.9f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        AddImage(panelGO, new Color(0.08f, 0.06f, 0.04f, 0.95f));

        // ── Título ───────────────────────────────────────────────────
        var titleGO = CreateTMP(panelGO, "Title", "Herrería", 20, TMPro.FontStyles.Bold,
            new Color(1f, 0.8f, 0.3f),
            new Vector2(0f, 0.88f), new Vector2(1f, 1f));

        // ── Oro ──────────────────────────────────────────────────────
        var goldGO = CreateTMP(panelGO, "GoldText", "Oro: 0g", 15, TMPro.FontStyles.Normal,
            new Color(1f, 0.9f, 0.2f),
            new Vector2(0.65f, 0.88f), new Vector2(1f, 1f));

        // ── Feedback ─────────────────────────────────────────────────
        var feedbackGO = CreateTMP(panelGO, "FeedbackText", "", 13, TMPro.FontStyles.Italic,
            new Color(0.5f, 1f, 0.5f),
            new Vector2(0f, 0.82f), new Vector2(1f, 0.89f));

        // ── Lista de items (scroll) ───────────────────────────────────
        var scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(panelGO.transform, false);
        var scrollRect = scrollGO.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.02f, 0.12f);
        scrollRect.anchorMax = new Vector2(0.98f, 0.82f);
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        AddImage(viewportGO, new Color(0, 0, 0, 0));
        viewportGO.AddComponent<Mask>().showMaskGraphic = false;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot     = new Vector2(0.5f, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth   = true;
        vlg.childControlHeight  = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing   = 6;
        vlg.padding   = new RectOffset(8, 8, 6, 6);
        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content  = contentRect;
        scroll.viewport = viewportRect;

        // ── Row prefab ───────────────────────────────────────────────
        var rowPrefab = CreateItemRow();

        // ── Botón cerrar ─────────────────────────────────────────────
        var closeBtnGO = new GameObject("CloseButton");
        closeBtnGO.transform.SetParent(panelGO.transform, false);
        var closeBtnRect = closeBtnGO.AddComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(0.35f, 0.02f);
        closeBtnRect.anchorMax = new Vector2(0.65f, 0.1f);
        closeBtnRect.offsetMin = Vector2.zero;
        closeBtnRect.offsetMax = Vector2.zero;
        AddImage(closeBtnGO, new Color(0.5f, 0.15f, 0.1f));
        var closeBtn = closeBtnGO.AddComponent<Button>();
        var closeLbl = CreateTMP(closeBtnGO, "Label", "Cerrar", 14, TMPro.FontStyles.Normal,
            Color.white, Vector2.zero, Vector2.one);

        // ── Wire BlacksmithShopPanel ──────────────────────────────────
        // Component goes on panelGO itself. Instance is resolved lazily via
        // FindObjectsByType(FindObjectsInactive.Include), so it works
        // even while panelGO is inactive. Awake() no longer calls
        // panel.SetActive(false), avoiding the self-deactivation race.
        var shopComp = panelGO.AddComponent<BlacksmithShopPanel>();
        var so = new SerializedObject(shopComp);
        so.FindProperty("panel").objectReferenceValue           = panelGO;
        so.FindProperty("itemListParent").objectReferenceValue  = contentGO.transform;
        so.FindProperty("itemRowPrefab").objectReferenceValue   = rowPrefab;
        so.FindProperty("goldText").objectReferenceValue        = goldGO.GetComponent<TextMeshProUGUI>();
        so.FindProperty("feedbackText").objectReferenceValue    = feedbackGO.GetComponent<TextMeshProUGUI>();
        so.FindProperty("closeButton").objectReferenceValue     = closeBtn;
        so.ApplyModifiedProperties();

        panelGO.SetActive(false);
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[BlacksmithSetup] Panel de herrería creado. Guardá con Ctrl+S.");
    }

    static GameObject CreateItemRow()
    {
        // Prefab temporal en memoria (no en Assets) — se instancia por código
        var row = new GameObject("ShopItemRow");
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, 48);

        AddImage(row, new Color(0.15f, 0.12f, 0.08f, 0.9f));

        // Name
        CreateTMP(row, "ItemName", "Arma", 14, TMPro.FontStyles.Bold, Color.white,
            new Vector2(0f, 0f), new Vector2(0.45f, 1f));

        // Stats
        CreateTMP(row, "ItemStats", "ATK +10", 12, TMPro.FontStyles.Normal,
            new Color(0.8f, 0.9f, 0.6f),
            new Vector2(0.45f, 0f), new Vector2(0.65f, 1f));

        // Price
        CreateTMP(row, "ItemPrice", "50g", 13, TMPro.FontStyles.Bold,
            new Color(1f, 0.85f, 0.2f),
            new Vector2(0.65f, 0f), new Vector2(0.82f, 1f));

        // Buy button
        var buyGO = new GameObject("BuyButton");
        buyGO.transform.SetParent(row.transform, false);
        var buyRect = buyGO.AddComponent<RectTransform>();
        buyRect.anchorMin = new Vector2(0.82f, 0.1f);
        buyRect.anchorMax = new Vector2(0.99f, 0.9f);
        buyRect.offsetMin = Vector2.zero;
        buyRect.offsetMax = Vector2.zero;
        AddImage(buyGO, new Color(0.2f, 0.5f, 0.2f));
        buyGO.AddComponent<Button>();
        CreateTMP(buyGO, "Label", "Comprar", 12, TMPro.FontStyles.Normal,
            Color.white, Vector2.zero, Vector2.one);

        // Save as prefab asset
        string prefabPath = "Assets/_RPG/Prefabs/UI/ShopItemRow.prefab";
        System.IO.Directory.CreateDirectory("Assets/_RPG/Prefabs/UI");
        var prefab = PrefabUtility.SaveAsPrefabAsset(row, prefabPath);
        Object.DestroyImmediate(row);
        Debug.Log($"[BlacksmithSetup] Prefab de fila creado en {prefabPath}");
        return prefab;
    }

    static GameObject CreateTMP(GameObject parent, string name, string text,
        float fontSize, TMPro.FontStyles style, Color color,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style;
        tmp.color     = color;
        tmp.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        return go;
    }
}

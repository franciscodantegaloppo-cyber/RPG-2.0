using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class MerchantSetup : EditorWindow
{
    [MenuItem("RPG/Fix Merchant Wander Values")]
    static void FixMerchantValues()
    {
        var merchants = Object.FindObjectsByType<NPCMerchant>(FindObjectsInactive.Exclude);
        if (merchants.Length == 0) { Debug.LogError("No NPCMerchant found in scene."); return; }

        foreach (var m in merchants)
        {
            var wander = m.GetComponent<NPCWander>();
            if (wander == null)
                wander = Undo.AddComponent<NPCWander>(m.gameObject);

            Undo.RecordObject(wander, "Fix Merchant Wander");
            var so = new SerializedObject(wander);
            so.FindProperty("moveSpeed").floatValue = 0.75f;
            so.FindProperty("animationWalkSpeed").floatValue = 1f;
            so.FindProperty("maxRadius").floatValue = 5f;
            so.FindProperty("waitMin").floatValue      = 3f;
            so.FindProperty("waitMax").floatValue      = 6f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(wander);
            Debug.Log($"[MerchantSetup] Fixed wander values on {m.name}");
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.DisplayDialog("Merchant Fixed",
            "Wander values reset:\n• Speed = 0.4\n• Radius = 5m\n• Wait = 3-6s\n\nSave the scene with Ctrl+S.", "OK");
    }

    [MenuItem("RPG/Setup Merchant (RPG skin + Wander + Dialogue UI)")]
    static void Run()
    {
        // --- 1. Apply Ganz material to merchant ---
        string matPath = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Materials/RPG-Character.mat";
        var ganzMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (ganzMat == null) { Debug.LogError("RPG-Character material not found at: " + matPath); return; }

        // Find merchant in scene
        var merchants = Object.FindObjectsByType<NPCMerchant>(FindObjectsInactive.Exclude);
        if (merchants.Length == 0) { Debug.LogError("NPCMerchant not found in scene. Open SpawnVillage first."); return; }

        foreach (var merchant in merchants)
        {
            GameObject go = merchant.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(go, "Merchant Setup");

            // Apply material to all renderers
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = new Material[smr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = ganzMat;
                smr.sharedMaterials = mats;
            }
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                mr.sharedMaterial = ganzMat;

            // Add CharacterController
            if (go.GetComponent<CharacterController>() == null)
            {
                var cc = Undo.AddComponent<CharacterController>(go);
                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0, 0.9f, 0);
            }

            // Add NPCWander
            if (go.GetComponent<NPCWander>() == null)
                Undo.AddComponent<NPCWander>(go);

            // Add NPCAnimationEvents to the child that owns the Animator
            // (animation events are dispatched on the Animator's GameObject, not the root)
            var animator = go.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.gameObject.GetComponent<NPCAnimationEvents>() == null)
                Undo.AddComponent<NPCAnimationEvents>(animator.gameObject);

            Debug.Log($"[MerchantSetup] Applied Ganz material + NPCWander to {go.name}");
        }

        // --- 2. Create MerchantDialoguePanel UI ---
        SetupDialogueUI();

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[MerchantSetup] Done! Save the scene (Ctrl+S).");
    }

    static void SetupDialogueUI()
    {
        // Check if panel already exists
        if (Object.FindAnyObjectByType<MerchantDialoguePanel>() != null)
        {
            Debug.Log("[MerchantSetup] MerchantDialoguePanel already exists, skipping UI creation.");
            return;
        }

        // Find or create HUD Canvas
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("No Canvas found in scene!"); return; }

        GameObject canvasGO = canvas.gameObject;

        // Create panel root
        var panelGO = new GameObject("MerchantDialoguePanel");
        Undo.RegisterCreatedObjectUndo(panelGO, "Create Merchant Dialogue Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);

        var panelRect = panelGO.AddComponent<UnityEngine.RectTransform>();
        panelRect.anchorMin = new Vector2(0.1f, 0.05f);
        panelRect.anchorMax = new Vector2(0.9f, 0.4f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Background image
        var bg = panelGO.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.1f, 0.08f, 0.06f, 0.92f);

        // Speaker name text
        var nameGO = new GameObject("MerchantName");
        nameGO.transform.SetParent(panelGO.transform, false);
        var nameRect = nameGO.AddComponent<UnityEngine.RectTransform>();
        nameRect.anchorMin = new Vector2(0.02f, 0.75f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        var nameTMP = nameGO.AddComponent<TMPro.TextMeshProUGUI>();
        nameTMP.text = "Mercader";
        nameTMP.fontSize = 18;
        nameTMP.fontStyle = TMPro.FontStyles.Bold;
        nameTMP.color = new Color(1f, 0.85f, 0.4f);

        // Body text
        var bodyGO = new GameObject("DialogueBody");
        bodyGO.transform.SetParent(panelGO.transform, false);
        var bodyRect = bodyGO.AddComponent<UnityEngine.RectTransform>();
        bodyRect.anchorMin = new Vector2(0.02f, 0.3f);
        bodyRect.anchorMax = new Vector2(0.98f, 0.75f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;
        var bodyTMP = bodyGO.AddComponent<TMPro.TextMeshProUGUI>();
        bodyTMP.text = "";
        bodyTMP.fontSize = 15;
        bodyTMP.color = Color.white;
        bodyTMP.textWrappingMode = TMPro.TextWrappingModes.Normal;

        // Accept button
        var btnGO = new GameObject("AcceptButton");
        btnGO.transform.SetParent(panelGO.transform, false);
        var btnRect = btnGO.AddComponent<UnityEngine.RectTransform>();
        btnRect.anchorMin = new Vector2(0.35f, 0.05f);
        btnRect.anchorMax = new Vector2(0.65f, 0.28f);
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;
        var btnImage = btnGO.AddComponent<UnityEngine.UI.Image>();
        btnImage.color = new Color(0.6f, 0.4f, 0.1f, 1f);
        var btn = btnGO.AddComponent<UnityEngine.UI.Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.8f, 0.6f, 0.2f);
        btn.colors = colors;

        var btnLabelGO = new GameObject("ButtonLabel");
        btnLabelGO.transform.SetParent(btnGO.transform, false);
        var lblRect = btnLabelGO.AddComponent<UnityEngine.RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        lblRect.offsetMin = Vector2.zero;
        lblRect.offsetMax = Vector2.zero;
        var lblTMP = btnLabelGO.AddComponent<TMPro.TextMeshProUGUI>();
        lblTMP.text = "Aceptar";
        lblTMP.fontSize = 14;
        lblTMP.alignment = TMPro.TextAlignmentOptions.Center;
        lblTMP.color = Color.white;

        // Add MerchantDialoguePanel component and wire up references
        var dialoguePanel = panelGO.AddComponent<MerchantDialoguePanel>();
        var so = new UnityEditor.SerializedObject(dialoguePanel);
        so.FindProperty("panel").objectReferenceValue = panelGO;
        so.FindProperty("nameText").objectReferenceValue = nameTMP;
        so.FindProperty("bodyText").objectReferenceValue = bodyTMP;
        so.FindProperty("acceptButton").objectReferenceValue = btn;
        so.FindProperty("acceptLabel").objectReferenceValue = lblTMP;
        so.ApplyModifiedProperties();

        panelGO.SetActive(false);
        Debug.Log("[MerchantSetup] MerchantDialoguePanel UI created in Canvas.");
    }
}

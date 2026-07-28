using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ConvertToHerrero
{
    [MenuItem("RPG/Convert Selected to NPCHerrero")]
    static void Run()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogError("Seleccioná el objeto primero."); return; }

        Undo.RegisterFullObjectHierarchyUndo(go, "Convert to NPCHerrero");

        // 1. Renombrar
        go.name = "NPCHerrero";

        // 2. Quitar NPCMerchant, agregar NPCHerrero
        var merchant = go.GetComponent<NPCMerchant>();
        if (merchant != null) Object.DestroyImmediate(merchant);
        if (go.GetComponent<NPCHerrero>() == null)
            go.AddComponent<NPCHerrero>();
        var herrero = go.GetComponent<NPCHerrero>();
        var wander = go.GetComponent<NPCWander>();
        if (wander == null)
            wander = go.AddComponent<NPCWander>();
        wander.Configure(speed: 0.75f, radius: 5f, step: 2.1f, minWait: 2.5f, maxWait: 5.5f);

        // 3. Layer 10 (Interactable)
        go.layer = 10;
        foreach (var col in go.GetComponentsInChildren<Collider>())
            col.gameObject.layer = 10;

        // 4. Material tintado cobre (distinto al merchant)
        string matPath = "Assets/_RPG/Materials/NPC-Herrero-Blacksmith.mat";
        System.IO.Directory.CreateDirectory("Assets/_RPG/Materials");
        var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        // Busca el material base del merchant para copiar shader+textura
        Material baseMat = null;
        var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs.Length > 0 && smrs[0].sharedMaterial != null)
            baseMat = smrs[0].sharedMaterial;

        if (existingMat == null && baseMat != null)
        {
            existingMat = new Material(baseMat);
            existingMat.name = "NPC-Herrero-Blacksmith";
            // Tinte cobre oscuro
            if (existingMat.HasProperty("_BaseColor"))
                existingMat.SetColor("_BaseColor", new Color(0.45f, 0.29f, 0.16f, 1f));
            if (existingMat.HasProperty("_Color"))
                existingMat.SetColor("_Color", new Color(0.45f, 0.29f, 0.16f, 1f));
            if (existingMat.HasProperty("_Smoothness"))
                existingMat.SetFloat("_Smoothness", 0.18f);
            AssetDatabase.CreateAsset(existingMat, matPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Herrero] Material creado con shader: " + existingMat.shader.name);
        }

        if (existingMat != null)
        {
            foreach (var smr in smrs)
            {
                var mats = new Material[smr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = existingMat;
                smr.sharedMaterials = mats;
            }
            Debug.Log("[Herrero] Material aplicado a " + smrs.Length + " renderers.");
        }
        else Debug.LogWarning("[Herrero] No se pudo crear material (no hay base material).");

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_RPG/Animations/MerchantWalk2.controller");
        var anim = go.GetComponentInChildren<Animator>(true);
        if (anim != null && controller != null)
        {
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
        }

        if (herrero != null)
        {
            var so = new SerializedObject(herrero);
            so.FindProperty("walkController").objectReferenceValue = controller;
            so.FindProperty("blacksmithMaterial").objectReferenceValue = existingMat;
            so.ApplyModifiedProperties();
        }

        // 5. Posicionar dentro de una casa
        Vector3 pos = FindHousePos();
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, 180, 0);
        Debug.Log("[Herrero] Posicionado en: " + pos);

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkAllScenesDirty();
        Selection.activeGameObject = go;
        Debug.Log("[Herrero] Conversion completa. Guarda con Ctrl+S.");
    }

    static Vector3 FindHousePos()
    {
        string[] kws = { "house", "casa", "building", "inn", "tavern", "hut", "shop" };
        GameObject best = null;
        float bestSize = 0;

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude))
        {
            if (go.transform.parent != null) continue;
            string n = go.name.ToLower();
            bool match = false;
            foreach (var kw in kws) if (n.Contains(kw)) { match = true; break; }
            if (!match) continue;

            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) continue;
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float sz = b.size.x * b.size.z;
            if (sz > bestSize) { bestSize = sz; best = go; }
        }

        if (best != null)
        {
            var rends = best.GetComponentsInChildren<Renderer>();
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            Vector3 p = b.center;
            p.y = b.min.y + 0.05f;
            Debug.Log("[Herrero] Casa: " + best.name);
            return p;
        }

        // Fallback: busca VillageLayout y usa su posicion + offset
        var vl = Object.FindAnyObjectByType<VillageLayout>();
        if (vl != null) return vl.transform.position + new Vector3(8, 0, 8);
        return new Vector3(15, 0, -5);
    }
}

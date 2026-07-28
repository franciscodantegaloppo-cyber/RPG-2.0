using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class FixHerreroFinal
{
    [MenuItem("RPG/Fix Herrero Final")]
    static void Run()
    {
        // 1. Encontrar NPCHerrero
        NPCHerrero herrero = null;
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            if (mb is NPCHerrero h) { herrero = h; break; }

        if (herrero == null) { Debug.LogError("No hay NPCHerrero en la escena."); return; }
        var go = herrero.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(go, "Fix Herrero Final");

        // 2. Buscar textura del personaje RPG directamente en los assets
        Texture2D charTex = null;
        string[] texGuids = AssetDatabase.FindAssets("RPG-Character t:Texture2D");
        foreach (var g in texGuids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (p.ToLower().Contains("texture") || p.ToLower().Contains("mat"))
            {
                charTex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (charTex != null) { Debug.Log("Textura encontrada: " + p); break; }
            }
        }
        // Fallback: buscar cualquier textura del pack
        if (charTex == null)
        {
            texGuids = AssetDatabase.FindAssets("t:Texture2D", new[]{"Assets/ExplosiveLLC"});
            if (texGuids.Length > 0)
                charTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(texGuids[0]));
        }

        // 3. Crear material URP con la textura
        string matPath = "Assets/_RPG/Materials/NPC-Herrero-Blacksmith.mat";
        System.IO.Directory.CreateDirectory("Assets/_RPG/Materials");

        var urpShader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Lit")
                     ?? Shader.Find("Standard");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(urpShader);
            mat.name = "NPC-Herrero-Blacksmith";
            AssetDatabase.CreateAsset(mat, matPath);
        }

        if (charTex != null)
        {
            // URP usa _BaseMap; Standard usa _MainTex
            if (mat.HasProperty("_BaseMap"))  mat.SetTexture("_BaseMap", charTex);
            if (mat.HasProperty("_MainTex"))  mat.SetTexture("_MainTex", charTex);
            mat.mainTexture = charTex;
        }
        // Tinte cobre ligeramente más oscuro que el merchant
        Color tint = new Color(0.45f, 0.29f, 0.16f, 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", tint);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.18f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("Material creado: " + matPath + " shader=" + urpShader.name);

        // 4. Aplicar material a todos los SkinnedMeshRenderers del herrero
        var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Debug.Log("SMRs encontrados: " + smrs.Length);
        foreach (var smr in smrs)
        {
            var mats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            smr.sharedMaterials = mats;
            Debug.Log("  Aplicado a: " + smr.name);
        }

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

        var so = new SerializedObject(herrero);
        so.FindProperty("walkController").objectReferenceValue = controller;
        so.FindProperty("blacksmithMaterial").objectReferenceValue = mat;
        so.ApplyModifiedProperties();

        // 5. Posicionar dentro de la primera casa grande
        Vector3 pos = FindHouseInteriorPosition();
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, 180, 0); // mira hacia adentro
        Debug.Log("Posicionado en: " + pos);

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkAllScenesDirty();
        Selection.activeGameObject = go;

        EditorUtility.DisplayDialog("Herrero arreglado",
            "Textura aplicada: " + (charTex != null ? charTex.name : "NO ENCONTRADA") +
            "\nShader: " + urpShader.name +
            "\nPosición: " + pos +
            "\n\nGuardá con Ctrl+S.", "OK");
    }

    static Vector3 FindHouseInteriorPosition()
    {
        // Busca objetos con "house" o similares en el nombre
        string[] keywords = { "house", "casa", "building", "inn", "tavern", "hut", "shop", "forge", "blacksmith" };
        GameObject bestHouse = null;
        float bestSize = 0;

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude))
        {
            if (go.transform.parent != null) continue; // solo raíces
            string n = go.name.ToLower();
            foreach (var kw in keywords)
            {
                if (!n.Contains(kw)) continue;
                var rends = go.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) continue;
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float size = b.size.x * b.size.z;
                if (size > bestSize) { bestSize = size; bestHouse = go; }
                break;
            }
        }

        if (bestHouse != null)
        {
            var rends = bestHouse.GetComponentsInChildren<Renderer>();
            var bounds = rends[0].bounds;
            foreach (var r in rends) bounds.Encapsulate(r.bounds);
            Vector3 p = bounds.center;
            p.y = bounds.min.y + 0.05f;
            Debug.Log("Casa usada: " + bestHouse.name + " size=" + bestSize);
            return p;
        }

        // Fallback: usa VillageLayout o busca cualquier objeto grande
        var village = Object.FindAnyObjectByType<MonoBehaviour>();
        return new Vector3(10f, 0f, 10f);
    }
}

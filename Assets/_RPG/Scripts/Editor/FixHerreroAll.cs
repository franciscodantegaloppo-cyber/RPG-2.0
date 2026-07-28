using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// Arregla todos los problemas del NPCHerrero de una sola vez:
/// 1. Material tipo "herrero" (oscuro/carbon, no soldado azul)
/// 2. Asegura layer 10 + collider visible para interaccion E
/// 3. Mueve a posicion dentro de la casa mas cercana al jugador
public static class FixHerreroAll
{
    [MenuItem("RPG/Fix Herrero - Todo (textura + interaccion + posicion)")]
    static void Run()
    {
        // ── 1. Encontrar herrero ──────────────────────────────────────────
        var herrero = Object.FindAnyObjectByType<NPCHerrero>();
        if (herrero == null) { Debug.LogError("[FixHerrero] No hay NPCHerrero en la escena."); return; }
        var go = herrero.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(go, "Fix Herrero All");

        // ── 2. Material "herrero" (carbón/tierra, NO soldado azul) ────────
        string matPath = "Assets/_RPG/Materials/NPC-Herrero-Blacksmith.mat";
        System.IO.Directory.CreateDirectory("Assets/_RPG/Materials");

        // Base: copia el material del merchant para mantener textura y shader
        string basePath = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Materials/RPG-Character.mat";
        var baseMat = AssetDatabase.LoadAssetAtPath<Material>(basePath);

        var herreroMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (herreroMat == null)
        {
            if (baseMat != null)
            {
                herreroMat = new Material(baseMat);
                herreroMat.name = "NPC-Herrero-Blacksmith";
            }
            else
            {
                var urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                herreroMat = new Material(urp) { name = "NPC-Herrero-Blacksmith" };
            }
            AssetDatabase.CreateAsset(herreroMat, matPath);
        }

        // Tinte marrón tierra visible — diferente al merchant (azul/neutral)
        // Valores altos necesarios en URP linear color space para que no quede negro
        Color blacksmithTint = new Color(0.65f, 0.42f, 0.22f, 1f); // marrón tierra/cuero cálido
        if (herreroMat.HasProperty("_BaseColor")) herreroMat.SetColor("_BaseColor", blacksmithTint);
        if (herreroMat.HasProperty("_Color"))     herreroMat.SetColor("_Color",     blacksmithTint);
        if (herreroMat.HasProperty("_Smoothness")) herreroMat.SetFloat("_Smoothness", 0.25f);
        if (herreroMat.HasProperty("_Metallic"))   herreroMat.SetFloat("_Metallic",   0.05f);

        EditorUtility.SetDirty(herreroMat);
        AssetDatabase.SaveAssets();

        // Aplicar a todos los renderers del herrero
        var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in smrs)
        {
            var mats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = herreroMat;
            smr.sharedMaterials = mats;
        }
        Debug.Log($"[FixHerrero] Material carbón aplicado a {smrs.Length} renderers.");

        string walkControllerPath = "Assets/_RPG/Animations/MerchantWalk2.controller";
        var walkController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(walkControllerPath);
        var anim = go.GetComponentInChildren<Animator>(true);
        if (anim != null && walkController != null)
        {
            anim.runtimeAnimatorController = walkController;
            anim.applyRootMotion = false;
            Debug.Log("[FixHerrero] Animator cambiado a MerchantWalk2 (Idle/Walk).");
        }

        var wander = go.GetComponent<NPCWander>();
        if (wander == null)
            wander = Undo.AddComponent<NPCWander>(go);
        wander.Configure(speed: 0.75f, radius: 5f, step: 2.1f, minWait: 2.5f, maxWait: 5.5f);

        var herreroSo = new SerializedObject(herrero);
        herreroSo.FindProperty("walkController").objectReferenceValue = walkController;
        herreroSo.FindProperty("blacksmithMaterial").objectReferenceValue = herreroMat;
        herreroSo.ApplyModifiedProperties();

        // ── 3. Layer 10 + asegurar collider detectable ────────────────────
        go.layer = 10;
        // Asegurar que el CharacterController (que ES el collider) esté en layer 10
        var cc = go.GetComponent<CharacterController>();
        if (cc != null) go.layer = 10; // CC vive en el GO raíz

        // Agregar SphereCollider extra en layer 10 para OverlapSphere (CC a veces no es detectado)
        var extraCol = go.GetComponent<SphereCollider>();
        if (extraCol == null)
        {
            extraCol = go.AddComponent<SphereCollider>();
            extraCol.isTrigger = true;
            extraCol.radius = 0.5f;
            extraCol.center = new Vector3(0, 0.9f, 0);
        }
        foreach (var col in go.GetComponentsInChildren<Collider>())
            col.gameObject.layer = 10;

        // ── 4. Posicionar dentro de una casa real de VillageLayout ────────
        Vector3 newPos = FindBestHouseInterior(go.transform.position);
        go.transform.position = newPos;
        go.transform.rotation = Quaternion.Euler(0, 180, 0);
        Debug.Log($"[FixHerrero] Posicionado en: {newPos}");

        // ── 5. Verificar BlacksmithShopPanel ─────────────────────────────
        var shopPanels = Object.FindObjectsByType<BlacksmithShopPanel>(FindObjectsInactive.Include);
        var shopPanel = shopPanels.Length > 0 ? shopPanels[0] : null;
        if (shopPanel == null)
            Debug.LogWarning("[FixHerrero] No hay BlacksmithShopPanel en la escena. Corré RPG > Setup Blacksmith Shop UI.");
        else
            Debug.Log("[FixHerrero] BlacksmithShopPanel encontrado: " + shopPanel.name);

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkAllScenesDirty();
        Selection.activeGameObject = go;

        EditorUtility.DisplayDialog("Herrero arreglado",
            $"✓ Material oscuro carbón aplicado\n" +
            $"✓ SphereCollider trigger en layer 10\n" +
            $"✓ Posición: {newPos}\n\n" +
            "Acordate de agregar ítems en NPCHerrero > Shop Items.\n" +
            "Guardá con Ctrl+S.", "OK");
    }

    static Vector3 FindBestHouseInterior(Vector3 currentPos)
    {
        // Busca hijos del VillageLayout con renderers (piezas de casas)
        var vl = Object.FindAnyObjectByType<VillageLayout>();
        if (vl != null && vl.transform.childCount > 0)
        {
            // Toma los primeros 20 hijos, busca el que tiene más piezas (una casa completa)
            int bestChildIdx = -1;
            int bestPieceCount = 0;
            float bestDist = float.MaxValue;

            for (int i = 0; i < Mathf.Min(vl.transform.childCount, 30); i++)
            {
                var child = vl.transform.GetChild(i);
                var rends = child.GetComponentsInChildren<Renderer>();
                if (rends.Length < 4) continue; // casas tienen varias piezas

                float dist = Vector3.Distance(child.position, currentPos);
                // Prefiere casas con muchas piezas Y no demasiado lejos
                float score = rends.Length / (1f + dist * 0.1f);
                if (rends.Length > bestPieceCount || (rends.Length >= bestPieceCount && dist < bestDist))
                {
                    bestPieceCount = rends.Length;
                    bestChildIdx = i;
                    bestDist = dist;
                }
            }

            if (bestChildIdx >= 0)
            {
                var house = vl.transform.GetChild(bestChildIdx);
                var rends = house.GetComponentsInChildren<Renderer>();
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                Vector3 center = b.center;
                // Always place at ground level (Y=0 is terrain level in this project)
                center.y = 0.05f;
                Debug.Log($"[FixHerrero] Casa VillageLayout[{bestChildIdx}]: {house.name} piezas={bestPieceCount} pos={center}");
                return center;
            }
        }

        // Fallback: busca objetos raíz con muchos children (edificios)
        GameObject bestBuilding = null;
        int bestCount = 0;
        foreach (var obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude))
        {
            if (obj.transform.parent != null) continue;
            var rends = obj.GetComponentsInChildren<Renderer>();
            if (rends.Length > bestCount && rends.Length > 5)
            {
                // Excluir terrain, naturaleza, etc.
                string n = obj.name.ToLower();
                if (n.Contains("terrain") || n.Contains("nature") || n.Contains("tree") || n.Contains("player")) continue;
                bestCount = rends.Length;
                bestBuilding = obj;
            }
        }

        if (bestBuilding != null)
        {
            var rends = bestBuilding.GetComponentsInChildren<Renderer>();
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            Vector3 c = b.center;
            c.y = 0.05f;
            Debug.Log($"[FixHerrero] Fallback edificio: {bestBuilding.name} pos={c}");
            return c;
        }

        return new Vector3(-2f, 0.1f, 3f);
    }
}

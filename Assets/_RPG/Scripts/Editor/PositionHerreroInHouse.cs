using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PositionHerreroInHouse
{
    [MenuItem("RPG/Position Herrero Inside House")]
    static void Run()
    {
        var herrero = Object.FindAnyObjectByType<NPCHerrero>();
        if (herrero == null) { Debug.LogError("No NPCHerrero en la escena."); return; }

        // Busca VillageLayout y lista todos sus hijos
        var vl = Object.FindAnyObjectByType<VillageLayout>();
        if (vl != null)
        {
            Debug.Log("[HouseFind] VillageLayout en: " + vl.transform.position + " hijos: " + vl.transform.childCount);
            for (int i = 0; i < vl.transform.childCount; i++)
            {
                var ch = vl.transform.GetChild(i);
                Debug.Log("[HouseFind] VL hijo[" + i + "]: " + ch.name + " pos=" + ch.position + " children=" + ch.childCount);
            }
        }
        else Debug.LogWarning("[HouseFind] No hay VillageLayout.");

        // Lista todos los objetos raiz en la escena con renderers
        Vector3 bestPos = Vector3.zero;
        float bestArea = 0;
        string bestName = "";

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude))
        {
            // Solo objetos raíz o hijos directos de VillageLayout
            bool isCandidate = go.transform.parent == null || (vl != null && go.transform.parent == vl.transform);
            if (!isCandidate) continue;

            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length < 3) continue; // casas tienen muchos renderers

            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float height = b.size.y;
            float area = b.size.x * b.size.z;

            // Una casa tiene: area grande, altura media (3-8m), no es el terrain
            if (height > 2.5f && height < 12f && area > 10f && area < 400f)
            {
                Debug.Log("[HouseFind] Candidato casa: " + go.name + " area=" + area.ToString("F1") + " h=" + height.ToString("F1") + " pos=" + go.transform.position);
                if (area > bestArea) { bestArea = area; bestPos = b.center; bestPos.y = b.min.y + 0.1f; bestName = go.name; }
            }
        }

        if (bestArea > 0)
        {
            Undo.RegisterFullObjectHierarchyUndo(herrero.gameObject, "Position Herrero in House");
            herrero.transform.position = bestPos;
            herrero.transform.rotation = Quaternion.Euler(0, 180, 0);
            EditorUtility.SetDirty(herrero.gameObject);
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("[HouseFind] Herrero movido a: " + bestPos + " (casa: " + bestName + ")");
            EditorUtility.DisplayDialog("Herrero reposicionado",
                "Movido dentro de: " + bestName + "\nPos: " + bestPos + "\n\nGuardá con Ctrl+S.", "OK");
        }
        else
        {
            Debug.LogWarning("[HouseFind] No se encontró ninguna casa por bounds. Revisa el Console.");
            EditorUtility.DisplayDialog("Sin casa encontrada",
                "Mirá el Console para ver los candidatos y ajustá manualmente.", "OK");
        }
    }
}

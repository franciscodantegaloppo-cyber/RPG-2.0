using UnityEngine;
using UnityEditor;

// Se ejecuta automáticamente cuando Unity termina de compilar
[InitializeOnLoad]
public static class AutoFixURPMaterials
{
    static AutoFixURPMaterials()
    {
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        var urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null) return;

        string[] matGuids = AssetDatabase.FindAssets("t:Material",
            new[] {
                "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Materials",
                "Assets/Advance Studios/Medieval Castle/Art/Model/Materials",
                "Assets/LowPolyMedievalPropsLite/Materials"
            });

        bool changed = false;
        foreach (var guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            if (mat.shader == urpShader) continue;

            var tex   = mat.mainTexture;
            var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

            mat.shader = urpShader;
            if (tex != null)   mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", color == Color.clear ? Color.white : color);

            EditorUtility.SetDirty(mat);
            changed = true;
            Debug.Log($"[AutoFixURP] Convertido a URP/Lit: {path}");
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[AutoFixURP] Materiales del pack convertidos a URP. Listo.");
        }

        // Elimínase a sí mismo después de correr para no repetirse
        EditorApplication.delayCall -= Run;
    }
}

using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering.Universal;
using UnityEditor.SceneManagement;

public static class FixNPCMaterials
{
    [MenuItem("RPG/Fix NPC Materials (Convert to URP)")]
    static void FixMaterials()
    {
        // Convierte todos los materiales del pack RPG a URP
        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[]
        {
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Materials"
        });

        int converted = 0;
        foreach (var guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Si ya usa URP Lit, no hace nada
            if (mat.shader.name.Contains("Universal Render Pipeline") ||
                mat.shader.name.Contains("URP") ||
                mat.shader.name.Contains("Lit"))
                continue;

            // Guarda la textura principal antes de cambiar el shader
            var mainTex = mat.mainTexture;
            var color   = mat.color;

            // Cambia a URP/Lit
            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null)
            {
                Debug.LogError("No se encontró shader 'Universal Render Pipeline/Lit'.");
                return;
            }

            mat.shader = urpShader;

            // Restaura textura y color en las propiedades URP
            if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
            mat.SetColor("_BaseColor", color == Color.clear ? Color.white : color);

            EditorUtility.SetDirty(mat);
            converted++;
            Debug.Log($"[FixNPCMaterials] Convertido: {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // También aplica el material al NPCHerrero si existe
        var herrero = Object.FindAnyObjectByType<NPCHerrero>();
        if (herrero != null)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Materials/RPG-Character.mat");
            if (mat != null)
            {
                foreach (var smr in herrero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mats = new Material[smr.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    smr.sharedMaterials = mats;
                }
                EditorUtility.SetDirty(herrero.gameObject);
            }
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.DisplayDialog("Materiales convertidos",
            $"Se convirtieron {converted} materiales a URP/Lit.\n\nGuardá con Ctrl+S.", "OK");
    }
}

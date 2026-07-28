using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TerrainTextureBombingSetup
{
    const string MaterialPath = "Assets/_RPG/Materials/Terrain_TextureBombing_URP.mat";
    const string ShaderName = "RPG/URP/Terrain Texture Bombing";
    const float BossSpeed = 3.8f;

    static TerrainTextureBombingSetup()
    {
        EditorApplication.delayCall += ApplyIfReady;
    }

    [MenuItem("RPG/Terrain/Apply Texture Bombing")]
    public static void Apply()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[TerrainTextureBombing] El shader todavía se está importando.");
            return;
        }
        if (!shader.isSupported)
        {
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                Debug.LogError($"[TerrainTextureBombing] {message.severity}: {message.message} ({message.file}:{message.line})");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "Terrain_TextureBombing_URP" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetFloat("_BombingCellScale", 4f);
        material.SetFloat("_BombingOffsetStrength", 1.15f);
        EditorUtility.SetDirty(material);

        bool sceneChanged = false;
        int terrainCount = 0;
        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(
                     FindObjectsInactive.Include))
        {
            terrainCount++;
            if (terrain.materialTemplate == material)
                continue;
            terrain.materialTemplate = material;
            EditorUtility.SetDirty(terrain);
            sceneChanged = true;
        }

        GameObject boss = GameObject.Find("InsectoidCrabBoss");
        if (boss != null && boss.TryGetComponent(out InsectoidCrabBossAI ai))
        {
            SerializedObject serializedAI = new SerializedObject(ai);
            SerializedProperty speed = serializedAI.FindProperty("moveSpeed");
            if (speed != null && !Mathf.Approximately(speed.floatValue, BossSpeed))
            {
                speed.floatValue = BossSpeed;
                serializedAI.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ai);
                sceneChanged = true;
            }
        }

        AssetDatabase.SaveAssets();
        if (sceneChanged && !Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        Debug.Log($"[TerrainTextureBombing] Bombing aplicado en {terrainCount} terrenos; shader={shader.name}, supported={shader.isSupported}, passes={shader.passCount}, velocidad del Insectoid Crab Boss=3.8.");
    }

    static void ApplyIfReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
            return;
        Apply();
    }
}

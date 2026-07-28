using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GoldCoinSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string CoinModelPath = "Assets/AI Toolkit/Moneda del juego/Meshy_AI_Quiero_que_hagas_una__0712170630_texture.fbx";
    const string CoinTexturePath = "Assets/AI Toolkit/Moneda del juego/Meshy_AI_Quiero_que_hagas_una__0712170630_texture.png";
    const string CoinMaterialPath = "Assets/_RPG/Materials/GoldCoin_Meshy.mat";
    const string PrefabPath = "Assets/_RPG/Resources/Items/GoldCoinPickup.prefab";

    // Same visible diameter as the previous Coin_01 pickup.
    const float TargetCoinSize = 0.27f;

    [MenuItem("RPG/Items/Setup Gold Coin")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[GoldCoinSetup] Ignorado durante Play Mode.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject prefab = BuildPrefab();
        if (prefab == null) return;

        PlaceSceneInstance(prefab);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[GoldCoinSetup] Moneda de oro lista (prefab + una instancia en el spawn).");
    }

    static GameObject BuildPrefab()
    {
        GameObject coinModel = AssetDatabase.LoadAssetAtPath<GameObject>(CoinModelPath);
        if (coinModel == null)
        {
            Debug.LogError("[GoldCoinSetup] No se encontro " + CoinModelPath);
            return null;
        }

        GameObject instance = GoldCoinPickup.BuildPrefab(coinModel, CalculateScale(coinModel), 10, CreateCoinMaterial());
        System.IO.Directory.CreateDirectory("Assets/_RPG/Resources/Items");
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        return saved;
    }

    static float CalculateScale(GameObject model)
    {
        GameObject preview = (GameObject)PrefabUtility.InstantiatePrefab(model);
        Bounds bounds = new Bounds(preview.transform.position, Vector3.zero);
        bool found = false;
        foreach (Renderer renderer in preview.GetComponentsInChildren<Renderer>(true))
        {
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        Object.DestroyImmediate(preview);

        float largestDimension = found ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : 1f;
        return TargetCoinSize / Mathf.Max(largestDimension, 0.001f);
    }

    static Material CreateCoinMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CoinMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "GoldCoin_Meshy" };
            System.IO.Directory.CreateDirectory("Assets/_RPG/Materials");
            AssetDatabase.CreateAsset(material, CoinMaterialPath);
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CoinTexturePath);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.75f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.62f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void PlaceSceneInstance(GameObject prefab)
    {
        GameObject existing = GameObject.Find("GoldCoinPickup_NearSpawn");
        Vector3 position = existing != null ? existing.transform.position : FindSpawnPosition();

        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "GoldCoinPickup_NearSpawn";
        instance.transform.position = position;
        EditorUtility.SetDirty(instance);
    }

    static Vector3 FindSpawnPosition()
    {
        // Right next to the existing Diamond/Rune pickups near spawn, for a consistent "pickup
        // cluster" the player already knows to check.
        GameObject diamond = GameObject.Find("DiamondEnhancerPickup_NearSpawn");
        Vector3 basePos = diamond != null ? diamond.transform.position : new Vector3(-11f, 1.5f, 13f);
        Vector3 position = basePos + new Vector3(0.6f, 0f, 0.3f);

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 0.15f;
        return position;
    }
}

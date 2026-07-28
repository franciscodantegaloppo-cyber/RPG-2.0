using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StoneWoodBridgeSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/StoneWoodBridgeSetup.generate";
    const string BridgePath = "Assets/_RPG/Imported/PuentePiedraMadera/Meshy_AI_Stone_arched_Footbrid_0710224303_texture_fbx/Meshy_AI_Stone_arched_Footbrid_0710224303_texture.fbx";
    const string TexturePath = "Assets/_RPG/Imported/PuentePiedraMadera/Meshy_AI_Stone_arched_Footbrid_0710224303_texture_fbx/Meshy_AI_Stone_arched_Footbrid_0710224303_texture.png";
    const string NormalPath = "Assets/_RPG/Imported/PuentePiedraMadera/Meshy_AI_Stone_arched_Footbrid_0710224303_texture_fbx/Meshy_AI_Stone_arched_Footbrid_0710224303_texture_normal.png";
    const string MaterialPath = "Assets/_RPG/Imported/PuentePiedraMadera/StoneWoodBridge_Runtime.mat";
    const string ObjectName = "StoneWoodBridge_Map";
    const float TargetBridgeLength = 22f;

    [MenuItem("RPG/World/Place Stone Wood Bridge")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[StoneWoodBridgeSetup] Sali de Play Mode para colocar el puente.");
            return;
        }

        EditorSceneUtility.OpenSceneSafely(ScenePath);
        GameObject bridgeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BridgePath);
        if (bridgeAsset == null)
        {
            Debug.LogError("[StoneWoodBridgeSetup] No se encontro el puente importado: " + BridgePath);
            return;
        }

        GameObject existing = GameObject.Find(ObjectName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject bridge = (GameObject)PrefabUtility.InstantiatePrefab(bridgeAsset);
        bridge.name = ObjectName;
        Vector3 targetPosition = FindBridgePosition();
        bridge.transform.SetPositionAndRotation(Vector3.zero, FindBridgeRotation());
        bridge.transform.localScale = Vector3.one;
        ApplyMaterial(bridge);
        ScaleRenderedBridgeToLength(bridge, TargetBridgeLength);
        MoveRenderedBridgeTo(bridge, targetPosition);
        AddColliders(bridge);
        SnapRenderedBridgeToGround(bridge);
        Bounds finalBounds = GetBounds(bridge);

        EditorUtility.SetDirty(bridge);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[StoneWoodBridgeSetup] Puente de piedra y madera visible colocado. Root=" + bridge.transform.position + " BoundsCenter=" + finalBounds.center + " BoundsSize=" + finalBounds.size + " BoundsMin=" + finalBounds.min);
    }

    [InitializeOnLoadMethod]
    static void RunRequestedSetup()
    {
        EditorApplication.delayCall += TryRunRequestedSetup;
    }

    static void TryRunRequestedSetup()
    {
        string absoluteRequestPath = Path.GetFullPath(RequestPath);
        if (!File.Exists(absoluteRequestPath))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        File.Delete(absoluteRequestPath);
        string metaPath = absoluteRequestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        Setup();
    }

    static Vector3 FindBridgePosition()
    {
        GameObject gate = GameObject.Find("MedievalCastle/Gate");
        if (gate != null)
            return gate.transform.position - gate.transform.forward * 10f;

        GameObject castle = GameObject.Find("MedievalCastle") ?? GameObject.Find("Generated_Medieval_Castle_LowPoly_FarFromSpawn");
        if (castle != null)
            return castle.transform.position + new Vector3(0f, 0f, -42f);

        VillageLayout village = Object.FindAnyObjectByType<VillageLayout>();
        if (village != null)
            return village.transform.position + new Vector3(0f, 0f, 34f);

        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform.position + new Vector3(12f, 0f, 18f) : new Vector3(12f, 0f, 18f);
    }

    static Quaternion FindBridgeRotation()
    {
        GameObject gate = GameObject.Find("MedievalCastle/Gate");
        if (gate != null)
            return Quaternion.LookRotation(gate.transform.forward, Vector3.up);
        return Quaternion.Euler(0f, 90f, 0f);
    }

    static void ApplyMaterial(GameObject bridge)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
        if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(mat);

        foreach (Renderer renderer in bridge.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = mat;
    }

    static void AddColliders(GameObject bridge)
    {
        foreach (MeshFilter filter in bridge.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;

            MeshCollider collider = filter.gameObject.GetComponent<MeshCollider>();
            if (collider == null)
                collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
        }

        // Keep only the model's mesh colliders. A broad auxiliary box blocks
        // the bridge approaches and does not follow its arched surface.
    }

    static void MoveRenderedBridgeTo(GameObject bridge, Vector3 targetPosition)
    {
        Bounds bounds = GetBounds(bridge);
        Vector3 delta = targetPosition - bounds.center;
        bridge.transform.position += delta;
    }

    static void ScaleRenderedBridgeToLength(GameObject bridge, float targetLength)
    {
        Bounds bounds = GetBounds(bridge);
        float currentLength = Mathf.Max(bounds.size.x, bounds.size.z);
        if (currentLength <= 0.001f)
            return;

        float factor = targetLength / currentLength;
        bridge.transform.localScale *= factor;
    }

    static void SnapRenderedBridgeToGround(GameObject bridge)
    {
        Bounds bounds = GetBounds(bridge);
        Vector3 samplePos = bounds.center;
        float groundY = samplePos.y;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            groundY = terrain.SampleHeight(samplePos) + terrain.transform.position.y;
        else if (Physics.Raycast(samplePos + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 160f, ~0, QueryTriggerInteraction.Ignore))
            groundY = hit.point.y;

        bridge.transform.position += Vector3.up * (groundY + 0.05f - bounds.min.y);
    }

    static Bounds GetBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}

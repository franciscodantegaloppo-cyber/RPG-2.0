#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MedievalMarketStagingSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath =
        "Assets/_RPG/Generated/MedievalMarketStagingSetup.generate";
    const string PrefabFolder =
        "Assets/PolyRonin/Medieval Market Stalls/Prefabs";
    const string TexturePath =
        "Assets/PolyRonin/Medieval Market Stalls/Textures/market-stalls-colors.png";
    const string MaterialFolder = "Assets/_RPG/Generated/Materials";
    const string MaterialPath =
        MaterialFolder + "/MedievalMarketStalls_URP.mat";
    const string RootName = "Medieval_Market_Staging";
    const int Columns = 6;
    const float SpacingX = 5.5f;
    const float SpacingZ = 5.5f;

    [MenuItem("RPG/World/Place Medieval Market Near Spawn")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject previous = GameObject.Find(RootName);
        if (previous != null)
            Object.DestroyImmediate(previous);

        List<string> prefabPaths = FindPrefabPaths();
        if (prefabPaths.Count == 0)
        {
            Debug.LogError("[MedievalMarket] No se encontraron prefabs en " +
                           PrefabFolder + ".");
            return;
        }

        Material urpMaterial = CreateOrUpdateUrpMaterial();
        GameObject root = new GameObject(RootName);
        root.transform.position = FindStagingCenter();

        int placed = 0;
        int generatedColliders = 0;
        for (int index = 0; index < prefabPaths.Count; index++)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[index]);
            if (prefab == null)
                continue;

            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                instance = Object.Instantiate(prefab);
            instance.name = "Market_" + prefab.name;
            instance.transform.SetParent(root.transform, true);

            int row = index / Columns;
            int column = index % Columns;
            float centeredColumn = column - (Columns - 1) * .5f;
            Vector3 position = root.transform.position +
                               new Vector3(centeredColumn * SpacingX, 0f,
                                   row * SpacingZ);
            instance.transform.SetPositionAndRotation(position,
                Quaternion.Euler(0f, row % 2 == 0 ? 180f : 0f, 0f));

            if (urpMaterial != null)
                foreach (Renderer renderer in
                         instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = urpMaterial;

            generatedColliders += EnsureMeshColliders(instance);
            EnableImportedEffects(instance);
            GroundOnTerrain(instance);
            placed++;
        }

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(
            EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("[MedievalMarket] " + placed +
                  " objetos colocados cerca del SpawnPoint bajo '" +
                  RootName + "'. Colliders generados=" +
                  generatedColliders + ".");
    }

    static List<string> FindPrefabPaths()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab",
            new[] { PrefabFolder });
        List<string> paths = new List<string>(guids.Length);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(PrefabFolder,
                    System.StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".prefab",
                    System.StringComparison.OrdinalIgnoreCase))
                paths.Add(path);
        }
        paths.Sort((a, b) =>
        {
            int priority = PrefabPriority(
                Path.GetFileNameWithoutExtension(a)).CompareTo(
                PrefabPriority(Path.GetFileNameWithoutExtension(b)));
            return priority != 0
                ? priority
                : System.StringComparer.OrdinalIgnoreCase.Compare(a, b);
        });
        return paths;
    }

    static int PrefabPriority(string prefabName)
    {
        if (prefabName.StartsWith("Stall",
                System.StringComparison.OrdinalIgnoreCase)) return 0;
        if (prefabName.StartsWith("Cart",
                System.StringComparison.OrdinalIgnoreCase)) return 1;
        if (prefabName.StartsWith("Barrel",
                System.StringComparison.OrdinalIgnoreCase) ||
            prefabName.StartsWith("Box",
                System.StringComparison.OrdinalIgnoreCase) ||
            prefabName.StartsWith("Crate",
                System.StringComparison.OrdinalIgnoreCase)) return 2;
        return 3;
    }

    static Material CreateOrUpdateUrpMaterial()
    {
        EnsureFolder("Assets/_RPG/Generated");
        EnsureFolder(MaterialFolder);
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogWarning("[MedievalMarket] No se encontro el shader URP/Lit.");
            return material;
        }
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "MedievalMarketStalls_URP"
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", .16f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static int EnsureMeshColliders(GameObject instance)
    {
        int count = 0;
        foreach (MeshFilter filter in
                 instance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null ||
                filter.GetComponent<Collider>() != null)
                continue;
            MeshCollider collider =
                filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
            count++;
        }
        return count;
    }

    static void EnableImportedEffects(GameObject instance)
    {
        foreach (Animator animator in
                 instance.GetComponentsInChildren<Animator>(true))
            animator.enabled = true;
        foreach (Animation animation in
                 instance.GetComponentsInChildren<Animation>(true))
        {
            animation.enabled = true;
            if (animation.clip != null)
                animation.Play();
        }
        foreach (ParticleSystem particles in
                 instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            if (main.playOnAwake)
                particles.Play(true);
        }
    }

    static Vector3 FindStagingCenter()
    {
        GameObject spawn = GameObject.FindGameObjectWithTag("SpawnPoint");
        Transform anchor = spawn != null ? spawn.transform : null;
        if (anchor == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                anchor = player.transform;
        }

        Vector3 center = anchor != null
            ? anchor.position + anchor.right * 24f +
              anchor.forward * 18f
            : new Vector3(24f, 0f, 18f);
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            center.y = terrain.SampleHeight(center) +
                       terrain.transform.position.y;
        return center;
    }

    static void GroundOnTerrain(GameObject instance)
    {
        Renderer[] renderers =
            instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
            return;
        float ground = terrain.SampleHeight(bounds.center) +
                       terrain.transform.position.y;
        instance.transform.position += Vector3.up *
                                       (ground - bounds.min.y + .015f);
    }

    [InitializeOnLoadMethod]
    static void QueueSetup()
    {
        EditorApplication.delayCall += TryRun;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += TryRun;
        };
    }

    static void TryRun()
    {
        string absolute = Path.GetFullPath(RequestPath);
        if (!File.Exists(absolute) ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        File.Delete(absolute);
        if (File.Exists(absolute + ".meta"))
            File.Delete(absolute + ".meta");
        Setup();
    }
}
#endif

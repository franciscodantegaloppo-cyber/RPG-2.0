#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CampingPropsMapSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/CampingPropsMapSetup.generate";
    const string PrefabFolder = "Assets/URP GanzSe Free Camping Props/Prefabs";
    const string RootName = "Camping_Props_Staging";
    const int Columns = 5;
    const float SpacingX = 7f;
    const float SpacingZ = 7f;

    [MenuItem("RPG/World/Place All Camping Props Near Spawn")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject previous = GameObject.Find(RootName);
        if (previous != null) Object.DestroyImmediate(previous);

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
        List<string> paths = new List<string>(guids.Length);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(PrefabFolder) && path.EndsWith(".prefab")) paths.Add(path);
        }
        paths.Sort(System.StringComparer.OrdinalIgnoreCase);

        if (paths.Count == 0)
        {
            Debug.LogError("[CampingProps] No se encontraron prefabs en " + PrefabFolder);
            return;
        }

        GameObject root = new GameObject(RootName);
        root.transform.position = FindStagingCenter();

        int placed = 0;
        int colliders = 0;
        int animators = 0;
        int legacyAnimations = 0;
        int particleSystems = 0;

        for (int index = 0; index < paths.Count; index++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[index]);
            if (prefab == null) continue;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null) instance = Object.Instantiate(prefab);
            instance.name = CleanName(prefab.name);
            instance.transform.SetParent(root.transform, true);

            int row = index / Columns;
            int column = index % Columns;
            float centeredColumn = column - (Columns - 1) * .5f;
            Vector3 position = root.transform.position + new Vector3(centeredColumn * SpacingX, 0f, row * SpacingZ);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, (index % 4) * 90f, 0f));
            GroundOnTerrain(instance);

            Collider[] instanceColliders = instance.GetComponentsInChildren<Collider>(true);
            if (instanceColliders.Length == 0)
            {
                BoxCollider generated = AddFittedBoxCollider(instance);
                if (generated != null) instanceColliders = new Collider[] { generated };
            }
            foreach (Collider collider in instanceColliders)
            {
                collider.enabled = true;
                collider.isTrigger = false;
                colliders++;
            }

            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = true;
                animators++;
            }
            foreach (Animation animation in instance.GetComponentsInChildren<Animation>(true))
            {
                animation.enabled = true;
                if (animation.clip != null && !animation.isPlaying) animation.Play();
                legacyAnimations++;
            }
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                if (main.playOnAwake && !particles.isPlaying) particles.Play(true);
                particleSystems++;
            }

            placed++;
        }

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log("[CampingProps] Colocados " + placed + " objetos bajo " + RootName +
                  " en " + root.transform.position + ". Colliders=" + colliders +
                  ", Animators=" + animators + ", Animation=" + legacyAnimations +
                  ", Particulas=" + particleSystems + ".");
    }

    static Vector3 FindStagingCenter()
    {
        Transform anchor = null;
        TonioQuestGiver tonio = Object.FindAnyObjectByType<TonioQuestGiver>(FindObjectsInactive.Include);
        if (tonio != null) anchor = tonio.transform;
        if (anchor == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) anchor = player.transform;
        }

        Vector3 center = anchor != null
            ? anchor.position + anchor.right * 34f + anchor.forward * 30f
            : new Vector3(34f, 0f, 30f);
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null) center.y = terrain.SampleHeight(center) + terrain.transform.position.y;
        return center;
    }

    static string CleanName(string original)
    {
        string result = original;
        if (result.StartsWith("FCP__")) result = "FCP_" + result.Substring(5);
        return "Camping_" + result;
    }

    static void GroundOnTerrain(GameObject instance)
    {
        if (!TryGetBounds(instance, out Bounds bounds)) return;
        Terrain terrain = Terrain.activeTerrain;
        float ground = terrain != null
            ? terrain.SampleHeight(bounds.center) + terrain.transform.position.y
            : instance.transform.position.y;
        instance.transform.position += Vector3.up * (ground - bounds.min.y + .015f);
    }

    static BoxCollider AddFittedBoxCollider(GameObject instance)
    {
        if (!TryGetBounds(instance, out Bounds world)) return null;
        BoxCollider collider = instance.AddComponent<BoxCollider>();
        Vector3[] corners = GetCorners(world);
        Vector3 min = instance.transform.InverseTransformPoint(corners[0]);
        Vector3 max = min;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 local = instance.transform.InverseTransformPoint(corners[i]);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }
        collider.center = (min + max) * .5f;
        collider.size = max - min;
        return collider;
    }

    static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { bounds = default; return false; }
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    static Vector3[] GetCorners(Bounds bounds)
    {
        Vector3[] result = new Vector3[8];
        int index = 0;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            result[index++] = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
        return result;
    }

    [InitializeOnLoadMethod]
    static void QueueSetup()
    {
        EditorApplication.delayCall += TryRun;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += TryRun;
        };
    }

    static void TryRun()
    {
        string absolute = Path.GetFullPath(RequestPath);
        if (!File.Exists(absolute) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(absolute);
        if (File.Exists(absolute + ".meta")) File.Delete(absolute + ".meta");
        Setup();
    }
}
#endif

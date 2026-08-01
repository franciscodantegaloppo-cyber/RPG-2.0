#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SlavicMedievalVillageSetup
{
    const string TargetScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string DemoScenePath =
        "Assets/EmaceArt/Slavic World Free/Scene/Slavica Free 2022 Version.unity";
    const string RootName = "Slavic_Medieval_Village_Staging";
    static readonly HashSet<string> VillageGroups =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Buildings",
            "Ground",
            "Fences",
            "Props"
        };

    static int retryCount;

    static SlavicMedievalVillageSetup()
    {
        EditorApplication.delayCall += InstallWhenReady;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += InstallWhenReady;
        };
    }

    [MenuItem("RPG/World/Agregar aldea eslava completa fuera del spawn")]
    public static void InstallWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene targetScene = SceneManager.GetActiveScene();
        if (!targetScene.IsValid() || !targetScene.isLoaded ||
            targetScene.path != TargetScenePath)
            return;

        GameObject existing = FindRoot(targetScene, RootName);
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) == null)
        {
            if (retryCount++ < 20)
                EditorApplication.delayCall += InstallWhenReady;
            return;
        }

        Scene demoScene = default;
        GameObject stagingRoot = null;
        try
        {
            demoScene = EditorSceneManager.OpenScene(
                DemoScenePath, OpenSceneMode.Additive);
            stagingRoot = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(stagingRoot, targetScene);

            int copiedGroups = 0;
            foreach (GameObject sourceRoot in demoScene.GetRootGameObjects())
            {
                if (sourceRoot == null ||
                    !VillageGroups.Contains(sourceRoot.name))
                    continue;

                GameObject copy = UnityEngine.Object.Instantiate(sourceRoot);
                copy.name = sourceRoot.name;
                SceneManager.MoveGameObjectToScene(copy, targetScene);
                copy.transform.SetParent(stagingRoot.transform, true);
                copiedGroups++;
            }

            if (copiedGroups != VillageGroups.Count)
                Debug.LogWarning("[SlavicVillage] Se copiaron " +
                                 copiedGroups + " de " +
                                 VillageGroups.Count +
                                 " grupos de la demostración.");

            EnsureBuildingColliders(stagingRoot);
            PlaceOutsideSpawn(stagingRoot);
            MarkStatic(stagingRoot.transform);

            EditorUtility.SetDirty(stagingRoot);
            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene);
            Selection.activeGameObject = stagingRoot;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("[SlavicVillage] Aldea completa instalada como '" +
                      RootName + "'. Incluye edificios, suelo/caminos, " +
                      "cercas y utilería; mueve el objeto raíz para " +
                      "reubicar todo el conjunto.");
        }
        catch (Exception exception)
        {
            if (stagingRoot != null)
                UnityEngine.Object.DestroyImmediate(stagingRoot);
            Debug.LogException(exception);
        }
        finally
        {
            if (demoScene.IsValid() && demoScene.isLoaded)
                EditorSceneManager.CloseScene(demoScene, true);
        }
    }

    static void PlaceOutsideSpawn(GameObject root)
    {
        Bounds content = CalculateBounds(root);
        Transform spawn = FindSpawn();
        Vector3 spawnPosition = spawn != null
            ? spawn.position
            : Vector3.zero;
        float footprintRadius = Mathf.Max(content.extents.x,
            content.extents.z);
        Vector3 target = FindTerrainPlacement(spawnPosition,
            Mathf.Max(95f, footprintRadius + 45f), footprintRadius);
        float targetGround = SampleTerrainHeight(target);
        Vector3 shift = new Vector3(
            target.x - content.center.x,
            targetGround - content.min.y,
            target.z - content.center.z);
        root.transform.position += shift;
    }

    static Vector3 FindTerrainPlacement(Vector3 spawn, float distance,
        float footprintRadius)
    {
        Terrain terrain = ClosestTerrain(spawn);
        if (terrain == null)
            return spawn + new Vector3(distance, 0f, distance * .35f);

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float margin = Mathf.Min(footprintRadius + 12f,
            Mathf.Min(size.x, size.z) * .35f);
        float minX = terrainPosition.x + margin;
        float maxX = terrainPosition.x + size.x - margin;
        float minZ = terrainPosition.z + margin;
        float maxZ = terrainPosition.z + size.z - margin;

        Vector3[] directions =
        {
            Vector3.right,
            Vector3.left,
            Vector3.forward,
            Vector3.back,
            new Vector3(1f, 0f, 1f).normalized,
            new Vector3(-1f, 0f, 1f).normalized,
            new Vector3(1f, 0f, -1f).normalized,
            new Vector3(-1f, 0f, -1f).normalized
        };

        Vector3 best = spawn;
        float bestDistance = float.NegativeInfinity;
        foreach (Vector3 direction in directions)
        {
            Vector3 candidate = spawn + direction * distance;
            if (candidate.x < minX || candidate.x > maxX ||
                candidate.z < minZ || candidate.z > maxZ)
                continue;
            float candidateDistance =
                (candidate - spawn).sqrMagnitude;
            if (candidateDistance <= bestDistance) continue;
            best = candidate;
            bestDistance = candidateDistance;
        }

        if (bestDistance < 0f)
        {
            Vector3[] corners =
            {
                new Vector3(minX, spawn.y, minZ),
                new Vector3(minX, spawn.y, maxZ),
                new Vector3(maxX, spawn.y, minZ),
                new Vector3(maxX, spawn.y, maxZ)
            };
            foreach (Vector3 corner in corners)
            {
                float candidateDistance =
                    (corner - spawn).sqrMagnitude;
                if (candidateDistance <= bestDistance) continue;
                best = corner;
                bestDistance = candidateDistance;
            }
        }
        return best;
    }

    static Terrain ClosestTerrain(Vector3 position)
    {
        Terrain best = null;
        float closest = float.PositiveInfinity;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
                continue;
            Vector3 center = terrain.transform.position +
                             terrain.terrainData.size * .5f;
            center.y = position.y;
            float distance = (center - position).sqrMagnitude;
            if (distance >= closest) continue;
            closest = distance;
            best = terrain;
        }
        return best;
    }

    static float SampleTerrainHeight(Vector3 position)
    {
        Terrain terrain = ClosestTerrain(position);
        return terrain != null
            ? terrain.SampleHeight(position) + terrain.transform.position.y
            : position.y;
    }

    static Transform FindSpawn()
    {
        try
        {
            GameObject tagged =
                GameObject.FindGameObjectWithTag("SpawnPoint");
            if (tagged != null) return tagged.transform;
        }
        catch (UnityException) { }

        GameObject named = GameObject.Find("SpawnPoint") ??
                           GameObject.Find("PlayerSpawn");
        if (named != null) return named.transform;

        PlayerController player =
            UnityEngine.Object.FindAnyObjectByType<PlayerController>(
                FindObjectsInactive.Include);
        return player != null ? player.transform : null;
    }

    static void EnsureBuildingColliders(GameObject root)
    {
        Transform buildings = root.transform.Find("Buildings");
        if (buildings == null) return;
        foreach (Transform building in buildings)
        {
            if (building == null ||
                building.GetComponentInChildren<Collider>(true) != null)
                continue;
            Bounds bounds = CalculateBounds(building.gameObject);
            if (bounds.size.sqrMagnitude < .01f) continue;

            BoxCollider collider =
                building.gameObject.AddComponent<BoxCollider>();
            Vector3 localCenter =
                building.InverseTransformPoint(bounds.center);
            Vector3 localSize =
                building.InverseTransformVector(bounds.size);
            collider.center = localCenter;
            collider.size = new Vector3(
                Mathf.Abs(localSize.x),
                Mathf.Abs(localSize.y),
                Mathf.Abs(localSize.z));
        }
    }

    static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void MarkStatic(Transform root)
    {
        root.gameObject.isStatic = true;
        foreach (Transform child in root)
            MarkStatic(child);
    }

    static GameObject FindRoot(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root != null && root.name == objectName)
                return root;
        return null;
    }
}
#endif

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TowerGuardProjectileSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/TowerGuardProjectileSetup.generate";

    [MenuItem("RPG/World/Make Tower Guard Fireballs Ignore Tower")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        TowerGuardStationary[] guards = Object.FindObjectsByType<TowerGuardStationary>(FindObjectsInactive.Include);
        if (guards.Length == 0)
        {
            Debug.LogError("[TowerGuardProjectile] No se encontro npc_vigia_1.");
            return;
        }

        Transform[] towers = FindTowers();
        if (towers.Length == 0)
        {
            Debug.LogError("[TowerGuardProjectile] No se encontro wooden_tower.");
            return;
        }

        int configured = 0;
        foreach (TowerGuardStationary guard in guards)
        {
            Transform nearest = FindNearest(guard.transform.position, towers);
            guard.ConfigureProjectileCollisionIgnore(nearest);
            EditorUtility.SetDirty(guard);
            configured++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[TowerGuardProjectile] " + configured +
                  " vigia(s) configurados. Sus bolas de fuego ignoran todos los colliders de wooden_tower; el jugador no.");
    }

    static Transform[] FindTowers()
    {
        System.Collections.Generic.List<Transform> result = new System.Collections.Generic.List<Transform>();
        foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (!candidate.gameObject.scene.IsValid()) continue;
            string normalized = candidate.name.ToLowerInvariant().Replace("_", "").Replace(" ", "").Replace("-", "");
            if (normalized.StartsWith("woodentower")) result.Add(candidate);
        }
        return result.ToArray();
    }

    static Transform FindNearest(Vector3 position, Transform[] choices)
    {
        Transform best = choices[0];
        float bestDistance = (best.position - position).sqrMagnitude;
        for (int i = 1; i < choices.Length; i++)
        {
            float distance = (choices[i].position - position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            best = choices[i];
            bestDistance = distance;
        }
        return best;
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

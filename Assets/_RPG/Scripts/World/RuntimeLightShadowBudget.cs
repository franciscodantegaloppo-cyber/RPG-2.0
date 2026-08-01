using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Decorative spell, camp and village lights remain visible. Only the closest
// punctual lights are allowed to consume the URP shadow atlas at once.
public sealed class RuntimeLightShadowBudget : MonoBehaviour
{
    // A point light renders six cubemap faces while a spot light renders one. Budgeting by
    // number of Light components allowed six point lights (36 maps) and still overflowed the
    // atlas. Twelve faces means at most two nearby point lights, or a useful mixture of spots.
    const int MaxPunctualShadowFaces = 12;
    const int MaxPunctualShadowLights = 6;
    const float MaximumShadowDistance = 26f;
    const float RefreshSeconds = .45f;

    readonly Dictionary<Light, LightShadows> originalShadows = new();
    readonly List<Light> candidates = new();
    readonly List<Light> staleLights = new();
    Transform viewer;
    float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (FindAnyObjectByType<RuntimeLightShadowBudget>() != null) return;
        GameObject manager = new GameObject("Runtime Light Shadow Budget");
        DontDestroyOnLoad(manager);
        manager.AddComponent<RuntimeLightShadowBudget>().Refresh();
    }

    void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        viewer = null;
        nextRefresh = 0f;
        Refresh();
        StartCoroutine(RefreshAfterSceneInitialization());
    }

    System.Collections.IEnumerator RefreshAfterSceneInitialization()
    {
        // Start/Awake-created VFX lights appear after sceneLoaded. Catch them before they can
        // remain in the expensive shadow set for the former .75-second refresh window.
        yield return null;
        Refresh();
        yield return null;
        Refresh();
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        Refresh();
    }

    void Refresh()
    {
        nextRefresh = Time.unscaledTime + RefreshSeconds;
        if (viewer == null && Camera.main != null) viewer = Camera.main.transform;

        CleanupDestroyedLights();
        candidates.Clear();
        Light primaryDirectional = null;
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (light == null) continue;
            if (!originalShadows.ContainsKey(light))
                originalShadows.Add(light, light.shadows);

            if (light.type == LightType.Directional)
            {
                if (originalShadows[light] == LightShadows.None) continue;
                if (primaryDirectional == null ||
                    light.intensity > primaryDirectional.intensity)
                    primaryDirectional = light;
                continue;
            }

            if (originalShadows[light] != LightShadows.None &&
                light.lightmapBakeType != LightmapBakeType.Baked)
                candidates.Add(light);
        }

        LimitDirectionalShadows(primaryDirectional);

        Vector3 origin = viewer != null ? viewer.position : Vector3.zero;
        candidates.Sort((a, b) => ShadowPriority(b, origin).CompareTo(ShadowPriority(a, origin)));
        int usedFaces = 0;
        int usedLights = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            Light light = candidates[i];
            int faceCost = light.type == LightType.Point ? 6 : 1;
            float distance = Vector3.Distance(light.transform.position, origin);
            bool relevantDistance = viewer != null &&
                                    distance <= Mathf.Min(MaximumShadowDistance,
                                        Mathf.Max(8f, light.range + 5f));
            bool receivesBudget = relevantDistance &&
                                  !IsDecorativeOrTransient(light) &&
                                  usedLights < MaxPunctualShadowLights &&
                                  usedFaces + faceCost <=
                                      MaxPunctualShadowFaces;
            light.shadows = receivesBudget
                ? originalShadows[light]
                : LightShadows.None;
            if (receivesBudget)
            {
                usedFaces += faceCost;
                usedLights++;
            }
        }
    }

    void LimitDirectionalShadows(Light primary)
    {
        foreach (KeyValuePair<Light, LightShadows> entry in originalShadows)
        {
            Light light = entry.Key;
            if (light == null || light.type != LightType.Directional ||
                entry.Value == LightShadows.None)
                continue;
            light.shadows = light == primary ? entry.Value : LightShadows.None;
        }
    }

    void CleanupDestroyedLights()
    {
        staleLights.Clear();
        foreach (Light light in originalShadows.Keys)
            if (light == null) staleLights.Add(light);
        foreach (Light light in staleLights)
            originalShadows.Remove(light);
    }

    static bool IsDecorativeOrTransient(Light light)
    {
        if (light.GetComponentInParent<ParticleSystem>() != null)
            return true;

        string objectName = light.name.ToLowerInvariant();
        return objectName.Contains("preview") ||
               objectName.Contains("icon") ||
               objectName.Contains("glow") ||
               objectName.Contains("aura") ||
               objectName.Contains("fireball") ||
               objectName.Contains("projectile") ||
               objectName.Contains("particle") ||
               objectName.Contains("inventory");
    }

    static float ShadowPriority(Light light, Vector3 viewerPosition)
    {
        float distance = Vector3.Distance(light.transform.position, viewerPosition);
        float typeEfficiency = light.type == LightType.Point ? .55f : 1f;
        float bossImportance = light.name.Contains("Boss",
            System.StringComparison.OrdinalIgnoreCase) ? 2f : 1f;
        return light.intensity * Mathf.Max(1f, light.range) *
               typeEfficiency * bossImportance / Mathf.Max(1f, distance);
    }
}

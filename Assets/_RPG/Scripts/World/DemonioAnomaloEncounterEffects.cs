using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Turns the whole world hostile as the player crosses the Anomalous Demon's 15 m encounter
// boundary. Every value is continuously blended back to its captured scene baseline on escape.
public class DemonioAnomaloEncounterEffects : MonoBehaviour
{
    [SerializeField] float radius = 15f;
    [SerializeField] float fadeOutRadius = 30f;
    [SerializeField] float transitionSpeed = 1.8f;

    Transform player;
    Light sun;
    Color baseAmbient;
    Color baseFogColor;
    float baseSunIntensity;
    float intensity;
    float visibility;
    bool hasBeenDetected;
    bool captured;
    Renderer[] demonRenderers;
    readonly List<Material> fadeMaterials = new List<Material>();
    readonly List<Color> originalColors = new List<Color>();

    public float Intensity => intensity;

    void Awake()
    {
        CaptureEnvironment();
        PrepareDemonFade();
        ApplyDemonFade(0f);
        if (GetComponent<DemonioAnomaloTornadoVFX>() == null)
            gameObject.AddComponent<DemonioAnomaloTornadoVFX>();
        if (GetComponent<DemonioAnomaloTreeDarkening>() == null)
            gameObject.AddComponent<DemonioAnomaloTreeDarkening>();
    }

    void Update()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null) player = found.transform;
        }

        float target = 0f;
        float visibilityTarget = 0f;
        if (player != null)
        {
            Vector3 delta = player.position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            target = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(distance / radius));
            if (distance <= radius)
                hasBeenDetected = true;

            // Once the player has discovered it at 15 m, the demon retains its presence while
            // the player retreats and only fades out across the wider 15-30 m escape range.
            if (hasBeenDetected)
                visibilityTarget = Mathf.InverseLerp(fadeOutRadius, radius, distance);
        }
        intensity = Mathf.MoveTowards(intensity, target, transitionSpeed * Time.deltaTime);
        visibility = Mathf.MoveTowards(visibility, visibilityTarget, transitionSpeed * Time.deltaTime);
        ApplyDemonFade(visibility);
        ApplyEnvironment();
        WindManager.Instance?.SetEncounterWind(intensity);
    }

    void CaptureEnvironment()
    {
        baseAmbient = RenderSettings.ambientLight;
        baseFogColor = RenderSettings.fogColor;
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include))
            if (light.type == LightType.Directional && (sun == null || light.intensity > sun.intensity)) sun = light;
        if (sun != null) baseSunIntensity = sun.intensity;
        captured = true;
    }

    void ApplyEnvironment()
    {
        if (!captured) CaptureEnvironment();
        RenderSettings.ambientLight = Color.Lerp(baseAmbient, new Color(0.018f, 0.004f, 0.004f), intensity);
        RenderSettings.fogColor = Color.Lerp(baseFogColor, new Color(0.09f, 0.005f, 0.003f), intensity * 0.7f);
        if (sun != null) sun.intensity = Mathf.Lerp(baseSunIntensity, baseSunIntensity * 0.08f, intensity);
    }

    // Imported Meshy materials are normally opaque.  Each renderer gets runtime-only material
    // copies so changing to transparent does not alter the source model or any other instance.
    void PrepareDemonFade()
    {
        demonRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in demonRenderers)
        {
            foreach (Material material in renderer.materials)
            {
                if (material == null) continue;
                MakeTransparent(material);
                fadeMaterials.Add(material);
                originalColors.Add(GetMaterialColor(material));
            }
        }
    }

    void ApplyDemonFade(float alpha)
    {
        for (int i = 0; i < fadeMaterials.Count; i++)
        {
            Material material = fadeMaterials[i];
            if (material == null) continue;
            Color color = originalColors[i];
            color.a = alpha;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        // Transparent meshes can otherwise still cast a very visible shadow before revealing.
        if (demonRenderers == null) return;
        ShadowCastingMode shadows = alpha <= 0.02f ? ShadowCastingMode.Off : ShadowCastingMode.On;
        foreach (Renderer renderer in demonRenderers)
            if (renderer != null) renderer.shadowCastingMode = shadows;
    }

    static Color GetMaterialColor(Material material)
    {
        if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color")) return material.GetColor("_Color");
        return Color.white;
    }

    static void MakeTransparent(Material material)
    {
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 5f); // SrcAlpha
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 10f); // OneMinusSrcAlpha
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    void OnDisable()
    {
        intensity = 0f;
        ApplyEnvironment();
        WindManager.Instance?.SetEncounterWind(0f);
    }
}

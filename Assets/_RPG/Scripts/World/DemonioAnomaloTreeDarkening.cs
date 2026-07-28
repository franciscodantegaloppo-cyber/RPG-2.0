using System.Collections.Generic;
using UnityEngine;

// Darkens only runtime material copies, leaving every imported tree material asset untouched.
// The effect follows the demon encounter and reaches the tree ring around it (30 m).
public class DemonioAnomaloTreeDarkening : MonoBehaviour
{
    const float Radius = 30f;
    const float ScanInterval = 2f;

    struct MaterialState
    {
        public Material material;
        public Color baseColor;
        public bool hasBaseColor;
        public Color color;
        public bool hasColor;
    }

    readonly List<MaterialState> darkenedMaterials = new List<MaterialState>();
    float nextScan;
    float nextMaterialUpdate;
    bool treeRingScanned;
    DemonioAnomaloEncounterEffects encounter;

    void Awake() => encounter = GetComponent<DemonioAnomaloEncounterEffects>();

    void Update()
    {
        float strength = encounter != null ? encounter.Intensity : 0f;
        // Trees are static scenery. One scan when the encounter begins is enough; repeatedly
        // enumerating every Renderer in the whole map was a major source of periodic stutter.
        if (strength > 0.001f && !treeRingScanned && Time.time >= nextScan)
        {
            nextScan = Time.time + ScanInterval;
            RegisterNearbyTrees();
            treeRingScanned = true;
        }
        // Material property updates are enough at 15 Hz for this slow distance fade.
        if (Time.time >= nextMaterialUpdate)
        {
            nextMaterialUpdate = Time.time + (1f / 15f);
            ApplyDarkness(strength);
        }
    }

    void RegisterNearbyTrees()
    {
        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (renderer == null || !IsTreeRenderer(renderer)) continue;
            Vector3 delta = renderer.bounds.center - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > Radius * Radius) continue;

            foreach (Material material in renderer.materials)
            {
                if (material == null || Contains(material)) continue;
                MaterialState state = new MaterialState { material = material };
                state.hasBaseColor = material.HasProperty("_BaseColor");
                state.hasColor = material.HasProperty("_Color");
                if (!state.hasBaseColor && !state.hasColor) continue;
                if (state.hasBaseColor) state.baseColor = material.GetColor("_BaseColor");
                if (state.hasColor) state.color = material.GetColor("_Color");
                darkenedMaterials.Add(state);
            }
        }
    }

    bool Contains(Material material)
    {
        foreach (MaterialState state in darkenedMaterials)
            if (state.material == material) return true;
        return false;
    }

    static bool IsTreeRenderer(Renderer renderer)
    {
        if (renderer.GetComponentInParent<EnemyStats>() != null) return false;
        for (Transform current = renderer.transform; current != null; current = current.parent)
        {
            string name = current.name.ToLowerInvariant();
            if (name.Contains("tree") || name.Contains("arbol") || name.Contains("árbol") ||
                name.Contains("spruce") || name.Contains("pine") || name.Contains("oak") ||
                name.Contains("fir") || name.Contains("conifer")) return true;
        }
        return false;
    }

    void ApplyDarkness(float strength)
    {
        Color shadowTint = new Color(0.12f, 0.035f, 0.19f, 1f);
        for (int i = darkenedMaterials.Count - 1; i >= 0; i--)
        {
            MaterialState state = darkenedMaterials[i];
            if (state.material == null) { darkenedMaterials.RemoveAt(i); continue; }
            if (state.hasBaseColor) state.material.SetColor("_BaseColor", Darken(state.baseColor, shadowTint, strength));
            if (state.hasColor) state.material.SetColor("_Color", Darken(state.color, shadowTint, strength));
        }
    }

    static Color Darken(Color original, Color tint, float strength)
    {
        Color dark = new Color(original.r * tint.r, original.g * tint.g, original.b * tint.b, original.a);
        return Color.Lerp(original, dark, strength * 0.85f);
    }

    void OnDisable() => ApplyDarkness(0f);
}

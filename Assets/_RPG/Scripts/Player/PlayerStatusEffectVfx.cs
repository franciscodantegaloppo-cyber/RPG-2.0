using UnityEngine;

public static class PlayerStatusEffectVfx
{
    public static void PlayHealing(Transform target, float duration = 2.5f)
    {
        Play(target, "VFX/StatusEffects/Healing", "Healing_StatusEffect",
            duration, new Vector3(0f, .85f, 0f), 1f);
        ActiveStatusIconHUD.ShowTimed("healing", "Healing", duration, "Curación");
    }

    public static GameObject Play(Transform target, string resourcePath, string objectName,
        float duration, Vector3 localPosition, float scale)
    {
        return Play(target, resourcePath, objectName, duration, localPosition, scale, null);
    }

    public static GameObject Play(Transform target, string resourcePath, string objectName,
        float duration, Vector3 localPosition, float scale, Color? tint)
    {
        if (target == null) return null;
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("[PlayerStatusEffectVfx] No se encontró " + resourcePath);
            return null;
        }

        GameObject effect = Object.Instantiate(prefab, target, false);
        effect.name = objectName;
        effect.transform.localPosition = localPosition;
        effect.transform.localRotation = Quaternion.identity;
        effect.transform.localScale = Vector3.one * scale;
        foreach (Collider collider in effect.GetComponentsInChildren<Collider>(true))
            Object.Destroy(collider);
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (tint.HasValue)
            {
                ParticleSystem.MainModule main = particles.main;
                main.startColor = TintGradient(main.startColor, tint.Value);
            }
            particles.Play(true);
        }
        if (tint.HasValue)
            TintRenderers(effect, tint.Value, duration);
        Object.Destroy(effect, Mathf.Max(.2f, duration));
        return effect;
    }

    static ParticleSystem.MinMaxGradient TintGradient(
        ParticleSystem.MinMaxGradient source, Color tint)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(
                    TintedColor(tint, source.color.a, 1f));
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(
                    TintedColor(tint, source.colorMin.a, .72f),
                    TintedColor(tint, source.colorMax.a, 1.2f));
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(
                    TintedGradient(source.gradient, tint));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(
                    TintedGradient(source.gradientMin, tint),
                    TintedGradient(source.gradientMax, tint));
            case ParticleSystemGradientMode.RandomColor:
                ParticleSystem.MinMaxGradient random =
                    new ParticleSystem.MinMaxGradient(
                        TintedGradient(source.gradient, tint));
                random.mode = ParticleSystemGradientMode.RandomColor;
                return random;
            default:
                return source;
        }
    }

    static Gradient TintedGradient(Gradient source, Color tint)
    {
        Gradient result = new Gradient();
        GradientColorKey[] sourceKeys = source != null
            ? source.colorKeys : System.Array.Empty<GradientColorKey>();
        GradientColorKey[] colorKeys = sourceKeys.Length > 0
            ? new GradientColorKey[sourceKeys.Length]
            : new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) };
        for (int i = 0; i < sourceKeys.Length; i++)
        {
            float brightness = Mathf.Max(.58f,
                Mathf.Max(sourceKeys[i].color.r,
                    Mathf.Max(sourceKeys[i].color.g, sourceKeys[i].color.b)));
            colorKeys[i] = new GradientColorKey(
                TintedColor(tint, 1f, brightness), sourceKeys[i].time);
        }
        GradientAlphaKey[] alphaKeys = source != null && source.alphaKeys.Length > 0
            ? source.alphaKeys
            : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f),
                new GradientAlphaKey(0f, 1f) };
        result.SetKeys(colorKeys, alphaKeys);
        return result;
    }

    static Color TintedColor(Color tint, float alpha, float brightness)
    {
        return new Color(tint.r * brightness, tint.g * brightness,
            tint.b * brightness, alpha);
    }

    static void TintRenderers(GameObject effect, Color tint, float duration)
    {
        foreach (ParticleSystemRenderer renderer in
                 effect.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null) continue;
                Color brightGold = new Color(tint.r * 2.1f, tint.g * 1.75f,
                    tint.b * 1.15f, .72f);
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", brightGold);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", brightGold);
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", brightGold);
                Object.Destroy(material, Mathf.Max(.25f, duration + .1f));
            }
        }
    }
}

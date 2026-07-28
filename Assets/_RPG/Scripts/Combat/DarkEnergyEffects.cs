using UnityEngine;

// Shared status used by the enchanted Dark Saber and the Insectoid Crab Boss.
// Its damage goes through the explicit true-damage entry points, so armor, block
// and other protection layers cannot reduce it.
public sealed class DarkEnergyBurn : MonoBehaviour
{
    const float TickInterval = .25f;

    EnemyStats enemy;
    PlayerStats player;
    float damagePerSecond;
    float expiresAt;
    float nextTick;

    public static void Apply(EnemyStats target, float dps = 20f, float duration = 5f)
    {
        if (target == null || target.IsDead) return;
        DarkEnergyBurn burn = target.GetComponent<DarkEnergyBurn>();
        if (burn == null) burn = target.gameObject.AddComponent<DarkEnergyBurn>();
        burn.enemy = target;
        burn.player = null;
        burn.Refresh(dps, duration);
    }

    public static void Apply(PlayerStats target, float dps = 20f, float duration = 5f)
    {
        if (target == null || target.IsDead) return;
        DarkEnergyBurn burn = target.GetComponent<DarkEnergyBurn>();
        if (burn == null) burn = target.gameObject.AddComponent<DarkEnergyBurn>();
        burn.player = target;
        burn.enemy = null;
        burn.Refresh(dps, duration);
    }

    void Awake()
    {
        DarkEnergyVfx.CreateBurningAura(transform);
    }

    void Refresh(float dps, float duration)
    {
        damagePerSecond = Mathf.Max(damagePerSecond, dps);
        expiresAt = Mathf.Max(expiresAt, Time.time + duration);
        nextTick = Mathf.Min(nextTick, Time.time);
    }

    void Update()
    {
        if (Time.time >= expiresAt || (enemy == null && player == null))
        {
            Destroy(this);
            return;
        }

        if (Time.time < nextTick) return;
        nextTick = Time.time + TickInterval;
        float damage = damagePerSecond * TickInterval;
        if (enemy != null)
        {
            enemy.TakeTrueDamage(damage, transform.position);
            if (enemy.IsDead) Destroy(this);
        }
        else if (player != null)
        {
            player.TakeTrueDamage(damage, transform.position);
            if (player.IsDead) Destroy(this);
        }
    }

    void OnDestroy()
    {
        Transform visual = transform.Find("DarkEnergyBurnVFX");
        if (visual != null) Destroy(visual.gameObject);
    }
}

public sealed class DarkEnergyWeaponEffect : MonoBehaviour
{
    Light glow;
    float nextPulse;

    void Awake()
    {
        if (!DarkEnergyVfx.AttachLittleEnchantDarkAura(transform))
            DarkEnergyVfx.CreateWeaponFlames(transform);
        GameObject lightObject = new GameObject("DarkEnergyWeaponLight");
        lightObject.transform.SetParent(transform, false);
        glow = lightObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(.34f, .03f, .58f);
        glow.range = 1.55f;
        glow.intensity = .21f;
        glow.shadows = LightShadows.None;
        nextPulse = Time.time + Random.Range(.35f, .9f);
    }

    void Update()
    {
        if (glow != null)
            glow.intensity = .16f + (Mathf.Sin(Time.time * 7.2f) + 1f) * .06f;
        if (Time.time >= nextPulse)
        {
            nextPulse = Time.time + Random.Range(.85f, 1.65f);
            DarkEnergyVfx.PlayWeaponPulse(transform);
        }
    }
}

public sealed class PlayerDarkEnergyAura : MonoBehaviour
{
    InsectoidCrabDarkAura vortex;

    public void SetActive(bool active)
    {
        ActiveStatusIconHUD.SetPersistent("dark_energy_aura", "DarkAura", active,
            "Aura de energía oscura");
        // Reuse the crab boss' exact manually-steered wind vortex. This keeps particle size,
        // transparency, inward pull and spiral cadence identical instead of approximating it
        // with a separate velocity-over-lifetime setup.
        if (vortex == null)
            vortex = GetComponent<InsectoidCrabDarkAura>();
        if (vortex == null && active)
            vortex = gameObject.AddComponent<InsectoidCrabDarkAura>();
        vortex?.SetOpacityMultiplier(1.8f);
        vortex?.SetAuraActive(active);

        Transform legacy = transform.Find("PlayerDarkEnergySpiral");
        if (legacy != null)
            legacy.gameObject.SetActive(false);
    }
}

public static class DarkEnergyVfx
{
    const string LittleEnchantDarkResource = "VFX/LittleEnchant_DarkLvl3_URP";
    static Material violetMaterial;
    static Material blackMaterial;
    static Material bloodMaterial;
    static Material sharpVioletMaterial;
    static Material sharpBlackMaterial;
    static Texture2D softParticle;
    static Texture2D sharpStreakParticle;

    public static bool AttachLittleEnchantDarkAura(Transform weapon)
    {
        if (weapon == null) return false;
        Transform existing = weapon.Find("LittleEnchant_DarkAura");
        if (existing != null) return true;

        GameObject prefab = Resources.Load<GameObject>(LittleEnchantDarkResource);
        if (prefab == null) return false;

        MeshFilter bestFilter = null;
        float bestSize = 0f;
        foreach (MeshFilter filter in weapon.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null ||
                filter.GetComponentInParent<ParticleSystem>() != null)
                continue;
            float size = Vector3.Scale(filter.sharedMesh.bounds.size,
                filter.transform.lossyScale).sqrMagnitude;
            if (size > bestSize)
            {
                bestSize = size;
                bestFilter = filter;
            }
        }

        SkinnedMeshRenderer bestSkinned = null;
        if (bestFilter == null)
        {
            foreach (SkinnedMeshRenderer renderer in
                     weapon.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null) continue;
                float size = renderer.bounds.size.sqrMagnitude;
                if (size > bestSize)
                {
                    bestSize = size;
                    bestSkinned = renderer;
                }
            }
        }

        Transform parent = bestFilter != null ? bestFilter.transform :
            bestSkinned != null ? bestSkinned.transform : weapon;
        GameObject aura = Object.Instantiate(prefab, parent, false);
        aura.name = "LittleEnchant_DarkAura";
        aura.transform.localPosition = Vector3.zero;
        aura.transform.localRotation = Quaternion.identity;
        aura.transform.localScale = Vector3.one;

        foreach (ParticleSystem particles in aura.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            if (bestFilter != null)
            {
                shape.shapeType = ParticleSystemShapeType.Mesh;
                shape.mesh = bestFilter.sharedMesh;
            }
            else if (bestSkinned != null)
            {
                shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                shape.skinnedMeshRenderer = bestSkinned;
            }
            ParticleSystemRenderer renderer =
                particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            ParticleSystem.MainModule main = particles.main;
            // The imported level-three enchant was authored for a large showcase
            // preview. On a held weapon its overlapping layers became an opaque
            // violet mass, so keep the detail while making every layer much lighter.
            main.startColor = ScaleOpacity(main.startColor, .22f);
            main.maxParticles = Mathf.Max(8, Mathf.RoundToInt(main.maxParticles * .58f));
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTimeMultiplier *= .48f;
            particles.Play(true);
        }

        Transform legacy = weapon.Find("DarkEnergyWeaponVFX");
        if (legacy != null) Object.Destroy(legacy.gameObject);
        return true;
    }

    static ParticleSystem.MinMaxGradient ScaleOpacity(
        ParticleSystem.MinMaxGradient source, float multiplier)
    {
        multiplier = Mathf.Clamp01(multiplier);
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                Color color = source.color;
                color.a *= multiplier;
                return new ParticleSystem.MinMaxGradient(color);
            case ParticleSystemGradientMode.TwoColors:
                Color minimum = source.colorMin;
                Color maximum = source.colorMax;
                minimum.a *= multiplier;
                maximum.a *= multiplier;
                return new ParticleSystem.MinMaxGradient(minimum, maximum);
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(
                    ScaleGradientOpacity(source.gradient, multiplier));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(
                    ScaleGradientOpacity(source.gradientMin, multiplier),
                    ScaleGradientOpacity(source.gradientMax, multiplier));
            case ParticleSystemGradientMode.RandomColor:
                ParticleSystem.MinMaxGradient random =
                    new ParticleSystem.MinMaxGradient(
                        ScaleGradientOpacity(source.gradient, multiplier));
                random.mode = ParticleSystemGradientMode.RandomColor;
                return random;
            default:
                return source;
        }
    }

    static Gradient ScaleGradientOpacity(Gradient source, float multiplier)
    {
        if (source == null) return new Gradient();
        Gradient result = new Gradient();
        GradientAlphaKey[] alphaKeys = source.alphaKeys;
        for (int i = 0; i < alphaKeys.Length; i++)
            alphaKeys[i].alpha *= multiplier;
        result.SetKeys(source.colorKeys, alphaKeys);
        result.mode = source.mode;
        return result;
    }

    static void ConfigureAuraVelocity(ParticleSystem particles, float upward,
        float radial, float orbital)
    {
        if (particles == null) return;
        ParticleSystem.VelocityOverLifetimeModule velocity =
            particles.velocityOverLifetime;
        velocity.enabled = true;
        // Keeping every axis in Constant mode also prevents Unity's
        // "Particle Velocity curves must all be in the same mode" warning.
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(upward);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);
        velocity.radial = new ParticleSystem.MinMaxCurve(radial);
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(orbital);
    }

    public static void CreateBurningAura(Transform target)
    {
        if (target == null || target.Find("DarkEnergyBurnVFX") != null) return;
        GameObject root = NewRoot("DarkEnergyBurnVFX", target, Vector3.up * .75f);
        ParticleSystem violet = CreateParticles(root.transform, "VioletWindStreaks",
            new Color(.35f, .025f, .68f, .28f),
            58, 31f, .58f, 1.05f, .027f, .72f, 1.65f);
        ParticleSystem black = CreateParticles(root.transform, "BlackWindStreaks",
            new Color(.012f, .004f, .022f, .23f),
            38, 20f, .5f, 1.25f, .018f, .95f, 1.15f);
        ConfigureAuraVelocity(violet, .14f, .34f, 1.65f);
        ConfigureAuraVelocity(black, .2f, .46f, 1.15f);
        ConfigureStreakRenderer(violet, 3.6f, .2f);
        ConfigureStreakRenderer(black, 2.8f, .16f);
        ParticleSystemRenderer violetRenderer =
            violet.GetComponent<ParticleSystemRenderer>();
        if (violetRenderer != null)
            violetRenderer.sharedMaterial = GetSharpVioletMaterial();
        ParticleSystemRenderer blackRenderer =
            black.GetComponent<ParticleSystemRenderer>();
        if (blackRenderer != null)
            blackRenderer.sharedMaterial = GetSharpBlackMaterial();
    }

    public static void CreateWeaponFlames(Transform weapon)
    {
        if (weapon == null || weapon.Find("DarkEnergyWeaponVFX") != null) return;
        GameObject root = NewRoot("DarkEnergyWeaponVFX", weapon, Vector3.zero);
        ParticleSystem violet = CreateParticles(root.transform, "BlackVioletFlame",
            new Color(.5f, .035f, .82f, .52f), 42, 12f, .38f, .58f, .028f, .52f, .08f);
        var violetShape = violet.shape;
        violetShape.shapeType = ParticleSystemShapeType.Box;
        violetShape.scale = new Vector3(.11f, .72f, .11f);

        ParticleSystem black = CreateParticles(root.transform, "BlackParticles",
            new Color(.008f, .002f, .012f, .42f), 30, 7f, .31f, .72f, .021f, .72f, .04f);
        var blackShape = black.shape;
        blackShape.shapeType = ParticleSystemShapeType.Box;
        blackShape.scale = new Vector3(.09f, .66f, .09f);
        ConfigureStreakRenderer(violet, 3.7f, .2f);
        ConfigureStreakRenderer(black, 2.8f, .16f);
        violet.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetSharpVioletMaterial();
    }

    public static void PlayWeaponPulse(Transform weapon)
    {
        if (weapon == null) return;

        GameObject root = NewRoot("DarkWeaponWindPulse", weapon, Vector3.zero);
        ParticleSystem pulse = CreateParticles(root.transform, "WindStreakPulse",
            new Color(.56f, .05f, .9f, .28f), 36, 0f, .18f, .42f, .025f, 1.45f, 0f);
        pulse.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = pulse.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.28f, .62f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.45f, 1.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(.012f, .038f);
        var emission = pulse.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Random.Range(12, 20)) });
        var shape = pulse.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(.12f, .72f, .12f);
        ConfigureStreakRenderer(pulse, 4.2f, .22f);
        pulse.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetSharpVioletMaterial();
        pulse.Play(true);

        Bounds bounds = new Bounds(weapon.position, Vector3.one * .5f);
        bool found = false;
        foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        int arcs = Random.Range(1, 3);
        for (int arcIndex = 0; arcIndex < arcs; arcIndex++)
            CreateElectricArc(bounds, weapon);
        Object.Destroy(root, 1.1f);
    }

    static void CreateElectricArc(Bounds bounds, Transform weapon)
    {
        GameObject arcObject = new GameObject("DarkElectricArc");
        LineRenderer line = arcObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 8;
        line.widthMultiplier = Random.Range(.004f, .009f);
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(.12f, .72f),
            new Keyframe(.5f, 1f),
            new Keyframe(.88f, .72f),
            new Keyframe(1f, 0f));
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.sharedMaterial = GetSharpVioletMaterial();
        Gradient arcColor = new Gradient();
        arcColor.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(.3f, .035f, .62f), 0f),
                new GradientColorKey(new Color(.82f, .42f, 1f), .5f),
                new GradientColorKey(new Color(.3f, .035f, .62f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(.28f, .18f),
                new GradientAlphaKey(.42f, .5f),
                new GradientAlphaKey(.28f, .82f),
                new GradientAlphaKey(0f, 1f)
            });
        line.colorGradient = arcColor;

        Vector3 axis = weapon.up;
        float length = Mathf.Max(.35f, Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * .72f);
        Vector3 center = bounds.center + Random.insideUnitSphere * .04f;
        Vector3 start = center - axis * length * .5f;
        Vector3 end = center + axis * length * .5f;
        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            Vector3 jitter = Random.insideUnitSphere * .035f;
            line.SetPosition(i, Vector3.Lerp(start, end, t) + jitter);
        }
        Object.Destroy(arcObject, Random.Range(.08f, .16f));
    }

    public static GameObject CreatePlayerSpiral(Transform player)
    {
        if (player == null) return null;
        GameObject root = NewRoot("PlayerDarkEnergySpiral", player, Vector3.up * .85f);
        ParticleSystem spiral = CreateParticles(root.transform, "OrbitingDarkEnergy",
            new Color(.32f, .015f, .56f, .29f), 96, 48f, .91f, 1.65f, .065f, .12f, 2.5f);
        var shape = spiral.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = .92f;
        shape.radiusThickness = .18f;
        var velocity = spiral.velocityOverLifetime;
        velocity.enabled = true;
        // Keep X/Y/Z in Constant mode. Unity validates after every individual assignment, so
        // changing the axes to TwoConstants one at a time produces a false transient warning.
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(2.4f);
        velocity.radial = new ParticleSystem.MinMaxCurve(-.16f);
        ConfigureStreakRenderer(spiral, 2.65f, .18f);
        return root;
    }

    public static void PlayImpact(Vector3 position, Transform parent = null)
    {
        GameObject root = NewRoot("DarkEnergyImpact", parent, parent == null ? position : parent.InverseTransformPoint(position));
        if (parent == null) root.transform.position = position;
        ParticleSystem burst = CreateParticles(root.transform, "ImpactBurst",
            new Color(.44f, .025f, .76f, .28f), 72, 0f, .1f, .52f, .052f, 3.25f, 0f);
        burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = burst.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.24f, .62f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.35f, 4.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(.022f, .075f);
        var emission = burst.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 42) });
        var shape = burst.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .12f;
        shape.radiusThickness = 1f;
        shape.randomDirectionAmount = 0f;
        ConfigureStreakRenderer(burst, 3.1f, .2f);
        burst.Play(true);
        Object.Destroy(root, 1.4f);
    }

    public static void PlaySwordBloodImpact(Vector3 position, Vector3 swingDirection)
    {
        GameObject root = NewRoot("SwordBloodStreaks", null, position);
        if (swingDirection.sqrMagnitude > .01f)
            root.transform.rotation = Quaternion.LookRotation(swingDirection.normalized, Vector3.up);

        ParticleSystem blood = CreateParticles(root.transform, "CentrifugalBloodLines",
            new Color(.56f, .015f, .02f, .38f), 48, 0f, .07f, .34f, .028f, 4.2f, 0f);
        blood.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = blood.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.16f, .42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.1f, 6.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(.012f, .042f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(.72f, .025f, .025f, .48f),
            new Color(.19f, .004f, .008f, .2f));
        var bloodColor = blood.colorOverLifetime;
        bloodColor.enabled = false;
        var emission = blood.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });
        var shape = blood.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .045f;
        shape.radiusThickness = 1f;
        ParticleSystemRenderer renderer = blood.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GetBloodMaterial();
        ConfigureStreakRenderer(blood, 4.2f, .25f);
        blood.Play(true);
        Object.Destroy(root, .9f);
    }

    public static void PlayEnchantBurst(Vector3 position, Transform player)
    {
        PlayImpact(position);
        if (player != null)
        {
            ItemInstance equipped = EquipmentManager.Instance?.GetEquippedInstance(ItemType.Weapon);
            if (equipped != null && equipped.darkEnergyEnchanted)
            {
                PlayerDarkEnergyAura aura = player.GetComponent<PlayerDarkEnergyAura>();
                if (aura == null) aura = player.gameObject.AddComponent<PlayerDarkEnergyAura>();
                aura.SetActive(true);
            }
        }
    }

    static GameObject NewRoot(string name, Transform parent, Vector3 localPosition)
    {
        GameObject root = new GameObject(name);
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
        }
        else
        {
            root.transform.position = localPosition;
        }
        return root;
    }

    static ParticleSystem CreateParticles(Transform parent, string name, Color color, int maxParticles,
        float rate, float radius, float lifetime, float size, float speed, float orbital)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.duration = 2f;
        main.loop = true;
        main.maxParticles = maxParticles;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .72f, lifetime * 1.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .55f, speed * 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * .55f, size * 1.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(color,
            new Color(Mathf.Min(1f, color.r * 1.6f), color.g, Mathf.Min(1f, color.b * 1.35f), color.a * .55f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = particles.emission;
        emission.rateOverTime = rate;
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        shape.radiusThickness = .35f;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(new Color(.42f, .03f, .72f), .48f), new GradientColorKey(Color.black, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(color.a, .18f), new GradientAlphaKey(color.a * .72f, .68f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(.18f, .48f);
        noise.frequency = .72f;
        noise.scrollSpeed = .32f;

        if (orbital > 0f)
        {
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(orbital);
        }

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = color.r < .08f ? GetBlackMaterial() : GetVioletMaterial();
        particles.Play(true);
        return particles;
    }

    static Material GetVioletMaterial()
    {
        if (violetMaterial == null)
            violetMaterial = CreateMaterial(new Color(.38f, .015f, .65f, .52f));
        return violetMaterial;
    }

    static Material GetBlackMaterial()
    {
        if (blackMaterial == null)
            blackMaterial = CreateMaterial(new Color(.006f, .002f, .012f, .68f));
        return blackMaterial;
    }

    static Material GetBloodMaterial()
    {
        if (bloodMaterial == null)
            bloodMaterial = CreateMaterial(new Color(.62f, .012f, .018f, .48f));
        return bloodMaterial;
    }

    static Material GetSharpVioletMaterial()
    {
        if (sharpVioletMaterial == null)
            sharpVioletMaterial = CreateMaterial(
                new Color(.52f, .045f, .88f, .42f), GetSharpStreakParticle());
        return sharpVioletMaterial;
    }

    static Material GetSharpBlackMaterial()
    {
        if (sharpBlackMaterial == null)
            sharpBlackMaterial = CreateMaterial(
                new Color(.018f, .004f, .032f, .34f), GetSharpStreakParticle());
        return sharpBlackMaterial;
    }

    static void ConfigureStreakRenderer(ParticleSystem particles, float lengthScale, float velocityScale)
    {
        if (particles == null) return;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = lengthScale;
        renderer.velocityScale = velocityScale;
        renderer.cameraVelocityScale = 0f;
    }

    static Material CreateMaterial(Color color)
    {
        return CreateMaterial(color, GetSoftParticle());
    }

    static Material CreateMaterial(Color color, Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        Material material = new Material(shader) { hideFlags = HideFlags.DontSave, renderQueue = 3000 };
        material.name = "Runtime_DarkEnergy_Particle";
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        return material;
    }

    static Texture2D GetSharpStreakParticle()
    {
        if (sharpStreakParticle != null) return sharpStreakParticle;
        const int width = 96;
        const int height = 32;
        sharpStreakParticle = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Runtime_SharpDarkEnergyStreak",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float nx = Mathf.Abs((x + .5f) / width * 2f - 1f);
            float ny = Mathf.Abs((y + .5f) / height * 2f - 1f);
            // A narrow double-pointed needle. Its width collapses completely at
            // both ends and the edge feathers out, so even a stretched billboard
            // can never reveal the rectangular particle quad.
            float halfWidth = Mathf.Pow(Mathf.Clamp01(1f - nx), .72f) * .78f;
            float edge = halfWidth > .0001f
                ? 1f - Mathf.SmoothStep(.32f, 1f, ny / halfWidth)
                : 0f;
            float tipFade = Mathf.SmoothStep(0f, .13f, 1f - nx);
            float core = 1f - Mathf.SmoothStep(0f, .2f, ny);
            float alpha = Mathf.Clamp01(edge * tipFade * (.58f + core * .34f));
            pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
        }
        sharpStreakParticle.SetPixels(pixels);
        sharpStreakParticle.Apply(false, true);
        return sharpStreakParticle;
    }

    static Texture2D GetSoftParticle()
    {
        if (softParticle != null) return softParticle;
        const int size = 64;
        softParticle = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime_SoftDarkParticle",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x + .5f) / size * 2f - 1f;
            float ny = (y + .5f) / size * 2f - 1f;
            float distance = Mathf.Sqrt(nx * nx + ny * ny);
            float alpha = 1f - Mathf.SmoothStep(.28f, 1f, distance);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        softParticle.SetPixels(pixels);
        softParticle.Apply(false, true);
        return softParticle;
    }
}

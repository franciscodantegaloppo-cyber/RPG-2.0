using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
public class PlayerSpellCaster : MonoBehaviour
{
    public enum Spell { Fireball, IceNova, Lightning, Heal, Shockwave }
    static readonly string[] SpellPrefabPaths =
    {
        "PlayerSpells/Fireball", "PlayerSpells/Ice", "PlayerSpells/Lightning",
        "PlayerSpells/Heal", "PlayerSpells/Shockwave"
    };
    static readonly string[] HandSpellPrefabPaths =
    {
        "PlayerSpellHands/Fire", "PlayerSpellHands/Ice", "PlayerSpellHands/Lightning",
        "PlayerSpellHands/Heal", "PlayerSpellHands/Shockwave"
    };
    static readonly Color[] SpellColours =
    {
        new Color(1f, .25f, .025f), new Color(.08f, .65f, 1f),
        new Color(.62f, .18f, 1f), new Color(.18f, 1f, .42f),
        new Color(1f, .3f, .04f)
    };
    PlayerStats stats;
    Animator animator;
    PlayerSpellAnimationPlayer spellAnimations;
    GameObject heldSpellVisual;
    float nextCast;
    public const float CastStaminaCost = 10f;
    public float CastCooldownRemaining => Mathf.Max(0f, nextCast - Time.time);
    public Spell EquippedSpell { get; private set; } = Spell.Fireball;
    public bool HasEquippedSpell { get; private set; }

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        animator = GetComponentInChildren<Animator>();
        spellAnimations = GetComponent<PlayerSpellAnimationPlayer>() ?? gameObject.AddComponent<PlayerSpellAnimationPlayer>();
    }

    public void Equip(Spell spell)
    {
        EquippedSpell = spell;
        HasEquippedSpell = true;
        StoreAndUnequipWeapon();
        CreateHeldSpellVisual();
    }

    public void UnequipSpell()
    {
        HasEquippedSpell = false;
        if (heldSpellVisual != null) Destroy(heldSpellVisual);
    }

    void CreateHeldSpellVisual()
    {
        if (heldSpellVisual != null) Destroy(heldSpellVisual);
        GanzPlayerVisual ganz = GetComponent<GanzPlayerVisual>();
        Animator visibleAnimator = ganz != null ? ganz.VisualAnimator : null;
        if (visibleAnimator != null) animator = visibleAnimator;
        else if (animator == null) animator = GetComponentInChildren<Animator>();
        Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (hand == null) hand = transform;

        heldSpellVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        heldSpellVisual.name = "EquippedSpell_" + EquippedSpell;
        heldSpellVisual.transform.SetParent(hand, false);
        heldSpellVisual.transform.localPosition = hand == transform ? new Vector3(.38f, 1.25f, .42f) : new Vector3(.11f, .035f, .015f);
        heldSpellVisual.transform.localScale = Vector3.one * .16f;
        Collider sphereCollider = heldSpellVisual.GetComponent<Collider>();
        if (sphereCollider != null) Destroy(sphereCollider);

        Color colour = SpellColours[(int)EquippedSpell];
        Renderer renderer = heldSpellVisual.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (renderer != null && shader != null)
        {
            Material material = new Material(shader);
            material.color = colour;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour * 2.4f);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", colour * 5f);
            renderer.material = material;
        }
        Light glow = heldSpellVisual.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = colour;
        glow.range = 2.8f;
        glow.intensity = 2.2f;
        HeldSpellPulse pulse = heldSpellVisual.AddComponent<HeldSpellPulse>();
        pulse.Configure(glow);
        AddImportedHandAura(colour);
    }

    void AddImportedHandAura(Color colour)
    {
        GameObject auraPrefab = Resources.Load<GameObject>(HandSpellPrefabPaths[(int)EquippedSpell]);
        if (auraPrefab == null) return;

        GameObject aura = Instantiate(auraPrefab, heldSpellVisual.transform);
        aura.name = "ImportedAura_" + EquippedSpell;
        aura.transform.localPosition = Vector3.zero;
        aura.transform.localRotation = Quaternion.identity;
        // The parent core is already scaled to hand size.
        aura.transform.localScale = Vector3.one * .9f;

        foreach (Collider auraCollider in aura.GetComponentsInChildren<Collider>(true))
            Destroy(auraCollider);

        foreach (ParticleSystem particles in aura.GetComponentsInChildren<ParticleSystem>(true))
        {
            // Aura prefabs were authored as one-shot effects around a full character. Restarting
            // them as small local loops turns the imported flames into a stable hand spell while
            // keeping their original textures, trails and materials.
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            Color bright = new Color(Mathf.Min(1f, colour.r * 1.3f), Mathf.Min(1f, colour.g * 1.3f), Mathf.Min(1f, colour.b * 1.3f), .92f);
            Color dark = new Color(colour.r * .55f, colour.g * .55f, colour.b * .55f, .45f);
            main.startColor = new ParticleSystem.MinMaxGradient(dark, bright);
            particles.Play(true);
        }

        foreach (Light auraLight in aura.GetComponentsInChildren<Light>(true))
        {
            auraLight.color = colour;
            auraLight.range = .85f;
            auraLight.intensity = .9f;
            auraLight.shadows = LightShadows.None;
        }
    }

    void StoreAndUnequipWeapon()
    {
        EquipmentManager equipment = EquipmentManager.Instance;
        ItemInstance weapon = equipment?.GetEquippedInstance(ItemType.Weapon);
        if (weapon == null) return;

        equipment.Unequip(weapon);
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null) return;
        bool alreadyStored = inventory.Slots.Exists(slot => slot != null &&
            (slot.instance == weapon || (slot.instance == null && slot.item == weapon.template)));
        if (!alreadyStored) inventory.AddItemInstance(weapon);
    }

    public void CastEquipped()
    {
        // A spell can never fire while a sword is still registered as equipped. Normally Equip()
        // stores it first; this guard also covers equipment restored by saves or other systems.
        bool hasWeapon = EquipmentManager.Instance?.GetEquippedInstance(ItemType.Weapon) != null;
        if (hasWeapon || !HasEquippedSpell || Time.time < nextCast || stats == null || stats.IsDead || !stats.HasStamina(CastStaminaCost)) return;
        Vector3 direction = GetCastDirection();
        EnemyStats lightningTarget = EquippedSpell == Spell.Lightning ? FindLightningTarget(direction) : null;
        // Lightning is target-locked: without a living enemy in front it neither spends stamina
        // nor creates a misleading strike on empty terrain.
        if (EquippedSpell == Spell.Lightning && lightningTarget == null) return;
        nextCast = Time.time + .65f;
        stats.DrainStamina(CastStaminaCost);
        ActiveStatusIconHUD.ShowTimed("spell_cast", GetSpellIconName(EquippedSpell),
            1.4f, EquippedSpell.ToString());
        // The playable helper drives the imported Human Spellcasting clip directly, avoiding
        // missing-parameter warnings from the existing melee Animator Controller.
        spellAnimations?.Play((int)EquippedSpell);
        float power = stats.TotalAttack *
            (1.45f + SkillTreeProgress.GetMagicBonus(transform)) *
            stats.MagicPowerMultiplier;
        Vector3 origin = GetCastOrigin(direction);
        switch (EquippedSpell)
        {
            case Spell.Fireball:
                LaunchProjectile(Spell.Fireball, origin, direction, power, 18f, .72f);
                break;
            case Spell.IceNova:
                Vector3 icePoint = transform.position + direction * 2f + Vector3.up * .15f;
                SpawnPackEffect(Spell.IceNova, icePoint, Quaternion.LookRotation(direction), 4f);
                AreaDamage(icePoint, 4f, power * .85f);
                break;
            case Spell.Lightning:
                Vector3 lightningPoint = GetEnemyStrikePoint(lightningTarget);
                SpawnPackEffect(Spell.Lightning, lightningPoint, Quaternion.identity, 4f);
                float lightningDamage = power * 1.15f;
                lightningTarget.TakeDamage(lightningDamage, transform.position);
                DamageNumberPool.Instance?.ShowMagicDamage(lightningTarget.transform.position + Vector3.up, lightningDamage);
                break;
            case Spell.Heal:
                SpawnPackEffect(Spell.Heal, transform.position + Vector3.up * .15f, transform.rotation, 4f);
                stats.Heal(25f + power * .4f);
                break;
            case Spell.Shockwave:
                Vector3 wavePoint = transform.position + direction * 2.2f + Vector3.up * .1f;
                SpawnPackEffect(Spell.Shockwave, wavePoint, Quaternion.LookRotation(direction), 4f);
                AreaDamage(wavePoint, 5.5f, power);
                break;
        }
    }

    static string GetSpellIconName(Spell spell)
    {
        switch (spell)
        {
            case Spell.Fireball: return "Fire";
            case Spell.IceNova: return "Ice";
            case Spell.Lightning: return "Lightning";
            case Spell.Heal: return "Healing";
            case Spell.Shockwave: return "Wind";
            default: return "Buff";
        }
    }

    Vector3 GetCastDirection()
    {
        Vector3 direction = transform.forward;
        direction.y = 0f;
        return direction.sqrMagnitude > .001f ? direction.normalized : Vector3.forward;
    }

    EnemyStats FindLightningTarget(Vector3 castDirection)
    {
        EnemyStats best = null;
        float bestScore = float.MaxValue;
        foreach (EnemyStats enemy in FindObjectsByType<EnemyStats>())
        {
            if (enemy == null || enemy.IsDead) continue;
            Vector3 toEnemy = enemy.transform.position - transform.position;
            float distance = toEnemy.magnitude;
            if (distance < .2f || distance > 25f) continue;
            Vector3 flatDirection = Vector3.ProjectOnPlane(toEnemy, Vector3.up).normalized;
            float alignment = Vector3.Dot(castDirection, flatDirection);
            if (alignment < .25f) continue;

            // Prefer the enemy closest to the centre of the player's aim, then distance.
            float score = distance + (1f - alignment) * 18f;
            if (score >= bestScore) continue;
            bestScore = score;
            best = enemy;
        }
        return best;
    }

    static Vector3 GetEnemyStrikePoint(EnemyStats enemy)
    {
        if (enemy == null) return Vector3.zero;
        Collider targetCollider = enemy.GetComponentInChildren<Collider>();
        if (targetCollider != null)
            return new Vector3(targetCollider.bounds.center.x, targetCollider.bounds.min.y + .05f, targetCollider.bounds.center.z);
        return enemy.transform.position;
    }

    Vector3 GetCastOrigin(Vector3 direction)
    {
        if (heldSpellVisual != null)
            return heldSpellVisual.transform.position + direction * .12f;
        if (animator != null && animator.avatar != null && animator.avatar.isHuman)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand != null) return hand.position + direction * .12f;
        }
        return transform.position + direction * .75f + Vector3.up * 1.3f;
    }

    void LaunchProjectile(Spell spell, Vector3 origin, Vector3 direction, float damage, float speed, float scale)
    {
        GameObject prefab = Resources.Load<GameObject>(SpellPrefabPaths[(int)spell]);
        GameObject projectile;
        if (prefab != null)
        {
            projectile = Instantiate(prefab, origin, Quaternion.LookRotation(direction));
            foreach (Collider childCollider in projectile.GetComponentsInChildren<Collider>()) Destroy(childCollider);
            projectile.transform.localScale *= scale;
        }
        else
        {
            projectile = Fireball.BuildRedFireball(scale);
            Destroy(projectile.GetComponent<Fireball>());
            projectile.transform.position = origin;
        }
        projectile.AddComponent<PlayerSpellProjectile>().Launch(direction, damage, speed, transform);
    }

    void SpawnPackEffect(Spell spell, Vector3 position, Quaternion rotation, float lifetime)
    {
        GameObject prefab = Resources.Load<GameObject>(SpellPrefabPaths[(int)spell]);
        if (prefab == null) return;
        GameObject effect = Instantiate(prefab, position, rotation);
        foreach (Collider childCollider in effect.GetComponentsInChildren<Collider>()) Destroy(childCollider);
        Destroy(effect, lifetime);
    }

    void AreaDamage(Vector3 center, float radius, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, 1 << LayerMask.NameToLayer("Enemy"));
        foreach (Collider hit in hits)
        {
            EnemyStats enemy = hit.GetComponentInParent<EnemyStats>();
            if (enemy == null) continue;
            enemy.TakeDamage(damage, transform.position);
            DamageNumberPool.Instance?.ShowMagicDamage(enemy.transform.position + Vector3.up, damage);
        }
    }

    void OnDestroy()
    {
        if (heldSpellVisual != null) Destroy(heldSpellVisual);
    }
}

// A small stable hand aura makes the selected spell visible before the one-shot pack VFX fires.
public class HeldSpellPulse : MonoBehaviour
{
    Light glow;
    Vector3 baseScale;

    public void Configure(Light spellGlow)
    {
        glow = spellGlow;
        baseScale = transform.localScale;
    }

    void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * 7f) * .14f;
        transform.localScale = baseScale * pulse;
        transform.Rotate(40f * Time.deltaTime, 75f * Time.deltaTime, 25f * Time.deltaTime, Space.Self);
        if (glow != null) glow.intensity = 2.1f + Mathf.Sin(Time.time * 8f) * .45f;
    }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] float maxHealth = 100f;
    float currentHealth;

    [Header("Stamina")]
    [SerializeField] float maxStamina = 100f;
    [SerializeField] float staminaRegenRate = 20f;
    [SerializeField] float staminaRegenDelay = 1.5f;
    float currentStamina;
    float staminaRegenTimer;
    float equipmentStaminaMultiplier = 1f;

    // Temporary stamina-potion buff: less cost per action, faster regen. 1 = no effect.
    float staminaCostMultiplier = 1f;
    float staminaRegenMultiplier = 1f;
    float staminaBuffEndTime;

    [Header("Combat")]
    public float baseAttack = 10f;
    public float baseDefense = 0f;

    [Header("Skills")]
    [SerializeField] int availableSkillPoints = 5;
    [SerializeField] int strengthLevel = 0;
    [SerializeField] int agilityLevel = 0;
    [SerializeField] int speedLevel = 0;
    [SerializeField] int vitalityLevel = 0;
    [SerializeField] int enduranceLevel = 0;
    [SerializeField] int precisionLevel = 0;
    [SerializeField] int criticalLevel = 0;
    [SerializeField] int criticalDamageLevel = 0;
    [SerializeField] int recoveryLevel = 0;
    [SerializeField] int magicLevel = 0;

    [Header("Experience")]
    [SerializeField] int level = 1;
    [SerializeField] float currentXP = 0f;
    [SerializeField] float xpToNextLevel = 100f;
    [SerializeField] float xpGrowthPerLevel = 1.25f;

    [Header("Knockdown")]
    [SerializeField] float knockdownThreshold = 30f; // single hit > this triggers knockdown

    public event System.Action<float, float> OnHealthChanged;
    public event System.Action<float, float> OnStaminaChanged;
    public event System.Action OnDeath;

    // Global hook so an attacker (e.g. CrabDemonBossAI's victory-dance trigger) can react to the
    // player dying without needing a direct reference wired up ahead of time - mirrors
    // EnemyStats.OnAnyDeath's same rationale.
    public static event System.Action GlobalOnDeath;

    public float CurrentHealth  => currentHealth;
    public float MaxHealth      => maxHealth + vitalityLevel * 5f;
    public float CurrentStamina => currentStamina;
    public float MaxStamina     => (maxStamina + enduranceLevel * 4f) *
                                   equipmentStaminaMultiplier *
                                   (1f + SkillTreeProgress.GetStaminaBonus(transform));
    public bool  IsDead         { get; private set; }
    public bool  IsKnockedDown  { get; private set; }
    public bool  IsRolling      { get; private set; }
    public bool  GodModeEnabled { get; private set; }
    public bool  MaxWindResistanceEnabled { get; private set; }
    public bool  InfiniteStaminaEnabled { get; private set; }
    public int AvailableSkillPoints => availableSkillPoints;
    public int StrengthLevel => strengthLevel;
    public int AgilityLevel => agilityLevel;
    public int SpeedLevel => speedLevel;
    public int VitalityLevel => vitalityLevel;
    public int EnduranceLevel => enduranceLevel;
    public int PrecisionLevel => precisionLevel;
    public int CriticalLevel => criticalLevel;
    public int CriticalDamageLevel => criticalDamageLevel;
    public int RecoveryLevel => recoveryLevel;
    public int MagicLevel => magicLevel;
    public int Level => level;
    public float CurrentXP => currentXP;
    public float XPToNextLevel => xpToNextLevel;
    public float AttackBonusFromSkills => strengthLevel * 2f;
    public float DefenseBonusFromSkills => agilityLevel * 0.5f;
    // Movement is deliberately a long-term investment. The first 50% is earned at the normal
    // rate; every percentage after that is reduced to 40% efficiency. Reaching x3 therefore
    // requires roughly 425 dedicated effective speed points instead of only 34.
    public float MoveSpeedMultiplier
    {
        get
        {
            float rawBonus = speedLevel * .01f + agilityLevel * .0025f +
                             SkillTreeProgress.GetMoveSpeedBonus(transform) +
                             (EquipmentManager.Instance
                                 ?.GetTotalMovementSpeedBonusPercent() ?? 0f) / 100f;
            float diminished = rawBonus <= .5f
                ? rawBonus
                : .5f + (rawBonus - .5f) * .4f;
            return Mathf.Clamp(1f + diminished, 1f, 3f);
        }
    }
    public float TotalAttack => (baseAttack + AttackBonusFromSkills) * (1f + SkillTreeProgress.GetPhysicalAttackBonus(transform));
    public float TotalDefense => baseDefense + DefenseBonusFromSkills + SkillTreeProgress.GetDefenseBonus(transform);
    public float CriticalChancePercent => 5f + criticalLevel * .25f +
        EquippedWeaponAffix(AffixType.CritChancePercent);
    public float CriticalDamagePercent => 50f + criticalDamageLevel * 1.5f +
        EquippedWeaponAffix(AffixType.CritDamagePercent);
    public float ArmorPenetration => precisionLevel * .5f +
        EquippedWeaponAffix(AffixType.ArmorPenetrationFlat) +
        EquippedWeaponAffix(AffixType.Filo);
    public float MagicPowerPercent => magicLevel * 2f +
        SkillTreeProgress.GetMagicBonus(transform) * 100f;
    public float MagicPowerMultiplier => 1f + magicLevel * .02f;
    public float StaminaRegenPerSecond => staminaRegenRate * (1f + recoveryLevel * .02f);
    public float AttackSpeedMultiplier
    {
        get
        {
            WeaponData weapon = EquipmentManager.Instance?.GetEquippedInstance(ItemType.Weapon)
                ?.template?.weaponData;
            float weaponSpeed = weapon != null ? weapon.attackSpeedMultiplier : 1f;
            return weaponSpeed * (1f + EquippedWeaponAffix(AffixType.AttackSpeedPercent) / 100f);
        }
    }
    public float WeaponRangePercent => EquippedWeaponAffix(AffixType.WeaponRangePercent);

    float EquippedWeaponAffix(AffixType type)
    {
        return EquipmentManager.Instance?.GetEquippedInstance(ItemType.Weapon)
            ?.GetAffixValue(type) ?? 0f;
    }

    public int GetSkillLevel(string skillId)
    {
        return skillId switch
        {
            "strength" => strengthLevel,
            "agility" => agilityLevel,
            "speed" => speedLevel,
            "vitality" => vitalityLevel,
            "endurance" => enduranceLevel,
            "precision" => precisionLevel,
            "critical" => criticalLevel,
            "critical_damage" => criticalDamageLevel,
            "recovery" => recoveryLevel,
            "magic" => magicLevel,
            _ => 0
        };
    }

    public event System.Action OnSkillsChanged;
    public event System.Action<float, float> OnExperienceChanged;
    public event System.Action<int> OnLevelUp;

    public void NotifySkillTreeChanged()
    {
        currentStamina = Mathf.Min(currentStamina, MaxStamina);
        OnSkillsChanged?.Invoke();
        OnStaminaChanged?.Invoke(currentStamina, MaxStamina);
    }

    PlayerAnimatorBridge animBridge;

    void Awake()
    {
        currentHealth  = MaxHealth;
        currentStamina = maxStamina;
        animBridge     = GetComponent<PlayerAnimatorBridge>();
        // Visual feedback is attached directly to the player so every path that awards XP
        // (kills, quests or the chat command) gets the imported status-effect feedback.
        if (GetComponent<PlayerLevelUpFireEffect>() == null)
            gameObject.AddComponent<PlayerLevelUpFireEffect>();
        if (GetComponent<PlayerPolishController>() == null)
            gameObject.AddComponent<PlayerPolishController>();
        if (GetComponent<PlayerDefenseController>() == null)
            gameObject.AddComponent<PlayerDefenseController>();
        if (GetComponent<PlayerRpgAnimationPackController>() == null)
            gameObject.AddComponent<PlayerRpgAnimationPackController>();
    }

    void OnEnable()  => EnemyStats.OnAnyDeath += HandleEnemyKilled;
    void OnDisable() => EnemyStats.OnAnyDeath -= HandleEnemyKilled;

    void HandleEnemyKilled(EnemyStats enemy)
    {
        if (enemy == null) return;
        AddExperience(enemy.xpReward);
    }

    public void AddExperience(float amount)
    {
        if (amount <= 0f || IsDead) return;

        currentXP += amount;
        while (currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            level++;
            xpToNextLevel *= xpGrowthPerLevel;
            AddSkillPoints(1);
            OnLevelUp?.Invoke(level);
        }

        OnExperienceChanged?.Invoke(currentXP, xpToNextLevel);
    }

    void Update()
    {
        RegenStamina();
        UpdateStaminaBuff();
    }

    void RegenStamina()
    {
        if (currentStamina >= MaxStamina) return;
        staminaRegenTimer -= Time.deltaTime;
        if (staminaRegenTimer > 0f) return;
        currentStamina = Mathf.Min(currentStamina +
            StaminaRegenPerSecond * staminaRegenMultiplier * Time.deltaTime, MaxStamina);
        OnStaminaChanged?.Invoke(currentStamina, MaxStamina);
    }

    void UpdateStaminaBuff()
    {
        if (staminaBuffEndTime <= 0f || Time.time < staminaBuffEndTime)
            return;

        staminaCostMultiplier = 1f;
        staminaRegenMultiplier = 1f;
        staminaBuffEndTime = 0f;
    }

    // Stamina-potion effect: less stamina consumed per action, faster regen, for a duration.
    // Reapplying refreshes the duration rather than stacking multipliers.
    public void ApplyStaminaBuff(float costReductionPercent, float regenBoostPercent, float duration)
    {
        staminaCostMultiplier = Mathf.Clamp01(1f - costReductionPercent / 100f);
        staminaRegenMultiplier = Mathf.Max(1f, 1f + regenBoostPercent / 100f);
        staminaBuffEndTime = Time.time + duration;
        ActiveStatusIconHUD.ShowTimed("stamina_buff", "Buff", duration,
            "Poción de estamina");
    }

    // Basic damage (no direction)
    public void TakeDamage(float amount)
    {
        TakeDamage(amount, transform.position - transform.forward); // default: front hit
    }

    // Damage with attacker position for directional hit reaction
    public void TakeDamage(float amount, Vector3 attackerWorldPos)
    {
        if (GodModeEnabled)
        {
            if (currentHealth < MaxHealth)
            {
                currentHealth = MaxHealth;
                OnHealthChanged?.Invoke(currentHealth, MaxHealth);
            }
            return;
        }

        if (IsDead) return;

        PlayerDefenseController defense = GetComponent<PlayerDefenseController>();
        if (defense != null && defense.TryMitigate(ref amount, attackerWorldPos) &&
            amount <= .001f)
            return;

        float reduced = Mathf.Max(1f, amount - TotalDefense);
        currentHealth = Mathf.Max(0f, currentHealth - reduced);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        DamageNumberPool.Instance?.ShowPlayerDamage(transform.position + Vector3.up, reduced);

        if (currentHealth <= 0f) { Die(); return; }

        // Dodge roll grants invincibility — skip reactions
        if (IsRolling) return;

        if (reduced >= knockdownThreshold && !IsKnockedDown)
        {
            StartCoroutine(KnockdownSequence());
        }
        else
        {
            // Regular directional hit reaction
            animBridge?.TriggerGetHitDirectional(attackerWorldPos);
        }
    }

    IEnumerator KnockdownSequence()
    {
        IsKnockedDown = true;
        animBridge?.TriggerKnockdown();
        // Wait for knockdown + getup animations
        yield return new WaitForSeconds(3.5f);
        IsKnockedDown = false;
    }

    public void Heal(float amount)
    {
        float previous = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        if (currentHealth > previous + .01f)
        {
            PlayerStatusEffectVfx.PlayHealing(transform);
            DamageNumberPool.Instance?.ShowHealing(transform.position + Vector3.up,
                currentHealth - previous);
        }
    }

    // Fall damage intentionally ignores defense: it is a percentage of the character's own
    // maximum health, not an enemy attack. God mode still protects the player as expected.
    public void TakeFallDamage(float maxHealthPercent)
    {
        if (GodModeEnabled || IsDead || maxHealthPercent <= 0f) return;

        float damage = MaxHealth * maxHealthPercent;
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        if (currentHealth <= 0f)
            Die();
    }

    // Environmental percentage damage bypasses defense, just like fall damage.
    // It is used for drowning so one tick is always a stable share of base life.
    public void TakeDrowningDamage(float maxHealthPercent)
    {
        if (GodModeEnabled || IsDead || maxHealthPercent <= 0f) return;

        float damage = MaxHealth * maxHealthPercent;
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        if (currentHealth <= 0f)
            Die();
    }

    public void TakeTrueDamage(float amount, Vector3 attackerWorldPos)
    {
        if (GodModeEnabled || IsDead || amount <= 0f) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        DamageNumberPool.Instance?.ShowDarkDamage(transform.position + Vector3.up, amount);
        if (currentHealth <= 0f)
        {
            Die();
            return;
        }
        if (!IsRolling)
            animBridge?.TriggerGetHitDirectional(attackerWorldPos);
    }

    // Story hazards may be absolute even while testing in god mode. Mission seven uses this
    // for the burning water surrounding the island so it cannot become an alternate route.
    public void ForceEnvironmentalDeath()
    {
        if (IsDead) return;
        currentHealth = 0f;
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        Die();
    }

    public bool HasStamina(float amount = 0.1f) => currentStamina >= amount;

    public void SetEquipmentStaminaMultiplier(float multiplier)
    {
        float previousMax = Mathf.Max(.01f, MaxStamina);
        float ratio = Mathf.Clamp01(currentStamina / previousMax);
        equipmentStaminaMultiplier = Mathf.Max(.1f, multiplier);
        currentStamina = MaxStamina * ratio;
        OnStaminaChanged?.Invoke(currentStamina, MaxStamina);
    }

    public void DrainStamina(float amount)
    {
        if (InfiniteStaminaEnabled)
        {
            if (currentStamina < MaxStamina)
            {
                currentStamina = MaxStamina;
                OnStaminaChanged?.Invoke(currentStamina, MaxStamina);
            }
            return;
        }

        currentStamina = Mathf.Max(0f, currentStamina - amount * staminaCostMultiplier);
        staminaRegenTimer = staminaRegenDelay;
        OnStaminaChanged?.Invoke(currentStamina, MaxStamina);
    }

    public void SetRolling(bool value) => IsRolling = value;

    public void SetMaxWindResistance(bool enabled) => MaxWindResistanceEnabled = enabled;

    public void SetInfiniteStamina(bool enabled)
    {
        InfiniteStaminaEnabled = enabled;
        if (!enabled) return;

        currentStamina = MaxStamina;
        staminaRegenTimer = 0f;
        OnStaminaChanged?.Invoke(currentStamina, MaxStamina);
    }

    public void SetGodMode(bool enabled)
    {
        GodModeEnabled = enabled;
        if (!enabled) return;

        IsDead = false;
        currentHealth = MaxHealth;
        currentStamina = MaxStamina;
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        OnStaminaChanged?.Invoke(currentStamina, MaxStamina);
    }

    public bool UpgradeSkill(string skillId)
    {
        if (availableSkillPoints <= 0)
            return false;

        switch (skillId)
        {
            case "strength":
                strengthLevel++;
                break;
            case "agility":
                agilityLevel++;
                break;
            case "speed":
                if (MoveSpeedMultiplier >= 3f)
                    return false;
                speedLevel++;
                break;
            case "vitality":
                vitalityLevel++;
                currentHealth = Mathf.Min(MaxHealth, currentHealth + 5f);
                OnHealthChanged?.Invoke(currentHealth, MaxHealth);
                break;
            case "endurance":
                enduranceLevel++;
                currentStamina = Mathf.Min(MaxStamina, currentStamina + 4f);
                OnStaminaChanged?.Invoke(currentStamina, MaxStamina);
                break;
            case "precision":
                precisionLevel++;
                break;
            case "critical":
                criticalLevel++;
                break;
            case "critical_damage":
                criticalDamageLevel++;
                break;
            case "recovery":
                recoveryLevel++;
                break;
            case "magic":
                magicLevel++;
                break;
            default:
                return false;
        }

        availableSkillPoints--;
        OnSkillsChanged?.Invoke();
        return true;
    }

    public void AddSkillPoints(int amount)
    {
        availableSkillPoints = Mathf.Max(0, availableSkillPoints + amount);
        OnSkillsChanged?.Invoke();
    }

    // Shared by the classic attributes and the Path-of-Exile-style tree. Keeping the spending
    // here guarantees that every level-up point belongs to one common player pool.
    public bool TrySpendSkillPoint()
    {
        if (availableSkillPoints <= 0) return false;
        availableSkillPoints--;
        OnSkillsChanged?.Invoke();
        return true;
    }

    void Die()
    {
        IsDead = true;
        IsKnockedDown = false;
        IsRolling = false;
        animBridge?.TriggerDeath();
        OnDeath?.Invoke();
        GlobalOnDeath?.Invoke();
        GameManager.Instance?.SetState(GameState.InMenu);
        StartCoroutine(DeathAndRespawn());
    }

    IEnumerator DeathAndRespawn()
    {
        GameObject message = CreateDeathMessage();
        yield return new WaitForSecondsRealtime(3f);

        GameObject spawn = null;
        try { spawn = GameObject.FindWithTag("SpawnPoint"); }
        catch (UnityException) { }
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        if (spawn != null)
            transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        if (controller != null) controller.enabled = true;

        currentHealth = MaxHealth;
        currentStamina = MaxStamina;
        staminaRegenTimer = 0f;
        IsDead = false;
        IsKnockedDown = false;
        IsRolling = false;
        GetComponent<PlayerController>()?.ResetAfterRespawn();
        animBridge?.TriggerRevive();
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        OnStaminaChanged?.Invoke(currentStamina, MaxStamina);
        if (message != null) Destroy(message);
        GameManager.Instance?.SetState(GameState.Exploration);
    }

    GameObject CreateDeathMessage()
    {
        Canvas canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null) return null;
        GameObject panel = new GameObject("PlagueDeathMessage", typeof(RectTransform), typeof(Image), typeof(Canvas));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.28f, .39f);
        rect.anchorMax = new Vector2(.72f, .61f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = panel.GetComponent<Image>();
        Sprite sprite = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        if (sprite != null) { image.sprite = sprite; image.type = Image.Type.Sliced; image.color = Color.white; }
        else image.color = new Color(.08f, .008f, .012f, .97f);
        Canvas top = panel.GetComponent<Canvas>();
        top.overrideSorting = true;
        top.sortingOrder = 5000;

        GameObject textObject = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = "TE COMIÃ“ LA PESTE";
        label.fontSize = 42f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 24f;
        label.fontSizeMax = 42f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(1f, .3f, .16f);
        label.alignment = TextAlignmentOptions.Center;
        label.outlineColor = Color.black;
        label.outlineWidth = .2f;
        RectTransform tr = label.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(34f, 20f); tr.offsetMax = new Vector2(-34f, -20f);
        return panel;
    }
}

// Kept alongside PlayerStats so every player build includes the level-up feedback component.
[RequireComponent(typeof(PlayerStats))]
public class PlayerLevelUpFireEffect : MonoBehaviour
{
    const string LevelUpVfxResource = "VFX/StatusEffects/LevelUp";
    const float Duration = 2f;
    PlayerStats stats;
    GameObject activeEffect;
    GameObject screenFlash;

    void Awake() => stats = GetComponent<PlayerStats>();
    void OnEnable() { if (stats != null) stats.OnLevelUp += Play; }
    void OnDisable() { if (stats != null) stats.OnLevelUp -= Play; }

    public static void PlayFor(Transform player)
    {
        if (player == null) return;
        PlayerLevelUpFireEffect effect = player.GetComponent<PlayerLevelUpFireEffect>();
        if (effect == null) effect = player.gameObject.AddComponent<PlayerLevelUpFireEffect>();
        effect.Play(0);
    }

    void Play(int _)
    {
        if (activeEffect != null) Destroy(activeEffect);
        StartCoroutine(PlayScreenFlash());
        // Buff_03a_Aura from the imported status-aura pack, recoloured to a
        // warm yellow/gold exclusively for the level-up celebration.
        activeEffect = PlayerStatusEffectVfx.Play(transform, LevelUpVfxResource,
            "LevelUp_YellowPackAura", Duration, new Vector3(0f, .95f, 0f), 1.2f,
            new Color(1f, .68f, .08f, 1f));
        ActiveStatusIconHUD.ShowTimed("level_up", "LevelUp", Duration, "Subida de nivel");
    }

    System.Collections.IEnumerator FadeAndRemove(GameObject effect, Light glow)
    {
        float elapsed = 0f;
        while (elapsed < Duration && effect != null)
        {
            elapsed += Time.deltaTime;
            if (glow != null) glow.intensity = 9f * (1f - Mathf.SmoothStep(0.58f, 1f, elapsed / Duration));
            yield return null;
        }
        if (effect != null) Destroy(effect);
        if (activeEffect == effect) activeEffect = null;
    }

    System.Collections.IEnumerator PlayScreenFlash()
    {
        if (screenFlash != null) Destroy(screenFlash);
        screenFlash = new GameObject("LevelUp_YellowScreenFlash", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = screenFlash.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5000;
        RectTransform rect = screenFlash.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = screenFlash.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = new Color(1f, 0.76f, 0.08f, 0f);

        yield return FadeFlash(image, 0f, 0.32f, 0.07f);
        yield return FadeFlash(image, 0.32f, 0f, 0.16f);
        yield return FadeFlash(image, 0f, 0.16f, 0.06f);
        yield return FadeFlash(image, 0.16f, 0f, 0.34f);
        if (screenFlash != null) Destroy(screenFlash);
        screenFlash = null;
    }

    static System.Collections.IEnumerator FadeFlash(Image image, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && image != null)
        {
            elapsed += Time.deltaTime;
            Color color = image.color;
            color.a = Mathf.Lerp(from, to, elapsed / duration);
            image.color = color;
            yield return null;
        }
    }
}

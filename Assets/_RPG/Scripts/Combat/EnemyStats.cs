using UnityEngine;

// New values must always be appended at the end (same serialization-drift rule as ItemType).
public enum CreatureType { Normal, Undead, Beast, Boss }

public class EnemyStats : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] float maxHealth = 50f;
    [SerializeField] float attack = 8f;
    [SerializeField] float armor = 0f;
    [SerializeField] CreatureType creatureType = CreatureType.Normal;
    [SerializeField] public int xpReward = 25;

    [Header("Drops")]
    [SerializeField] int minGoldDrop = 5;
    [SerializeField] int maxGoldDrop = 10;
    [SerializeField, Range(0f, 1f)] float runeDropChance = 0.2f;

    float currentHealth;
    bool bloodRainBuffed;
    EnemyAI ai;
    KingGoblinBossAI kingBoss;
    CrabDemonBossAI crabBoss;
    OrcWarriorAI orcWarrior;
    AnimalAI animalAi;

    public float Attack => attack;
    public float Armor => armor;
    public CreatureType Type => creatureType;
    public bool IsDead { get; private set; }
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    // Used by GoblinSpawner to build the "pink goblin" variant (2x health/attack) at runtime
    // without needing a separate prefab asset. Must run before Awake() has fully elapsed isn't
    // required - it also rescales currentHealth so a post-Awake call still fully heals the buff.
    public void ApplyStatMultiplier(float healthMultiplier, float attackMultiplier)
    {
        maxHealth *= healthMultiplier;
        attack *= attackMultiplier;
        currentHealth = maxHealth;
    }

    public void SetBloodRainBuff(bool enabled)
    {
        if (bloodRainBuffed == enabled) return;
        float ratio = maxHealth > .001f ? Mathf.Clamp01(currentHealth / maxHealth) : 1f;
        float multiplier = enabled ? 3f : 1f / 3f;
        maxHealth *= multiplier;
        attack *= multiplier;
        currentHealth = Mathf.Max(1f, maxHealth * ratio);
        bloodRainBuffed = enabled;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // Keeps rare-spawn rewards in sync with their combat multiplier.  This is deliberately
    // separate from ApplyStatMultiplier so callers can create variants with only stat changes.
    public void ApplyRewardMultiplier(float multiplier)
    {
        if (multiplier <= 0f) return;
        xpReward = Mathf.Max(1, Mathf.RoundToInt(xpReward * multiplier));
        minGoldDrop = Mathf.Max(0, Mathf.RoundToInt(minGoldDrop * multiplier));
        maxGoldDrop = Mathf.Max(minGoldDrop, Mathf.RoundToInt(maxGoldDrop * multiplier));
    }

    // Elite Superior Tree Anomalies increase experience without silently
    // multiplying their gold or rune rewards.
    public void ApplyExperienceMultiplier(float multiplier)
    {
        if (multiplier <= 0f) return;
        xpReward = Mathf.Max(1, Mathf.RoundToInt(xpReward * multiplier));
    }

    public event System.Action OnDeath;
    // (current, max) - fired on every hit so a health bar can update without polling.
    public event System.Action<float, float> OnHealthChanged;

    // Global hook so systems like QuestManager can react to any kill without each enemy needing
    // to be individually registered (enemies can be pre-placed in the scene or spawned later).
    public static event System.Action<EnemyStats> OnAnyDeath;

    void Awake()
    {
        currentHealth = maxHealth;
        ai = GetComponent<EnemyAI>();
        kingBoss = GetComponent<KingGoblinBossAI>();
        crabBoss = GetComponent<CrabDemonBossAI>();
        orcWarrior = GetComponent<OrcWarriorAI>();
        animalAi = GetComponent<AnimalAI>();
        if (GetComponent<EnemySeparationController>() == null)
            gameObject.AddComponent<EnemySeparationController>();
        if (GetComponent<GrassStepInteraction>() == null)
            gameObject.AddComponent<GrassStepInteraction>();

        // Every enemy gets a health bar for free, regardless of type or which setup script
        // created it - matches the self-bootstrapping pattern used elsewhere in this project.
        if (kingBoss == null && crabBoss == null && GetComponent<KingGoblinHealthBar>() == null &&
            GetComponent<CrabDemonHealthBar>() == null && GetComponent<EnemyHealthBar>() == null)
            gameObject.AddComponent<EnemyHealthBar>();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount, Vector3 attackerPos = default)
    {
        if (IsDead) return;

        // Crab Demon's 50% block chance fully negates the hit before health is even touched -
        // TryBlock() also fires its own Block animation/reaction, so nothing else needs to run.
        if (crabBoss != null && crabBoss.TryBlock(attackerPos, amount))
            return;
        if (orcWarrior != null &&
            orcWarrior.TryBlock(attackerPos, amount))
            return;

        currentHealth -= amount;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
            Die();
        else
        {
            ai?.OnHurt(attackerPos);
            kingBoss?.OnHurt();
            crabBoss?.OnHurt(amount);
            orcWarrior?.OnHurt(attackerPos, amount);
            animalAi?.OnHurt(attackerPos);
        }
    }

    public void TakeTrueDamage(float amount, Vector3 attackerPos = default)
    {
        if (IsDead || amount <= 0f) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth <= 0f)
            Die();
    }

    void Die()
    {
        IsDead = true;
        DropGold();
        DropRune();
        OnDeath?.Invoke();
        OnAnyDeath?.Invoke(this);
        ai?.OnDeath();
        orcWarrior?.OnDeath();
    }

    void DropGold()
    {
        int min = Mathf.Min(minGoldDrop, maxGoldDrop);
        int max = Mathf.Max(minGoldDrop, maxGoldDrop);
        int amount = Random.Range(min, max + 1);
        GameObject player = GameManager.Instance != null ? GameManager.Instance.PlayerObject : null;
        if (player == null)
            player = GameObject.FindWithTag("Player");
        if (player != null)
            amount = Mathf.Max(0, Mathf.RoundToInt(amount *
                (1f + SkillTreeProgress.GetGoldBonus(player.transform))));
        if (amount <= 0)
            return;

        // Drops the physical coin pickup instead of silently crediting gold - the player picks
        // it up with E like diamonds/runes, matching every other world drop in this game.
        GameObject coinPrefab = Resources.Load<GameObject>("Items/GoldCoinPickup");
        if (coinPrefab == null)
        {
            // A missing Resources reference must never turn a world drop into invisible,
            // automatic currency. Create a simple but unmistakable 3D gold coin instead.
            GoldCoinPickup.CreateFallbackCoin(transform.position + Vector3.up * 0.18f, amount);
            return;
        }

        GameObject coin = Instantiate(coinPrefab, transform.position + Vector3.up * 0.6f, Quaternion.identity);
        GoldCoinPickup pickup = coin.GetComponent<GoldCoinPickup>();
        if (pickup != null)
            pickup.SetGoldValue(amount);
    }

    void DropRune()
    {
        if (Random.value > runeDropChance)
            return;

        ItemData rune = Resources.Load<ItemData>("Items/Rune");
        if (rune != null)
            InventoryManager.Instance?.AddItem(rune, 1);
    }
}

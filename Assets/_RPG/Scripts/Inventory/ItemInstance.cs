using System.Collections.Generic;
using UnityEngine;

public enum ItemRarity { Common, Magic, Rare, Epic, Legendary }

public static class ItemRarityUtil
{
    public static ItemRarity FromAffixCount(int count)
    {
        switch (count)
        {
            case 0: return ItemRarity.Common;
            case 1: return ItemRarity.Magic;
            case 2: return ItemRarity.Rare;
            case 3: return ItemRarity.Epic;
            default: return ItemRarity.Legendary;
        }
    }

    public static Color GetColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Magic: return new Color(0.4f, 0.62f, 1f);
            case ItemRarity.Rare: return new Color(1f, 0.85f, 0.2f);
            case ItemRarity.Epic: return new Color(0.75f, 0.35f, 1f);
            case ItemRarity.Legendary: return new Color(1f, 0.45f, 0.1f);
            default: return Color.white;
        }
    }

    public static string GetName(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Magic: return "Magico";
            case ItemRarity.Rare: return "Raro";
            case ItemRarity.Epic: return "Epico";
            case ItemRarity.Legendary: return "Legendario";
            default: return "Comun";
        }
    }
}

// A specific owned weapon/armor item: the shared ScriptableObject template plus per-owner
// progress (rune upgrade level + randomly rolled affixes). Every non-stackable equip-type
// InventorySlot gets one of these automatically (see InventoryManager.InventorySlot).
[System.Serializable]
public class ItemInstance
{
    public const int MaxUpgradeLevel = 100;
    const float StatPerLevel = 0.03f; // +3% of base stat per upgrade level (+300% at +100)

    public ItemData template;
    public int upgradeLevel;
    public int excellenceLevel;
    public float extraAttackPercent;
    public float extraDefensePercent;
    public float extraSpeedPercent;
    public bool darkEnergyEnchanted;
    public List<RolledAffix> affixes = new List<RolledAffix>();

    public ItemInstance(ItemData template)
    {
        this.template = template;
    }

    public float AttackBonus => template != null ? template.attackBonus * (1f + upgradeLevel * StatPerLevel + extraAttackPercent / 100f) : 0f;
    public float DefenseBonus => template != null ? template.defenseBonus * (1f + upgradeLevel * StatPerLevel + extraDefensePercent / 100f) : 0f;
    public float SpeedBonus => template != null ? template.speedBonus * (1f + upgradeLevel * StatPerLevel + extraSpeedPercent / 100f) : 0f;

    public ItemRarity Rarity => ItemRarityUtil.FromAffixCount(Affixes.Count);

    public string DisplayName => template != null ? template.itemName + (upgradeLevel > 0 ? " +" + upgradeLevel : "") + (excellenceLevel > 0 ? " exe +" + excellenceLevel : "") : "";

    public Color ExcellenceGlowColor
    {
        get
        {
            if (excellenceLevel >= 50) return Color.red;
            if (excellenceLevel >= 40) return Color.green;
            if (excellenceLevel >= 30) return Color.yellow;
            if (excellenceLevel >= 20) return new Color(1f, 0.48f, 0.05f);
            if (excellenceLevel >= 10) return new Color(0.1f, 0.35f, 1f);
            if (excellenceLevel >= 5) return Color.white;
            return Color.clear;
        }
    }

    public float ExcellenceGlowStrength
    {
        get
        {
            if (excellenceLevel < 5) return 0f;
            if (excellenceLevel >= 50) return 7f;
            if (excellenceLevel >= 40) return 5.8f;
            if (excellenceLevel >= 30) return 4.6f;
            if (excellenceLevel >= 20) return 3.5f;
            if (excellenceLevel >= 10) return 2.4f;
            return 1.4f;
        }
    }

    public List<RolledAffix> Affixes
    {
        get
        {
            if (affixes == null)
                affixes = new List<RolledAffix>();
            return affixes;
        }
    }

    public float GetAffixValue(AffixType type)
    {
        float sum = 0f;
        List<RolledAffix> safeAffixes = Affixes;
        for (int i = 0; i < safeAffixes.Count; i++)
            if (safeAffixes[i].type == type)
                sum += safeAffixes[i].value;
        return sum;
    }

    public string BuildTooltip()
    {
        if (template == null) return "";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(DisplayName);
        sb.Append("\n").Append(ItemRarityUtil.GetName(Rarity));

        if (template.attackBonus > 0f) sb.Append("\nAtaque: +").Append(AttackBonus.ToString("0.0"));
        if (template.defenseBonus > 0f) sb.Append("\nDefensa: +").Append(DefenseBonus.ToString("0.0"));
        if (template.speedBonus > 0f) sb.Append("\nVelocidad: +").Append(SpeedBonus.ToString("0.00"));
        if (excellenceLevel > 0) sb.Append("\nExcelencia: +").Append(excellenceLevel);
        if (extraAttackPercent > 0f) sb.Append("\nExe ataque: +").Append(extraAttackPercent.ToString("0")).Append("%");
        if (extraDefensePercent > 0f) sb.Append("\nExe defensa: +").Append(extraDefensePercent.ToString("0")).Append("%");
        if (extraSpeedPercent > 0f) sb.Append("\nExe velocidad: +").Append(extraSpeedPercent.ToString("0")).Append("%");
        if (darkEnergyEnchanted)
            sb.Append("\n<color=#a45cff>Energ\u00eda oscura:</color> incendia con 20 de da\u00f1o verdadero por segundo durante 5 segundos.");

        foreach (var affix in Affixes)
            sb.Append("\n").Append(affix.Describe());

        if (!string.IsNullOrEmpty(template.description))
            sb.Append("\n").Append(template.description);

        return sb.ToString();
    }
}

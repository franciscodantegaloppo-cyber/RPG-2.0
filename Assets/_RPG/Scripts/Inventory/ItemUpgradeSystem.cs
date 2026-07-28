using UnityEngine;

// The rune-consuming upgrade action (Espada +1, +2, ... +100). Called by InventoryUI when the
// player uses a rune on an equip-type item. Also responsible for rolling the chance to add a
// new random affix on that upgrade - see AffixLibrary for the pool of possible modifiers.
public static class ItemUpgradeSystem
{
    public const int MaxAffixes = 4;
    const float NewAffixChance = 0.35f;
    public static bool NoBreakModeEnabled { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntimeFlags()
    {
        NoBreakModeEnabled = false;
    }

    public static void SetNoBreakMode(bool enabled)
    {
        NoBreakModeEnabled = enabled;
    }

    public struct UpgradeResult
    {
        public bool success;
        public bool destroyed;
        public string message;
        public RolledAffix newAffix;
    }

    public static UpgradeResult Upgrade(ItemInstance instance)
    {
        if (instance == null || instance.template == null)
            return new UpgradeResult { success = false, message = "Objeto invalido." };

        if (instance.upgradeLevel >= ItemInstance.MaxUpgradeLevel)
            return new UpgradeResult { success = false, message = "Ya esta al nivel maximo (+100)." };

        instance.upgradeLevel++;

        RolledAffix newAffix = null;
        if (instance.Affixes.Count < MaxAffixes && Random.value <= NewAffixChance)
        {
            newAffix = AffixLibrary.RollRandom();
            instance.Affixes.Add(newAffix);
        }

        string message = instance.DisplayName + " mejorado a +" + instance.upgradeLevel + ".";
        if (newAffix != null)
            message += "\nNuevo modificador: " + newAffix.Describe();

        return new UpgradeResult { success = true, message = message, newAffix = newAffix };
    }

    public static UpgradeResult ApplyDiamondExcellence(ItemInstance instance)
    {
        if (instance == null || instance.template == null)
            return new UpgradeResult { success = false, message = "Objeto invalido." };
        if (!InventorySlot.IsEquipType(instance.template.itemType))
            return new UpgradeResult { success = false, message = "El diamante solo mejora equipo." };
        if (instance.excellenceLevel >= 99)
            return new UpgradeResult { success = false, message = "La excelencia ya esta al maximo." };

        int nextLevel = instance.excellenceLevel + 1;
        float destroyChance = NoBreakModeEnabled ? 0f : GetDiamondDestroyChancePercent(nextLevel);
        if (Random.value * 100f < destroyChance)
            return new UpgradeResult { success = false, destroyed = true, message = instance.DisplayName + " se destruyo al intentar exe +" + nextLevel + "." };

        instance.excellenceLevel = nextLevel;
        string stat = ApplyRandomPercent(instance, 50f);
        return new UpgradeResult
        {
            success = true,
            message = instance.DisplayName + " mejoro a exe +" + instance.excellenceLevel + ".\n" + stat + " +50%." +
                (NoBreakModeEnabled ? "\nProteccion /notbreak: exito asegurado." : "")
        };
    }

    public static UpgradeResult ApplyRunePercent(ItemInstance instance, bool craftingTable)
    {
        if (instance == null || instance.template == null)
            return new UpgradeResult { success = false, message = "Objeto invalido." };
        if (!InventorySlot.IsEquipType(instance.template.itemType))
            return new UpgradeResult { success = false, message = "La runa solo mejora equipo." };

        float percent = craftingTable ? 2f : 1f;
        string stat = ApplyRandomPercent(instance, percent);
        return new UpgradeResult
        {
            success = true,
            message = instance.DisplayName + " recibio " + stat + " +" + percent.ToString("0") + "%."
        };
    }

    public static float GetDiamondDestroyChancePercent(int targetExcellenceLevel)
    {
        if (targetExcellenceLevel <= 1) return 0.1f;
        if (targetExcellenceLevel == 2) return 1f;
        return Mathf.Clamp(targetExcellenceLevel - 1, 2f, 99f);
    }

    static string ApplyRandomPercent(ItemInstance instance, float percent)
    {
        int pick = Random.Range(0, 3);
        switch (pick)
        {
            case 0:
                instance.extraAttackPercent += percent;
                return "Ataque";
            case 1:
                instance.extraDefensePercent += percent;
                return "Defensa";
            default:
                instance.extraSpeedPercent += percent;
                return "Velocidad";
        }
    }
}

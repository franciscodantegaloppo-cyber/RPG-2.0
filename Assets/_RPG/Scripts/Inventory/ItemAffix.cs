using UnityEngine;

// New values must always be appended at the end (same serialization-drift rule as ItemType).
public enum AffixType
{
    PhysicalDamagePercent,
    MagicDamagePercent,
    DamageVsUndeadPercent,
    DamageVsBeastPercent,
    DamageVsBossPercent,
    CritChancePercent,
    CritDamagePercent,
    ArmorPenetrationFlat,
    Filo,
    AttackSpeedPercent,
    WeaponRangePercent,
}

public struct AffixDef
{
    public AffixType type;
    public string displayName;
    public float min;
    public float max;
    public bool isPercent;
    public string explanation;

    public AffixDef(AffixType type, string displayName, float min, float max, bool isPercent, string explanation)
    {
        this.type = type;
        this.displayName = displayName;
        this.min = min;
        this.max = max;
        this.isPercent = isPercent;
        this.explanation = explanation;
    }

    public string Format(float value) => displayName + " +" + value.ToString(isPercent ? "0.0" : "0") + (isPercent ? "%" : "");
}

[System.Serializable]
public class RolledAffix
{
    public AffixType type;
    public float value;

    public string Describe()
    {
        AffixDef def = AffixLibrary.Get(type);
        return def.Format(value);
    }
}

// Every possible upgrade modifier lives here. To add a new one: append an AffixDef to the
// array below (new AffixType value + entry) - nothing else in the upgrade/roll/tooltip/help
// pipeline needs to change, it all reads from this table.
public static class AffixLibrary
{
    public static readonly AffixDef[] All =
    {
        new AffixDef(AffixType.PhysicalDamagePercent, "Dano fisico", 1f, 100f, true,
            "Aumenta el dano fisico infligido por el arma."),
        new AffixDef(AffixType.MagicDamagePercent, "Dano magico", 1f, 100f, true,
            "Aumenta el dano magico infligido por el arma."),
        new AffixDef(AffixType.DamageVsUndeadPercent, "Dano contra no muertos", 5f, 30f, true,
            "Dano extra contra enemigos no muertos (esqueletos, etc)."),
        new AffixDef(AffixType.DamageVsBeastPercent, "Dano contra bestias", 5f, 30f, true,
            "Dano extra contra animales/bestias."),
        new AffixDef(AffixType.DamageVsBossPercent, "Dano contra jefes", 2f, 15f, true,
            "Dano extra contra jefes."),
        new AffixDef(AffixType.CritChancePercent, "Probabilidad de golpe critico", 0.2f, 3f, true,
            "Probabilidad de infligir el doble de dano en un golpe."),
        new AffixDef(AffixType.CritDamagePercent, "Dano critico", 5f, 50f, true,
            "Aumenta el dano extra que causan los golpes criticos."),
        new AffixDef(AffixType.ArmorPenetrationFlat, "Penetracion de armadura", 1f, 20f, false,
            "Ignora una cantidad fija de la armadura del objetivo."),
        new AffixDef(AffixType.Filo, "Filo", 1f, 10f, false,
            "Aumenta la penetracion de armadura del arma."),
        new AffixDef(AffixType.AttackSpeedPercent, "Velocidad de ataque", 1f, 15f, true,
            "Reduce el tiempo entre ataques."),
        new AffixDef(AffixType.WeaponRangePercent, "Alcance del arma", 2f, 20f, true,
            "Aumenta el alcance de los golpes."),
    };

    public static AffixDef Get(AffixType type)
    {
        foreach (var def in All)
            if (def.type == type)
                return def;
        return All[0];
    }

    public static RolledAffix RollRandom()
    {
        AffixDef def = All[Random.Range(0, All.Length)];
        float value = Random.Range(def.min, def.max);
        return new RolledAffix { type = def.type, value = value };
    }
}

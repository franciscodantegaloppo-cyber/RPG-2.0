using UnityEngine;

// New values must always be appended at the end - ScriptableObject .asset files serialize
// this as a raw int, so inserting/reordering silently corrupts every existing item asset.
public enum ItemType { Weapon, Helmet, Chest, Gloves, Legs, Boots, Shield, Consumable, Rune, Diamond }

[CreateAssetMenu(fileName = "NewItem", menuName = "RPG/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    public string itemID;
    public string itemName;
    public ItemType itemType;
    public Sprite icon;
    [TextArea] public string description;

    [Header("Stats")]
    public float attackBonus;
    public float defenseBonus;
    public float speedBonus;
    public float healAmount;

    [Header("Consumable Buff (stamina potions, etc.)")]
    [Tooltip("Percent less stamina consumed per action (attack/dodge/sprint) while the buff is active. 0 = no effect.")]
    public float staminaCostReductionPercent;
    [Tooltip("Percent faster stamina regeneration while the buff is active. 0 = no effect.")]
    public float staminaRegenBoostPercent;
    [Tooltip("How long the buff lasts, in seconds. Only relevant if either percent above is non-zero.")]
    public float buffDuration = 30f;

    [Header("Stack")]
    public bool stackable;
    public int maxStack = 1;

    [Header("Weapon Reference")]
    public WeaponData weaponData;

    [Header("Visible Equipment")]
    public GameObject equipmentPrefab;
    public HumanBodyBones equipmentBone = HumanBodyBones.Hips;
    public Vector3 equipmentPositionOffset;
    public Vector3 equipmentRotationOffset;
    public Vector3 equipmentScale = Vector3.one;
    [Tooltip("Shift the instantiated visual so its render bounds are centered on the bone, instead of trusting the prefab's authored pivot. Useful for parts (like gloves) whose pivot isn't at the attach point.")]
    public bool centerVisualOnBone;
    [Tooltip("If not white, tints the equipped visual's materials with this color (used for the merchant's starter leather set).")]
    public Color tintColor = Color.white;
}

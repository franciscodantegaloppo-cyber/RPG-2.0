using UnityEngine;

public enum WeaponType { Unarmed = 0, Sword1H = 1, Sword2H = 2, Axe = 3, Bow = 4 }

[CreateAssetMenu(fileName = "NewWeapon", menuName = "RPG/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string weaponID;
    public string weaponName;
    public WeaponType weaponType = WeaponType.Sword2H;

    [Header("Prefab")]
    public GameObject weaponPrefab;

    [Header("Equipped Visual")]
    public bool useCustomEquippedVisual;
    public Vector3 customHandPositionOffset = new Vector3(0.11f, 0.06f, 0.09f);
    public Vector3 customHandRotationOffset = new Vector3(289.52f, 32.80f, 107.87f);
    [Range(0.02f, 0.35f)] public float customGripAnchorPercent = 0.16f;
    public float customMaxEquippedWeaponSize = 1.45f;
    public bool hasIntrinsicGlow;
    public Color intrinsicGlowColor = Color.cyan;
    public float intrinsicGlowStrength;

    // WeaponSocket finds the blade's long axis from the two farthest-apart mesh vertices, but has
    // no way to know which of those two ends is the handle - for most swords that guess lands on
    // the handle, but it guessed backwards for this mesh (grip ended up anchored at the blade
    // tip). Flip this instead of touching code when a new weapon comes out mirrored the same way.
    // Applies regardless of useCustomEquippedVisual - it's about mesh geometry, not hand offsets.
    public bool invertGripAxis;

    // Opt-in: anchor the grip position at the two farthest-apart mesh vertices instead of the
    // axis-aligned bounding box. Needed for meshes authored diagonally through their own local
    // axes (King Goblin sword), where the bounding-box anchor lands mid-blade instead of at the
    // handle. Defaults off because the bounds-based anchor is what StarterSword (and everything
    // else already tuned) was built against - turning this on for an already-working weapon
    // shifts its grip point to a different spot along the blade.
    public bool useVertexGripAnchor;

    [Header("Stats")]
    public float baseDamage = 15f;
    public float attackRange = 1.5f;
    public float attackCooldown = 0.6f;
    [Min(0.1f)] public float attackSpeedMultiplier = 1f;
    [Min(0.1f)] public float staminaMultiplier = 1f;

    [Header("Animation")]
    public AnimatorOverrideController animatorOverride;

    [Header("Audio")]
    public AudioClip swingSound;
    public AudioClip hitSound;
    public AudioClip drawSound;
}

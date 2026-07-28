using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }

    Dictionary<ItemType, ItemInstance> equipped = new Dictionary<ItemType, ItemInstance>();
    Dictionary<ItemType, GameObject> equippedVisuals = new Dictionary<ItemType, GameObject>();

    WeaponSocket weaponSocket;
    PlayerStats playerStats;
    PlayerAnimatorBridge animBridge;
    GanzPlayerVisual ganzVisual;
    WeaponDrawSystem weaponDrawSystem;

    public event System.Action OnEquipmentChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        CacheReferences();
    }

    void Start()
    {
        CacheReferences();
    }

    void CacheReferences()
    {
        weaponSocket = GetComponent<WeaponSocket>();
        playerStats = GetComponent<PlayerStats>();
        animBridge = GetComponent<PlayerAnimatorBridge>();
        weaponDrawSystem = GetComponent<WeaponDrawSystem>();
        ganzVisual = GetComponent<GanzPlayerVisual>();
        if (ganzVisual == null)
            ganzVisual = gameObject.AddComponent<GanzPlayerVisual>();
        ganzVisual.EnsureInitialized();
    }

    // Convenience for callers that hand over a raw template (shop purchase, starter kit) with
    // no upgrade history yet - wraps it in a fresh level-0 instance.
    public void Equip(ItemData item)
    {
        if (item == null) return;
        Equip(new ItemInstance(item));
    }

    public void Equip(ItemInstance instance)
    {
        if (instance == null || instance.template == null) return;
        ItemData item = instance.template;
        CacheReferences();

        // Weapons and spells are exclusive combat modes. Equipping a weapon from any source
        // (inventory, quickbar, shop or reward) must also clear the active hand spell.
        if (item.itemType == ItemType.Weapon)
            GetComponent<PlayerSpellCaster>()?.UnequipSpell();

        // Unequip current in slot
        if (equipped.TryGetValue(item.itemType, out var old))
            Unequip(old);

        equipped[item.itemType] = instance;
        ApplyStats(instance, 1);

        if (item.itemType == ItemType.Weapon && item.weaponData != null)
        {
            weaponSocket?.AttachWeapon(item.weaponData.weaponPrefab, item.weaponData);
            weaponSocket?.ApplyItemInstanceVisual(instance);
            animBridge?.SetWeaponType((int)item.weaponData.weaponType);

            // Apply animator override if present
            if (item.weaponData.animatorOverride != null)
            {
                var anim = GetComponentInChildren<Animator>();
                if (anim != null)
                    anim.runtimeAnimatorController = item.weaponData.animatorOverride;
            }
        }
        else
        {
            if (ganzVisual == null || !ganzVisual.ApplyItem(item))
                AttachEquipmentVisual(item);
        }

        OnEquipmentChanged?.Invoke();
        SyncDarkEnergyAura();
        SyncWeaponResourceBonus();
    }

    // For automatic equipment sources (starter kits, world pickups, rewards): preserve the
    // exact previously equipped instance in the bag before replacing it. Manual inventory UI
    // equips already perform their own slot swap and should continue using Equip(instance).
    public bool EquipAndStorePrevious(ItemData item)
    {
        return item != null && EquipAndStorePrevious(new ItemInstance(item));
    }

    public bool EquipAndStorePrevious(ItemInstance instance)
    {
        if (instance == null || instance.template == null)
            return false;

        ItemInstance previous = GetEquippedInstance(instance.template.itemType);
        if (previous == instance)
            return true;

        if (previous != null)
        {
            InventoryManager inventory = InventoryManager.Instance;
            // Never replace/delete the equipped object when there is nowhere safe to put it.
            if (inventory == null || !inventory.AddItemInstance(previous))
                return false;
        }

        Equip(instance);
        return true;
    }

    public void Unequip(ItemData item)
    {
        if (item == null || !equipped.TryGetValue(item.itemType, out var instance) || instance.template != item)
            return;
        Unequip(instance);
    }

    public void Unequip(ItemInstance instance)
    {
        if (instance == null || instance.template == null) return;
        ItemData item = instance.template;
        if (!equipped.TryGetValue(item.itemType, out ItemInstance current) || current != instance) return;

        ApplyStats(instance, -1);
        equipped.Remove(item.itemType);

        if (item.itemType == ItemType.Weapon)
        {
            weaponSocket?.DetachWeapon();
            // Plays the actual sheath transition (not just SetWeaponType(0)) and resyncs
            // WeaponDrawSystem's own drawn/sheathed flag - see WeaponDrawSystem.ForceSheath().
            weaponDrawSystem?.ForceSheath();
        }
        else
        {
            ganzVisual?.Unequip(item.itemType);
            DetachEquipmentVisual(item.itemType);
        }

        OnEquipmentChanged?.Invoke();
        SyncDarkEnergyAura();
        SyncWeaponResourceBonus();
    }

    void AttachEquipmentVisual(ItemData item)
    {
        GameObject equipmentPrefab = item.equipmentPrefab != null ? item.equipmentPrefab : LoadFallbackEquipmentPrefab(item.itemType);
        if (equipmentPrefab == null)
            return;

        DetachEquipmentVisual(item.itemType);

        Animator animator = GetComponentInChildren<Animator>();
        Transform parent = null;
        HumanBodyBones bone = item.equipmentPrefab != null ? item.equipmentBone : GetFallbackBone(item.itemType);
        if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            parent = animator.GetBoneTransform(bone);
        if (parent == null)
            parent = transform;

        GameObject visual = new GameObject("Equipped_" + item.itemType + "_" + item.itemName);
        EquippedVisualGroup group = visual.AddComponent<EquippedVisualGroup>();
        GameObject firstPiece = AddVisualPiece(item, equipmentPrefab, parent, group);

        if (item.itemType == ItemType.Gloves)
            AddMirroredVisualPiece(item, animator, HumanBodyBones.RightLowerArm, parent, group);
        else if (item.itemType == ItemType.Boots)
            AddMirroredVisualPiece(item, animator, HumanBodyBones.RightFoot, parent, group);

        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            Destroy(collider);

        bool centerVisual = item.centerVisualOnBone || item.equipmentPrefab == null;
        if (firstPiece != null && centerVisual)
            CenterOnParent(firstPiece, parent, GetProximalBone(animator, bone), item.itemType);

        equippedVisuals[item.itemType] = visual;
    }

    void AddMirroredVisualPiece(ItemData item, Animator animator, HumanBodyBones bone, Transform originalParent, EquippedVisualGroup group)
    {
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            return;

        Transform parent = animator.GetBoneTransform(bone);
        if (parent == null || parent == originalParent)
            return;

        GameObject prefab = item.equipmentPrefab != null ? item.equipmentPrefab : LoadFallbackEquipmentPrefab(item.itemType);
        GameObject piece = AddVisualPiece(item, prefab, parent, group);
        if (piece != null && item.centerVisualOnBone)
            CenterOnParent(piece, parent, GetProximalBone(animator, bone), item.itemType);
    }

    GameObject AddVisualPiece(ItemData item, GameObject prefab, Transform parent, EquippedVisualGroup group)
    {
        if (prefab == null)
            return null;

        GameObject piece = Instantiate(prefab, parent);
        piece.name = item.itemType + "_" + item.itemName + "_Visual";
        piece.transform.localPosition = item.equipmentPositionOffset;
        piece.transform.localRotation = Quaternion.Euler(item.equipmentRotationOffset);
        piece.transform.localScale = GetVisualScale(item);

        foreach (Collider collider in piece.GetComponentsInChildren<Collider>(true))
            Destroy(collider);

        if (item.tintColor != Color.white)
            ApplyTint(piece, item.tintColor);

        group.Add(piece);
        return piece;
    }

    static GameObject LoadFallbackEquipmentPrefab(ItemType type)
    {
#if UNITY_EDITOR
        string path = type switch
        {
            ItemType.Helmet => "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Head Armor Type 1 Color 1 Part.prefab",
            ItemType.Chest => "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Chest Armor Type 1 Color 1 Part.prefab",
            ItemType.Gloves => "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Arm Armor Type 1 Color 1 Part.prefab",
            ItemType.Legs => "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Legs Armor Type 1 Color 1 Part.prefab",
            ItemType.Boots => "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Feet Armor Type 1 Color 1 Part.prefab",
            _ => ""
        };

        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }

    static HumanBodyBones GetFallbackBone(ItemType type)
    {
        return type switch
        {
            ItemType.Helmet => HumanBodyBones.Head,
            ItemType.Chest => HumanBodyBones.Chest,
            ItemType.Gloves => HumanBodyBones.LeftLowerArm,
            ItemType.Legs => HumanBodyBones.Hips,
            ItemType.Boots => HumanBodyBones.LeftFoot,
            _ => HumanBodyBones.Hips
        };
    }

    // The GanzSe pack has no dedicated hand/glove part, so gloves reuse an "Arm Armor"
    // piece that spans forearm-to-hand. Returns the bone one joint up the chain (e.g. the
    // forearm for a hand) so CenterOnParent can bias the mesh toward the hand end instead
    // of straddling the wrist.
    static Transform GetProximalBone(Animator animator, HumanBodyBones bone)
    {
        if (animator == null) return null;
        HumanBodyBones proximal = bone switch
        {
            HumanBodyBones.LeftHand => HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightHand => HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftFoot => HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightFoot => HumanBodyBones.RightLowerLeg,
            _ => HumanBodyBones.LastBone
        };
        return proximal == HumanBodyBones.LastBone ? null : animator.GetBoneTransform(proximal);
    }

    // Re-centers the visual so its rendered bounds sit on the parent bone, regardless of
    // where the source prefab's own pivot happens to be. Applied as an extra correction
    // on top of the manual position offset (so the offset still nudges it from there).
    static Vector3 GetVisualScale(ItemData item)
    {
        Vector3 authoredScale = item.equipmentScale == Vector3.zero ? Vector3.one : item.equipmentScale;
        if (!item.centerVisualOnBone)
            return authoredScale;

        float slotMultiplier = item.itemType switch
        {
            ItemType.Helmet => 0.82f,
            ItemType.Chest => 1.05f,
            ItemType.Gloves => 0.42f,
            ItemType.Legs => 1.12f,
            ItemType.Boots => 0.55f,
            _ => 1f
        };
        return authoredScale * slotMultiplier;
    }

    static void CenterOnParent(GameObject visual, Transform parent, Transform proximal, ItemType type)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 worldDelta = parent.position - bounds.center;

        // Elongated pieces (e.g. that forearm-length "Arm Armor" standing in for a glove)
        // would otherwise have their midpoint centered on the hand, leaving half the mesh
        // hovering up near the wrist. Push further along the proximal->parent direction so
        // the piece sits over the hand itself instead of straddling the joint.
        if (proximal != null)
        {
            Vector3 distalDir = (parent.position - proximal.position).normalized;
            worldDelta += distalDir * (bounds.extents.magnitude * 0.32f);
        }

        worldDelta += type switch
        {
            ItemType.Chest => Vector3.up * bounds.extents.y * 0.12f,
            ItemType.Legs => Vector3.down * bounds.extents.y * 0.34f,
            ItemType.Boots => Vector3.down * bounds.extents.y * 0.12f,
            _ => Vector3.zero
        };

        visual.transform.position += worldDelta;
    }

    static void ApplyTint(GameObject visual, Color tint)
    {
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = renderer.materials; // instances, safe to modify
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].HasProperty("_BaseColor"))
                    mats[i].SetColor("_BaseColor", tint);
                else if (mats[i].HasProperty("_Color"))
                    mats[i].SetColor("_Color", tint);
            }
            renderer.materials = mats;
        }
    }

    void DetachEquipmentVisual(ItemType type)
    {
        if (!equippedVisuals.TryGetValue(type, out GameObject visual))
            return;

        if (visual != null)
            Destroy(visual);
        equippedVisuals.Remove(type);
    }

    void ApplyStats(ItemInstance instance, int sign)
    {
        if (playerStats == null) return;
        playerStats.baseAttack += instance.AttackBonus * sign;
        playerStats.baseDefense += instance.DefenseBonus * sign;
    }

    // ItemData.speedBonus is expressed as a percentage contribution. Keeping it centralized
    // here lets PlayerStats apply the same diminishing curve and the absolute x3 movement cap
    // to skills, tree nodes and every equipped item together.
    public float GetTotalMovementSpeedBonusPercent()
    {
        float total = 0f;
        foreach (ItemInstance instance in equipped.Values)
            if (instance != null)
                total += instance.SpeedBonus;
        return total;
    }

    public ItemData GetEquipped(ItemType type)
    {
        equipped.TryGetValue(type, out var instance);
        return instance?.template;
    }

    public ItemInstance GetEquippedInstance(ItemType type)
    {
        equipped.TryGetValue(type, out var instance);
        return instance;
    }

    public bool IsEquipped(ItemData item) =>
        item != null && equipped.TryGetValue(item.itemType, out var eq) && eq.template == item;

    public bool IsEquipped(ItemInstance instance) =>
        instance != null && instance.template != null &&
        equipped.TryGetValue(instance.template.itemType, out var eq) && eq == instance;

    public void RefreshEquippedWeaponVisual()
    {
        ItemInstance weapon = GetEquippedInstance(ItemType.Weapon);
        if (weapon != null)
            weaponSocket?.ApplyItemInstanceVisual(weapon);
        SyncDarkEnergyAura();
        SyncWeaponResourceBonus();
        OnEquipmentChanged?.Invoke();
    }

    void SyncWeaponResourceBonus()
    {
        if (playerStats == null) playerStats = GetComponent<PlayerStats>();
        WeaponData weapon = GetEquippedInstance(ItemType.Weapon)?.template?.weaponData;
        playerStats?.SetEquipmentStaminaMultiplier(weapon != null ? weapon.staminaMultiplier : 1f);
    }

    void SyncDarkEnergyAura()
    {
        PlayerDarkEnergyAura aura = GetComponent<PlayerDarkEnergyAura>();
        ItemInstance weapon = GetEquippedInstance(ItemType.Weapon);
        bool active = weapon != null && weapon.darkEnergyEnchanted;
        if (aura == null && active)
            aura = gameObject.AddComponent<PlayerDarkEnergyAura>();
        aura?.SetActive(active);
    }
}

public class EquippedVisualGroup : MonoBehaviour
{
    readonly List<GameObject> pieces = new List<GameObject>();

    public void Add(GameObject piece)
    {
        if (piece != null)
            pieces.Add(piece);
    }

    void OnDestroy()
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] != null)
                Destroy(pieces[i]);
        }
        pieces.Clear();
    }
}

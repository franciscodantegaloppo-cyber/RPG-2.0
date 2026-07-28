using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NPCMerchant : MonoBehaviour, IInteractable
{
    [Header("Starter Kit")]
    [SerializeField] ItemData starterSword;
    [SerializeField] ItemData starterHelmet;
    [SerializeField] ItemData starterChestplate;
    [SerializeField] ItemData starterBoots;
    [SerializeField] ItemData healthPotion;
    [SerializeField] int potionCount = 10;
    [SerializeField] ItemData staminaPotion;
    [SerializeField] int staminaPotionCount = 5;

    [Header("Starter Skin")]
    [SerializeField] Material starterSkinMaterial;

    [Header("Dialogue")]
    [SerializeField] string merchantName = "Mercader";
    [SerializeField] string greetingText = "¡Bienvenido, aventurero! Tengo equipamiento inicial para ti.\n\n¿Quieres aceptar la armadura de inicio?";
    [SerializeField] string afterKitText = "¡Que la suerte te acompañe en tus aventuras!";

    bool kitGiven;
    NPCWander wander;

    void Awake()
    {
        wander = GetComponent<NPCWander>();
        if (wander == null)
            wander = gameObject.AddComponent<NPCWander>();
        wander.Configure(speed: 0.75f, radius: 5f, step: 2.1f, minWait: 2.5f, maxWait: 5.5f);
        QuestNpcAttentionIcon icon = GetComponent<QuestNpcAttentionIcon>();
        if (icon == null) icon = gameObject.AddComponent<QuestNpcAttentionIcon>();
        icon.Configure(QuestNpcAttentionRole.Merchant);
    }

    public string GetInteractionText() => "[E] Hablar con el Mercader";
    public bool CanInteract(PlayerInteraction player) => true;

    public void Interact(PlayerInteraction player)
    {
        // Pausa el wander y lo reanuda al cerrar diálogo
        wander?.PauseForInteraction();
        // Mira al jugador
        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);

        // Was relying on some other NPC (Tonio) having been talked to first to lazily create
        // this shared panel - if the merchant was the first interaction of the session, this
        // silently fell back to a dialogue with no accept button and auto-gave the kit instead.
        var panel = MerchantDialoguePanel.EnsureRuntime();
        if (panel == null)
        {
            FindAnyObjectByType<HUDController>()?.ShowDialogue(kitGiven ? afterKitText : greetingText);
            if (!kitGiven) { GiveStarterKit(); kitGiven = true; }
            QuestManager.Instance?.CompleteMerchantIntroduction();
            wander?.ResumeWander();
            return;
        }

        if (!kitGiven)
        {
            panel.Show(merchantName, greetingText, "Aceptar armadura", () =>
            {
                GiveStarterKit();
                kitGiven = true;
                QuestManager.Instance?.CompleteMerchantIntroduction();
                wander?.ResumeWander();
            });
        }
        else
        {
            QuestManager.Instance?.CompleteMerchantIntroduction();
            panel.Show(merchantName, afterKitText, "Hasta luego", () =>
            {
                wander?.ResumeWander();
            });
        }
    }

    void GiveStarterKit()
    {
        var inv = InventoryManager.Instance;
        var eq  = EquipmentManager.Instance;
        if (inv == null) return;

        void AddAndEquip(ItemData item, int qty = 1)
        {
            if (item == null) return;
            if (eq != null && InventorySlot.IsEquipType(item.itemType))
            {
                // The equipped starter piece is the reward itself; it must not also create a
                // duplicate in the bag. Equip safely so a King Goblin sword (or any previous
                // gear in this slot) is moved into the inventory before the starter replaces it.
                if (!eq.EquipAndStorePrevious(new ItemInstance(item)))
                {
                    // Full inventory: keep the currently equipped valuable item untouched and
                    // try to deliver the starter piece to the bag instead.
                    if (!inv.AddItem(item, qty))
                        Debug.LogWarning("NPCMerchant: no hay espacio para entregar " + item.itemName + ".");
                }
                return;
            }

            inv.AddItem(item, qty);
        }

        AddAndEquip(starterSword != null ? starterSword : CreateStarterSword());
        AddAndEquip(starterHelmet != null ? starterHelmet : CreateStarterArmor(
            "starter_head_ganz", "Casco de Cuero Inicial", ItemType.Helmet, HumanBodyBones.Head,
            "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Head Armor Type 1 Color 1 Part.prefab",
            new Vector3(0f, 0.03f, 0f), Vector3.zero, Vector3.one));
        AddAndEquip(starterChestplate != null ? starterChestplate : CreateStarterArmor(
            "starter_chest_ganz", "Peto de Cuero Inicial", ItemType.Chest, HumanBodyBones.Chest,
            "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Chest Armor Type 1 Color 1 Part.prefab",
            Vector3.zero, Vector3.zero, Vector3.one));
        AddAndEquip(CreateStarterArmor(
            "starter_gloves_ganz", "Guantes de Cuero Iniciales", ItemType.Gloves, HumanBodyBones.LeftHand,
            "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Arm Armor Type 1 Color 1 Part.prefab",
            // GanzSe's rig and the RPG-Character pack's rig don't share a bone-orientation
            // convention, so this cross-pack piece can't be trusted to align perfectly on the
            // hand bone even after centering. Scaled down so any residual offset reads as a
            // small glove near the hand instead of an obviously misplaced forearm-sized chunk.
            Vector3.zero, Vector3.zero, Vector3.one * 0.55f, centerOnBone: true));
        AddAndEquip(starterBoots != null ? starterBoots : CreateStarterArmor(
            "starter_boots_ganz", "Botas de Cuero Iniciales", ItemType.Boots, HumanBodyBones.LeftFoot,
            "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Feet Armor Type 1 Color 1 Part.prefab",
            Vector3.zero, Vector3.zero, Vector3.one));
        AddAndEquip(CreateStarterArmor(
            // A full "Legs Armor" piece is a single rigid mesh with no bone weights (GanzSe's
            // pack has no skinned alternative). Parented to Hips it would follow the pelvis but
            // never bend at the knees or swing with each stride, so it'd visibly float apart
            // from the character's own legs the instant they moved - not fixable by repositioning.
            // A Belt-sized piece has the same rigid-single-bone limitation but only needs to sit
            // at the waist, which barely moves relative to Hips, so it reads as properly worn.
            "starter_legs_ganz", "Pantalon de Cuero Inicial", ItemType.Legs, HumanBodyBones.Hips,
            "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Belt Armor Type 1 Color 1 Part.prefab",
            Vector3.zero, Vector3.zero, Vector3.one, centerOnBone: true));

        if (healthPotion != null)
            inv.AddItem(healthPotion, potionCount);
        if (staminaPotion != null)
            inv.AddItem(staminaPotion, staminaPotionCount);

        Debug.Log("NPCMerchant: Starter kit entregado.");
    }

    ItemData CreateStarterSword()
    {
        var weapon = ScriptableObject.CreateInstance<WeaponData>();
        weapon.weaponID = "starter_ganz_sword";
        weapon.weaponName = "Espada Ganz Inicial";
        weapon.weaponType = WeaponType.Sword1H;
        weapon.weaponPrefab = LoadPrefab("Assets/URP GanzSe Free Modular Character Pack/Prefabs/ONE-HANDED SWORDS/FREE ONE HANDED SWORD 1 COLOR 1.prefab");
        weapon.baseDamage = 18f;
        weapon.attackRange = 1.55f;
        weapon.attackCooldown = 0.62f;

        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemID = weapon.weaponID;
        item.itemName = weapon.weaponName;
        item.itemType = ItemType.Weapon;
        item.description = "Espada inicial del pack Ganz.";
        item.attackBonus = 18f;
        item.weaponData = weapon;
        item.maxStack = 1;
        return item;
    }

    static readonly Color StarterLeatherTint = new Color(0.40f, 0.26f, 0.14f);

    ItemData CreateStarterArmor(string id, string itemName, ItemType type, HumanBodyBones bone, string prefabPath, Vector3 pos, Vector3 rot, Vector3 scale, bool centerOnBone = false)
    {
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemID = id;
        item.itemName = itemName;
        item.itemType = type;
        item.description = "Armadura de cuero inicial.";
        item.defenseBonus = type == ItemType.Chest ? 4f : 2f;
        item.equipmentPrefab = LoadPrefab(prefabPath);
        item.equipmentBone = bone;
        item.equipmentPositionOffset = pos;
        item.equipmentRotationOffset = rot;
        item.equipmentScale = scale;
        item.centerVisualOnBone = centerOnBone;
        item.tintColor = StarterLeatherTint;
        item.maxStack = 1;
        return item;
    }

    GameObject LoadPrefab(string path)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }

    void ApplyStarterSkin()
    {
        if (starterSkinMaterial == null) return;
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;

        foreach (var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = starterSkinMaterial;
            smr.sharedMaterials = mats;
        }
        foreach (var mr in player.GetComponentsInChildren<MeshRenderer>(true))
            mr.sharedMaterial = starterSkinMaterial;
    }
}

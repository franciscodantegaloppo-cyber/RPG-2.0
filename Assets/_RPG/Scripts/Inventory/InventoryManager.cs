using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int quantity;

    // Non-null for non-stackable equip-type items (weapon/armor) so every piece of gear the
    // player picks up can be individually rune-upgraded, regardless of how it was acquired
    // (starter kit, shop purchase, loot). Stackables (potions, runes) stay instance-less.
    public ItemInstance instance;

    public InventorySlot(ItemData item, int qty)
    {
        this.item = item;
        quantity = qty;
        if (!item.stackable && IsEquipType(item.itemType))
            instance = new ItemInstance(item);
    }

    public InventorySlot(ItemInstance instance, int qty)
    {
        this.instance = instance;
        item = instance.template;
        quantity = qty;
    }

    public static bool IsEquipType(ItemType type) =>
        type == ItemType.Weapon || type == ItemType.Helmet || type == ItemType.Chest ||
        type == ItemType.Gloves || type == ItemType.Legs || type == ItemType.Boots || type == ItemType.Shield;
}

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [SerializeField] int maxSlots = 48;
    [SerializeField] int startingGold = 100;

    public List<InventorySlot> Slots = new List<InventorySlot>();
    public int Gold { get; private set; }
    public int MaxSlots => maxSlots;

    public event System.Action OnInventoryChanged;
    public event System.Action OnGoldChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        // The baked inventory art contains a complete 8 x 6 grid. Older scene
        // instances serialized the former value of 30, leaving 18 drawn cells
        // unusable even after the script default was raised.
        maxSlots = 48;
        Gold = startingGold;
        DontDestroyOnLoad(gameObject);
    }

    public void AddGold(int amount)
    {
        Gold += amount;
        OnGoldChanged?.Invoke();
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        OnGoldChanged?.Invoke();
        return true;
    }

    public bool AddItem(ItemData item, int qty = 1)
    {
        if (item == null) return false;

        if (item.stackable)
        {
            int remaining = qty;
            int maxStack = Mathf.Max(1, item.maxStack);

            foreach (InventorySlot existing in Slots)
            {
                if (existing == null || existing.item != item || existing.quantity >= maxStack)
                    continue;

                int add = Mathf.Min(remaining, maxStack - existing.quantity);
                existing.quantity += add;
                remaining -= add;
                if (remaining <= 0)
                {
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }

            while (remaining > 0)
            {
                int stack = Mathf.Min(remaining, maxStack);
                int stackEmptyIndex = Slots.FindIndex(s => s == null || s.item == null);
                if (stackEmptyIndex >= 0)
                    Slots[stackEmptyIndex] = new InventorySlot(item, stack);
                else
                {
                    if (Slots.Count >= maxSlots)
                    {
                        OnInventoryChanged?.Invoke();
                        return remaining != qty;
                    }
                    Slots.Add(new InventorySlot(item, stack));
                }
                remaining -= stack;
            }

            if (remaining != qty)
                OnInventoryChanged?.Invoke();
            return true;
        }

        int emptyIndex = Slots.FindIndex(s => s == null || s.item == null);
        if (emptyIndex >= 0)
        {
            Slots[emptyIndex] = new InventorySlot(item, qty);
            OnInventoryChanged?.Invoke();
            return true;
        }

        if (Slots.Count >= maxSlots) return false;
        Slots.Add(new InventorySlot(item, qty));
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(ItemData item, int qty = 1)
    {
        var slot = Slots.Find(s => s != null && s.item == item);
        if (slot == null) return false;

        slot.quantity -= qty;
        if (slot.quantity <= 0) ClearSlot(slot);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // Non-stackable gear can have several slots sharing the same ItemData template (two swords
    // of the same type at different upgrade levels), so equip/unequip needs to target one exact
    // slot rather than "any slot matching this template" like RemoveItem(ItemData) does.
    public bool RemoveSlot(InventorySlot slot)
    {
        if (slot == null || !ClearSlot(slot)) return false;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool AddItemInstance(ItemInstance instance)
    {
        if (instance == null) return false;
        int emptyIndex = Slots.FindIndex(s => s == null || s.item == null);
        if (emptyIndex >= 0)
        {
            Slots[emptyIndex] = new InventorySlot(instance, 1);
            OnInventoryChanged?.Invoke();
            return true;
        }

        if (Slots.Count >= maxSlots) return false;
        Slots.Add(new InventorySlot(instance, 1));
        OnInventoryChanged?.Invoke();
        return true;
    }

    // Used by the stack-split flow: the split-off quantity should land exactly where the player
    // clicks (an empty bag slot, or merge into a matching stack there), not wherever AddItem's
    // generic auto-placement happens to pick. Only succeeds if the full quantity fits in that one
    // slot - the caller falls back to AddItem (scattering it wherever it fits) on failure, so nothing
    // is ever lost, it just might not land exactly where the player aimed if that slot can't hold it all.
    public bool PlaceAtIndex(ItemData item, int qty, int index)
    {
        if (item == null || qty <= 0 || index < 0 || index >= maxSlots)
            return false;

        while (Slots.Count <= index)
            Slots.Add(null);

        InventorySlot existing = Slots[index];
        if (existing == null || existing.item == null)
        {
            Slots[index] = new InventorySlot(item, qty);
            OnInventoryChanged?.Invoke();
            return true;
        }

        if (existing.item == item && item.stackable)
        {
            int maxStack = Mathf.Max(1, item.maxStack);
            if (existing.quantity + qty > maxStack)
                return false;
            existing.quantity += qty;
            OnInventoryChanged?.Invoke();
            return true;
        }

        return false;
    }

    // Places an already-existing owned slot (for example the finished output retained by the
    // crafting table) into one exact empty bag cell without rebuilding its ItemInstance. This
    // preserves every upgrade, excellence level and rolled affix on the object.
    public bool PlaceExternalSlotAtIndex(InventorySlot slot, int index)
    {
        if (slot == null || slot.item == null || index < 0 || index >= maxSlots || Slots.Contains(slot))
            return false;

        while (Slots.Count <= index)
            Slots.Add(null);

        if (Slots[index] != null && Slots[index].item != null)
            return false;

        Slots[index] = slot;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool MoveSlotToIndex(InventorySlot slot, int targetIndex)
    {
        if (slot == null)
            return false;

        int sourceIndex = Slots.IndexOf(slot);
        if (sourceIndex < 0)
            return false;

        targetIndex = Mathf.Clamp(targetIndex, 0, maxSlots - 1);
        if (sourceIndex == targetIndex)
            return false;

        while (Slots.Count <= targetIndex)
            Slots.Add(null);

        InventorySlot target = Slots[targetIndex];
        Slots[targetIndex] = slot;
        Slots[sourceIndex] = target;
        TrimTrailingEmptySlots();
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool HasItem(ItemData item) => Slots.Exists(s => s != null && s.item == item);

    public void NotifyInventoryChanged() => OnInventoryChanged?.Invoke();

    public bool ConsumeRune(int qty = 1)
    {
        var slot = Slots.Find(s => s != null && s.item != null && s.item.itemType == ItemType.Rune && s.quantity >= qty);
        if (slot == null) return false;

        slot.quantity -= qty;
        if (slot.quantity <= 0) ClearSlot(slot);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public int RuneCount()
    {
        int total = 0;
        foreach (var slot in Slots)
            if (slot != null && slot.item != null && slot.item.itemType == ItemType.Rune)
                total += slot.quantity;
        return total;
    }

    public bool ConsumeDiamond(int qty = 1)
    {
        if (qty <= 0 || DiamondCount() < qty) return false;

        int remaining = qty;
        for (int i = Slots.Count - 1; i >= 0 && remaining > 0; i--)
        {
            InventorySlot slot = Slots[i];
            if (slot == null || slot.item == null || slot.item.itemType != ItemType.Diamond)
                continue;
            int used = Mathf.Min(slot.quantity, remaining);
            slot.quantity -= used;
            remaining -= used;
            if (slot.quantity <= 0) ClearSlot(slot);
        }
        OnInventoryChanged?.Invoke();
        return true;
    }

    public int DiamondCount()
    {
        int total = 0;
        foreach (var slot in Slots)
            if (slot != null && slot.item != null && slot.item.itemType == ItemType.Diamond)
                total += slot.quantity;
        return total;
    }

    public bool ConsumeSlotItem(InventorySlot slot, int qty = 1)
    {
        if (slot == null || !Slots.Contains(slot))
            return false;

        slot.quantity -= qty;
        if (slot.quantity <= 0)
            ClearSlot(slot);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void NotifyChanged() => OnInventoryChanged?.Invoke();

    bool ClearSlot(InventorySlot slot)
    {
        int index = Slots.IndexOf(slot);
        if (index < 0)
            return false;
        Slots[index] = null;
        TrimTrailingEmptySlots();
        return true;
    }

    void TrimTrailingEmptySlots()
    {
        for (int i = Slots.Count - 1; i >= 0; i--)
        {
            if (Slots[i] != null && Slots[i].item != null)
                break;
            Slots.RemoveAt(i);
        }
    }
}

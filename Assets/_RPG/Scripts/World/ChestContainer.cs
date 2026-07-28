using System.Collections.Generic;
using UnityEngine;

// A world chest with its own 8x4 grid of storage - matches the "Grid panel chest" art's own
// baked-in cell count exactly, since ChestUI now displays that art as the panel background with
// real slots overlaid on top of its drawn cells. Mirrors InventoryManager's slot model
// (stackables merge by ItemData, equip-type items keep their own ItemInstance) so items moving
// between chest and bag keep upgrade level / affixes intact.
public class ChestContainer : MonoBehaviour, IInteractable
{
    public const int Columns = 8;
    public const int Rows = 4;
    public const int MaxSlots = Columns * Rows;

    [SerializeField] string chestName = "Cofre";
    // Populated into Slots once on Awake, so a chest placed via an Editor setup script (e.g. the
    // wagon backpack "starts with a sword") has real starting loot instead of opening empty.
    [SerializeField] ItemData[] startingItems;

    public List<InventorySlot> Slots = new List<InventorySlot>();

    public event System.Action OnChanged;

    public string ChestName => chestName;

    void Awake()
    {
        if (startingItems == null) return;
        foreach (ItemData item in startingItems)
            TryAddItem(item);
    }

    public string GetInteractionText() => "[E] Abrir " + chestName.ToLowerInvariant();

    // While this chest's own panel is open, let ChestUI's Update() own the E-to-close handling
    // instead of PlayerInteraction re-triggering Interact() on the same target every frame.
    public bool CanInteract(PlayerInteraction player)
    {
        GoblinPassageQuestChest questGate = GetComponent<GoblinPassageQuestChest>();
        if (questGate != null && !questGate.IsAvailable) return false;
        FifthMissionSwordChest fifthMissionGate = GetComponent<FifthMissionSwordChest>();
        if (fifthMissionGate != null && !fifthMissionGate.IsAvailable) return false;
        return ChestUI.Instance == null || !ChestUI.Instance.IsOpen || ChestUI.Instance.CurrentChest != this;
    }

    public void Interact(PlayerInteraction player) => ChestUI.Instance?.Open(this);

    public bool TryAddItem(ItemData item, int qty = 1)
    {
        if (item == null) return false;

        if (item.stackable)
        {
            var existing = Slots.Find(s => s.item == item && s.quantity < item.maxStack);
            if (existing != null)
            {
                existing.quantity = Mathf.Min(existing.quantity + qty, item.maxStack);
                OnChanged?.Invoke();
                return true;
            }
        }

        if (Slots.Count >= MaxSlots) return false;

        Slots.Add(new InventorySlot(item, qty));
        OnChanged?.Invoke();
        return true;
    }

    public bool TryAddInstance(ItemInstance instance)
    {
        if (instance == null || Slots.Count >= MaxSlots) return false;
        Slots.Add(new InventorySlot(instance, 1));
        OnChanged?.Invoke();
        return true;
    }

    public bool RemoveSlot(InventorySlot slot)
    {
        if (slot == null || !Slots.Remove(slot)) return false;
        OnChanged?.Invoke();
        return true;
    }

    public bool MoveSlotToIndex(InventorySlot slot, int targetIndex)
    {
        if (slot == null)
            return false;

        int sourceIndex = Slots.IndexOf(slot);
        if (sourceIndex < 0)
            return false;

        targetIndex = Mathf.Clamp(targetIndex, 0, Mathf.Max(0, Slots.Count - 1));
        if (sourceIndex == targetIndex)
            return false;

        Slots.RemoveAt(sourceIndex);
        if (targetIndex > sourceIndex)
            targetIndex--;
        Slots.Insert(Mathf.Clamp(targetIndex, 0, Slots.Count), slot);
        OnChanged?.Invoke();
        return true;
    }

    public bool HasRoom => Slots.Count < MaxSlots;
}

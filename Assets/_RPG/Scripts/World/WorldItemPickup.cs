using UnityEngine;

public class WorldItemPickup : MonoBehaviour, IInteractable
{
    [SerializeField] ItemData item;
    [SerializeField] bool equipOnPickup;
    [SerializeField] string interactionText = "[E] Recoger";

    public string GetInteractionText() => item != null ? interactionText + " " + item.itemName : interactionText;

    public bool CanInteract(PlayerInteraction player) => item != null;

    public void Interact(PlayerInteraction player)
    {
        if (item == null)
            return;

        bool received;
        if (equipOnPickup && InventorySlot.IsEquipType(item.itemType))
            received = EquipmentManager.Instance != null &&
                EquipmentManager.Instance.EquipAndStorePrevious(new ItemInstance(item));
        else
            received = InventoryManager.Instance != null && InventoryManager.Instance.AddItem(item, 1);

        // If the inventory is full, leave the pickup in the world instead of deleting either
        // the pickup or the player's previously equipped object.
        if (received)
            Destroy(gameObject);
    }
}

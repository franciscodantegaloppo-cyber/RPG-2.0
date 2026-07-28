using UnityEngine;

public class CraftingTableInteractable : MonoBehaviour, IInteractable
{
    void Awake()
    {
        QuestNpcAttentionIcon icon = GetComponent<QuestNpcAttentionIcon>();
        if (icon == null) icon = gameObject.AddComponent<QuestNpcAttentionIcon>();
        icon.Configure(QuestNpcAttentionRole.CraftingTable);
    }

    public string GetInteractionText() => "[E] Usar mesa de crafting";
    public bool CanInteract(PlayerInteraction player) => CraftingTableUI.Instance == null || !CraftingTableUI.Instance.IsOpen;
    public void Interact(PlayerInteraction player) => CraftingTableUI.GetOrCreateInstance()?.Open();
}

public interface IInteractable
{
    string GetInteractionText();
    void Interact(PlayerInteraction player);
    bool CanInteract(PlayerInteraction player);
}

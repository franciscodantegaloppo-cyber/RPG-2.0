using UnityEngine;

public class DungeonExit : MonoBehaviour, IInteractable
{
    [SerializeField] AudioClip exitSound;

    public string GetInteractionText() => "[E] Salir del dungeon";
    public bool CanInteract(PlayerInteraction player) => true;

    public void Interact(PlayerInteraction player)
    {
        AudioManager.Instance?.PlaySFX(exitSound);
        SceneLoader.Instance?.LoadScene("SpawnVillage");
    }
}

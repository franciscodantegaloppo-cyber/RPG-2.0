using System.Collections;
using UnityEngine;

public class DungeonEntrance : MonoBehaviour, IInteractable
{
    [SerializeField] ParticleSystem crackParticles;
    [SerializeField] AudioClip enterSound;
    [SerializeField] int dungeonSceneIndex = 1;
    [SerializeField] string dungeonSceneName;

    bool entering;

    void Start()
    {
        if (crackParticles != null) crackParticles.Play();
    }

    public string GetInteractionText() => "[E] Entrar al dungeon";
    public bool CanInteract(PlayerInteraction player) => !entering;

    public void Interact(PlayerInteraction player)
    {
        if (entering) return;
        if (QuestManager.Instance == null || !QuestManager.Instance.HasDungeonKey)
        {
            QuestOverheadThought.Show(player.transform,
                "La entrada est\u00e1 sellada. Necesito la llave del calabozo.", 4f);
            return;
        }
        entering = true;
        StartCoroutine(EnterDungeon(player.transform));
    }

    IEnumerator EnterDungeon(Transform player)
    {
        AudioManager.Instance?.PlaySFX(enterSound);

        // Shrink animation
        float t = 0f;
        float duration = 0.8f;
        Vector3 originalScale = player.localScale;

        while (t < duration)
        {
            t += Time.deltaTime;
            player.localScale = Vector3.Lerp(originalScale, Vector3.zero, t / duration);
            yield return null;
        }

        string targetScene = !string.IsNullOrWhiteSpace(dungeonSceneName)
            ? dungeonSceneName
            : dungeonSceneIndex >= 2 ? "dungeon_1" : "Dungeon";
        SceneLoader.Instance?.LoadScene(targetScene);
    }
}

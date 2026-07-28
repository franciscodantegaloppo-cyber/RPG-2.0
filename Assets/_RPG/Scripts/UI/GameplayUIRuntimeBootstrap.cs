using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Creates gameplay-only UI after leaving the NewGame scene. RuntimeInitializeOnLoadMethod with
// AfterSceneLoad runs only for the first loaded scene, so components that deliberately skipped
// NewGame were never given another chance to appear in SpawnVillage.
public sealed class GameplayUIRuntimeBootstrap : MonoBehaviour
{
    static GameplayUIRuntimeBootstrap instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInitialScene()
    {
        EnsureForScene(SceneManager.GetActiveScene());
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForScene(scene);
    }

    public static void EnsureForScene(Scene scene)
    {
        if (scene.name == "NewGame")
            return;

        if (instance == null)
        {
            GameObject root = new GameObject("GameplayUIRuntimeBootstrap");
            instance = root.AddComponent<GameplayUIRuntimeBootstrap>();
            DontDestroyOnLoad(root);
        }
        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.EnsureAfterSceneReady());
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name == "NewGame")
            return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        // Text entry owns every printable key. Without this guard typing "i", "j"
        // or "k" into chat also opened gameplay windows behind the chat.
        if (TextEntryOwnsKeyboard())
            return;

        if (keyboard.tabKey.wasPressedThisFrame)
        {
            RuntimeInventoryBootstrap.EnsureInventoryUI();
            InventoryUI.Instance?.ToggleInventory();
            return;
        }

        if (keyboard.kKey.wasPressedThisFrame)
        {
            Ensure<SkillTreeUI>("RuntimeSkillTreeUI").Toggle();
            return;
        }

        if (keyboard.jKey.wasPressedThisFrame)
        {
            Ensure<QuestJournalUI>("QuestJournalUI").Toggle();
            return;
        }

        if (keyboard.iKey.wasPressedThisFrame)
            Ensure<HelpPanelUI>("HelpPanelUI").ToggleHelp();
    }

    static bool TextEntryOwnsKeyboard()
    {
        if (RuntimeChatConsole.IsTyping)
            return true;
        GameObject selected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
        TMP_InputField input = selected != null
            ? selected.GetComponentInParent<TMP_InputField>()
            : null;
        return input != null && input.isFocused;
    }

    IEnumerator EnsureAfterSceneReady()
    {
        // Let the scene's own Canvas, player and HUD finish Awake before selecting parents.
        yield return null;
        yield return null;

        RuntimeInventoryBootstrap.EnsureInventoryUI();
        Ensure<QuickbarManager>("RuntimeQuickbar");
        Ensure<QuestTrackerUI>("QuestTrackerUI");
        Ensure<RuntimeChatConsole>("RuntimeChatConsole");
        Ensure<HelpPanelUI>("HelpPanelUI");
        Ensure<SkillTreeUI>("RuntimeSkillTreeUI");
        Ensure<QuestJournalUI>("QuestJournalUI");

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameState.Exploration);

        Debug.Log("[GameplayUI] Interfaz lista: Tab inventario, K habilidades, J misiones, I ayuda.");
    }

    static T Ensure<T>(string objectName) where T : Component
    {
        T existing = FindAnyObjectByType<T>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;
        return new GameObject(objectName).AddComponent<T>();
    }
}

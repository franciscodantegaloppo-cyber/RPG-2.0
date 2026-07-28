using UnityEngine;
using UnityEngine.SceneManagement;

public static class RuntimeInventoryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "NewGame")
            return;
        EnsureInventoryUI();
    }

    public static void EnsureInventoryUI()
    {
        if (Object.FindAnyObjectByType<InventoryUI>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("RuntimeInventoryUI");
        go.AddComponent<InventoryUI>();
        // AfterSceneLoad only fires once at game start, not on every scene switch, so this
        // has to survive the SpawnVillage -> dungeon_1 transition itself or Tab stops working
        // the moment the player leaves the first scene.
        Object.DontDestroyOnLoad(go);
    }
}

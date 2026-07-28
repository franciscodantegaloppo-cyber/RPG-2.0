using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Exploration, Combat, Dialogue, InMenu, Loading, Paused }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("State")]
    public GameState CurrentState = GameState.Exploration;

    [Header("Player References")]
    public GameObject PlayerObject;
    public Transform PlayerTransform;

    [Header("Scene Config")]
    public int VillageSceneIndex = 1;
    public int DungeonSceneIndex = 2;

    public static event System.Action<GameState> OnGameStateChanged;

    GameObject persistentCamera;
    GameObject persistentHUD;
    bool playerIsPersistent;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        if (SceneManager.GetActiveScene().name == "NewGame")
        {
            SetState(GameState.InMenu);
            return;
        }
        AdoptOrRebindPlayer();
        // Lock cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "NewGame")
        {
            SetState(GameState.InMenu);
            return;
        }
        AdoptOrRebindPlayer();
        GameplayUIRuntimeBootstrap.EnsureForScene(scene);
        SetState(GameState.Exploration);
    }

    void LateUpdate()
    {
        // Cursor ownership follows the global state every frame. This prevents a camera or a
        // late-created gameplay component from hiding it after Inventory/Dialogue has opened.
        ApplyCursorForState(CurrentState);
    }

    // First scene of the session: keep the player/camera/HUD that ship with it and mark them
    // persistent. Every scene loaded afterwards: destroy that scene's own baked-in copies and
    // carry the persistent ones over instead, repositioning at the scene's SpawnPoint.
    void AdoptOrRebindPlayer()
    {
        if (!playerIsPersistent)
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return;

            PlayerObject = p;
            PlayerTransform = p.transform;
            DontDestroyOnLoad(p);

            if (Camera.main != null)
            {
                persistentCamera = Camera.main.gameObject;
                DontDestroyOnLoad(persistentCamera);
            }

            var hud = FindAnyObjectByType<HUDController>(FindObjectsInactive.Include);
            if (hud != null)
            {
                persistentHUD = hud.transform.root.gameObject;
                DontDestroyOnLoad(persistentHUD);
            }

            playerIsPersistent = true;
            return;
        }

        foreach (var dupe in GameObject.FindGameObjectsWithTag("Player"))
            if (dupe != PlayerObject) Destroy(dupe);

        foreach (var cam in FindObjectsByType<Camera>())
            if (cam.CompareTag("MainCamera") && cam.gameObject != persistentCamera) Destroy(cam.gameObject);

        foreach (var hud in FindObjectsByType<HUDController>())
            if (hud.transform.root.gameObject != persistentHUD) Destroy(hud.transform.root.gameObject);

        if (PlayerTransform != null)
            PlayerTransform.localScale = Vector3.one; // undo DungeonEntrance's shrink-to-enter animation

        var spawn = GameObject.FindWithTag("SpawnPoint");
        if (spawn != null && PlayerObject != null)
            PlayerTransform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);

        if (persistentCamera != null)
        {
            var tpc = persistentCamera.GetComponent<ThirdPersonCamera>();
            if (tpc != null)
                tpc.InteriorMode = SceneManager.GetActiveScene().name.Contains("Dungeon") ||
                    SceneManager.GetActiveScene().name.Contains("dungeon");
        }
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState)
        {
            ApplyCursorForState(newState);
            return;
        }

        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);
        ApplyCursorForState(newState);
    }

    void ApplyCursorForState(GameState newState)
    {
        bool lockCursor = newState == GameState.Exploration || newState == GameState.Combat;
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;
    }

    public bool IsGameplayActive()
    {
        return CurrentState == GameState.Exploration || CurrentState == GameState.Combat;
    }

    public void PauseGameplay() => SetState(GameState.InMenu);
    public void ResumeGameplay() => SetState(GameState.Exploration);
}

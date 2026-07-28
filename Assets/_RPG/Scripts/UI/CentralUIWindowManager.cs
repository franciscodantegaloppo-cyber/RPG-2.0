using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Single authority for gameplay windows, cursor ownership and responsive canvas scaling.
public sealed class CentralUIWindowManager : MonoBehaviour
{
    static CentralUIWindowManager instance;
    readonly List<ManagedUIWindow> windows = new();
    bool changingWindows;
    bool ownedInputLastFrame;
    float nextCanvasRefresh;

    public static CentralUIWindowManager Instance
    {
        get
        {
            EnsureInstalled();
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InstallBeforeScene() => EnsureInstalled();

    static void EnsureInstalled()
    {
        if (instance != null) return;
        if (SceneManager.GetActiveScene().name == "NewGame") return;
        GameObject root = new GameObject("Central UI Window Manager");
        instance = root.AddComponent<CentralUIWindowManager>();
        DontDestroyOnLoad(root);
    }

    public static ManagedUIWindow Register(GameObject panel, string windowId,
        UnityEngine.Events.UnityAction closeAction, bool exclusive = true)
    {
        if (panel == null) return null;
        EnsureInstalled();
        ManagedUIWindow managed = panel.GetComponent<ManagedUIWindow>();
        if (managed == null) managed = panel.AddComponent<ManagedUIWindow>();
        managed.Configure(windowId, closeAction, exclusive);
        if (panel.GetComponent<AdaptiveUIWindowLayout>() == null)
            panel.AddComponent<AdaptiveUIWindowLayout>();
        instance?.Track(managed);
        return managed;
    }

    void Track(ManagedUIWindow window)
    {
        if (window != null && !windows.Contains(window)) windows.Add(window);
    }

    public void NotifyOpened(ManagedUIWindow opened)
    {
        if (opened == null) return;
        Track(opened);
        if (changingWindows) return;
        changingWindows = true;
        try
        {
            if (opened.Exclusive)
            {
                var snapshot = windows.ToArray();
                foreach (ManagedUIWindow other in snapshot)
                {
                    if (other == null || other == opened || !other.Exclusive ||
                        !other.gameObject.activeInHierarchy) continue;
                    other.RequestClose();
                }
            }
            opened.transform.SetAsLastSibling();
        }
        finally { changingWindows = false; }
        ApplyInputOwnership();
    }

    public void NotifyClosed(ManagedUIWindow closed)
    {
        if (!changingWindows) ApplyInputOwnership();
    }

    public void CloseAll()
    {
        if (changingWindows) return;
        changingWindows = true;
        try
        {
            foreach (ManagedUIWindow window in windows.ToArray())
                if (window != null && window.gameObject.activeInHierarchy)
                    window.RequestClose();
        }
        finally { changingWindows = false; }
        ApplyInputOwnership();
    }

    public bool HasOpenWindow()
    {
        Cleanup();
        foreach (ManagedUIWindow window in windows)
            if (window != null && window.gameObject.activeInHierarchy) return true;
        return false;
    }

    void LateUpdate()
    {
        ApplyInputOwnership();
        if (Time.unscaledTime < nextCanvasRefresh) return;
        nextCanvasRefresh = Time.unscaledTime + 1f;
        ConfigureGameplayCanvases();
        Cleanup();
    }

    void ApplyInputOwnership()
    {
        bool uiOwnsInput = HasOpenWindow() || RuntimeChatConsole.IsTyping;
        if (uiOwnsInput)
        {
            ownedInputLastFrame = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameManager.Instance?.SetState(GameState.InMenu);
        }
        else if (ownedInputLastFrame)
        {
            ownedInputLastFrame = false;
            // A dialogue can begin as the previous window closes. It owns the cursor itself and
            // must not be mistaken for an abandoned menu state.
            if (MerchantDialoguePanel.Instance != null &&
                MerchantDialoguePanel.Instance.IsOpen) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.InMenu)
                GameManager.Instance.SetState(GameState.Exploration);
        }
    }

    void ConfigureGameplayCanvases()
    {
        if (SceneManager.GetActiveScene().name == "NewGame") return;
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) continue;
            AdaptiveUICanvas canvasAdapter =
                canvas.GetComponent<AdaptiveUICanvas>() ??
                canvas.gameObject.AddComponent<AdaptiveUICanvas>();
            canvasAdapter.Apply();
        }
    }

    void Cleanup() => windows.RemoveAll(window => window == null);
}

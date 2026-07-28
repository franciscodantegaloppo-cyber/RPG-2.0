using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// Guarantees exactly one EventSystem exists, in every scene, for the entire session. Without an
// EventSystem NO UI (buttons, sliders, dropdowns) can receive pointer input anywhere in the
// game - clicks are just silently ignored. Several panels (MerchantDialoguePanel, etc.) used to
// each lazily create their own EventSystem on first use, but that instance wasn't marked
// DontDestroyOnLoad, so it died on the very next scene transition while the panel itself (parented
// under the persistent HUD canvas) survived - leaving a visible but permanently unclickable UI
// until the next domain reload. This bootstraps once, persistently, before any of that can happen.
public static class UIEventSystemBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
        Object.DontDestroyOnLoad(go);
    }
}

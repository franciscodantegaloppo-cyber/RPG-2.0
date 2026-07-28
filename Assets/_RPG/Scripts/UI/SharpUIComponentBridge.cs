using SharpUI.Source.Common.UI.Elements.Button;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SharpInputField = SharpUI.Source.Common.UI.Elements.Input.InputField;

/// <summary>
/// Bridges legacy/game-generated controls to SharpUI's real event/decorator
/// layer. New controls should be created through SharpUIRuntimeFactory.
/// </summary>
public sealed class SharpUIComponentBridge : MonoBehaviour
{
    static SharpUIComponentBridge instance;
    float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        GameObject root = new GameObject("SharpUI_ComponentBridge");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<SharpUIComponentBridge>();
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .75f;
        Apply();
    }

    static void Apply()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (button == null || button.GetComponent<BaseButton>() != null)
                    continue;
                RectButton sharpButton = button.gameObject.AddComponent<RectButton>();
                sharpButton.isEnabled = button.interactable;
                sharpButton.isClickable = true;
                sharpButton.isSelectable = true;
            }

            foreach (TMP_InputField field in
                     canvas.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (field == null || field.GetComponent<SharpInputField>() != null)
                    continue;
                SharpInputField sharpInput =
                    field.gameObject.AddComponent<SharpInputField>();
                sharpInput.isEnabled = field.interactable;
                sharpInput.isClickable = true;
                sharpInput.isSelectable = true;
            }
        }
    }
}

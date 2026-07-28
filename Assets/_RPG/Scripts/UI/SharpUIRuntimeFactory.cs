using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Instantiates the original SharpUI prefabs while keeping gameplay presenters
/// independent from the demo models shipped with the asset.
/// </summary>
public static class SharpUIRuntimeFactory
{
    const string Root = "UI/SharpUI/Components/";

    public static Button CreateRectButton(Transform parent, string name,
        string label, UnityAction action, float preferredHeight = 44f)
    {
        GameObject prefab = Resources.Load<GameObject>(Root + "Button/RectButton");
        GameObject instance;
        if (prefab != null)
            instance = Object.Instantiate(prefab, parent, false);
        else
        {
            instance = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(Button));
            instance.transform.SetParent(parent, false);
            Image fallback = instance.GetComponent<Image>();
            fallback.sprite = Resources.Load<Sprite>("UI/SharpUI/Button");
            fallback.type = Image.Type.Sliced;
        }

        instance.name = name;
        LayoutElement layout = instance.GetComponent<LayoutElement>() ??
                               instance.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        Button button = instance.GetComponent<Button>();
        if (button == null)
            button = instance.AddComponent<Button>();
        button.onClick.RemoveAllListeners();
        if (action != null)
            button.onClick.AddListener(action);

        TMP_Text text = instance.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
            text.fontSize = Mathf.Min(text.fontSize, 18f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
        return button;
    }

    public static GameObject LoadComponent(string relativePath)
        => Resources.Load<GameObject>(Root + relativePath);
}

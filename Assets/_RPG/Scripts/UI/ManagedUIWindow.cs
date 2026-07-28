using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class ManagedUIWindow : MonoBehaviour
{
    UnityAction closeAction;
    bool configured;
    bool requestingClose;

    public string WindowId { get; private set; }
    public bool Exclusive { get; private set; }

    public void Configure(string windowId, UnityAction action, bool exclusive)
    {
        WindowId = string.IsNullOrWhiteSpace(windowId) ? gameObject.name : windowId;
        closeAction = action;
        Exclusive = exclusive;
        configured = true;
        if (isActiveAndEnabled)
            CentralUIWindowManager.Instance?.NotifyOpened(this);
    }

    void OnEnable()
    {
        if (configured)
            CentralUIWindowManager.Instance?.NotifyOpened(this);
    }

    void OnDisable()
    {
        if (configured)
            CentralUIWindowManager.Instance?.NotifyClosed(this);
    }

    public void RequestClose()
    {
        if (requestingClose || !gameObject.activeSelf) return;
        requestingClose = true;
        try
        {
            if (closeAction != null) closeAction.Invoke();
            else gameObject.SetActive(false);
        }
        finally { requestingClose = false; }
    }
}

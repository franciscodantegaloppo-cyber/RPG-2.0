using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class BossRoomCameraTrigger : MonoBehaviour
{
    ThirdPersonCamera cam;

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (cam == null) cam = Object.FindAnyObjectByType<ThirdPersonCamera>();
        if (cam != null) cam.BossRoomMode = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (cam == null) cam = Object.FindAnyObjectByType<ThirdPersonCamera>();
        if (cam != null) cam.BossRoomMode = false;
    }
}

using UnityEngine;

public class HouseInterior : MonoBehaviour
{
    [SerializeField] GameObject[] roofObjects;
    [SerializeField] ThirdPersonCamera thirdPersonCamera;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        SetRoofVisible(false);
        if (thirdPersonCamera != null) thirdPersonCamera.InteriorMode = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        SetRoofVisible(true);
        if (thirdPersonCamera != null) thirdPersonCamera.InteriorMode = false;
    }

    void SetRoofVisible(bool visible)
    {
        foreach (var r in roofObjects)
            if (r != null) r.SetActive(visible);
    }
}

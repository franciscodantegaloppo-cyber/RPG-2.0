using UnityEngine;

// Keeps a world-space marker (elite star, etc.) facing the main camera every frame, so it reads
// correctly regardless of view angle.
public class BillboardToCamera : MonoBehaviour
{
    Camera mainCamera;

    void LateUpdate()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera != null)
            transform.rotation = mainCamera.transform.rotation;
    }
}

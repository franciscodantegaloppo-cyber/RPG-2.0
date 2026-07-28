using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FixPhysicsWarnings
{
    private const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";

    [MenuItem("Tools/RPG/Fix Physics Warnings")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        int wheelRigidbodies = FixWheelColliders();
        int doorColliders = FixNegativeScaleDoorColliders();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log($"[FixPhysicsWarnings] Added {wheelRigidbodies} kinematic rigidbodies for WheelColliders; fixed {doorColliders} negative-scale door BoxColliders.");
    }

    private static int FixWheelColliders()
    {
        int fixedCount = 0;
        foreach (WheelCollider wheel in Object.FindObjectsByType<WheelCollider>(FindObjectsInactive.Include))
        {
            if (wheel.GetComponent<Rigidbody>() != null)
                continue;

            Rigidbody rb = wheel.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            EditorUtility.SetDirty(wheel.gameObject);
            fixedCount++;
        }

        return fixedCount;
    }

    private static int FixNegativeScaleDoorColliders()
    {
        int fixedCount = 0;
        foreach (BoxCollider box in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include))
        {
            if (box == null || !IsProblemDoor(box.transform))
                continue;

            Vector3 lossy = box.transform.lossyScale;
            if (lossy.x >= 0f && lossy.y >= 0f && lossy.z >= 0f &&
                box.size.x >= 0f && box.size.y >= 0f && box.size.z >= 0f)
                continue;

            if (lossy.x < 0f || lossy.y < 0f || lossy.z < 0f)
            {
                MeshFilter meshFilter = box.GetComponent<MeshFilter>();
                Object.DestroyImmediate(box);
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.sharedMesh;
                    meshCollider.convex = true;
                    EditorUtility.SetDirty(meshCollider);
                }
            }
            else
            {
                box.size = Abs(box.size);
                EditorUtility.SetDirty(box);
            }

            fixedCount++;
        }

        return fixedCount;
    }

    private static bool IsProblemDoor(Transform transform)
    {
        string path = GetPath(transform);
        return path.Contains("MedievalCastle/") && path.Contains("Double Door");
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static string GetPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }
}

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CampingTentInteriorSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/CampingTentInteriorSetup.generate";
    const string ColliderRootName = "WalkableTentColliders";

    [MenuItem("RPG/World/Make Camping Tent Enterable")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        GameObject tent = FindTent();
        if (tent == null)
        {
            Debug.LogError("[CampingTent] No se encontro camping_tent_type1_color1 en la escena.");
            return;
        }

        Transform previousColliderRoot = tent.transform.Find(ColliderRootName);
        if (previousColliderRoot != null) Object.DestroyImmediate(previousColliderRoot.gameObject);

        // Remove only the collider shipped on the tent itself. Do not delete colliders belonging
        // to an NPC or another prop that the user may have placed as a child inside the tent.
        foreach (Collider collider in tent.GetComponentsInChildren<Collider>(true))
            if (collider.transform == tent.transform || collider.name.StartsWith("TentCollider"))
                Object.DestroyImmediate(collider);

        if (!TryGetLocalVisualBounds(tent, out Bounds bounds))
        {
            Debug.LogError("[CampingTent] La carpa no tiene renderers para ajustar los colliders.");
            return;
        }

        GameObject colliderRoot = new GameObject(ColliderRootName);
        colliderRoot.transform.SetParent(tent.transform, false);

        // The raw mesh bounds include long guy ropes and four distant stakes. Colliders based on
        // those extremes float outside the cloth. Inset to the actual fabric body instead.
        float fullWidth = bounds.size.x;
        float fullHeight = bounds.size.y;
        float fullDepth = bounds.size.z;
        float bodyMinX = bounds.center.x - fullWidth * .31f;
        float bodyMaxX = bounds.center.x + fullWidth * .31f;
        float bodyMinZ = bounds.min.z + fullDepth * .25f;
        float bodyMaxZ = bounds.max.z - fullDepth * .23f;
        float bodyBottom = bounds.min.y + fullHeight * .10f;
        float width = bodyMaxX - bodyMinX;
        float depth = bodyMaxZ - bodyMinZ;
        float sideThickness = Mathf.Max(.10f, width * .045f);
        float endThickness = Mathf.Max(.10f, depth * .045f);
        float wallHeight = fullHeight * .63f;
        float wallY = bodyBottom + wallHeight * .5f;
        float bodyCenterZ = (bodyMinZ + bodyMaxZ) * .5f;

        // Mesh inspection confirms that the visible doorway faces local +Z. The old setup had
        // front and back reversed, which put a solid rear wall directly across the entrance.
        AddBox(colliderRoot.transform, "TentCollider_Left",
            new Vector3(bodyMinX + sideThickness * .5f, wallY, bodyCenterZ),
            new Vector3(sideThickness, wallHeight, depth));
        AddBox(colliderRoot.transform, "TentCollider_Right",
            new Vector3(bodyMaxX - sideThickness * .5f, wallY, bodyCenterZ),
            new Vector3(sideThickness, wallHeight, depth));
        AddBox(colliderRoot.transform, "TentCollider_Back",
            new Vector3((bodyMinX + bodyMaxX) * .5f, wallY, bodyMinZ + endThickness * .5f),
            new Vector3(width, wallHeight, endThickness));

        // Small front corners collide with the visible cloth flaps while leaving 62% of the
        // facade free. Clamp the doorway to at least 1.55 world metres for the player capsule.
        float localWorldX = Mathf.Max(.0001f, Mathf.Abs(tent.transform.lossyScale.x));
        float desiredDoorWidth = Mathf.Max(width * .36f, 1.65f / localWorldX);
        desiredDoorWidth = Mathf.Min(desiredDoorWidth, width * .56f);
        float frontPieceWidth = Mathf.Max(.05f, (width - desiredDoorWidth) * .5f);
        float frontZ = bodyMaxZ - endThickness * .5f;
        AddBox(colliderRoot.transform, "TentCollider_FrontLeft",
            new Vector3(bodyMinX + frontPieceWidth * .5f, wallY, frontZ),
            new Vector3(frontPieceWidth, wallHeight, endThickness));
        AddBox(colliderRoot.transform, "TentCollider_FrontRight",
            new Vector3(bodyMaxX - frontPieceWidth * .5f, wallY, frontZ),
            new Vector3(frontPieceWidth, wallHeight, endThickness));

        int layer = tent.layer;
        foreach (Transform child in colliderRoot.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;

        EditorUtility.SetDirty(tent);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = tent;

        Debug.Log("[CampingTent] Carpa transitable lista en " + tent.transform.position +
                  ". Entrada abierta hacia local +Z, anchoMundo=" + (desiredDoorWidth * localWorldX).ToString("0.00") +
                  "m, colliders=5.");
    }

    static GameObject FindTent()
    {
        foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (!candidate.gameObject.scene.IsValid()) continue;
            string normalized = candidate.name.ToLowerInvariant().Replace("_", "").Replace(" ", "").Replace("-", "");
            if (normalized.Contains("fcptenttype1color1") || normalized.Contains("campingtenttype1color1"))
                return candidate.gameObject;
        }
        return null;
    }

    static void AddBox(Transform parent, string name, Vector3 center, Vector3 size)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        BoxCollider collider = child.AddComponent<BoxCollider>();
        collider.center = center;
        collider.size = size;
        collider.isTrigger = false;
    }

    static bool TryGetLocalVisualBounds(GameObject root, out Bounds localBounds)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0) { localBounds = default; return false; }

        bool initialized = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null) continue;
            Bounds meshBounds = filter.sharedMesh.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 meshPoint = meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(x, y, z));
                Vector3 point = root.transform.InverseTransformPoint(filter.transform.TransformPoint(meshPoint));
                if (!initialized) { min = max = point; initialized = true; }
                else { min = Vector3.Min(min, point); max = Vector3.Max(max, point); }
            }
        }
        localBounds = new Bounds((min + max) * .5f, max - min);
        return initialized;
    }

    [InitializeOnLoadMethod]
    static void QueueSetup()
    {
        EditorApplication.delayCall += TryRun;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += TryRun;
        };
    }

    static void TryRun()
    {
        string absolute = Path.GetFullPath(RequestPath);
        if (!File.Exists(absolute) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(absolute);
        if (File.Exists(absolute + ".meta")) File.Delete(absolute + ".meta");
        Setup();
    }
}
#endif

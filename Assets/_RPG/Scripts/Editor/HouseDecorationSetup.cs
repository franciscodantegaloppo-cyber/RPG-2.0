using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Drops a small furniture set inside each VillageLayout house (table+chair+tablecloth,
// shelve+lamp, or barrel+box+lamp - rotated per house index for variety). Positions are
// derived from each house's own renderer bounds so it adapts if the ring layout changes.
public static class HouseDecorationSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string ArmoryRoot = "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Decorative Props/";
    const string DecorHolderName = "HouseDecor";

    [MenuItem("RPG/Setup House Decorations")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[HouseDecorationSetup] Ignorado durante Play Mode.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        VillageLayout layout = Object.FindAnyObjectByType<VillageLayout>();
        if (layout == null)
        {
            Debug.LogError("[HouseDecorationSetup] No se encontro VillageLayout en la escena.");
            return;
        }

        GameObject table = Load("Table/Table 1.prefab");
        GameObject chair = Load("Chair/Chair.prefab");
        GameObject cloth = Load("Table/Tablecloth.prefab");
        GameObject[] shelves = { Load("Shelve/Shelve.001.prefab"), Load("Shelve/Shelve.002.prefab"), Load("Shelve/Shelve.003.prefab") };
        GameObject[] barrels = { Load("Barrel/Barrel.001.prefab"), Load("Barrel/Barrel.002.prefab") };
        GameObject[] boxes = { Load("Box/Box.001.prefab"), Load("Box/Box.002.prefab") };
        GameObject[] lamps = { Load("Lamp/Lamp.001.prefab"), Load("Lamp/Lamp.002.prefab") };

        int decorated = 0;
        int index = 0;
        foreach (Transform house in layout.transform)
        {
            if (!house.name.StartsWith("House_", System.StringComparison.Ordinal))
                continue;

            Transform oldHolder = house.Find(DecorHolderName);
            if (oldHolder != null)
                Object.DestroyImmediate(oldHolder.gameObject);

            // The whole house's bounds include the roof's eave overhang, which is wider than the
            // walkable interior - that pushed furniture out past the walls. The "Interior" child
            // is the walled floor shell on its own, so its bounds are the actual usable footprint.
            Transform interior = house.Find("Interior");
            Bounds bounds = ComputeBounds(interior != null ? interior : house);
            if (bounds.size.sqrMagnitude < 0.01f)
            {
                index++;
                continue;
            }

            GameObject holder = new GameObject(DecorHolderName);
            holder.transform.SetParent(house, false);

            float floorY = bounds.min.y;
            Vector3 center = new Vector3(bounds.center.x, floorY, bounds.center.z);
            Vector3 fwd = house.forward;
            Vector3 right = house.right;

            // Stay well inside the interior footprint - walls eat a meaningful margin.
            float depthReach = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.3f;
            float sideReach = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.35f;

            switch (index % 3)
            {
                case 0:
                    PlaceAt(table, holder.transform, center - fwd * depthReach, house.rotation);
                    PlaceAt(cloth, holder.transform, center - fwd * depthReach, house.rotation);
                    PlaceAt(chair, holder.transform, center - fwd * (depthReach + 0.6f), house.rotation * Quaternion.Euler(0f, 180f, 0f));
                    PlaceAt(lamps[index % lamps.Length], holder.transform, center - fwd * depthReach + right * 0.35f, house.rotation);
                    break;
                case 1:
                    PlaceAt(shelves[index % shelves.Length], holder.transform, center + right * sideReach, house.rotation * Quaternion.Euler(0f, 90f, 0f));
                    PlaceAt(barrels[index % barrels.Length], holder.transform, center - right * sideReach - fwd * depthReach * 0.4f, house.rotation);
                    PlaceAt(lamps[index % lamps.Length], holder.transform, center - fwd * depthReach, house.rotation);
                    break;
                default:
                    PlaceAt(barrels[index % barrels.Length], holder.transform, center + right * sideReach * 0.7f - fwd * depthReach * 0.3f, house.rotation);
                    PlaceAt(boxes[index % boxes.Length], holder.transform, center + right * sideReach * 0.7f - fwd * depthReach * 0.7f, house.rotation * Quaternion.Euler(0f, 25f, 0f));
                    PlaceAt(boxes[(index + 1) % boxes.Length], holder.transform, center - right * sideReach * 0.6f - fwd * depthReach * 0.5f, house.rotation * Quaternion.Euler(0f, -15f, 0f));
                    break;
            }

            decorated++;
            index++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[HouseDecorationSetup] Decoradas " + decorated + " casas.");
    }

    static GameObject Load(string relativePath)
    {
        string path = ArmoryRoot + relativePath;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            Debug.LogWarning("[HouseDecorationSetup] No se encontro prefab: " + path);
        return prefab;
    }

    static void PlaceAt(GameObject prefab, Transform parent, Vector3 worldPos, Quaternion worldRot)
    {
        if (prefab == null) return;
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = worldPos;
        go.transform.rotation = worldRot;
    }

    static Bounds ComputeBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(root.position, Vector3.zero);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GoblinPassageMainQuestSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/GoblinPassageMainQuest.generate";
    const string PrefabPath = "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Wagon/Wagon Parts/WagonBackpack.001.prefab";
    const string RunePath = "Assets/_RPG/Resources/Items/Rune.asset";
    const string DiamondPath = "Assets/_RPG/Resources/Items/Diamond.asset";
    const string ObjectName = "WagonBackpackChest_GoblinPassage";

    [MenuItem("RPG/Quests/Setup Goblin Passage Main Quest Chest")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[GoblinPassageMainQuestSetup] Se colocará el cofre al salir de Play Mode.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        ItemData rune = AssetDatabase.LoadAssetAtPath<ItemData>(RunePath);
        ItemData diamond = AssetDatabase.LoadAssetAtPath<ItemData>(DiamondPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (rune == null || diamond == null || prefab == null)
        {
            Debug.LogError("[GoblinPassageMainQuestSetup] Falta la mochila, Rune.asset o Diamond.asset.");
            return;
        }

        GameObject backpack = GameObject.Find(ObjectName);
        bool created = backpack == null;
        if (created)
        {
            backpack = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            backpack.name = ObjectName;
            Transform boss = FindBoss();
            Vector3 position = boss != null
                ? boss.position - boss.forward * 6f + boss.right * 1.5f
                : new Vector3(-37f, 0f, 49f);
            backpack.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, -30f, 0f));
            SnapToTerrain(backpack.transform);
        }

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0) SetLayerRecursive(backpack.transform, interactableLayer);
        EnsureCollider(backpack);

        ChestContainer chest = backpack.GetComponent<ChestContainer>();
        if (chest == null) chest = backpack.AddComponent<ChestContainer>();
        SerializedObject chestData = new SerializedObject(chest);
        chestData.FindProperty("chestName").stringValue = "Botín del pasaje goblin";
        SerializedProperty startingItems = chestData.FindProperty("startingItems");
        startingItems.arraySize = 25;
        for (int i = 0; i < 20; i++) startingItems.GetArrayElementAtIndex(i).objectReferenceValue = rune;
        for (int i = 20; i < 25; i++) startingItems.GetArrayElementAtIndex(i).objectReferenceValue = diamond;
        chestData.ApplyModifiedPropertiesWithoutUndo();

        GoblinPassageQuestChest questChest = backpack.GetComponent<GoblinPassageQuestChest>();
        if (questChest == null) questChest = backpack.AddComponent<GoblinPassageQuestChest>();
        SerializedObject questData = new SerializedObject(questChest);
        questData.FindProperty("runeItem").objectReferenceValue = rune;
        questData.FindProperty("diamondItem").objectReferenceValue = diamond;
        questData.FindProperty("discoveryDistance").floatValue = 7f;
        questData.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(backpack);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[GoblinPassageMainQuestSetup] Cofre listo en " + backpack.transform.position +
                  " con 20 runas y 5 diamantes" + (created ? "." : " (posición manual conservada)."));
    }

    [InitializeOnLoadMethod]
    static void RunRequestedSetup() => EditorApplication.delayCall += TryRunRequestedSetup;

    static void TryRunRequestedSetup()
    {
        string absolute = Path.GetFullPath(RequestPath);
        if (!File.Exists(absolute)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRunRequestedSetup;
            return;
        }
        File.Delete(absolute);
        if (File.Exists(absolute + ".meta")) File.Delete(absolute + ".meta");
        Setup();
    }

    static Transform FindBoss()
    {
        GameObject named = GameObject.Find("KingGoblinBoss");
        if (named != null) return named.transform;
        KingGoblinBossAI boss = Object.FindAnyObjectByType<KingGoblinBossAI>();
        return boss != null ? boss.transform : null;
    }

    static void SnapToTerrain(Transform target)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;
        Vector3 position = target.position;
        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        target.position = position;
    }

    static void SetLayerRecursive(Transform target, int layer)
    {
        target.gameObject.layer = layer;
        foreach (Transform child in target) SetLayerRecursive(child, layer);
    }

    static void EnsureCollider(GameObject backpack)
    {
        if (backpack.GetComponentInChildren<Collider>(true) != null) return;
        Renderer[] renderers = backpack.GetComponentsInChildren<Renderer>(true);
        BoxCollider box = backpack.AddComponent<BoxCollider>();
        if (renderers.Length == 0) { box.size = new Vector3(1.2f, 1.1f, .8f); return; }
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        box.center = backpack.transform.InverseTransformPoint(bounds.center);
        Vector3 scale = backpack.transform.lossyScale;
        box.size = new Vector3(bounds.size.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(.001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(.001f, Mathf.Abs(scale.z)));
    }
}

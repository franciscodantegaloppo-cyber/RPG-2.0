#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OldManNpcSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/OldManNpcSetup.generate";
    const string MaterialPath = "Assets/_RPG/Materials/NPC-ElViejo.mat";
    const string PalettePath = "Assets/URP GanzSe Free Modular Character Pack/Material/Base Palette Material URP.mat";

    [MenuItem("RPG/NPCs/Create El viejo Near Spawn")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        foreach (OldManDialogue existing in Object.FindObjectsByType<OldManDialogue>(FindObjectsInactive.Include))
            Object.DestroyImmediate(existing.gameObject);

        TonioQuestGiver tonio = Object.FindAnyObjectByType<TonioQuestGiver>(FindObjectsInactive.Include);
        if (tonio == null)
        {
            Debug.LogError("[ElViejo] No se encontro a Tonio para duplicar su NPC.");
            return;
        }

        GameObject oldMan = Object.Instantiate(tonio.gameObject);
        oldMan.name = "NPC_El_viejo";

        RemoveIfPresent(oldMan.GetComponent<TonioQuestGiver>());
        RemoveIfPresent(oldMan.GetComponent<TonioRetaliation>());
        RemoveIfPresent(oldMan.GetComponent<EnemyStats>());
        RemoveIfPresent(oldMan.GetComponent<EnemyHealthBar>());
        RemoveIfPresent(oldMan.GetComponent<WeaponSocket>());
        Transform combatHitbox = oldMan.transform.Find("CombatHitbox");
        if (combatHitbox != null) Object.DestroyImmediate(combatHitbox.gameObject);

        if (oldMan.GetComponent<OldManDialogue>() == null)
            oldMan.AddComponent<OldManDialogue>();
        NPCWander wander = oldMan.GetComponent<NPCWander>();
        if (wander == null) wander = oldMan.AddComponent<NPCWander>();
        wander.Configure(speed: .38f, radius: 1.6f, step: .65f, minWait: 4f, maxWait: 8f);
        wander.UseTransformMovement(true);

        Vector3 position = tonio.transform.position + tonio.transform.right * 6.5f + tonio.transform.forward * 3.5f;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null) position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        oldMan.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-tonio.transform.forward));

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer < 0) interactableLayer = 10;
        oldMan.layer = interactableLayer;
        foreach (Collider collider in oldMan.GetComponentsInChildren<Collider>(true))
            collider.gameObject.layer = interactableLayer;

        ApplyOldManMaterial(oldMan);
        EditorUtility.SetDirty(oldMan);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[ElViejo] NPC creado cerca del spawn en " + position + ". Dialogo dividido en 5 cuadros.");
    }

    static void ApplyOldManMaterial(GameObject oldMan)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Material palette = AssetDatabase.LoadAssetAtPath<Material>(PalettePath);
            if (palette == null) return;
            material = new Material(palette) { name = "NPC-ElViejo" };
            Color aged = new Color(.48f, .42f, .34f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", aged);
            if (material.HasProperty("_Color")) material.SetColor("_Color", aged);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .12f);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        Transform visual = oldMan.transform.Find("GanzMerchant");
        Transform target = visual != null ? visual : oldMan.transform;
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }
    }

    static void RemoveIfPresent(Object component)
    {
        if (component != null) Object.DestroyImmediate(component);
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

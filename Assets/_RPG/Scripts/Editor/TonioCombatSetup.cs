using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class TonioCombatSetup
{
    const string CombatControllerPath = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animation Controller/RPG-Character-Animation-Controller.controller";
    const string SwordWeaponDataPath = "Assets/_RPG/ScriptableObjects/Weapons/KingGoblinSwordWeapon.asset";

    [MenuItem("RPG/Setup Tonio Combat Reaction")]
    static void Setup()
    {
        TonioQuestGiver tonio = Object.FindAnyObjectByType<TonioQuestGiver>(FindObjectsInactive.Include);
        if (tonio == null)
        {
            Debug.LogError("[TonioCombatSetup] No hay NPC Tonio en la escena.");
            return;
        }

        GameObject go = tonio.gameObject;

        EnemyStats stats = go.GetComponent<EnemyStats>();
        if (stats == null)
            stats = Undo.AddComponent<EnemyStats>(go);

        SerializedObject so = new SerializedObject(stats);
        so.FindProperty("maxHealth").floatValue = 99999f;
        so.FindProperty("attack").floatValue = 0f;
        so.FindProperty("minGoldDrop").intValue = 0;
        so.FindProperty("maxGoldDrop").intValue = 0;
        so.FindProperty("runeDropChance").floatValue = 0f;
        so.ApplyModifiedProperties();

        // Separate trigger hitbox on the Enemy layer so CombatSystem's OverlapSphere can find him,
        // without moving his real colliders off layer 10 (Interactable) - PlayerInteraction's own
        // overlap check is hardcoded to that layer for the E-to-talk prompt.
        Transform hitboxTransform = go.transform.Find("CombatHitbox");
        GameObject hitbox = hitboxTransform != null ? hitboxTransform.gameObject : null;
        if (hitbox == null)
        {
            hitbox = new GameObject("CombatHitbox");
            Undo.RegisterCreatedObjectUndo(hitbox, "Add Tonio Combat Hitbox");
            hitbox.transform.SetParent(go.transform, false);
        }
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0)
        {
            Debug.LogError("[TonioCombatSetup] No existe el layer 'Enemy'. Corre RPG/Setup Project primero.");
            return;
        }
        hitbox.layer = enemyLayer;
        CapsuleCollider capsule = hitbox.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = hitbox.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.radius = 0.4f;
        capsule.height = 1.8f;
        capsule.center = new Vector3(0f, 0.9f, 0f);

        TonioRetaliation retaliation = go.GetComponent<TonioRetaliation>();
        if (retaliation == null)
            retaliation = Undo.AddComponent<TonioRetaliation>(go);

        SerializedObject retSo = new SerializedObject(retaliation);
        RuntimeAnimatorController combatController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CombatControllerPath);
        WeaponData swordWeaponData = AssetDatabase.LoadAssetAtPath<WeaponData>(SwordWeaponDataPath);
        if (combatController == null)
            Debug.LogWarning("[TonioCombatSetup] No se encontro RPG-Character-Animation-Controller en " + CombatControllerPath);
        if (swordWeaponData == null)
            Debug.LogWarning("[TonioCombatSetup] No se encontro KingGoblinSwordWeapon.asset en " + SwordWeaponDataPath);
        retSo.FindProperty("combatController").objectReferenceValue = combatController;
        retSo.FindProperty("swordWeaponData").objectReferenceValue = swordWeaponData;
        retSo.ApplyModifiedProperties();

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkAllScenesDirty();

        EditorUtility.DisplayDialog("Tonio: reaccion de combate lista",
            "Tonio ahora contraataca con la Espada del Rey Goblin si el jugador lo golpea (no puede morir - su vida se restaura siempre).\n\n" +
            "Ctrl+S para guardar la escena.", "OK");
    }
}

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BlacksmithCombatAnimationSetup
{
    const string ControllerPath =
        "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE/Animation Controller/RPG-Character-Animation-Controller.controller";
    const string ClaymorePath =
        "Assets/URP GanzSe Free Modular Character Pack/Prefabs/GREAT SWORDS/FREE GREAT SWORD 4 COLOR 1.prefab";

    static BlacksmithCombatAnimationSetup()
    {
        EditorApplication.delayCall += EnsureOpenSceneBlacksmiths;
    }

    [MenuItem("RPG/NPC/Fix Blacksmith Sword Attack")]
    public static void EnsureOpenSceneBlacksmiths()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling)
            return;

        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                ControllerPath);
        GameObject claymore =
            AssetDatabase.LoadAssetAtPath<GameObject>(ClaymorePath);
        if (controller == null || claymore == null)
        {
            Debug.LogWarning(
                "[BlacksmithCombat] No se encontró el controlador o la claymore.");
            return;
        }

        int changed = 0;
        foreach (NPCHerrero blacksmith in
                 Object.FindObjectsByType<NPCHerrero>(
                     FindObjectsInactive.Include))
        {
            if (blacksmith == null ||
                !blacksmith.gameObject.scene.IsValid())
                continue;
            NPCMeleeDefender defender =
                blacksmith.GetComponent<NPCMeleeDefender>();
            if (defender == null)
            {
                defender =
                    blacksmith.gameObject.AddComponent<NPCMeleeDefender>();
                changed++;
            }

            SerializedObject serialized = new SerializedObject(defender);
            bool modified = SetObject(serialized, "combatController",
                                controller) |
                            SetObject(serialized, "weaponPrefab",
                                claymore) |
                            SetEnum(serialized, "weaponType",
                                (int)WeaponType.Sword2H) |
                            SetInt(serialized, "swordAttackAction", 7);
            if (!modified) continue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(defender);
            EditorSceneManager.MarkSceneDirty(
                blacksmith.gameObject.scene);
            changed++;
        }

        if (changed > 0)
            Debug.Log("[BlacksmithCombat] Herrero configurado con " +
                      "claymore y animación 2Hand-Sword-Attack7.");
    }

    static bool SetObject(SerializedObject serialized, string name,
        Object value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null || property.objectReferenceValue == value)
            return false;
        property.objectReferenceValue = value;
        return true;
    }

    static bool SetEnum(SerializedObject serialized, string name,
        int value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null || property.enumValueIndex == value)
            return false;
        property.enumValueIndex = value;
        return true;
    }

    static bool SetInt(SerializedObject serialized, string name, int value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null || property.intValue == value)
            return false;
        property.intValue = value;
        return true;
    }
}
#endif

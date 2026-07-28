using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PlayerSetup
{
    [MenuItem("RPG/Setup Player Components")]
    static void SetupPlayer()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) { Debug.LogError("No Player found in scene."); return; }

        int added = 0;

        if (player.GetComponent<PlayerDodge>() == null)
        {
            Undo.AddComponent<PlayerDodge>(player);
            added++;
            Debug.Log("Added PlayerDodge");
        }

        if (player.GetComponent<WeaponDrawSystem>() == null)
        {
            Undo.AddComponent<WeaponDrawSystem>(player);
            added++;
            Debug.Log("Added WeaponDrawSystem");
        }

        if (player.GetComponent<WeaponSocket>() == null)
        {
            Undo.AddComponent<WeaponSocket>(player);
            added++;
            Debug.Log("Added WeaponSocket");
        }

        if (player.GetComponent<EquipmentManager>() == null)
        {
            Undo.AddComponent<EquipmentManager>(player);
            added++;
            Debug.Log("Added EquipmentManager");
        }

        if (Object.FindAnyObjectByType<InventoryManager>() == null)
        {
            var invGO = new GameObject("InventoryManager");
            Undo.RegisterCreatedObjectUndo(invGO, "Create InventoryManager");
            Undo.AddComponent<InventoryManager>(invGO);
            added++;
            Debug.Log("Created InventoryManager");
        }

        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkAllScenesDirty();

        EditorUtility.DisplayDialog("Player Setup",
            added > 0
                ? $"Added {added} component(s) to Player.\nSave the scene with Ctrl+S."
                : "Player already has all components.",
            "OK");
    }
}

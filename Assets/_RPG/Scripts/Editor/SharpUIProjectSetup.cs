using System.IO;
using UnityEditor;
using UnityEngine;

public static class SharpUIProjectSetup
{
    const string RequestPath = "Assets/_RPG/Generated/SharpUIProjectSetup.generate";
    const string Destination = "Assets/_RPG/Resources/UI/SharpUI";
    const string ComponentDestination = Destination + "/Components";
    const string SkillIconCatalogPath =
        "Assets/_RPG/Resources/UI/SkillIconCatalog.asset";

    [InitializeOnLoadMethod]
    static void Queue() => EditorApplication.delayCall += EnsureRuntimeComponents;

    [MenuItem("RPG/UI/Apply SharpUI Pack")]
    public static void ApplyFromMenu() => TryRun();

    static void TryRun()
    {
        if (!File.Exists(Path.GetFullPath(RequestPath)) ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        EnsurePath(Destination);
        Copy("info_box.png", "Panel.png");
        Copy("rect_button.png", "Button.png");
        Copy("square_button_neutral.png", "Slot.png");
        Copy("round_button.png", "RoundButton.png");
        Copy("round_button_selected.png", "RoundButtonSelected.png");
        Copy("resourc_bar_frame.png", "ResourceFrame.png");
        Copy("resource_bar_fill.png", "ResourceFill.png");
        Copy("tooltip_box.png", "Tooltip.png");
        Copy("square_frame_grayscale.png", "StatusSlot.png");
        Copy("background.png", "SkillTreeBackdrop.png");
        Copy("rect_input.png", "Input.png");
        Copy("dialog_header_background.png", "DialogHeader.png");
        Copy("list_item_background.png", "ListItem.png");
        Copy("list_item_background_selected.png", "ListItemSelected.png");
        Copy("notification_line.png", "NotificationLine.png");
        Copy("scroll_background.png", "ScrollBackground.png");
        Copy("scroll_fill.png", "ScrollFill.png");
        Copy("scroll_handle.png", "ScrollHandle.png");
        Copy("checkbox.png", "Checkbox.png");
        Copy("checkbox_checkmark_check.png", "Checkmark.png");
        CopyRuntimeComponents();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string request = Path.GetFullPath(RequestPath);
        if (File.Exists(request)) File.Delete(request);
        if (File.Exists(request + ".meta")) File.Delete(request + ".meta");
        Debug.Log("[SharpUIProjectSetup] Tema SharpUI aplicado al sistema de interfaz.");
    }

    static void EnsureRuntimeComponents()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // The first integration only copied textures.  Keep an actual runtime
        // library of the package prefabs as well, so generated game windows use
        // SharpUI behaviour/decorators instead of merely imitating its artwork.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(
                ComponentDestination + "/Button/RectButton.prefab") == null)
        {
            CopyRuntimeComponents();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SharpUIProjectSetup] Componentes runtime de SharpUI preparados.");
        }

        EnsureSkillIconCatalog();
        TryRun();
    }

    static void EnsureSkillIconCatalog()
    {
        SkillIconCatalog catalog =
            AssetDatabase.LoadAssetAtPath<SkillIconCatalog>(SkillIconCatalogPath);
        if (catalog != null && catalog.Icons.Count >= 500)
            return;

        EnsurePath("Assets/_RPG/Resources/UI");
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SkillIconCatalog>();
            AssetDatabase.CreateAsset(catalog, SkillIconCatalogPath);
        }

        string[] guids = AssetDatabase.FindAssets(
            "t:Sprite", new[] { "Assets/500FreeSkillIcons/Icons" });
        System.Array.Sort(guids, (a, b) => string.CompareOrdinal(
            AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
        catalog.Icons.Clear();
        foreach (string guid in guids)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (sprite != null)
                catalog.Icons.Add(sprite);
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("[SharpUIProjectSetup] " + catalog.Icons.Count +
                  " iconos de habilidades catalogados.");
    }

    static void CopyRuntimeComponents()
    {
        CopyPrefab("Button/RectButton.prefab");
        CopyPrefab("Button/RoundButton.prefab");
        CopyPrefab("Button/TabButton.prefab");
        CopyPrefab("Button/IconButton.prefab");
        CopyPrefab("Button/ActionBarButton.prefab");
        CopyPrefab("Dialog/DialogDefault.prefab");
        CopyPrefab("Input/InputField.prefab");
        CopyPrefab("Input/Dropdown.prefab");
        CopyPrefab("Input/SimpleSlider.prefab");
        CopyPrefab("Input/CheckBoxSquare.prefab");
        CopyPrefab("List/SharpList.prefab");
        CopyPrefab("Progress/LoadingBar.prefab");
        CopyPrefab("Progress/ResourceBar.prefab");
        CopyPrefab("Progress/SkillBar.prefab");
        CopyPrefab("SkillTree/SkillTreeButton.prefab");
        CopyPrefab("SkillTree/SkillTreeNode.prefab");
        CopyPrefab("SkillTree/SkillTreeProgressLine.prefab");
        CopyPrefab("Tooltip.prefab");
        CopyPrefab("ModalView.prefab");
        CopyPrefab("Notification.prefab");
    }

    static void CopyPrefab(string relativePath)
    {
        string source = "Assets/SharpUI/Prefabs/UI/" + relativePath;
        string destination = ComponentDestination + "/" + relativePath;
        int separator = destination.LastIndexOf('/');
        EnsurePath(destination.Substring(0, separator));
        if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
            AssetDatabase.DeleteAsset(destination);
        if (!AssetDatabase.CopyAsset(source, destination))
            Debug.LogError("[SharpUIProjectSetup] No se pudo copiar prefab " + source);
    }

    static void Copy(string sourceName, string destinationName)
    {
        string source = "Assets/SharpUI/Textures/" + sourceName;
        string destination = Destination + "/" + destinationName;
        if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
            AssetDatabase.DeleteAsset(destination);
        if (!AssetDatabase.CopyAsset(source, destination))
        {
            Debug.LogError("[SharpUIProjectSetup] No se pudo copiar " + source);
            return;
        }
        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(destination) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    static void EnsurePath(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}

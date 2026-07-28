using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class DisableEditorGridSnapping
{
    [MenuItem("Tools/RPG/Disable Editor Grid Snapping")]
    public static void DisableSnap()
    {
        TrySetStaticProperty("UnityEditor.EditorSnapSettings", "gridSnapEnabled", false);
        TrySetStaticProperty("UnityEditor.EditorSnapSettings", "incrementalSnapActive", false);

        // Keep the increment tiny as a fallback in Unity versions where the toolbar toggle is per-window.
        TrySetStaticProperty("UnityEditor.EditorSnapSettings", "move", new Vector3(0.01f, 0.01f, 0.01f));
        TrySetStaticProperty("UnityEditor.EditorSnapSettings", "rotate", 1f);
        TrySetStaticProperty("UnityEditor.EditorSnapSettings", "scale", 0.01f);

        SceneView.RepaintAll();
        Debug.Log("Editor grid/increment snapping disabled or minimized. You can also toggle it from the Scene view Grid and Snap toolbar.");
    }

    private static void TrySetStaticProperty(string typeName, string propertyName, object value)
    {
        var type = typeof(Editor).Assembly.GetType(typeName);
        var property = type?.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite)
            property.SetValue(null, value);
    }
}

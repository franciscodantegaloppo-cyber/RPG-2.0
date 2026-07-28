using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// One-off diagnostics dumped to a text file (not the console) so results can be read back
// without a live Editor connection - see Assets/_RPG/Generated/Diagnostics.txt after running.
public static class RpgDiagnostics
{
    const string OutputPath = "Assets/_RPG/Generated/Diagnostics.txt";

    [MenuItem("RPG/Diagnostics/Dump Broken Materials Near NPCs")]
    public static void DumpBrokenMaterialsNearNpcs()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Broken/Missing Material Scan ===");

        GameObject[] anchors = { GameObject.Find("NPCMerchant"), GameObject.Find("NPCHerrero"), GameObject.Find("NPCTonio") };

        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            bool broken = false;
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader")
                {
                    broken = true;
                    break;
                }
            }
            if (!broken) continue;

            foreach (GameObject anchor in anchors)
            {
                if (anchor == null) continue;
                if (Vector3.Distance(r.transform.position, anchor.transform.position) > 6f) continue;

                sb.AppendLine(r.name + " near " + anchor.name +
                    " pos=" + r.transform.position +
                    " parentChain=" + BuildParentChain(r.transform) +
                    " prefab=" + PrefabUtility.GetCorrespondingObjectFromSource(r.gameObject));
                break;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputPath)));
        File.WriteAllText(OutputPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log("[RpgDiagnostics] Wrote " + OutputPath);
    }

    [MenuItem("RPG/Diagnostics/Dump All Broken Materials")]
    public static void DumpAllBrokenMaterials()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Full-Scene Broken/Missing Material Scan ===");
        sb.AppendLine("ActiveScene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        sb.AppendLine("PlayMode: " + EditorApplication.isPlaying);

        int count = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            foreach (Material m in r.sharedMaterials)
            {
                bool broken = m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader";
                if (!broken) continue;

                count++;
                sb.AppendLine((count) + ". " + r.name +
                    " type=" + r.GetType().Name +
                    " pos=" + r.transform.position +
                    " material=" + (m == null ? "null" : m.name) +
                    " shader=" + (m == null || m.shader == null ? "null" : m.shader.name) +
                    " parentChain=" + BuildParentChain(r.transform) +
                    " prefab=" + PrefabUtility.GetCorrespondingObjectFromSource(r.gameObject));
                break;
            }
        }

        sb.AppendLine();
        sb.AppendLine("Total broken renderers: " + count);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputPath)));
        File.WriteAllText(OutputPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log("[RpgDiagnostics] Wrote " + OutputPath + " (" + count + " broken renderers)");
    }

    static string BuildParentChain(Transform t)
    {
        var names = new System.Collections.Generic.List<string>();
        Transform current = t;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    [MenuItem("RPG/Diagnostics/Dump NPC Info")]
    public static void DumpNpcInfo()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== NPC Diagnostics ===");
        sb.AppendLine("ActiveScene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (!(mb is NPCMerchant) && !(mb is NPCHerrero) && !(mb is NPCWander))
                continue;

            GameObject go = mb.gameObject;
            if (go.GetComponent<NPCWander>() == null && !(mb is NPCWander))
                continue; // report each root once, driven by presence of NPCWander

            DumpGameObject(sb, go);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputPath)));
        File.WriteAllText(OutputPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log("[RpgDiagnostics] Wrote " + OutputPath);
    }

    static void DumpGameObject(StringBuilder sb, GameObject go)
    {
        sb.AppendLine();
        sb.AppendLine("--- " + go.name + " ---");
        sb.AppendLine("Position: " + go.transform.position);
        sb.AppendLine("LocalPosition: " + go.transform.localPosition);
        sb.AppendLine("Parent: " + (go.transform.parent != null ? go.transform.parent.name : "none"));
        sb.AppendLine("HideFlags: " + go.hideFlags);
        sb.AppendLine("StaticFlags: " + GameObjectUtility.GetStaticEditorFlags(go));
        sb.AppendLine("IsPrefabInstance: " + (PrefabUtility.GetPrefabInstanceStatus(go)));

        foreach (Component comp in go.GetComponents<Component>())
        {
            if (comp == null) { sb.AppendLine("  Component: <missing script>"); continue; }
            sb.AppendLine("  Component: " + comp.GetType().Name + (comp is Behaviour b ? " (enabled=" + b.enabled + ")" : ""));
        }

        foreach (Transform child in go.transform)
        {
            sb.AppendLine("  Child: " + child.name +
                " pos=" + child.position +
                " hideFlags=" + child.gameObject.hideFlags +
                " static=" + GameObjectUtility.GetStaticEditorFlags(child.gameObject));
            foreach (Component comp in child.GetComponents<Component>())
            {
                if (comp == null) continue;
                if (comp is Transform) continue;
                sb.AppendLine("    Component: " + comp.GetType().Name);
            }
        }
    }
}

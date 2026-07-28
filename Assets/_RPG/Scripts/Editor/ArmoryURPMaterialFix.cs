using System.IO;
using UnityEditor;
using UnityEngine;

// The Stylized Fantasy Armory pack ships its shared materials (BASIC MATERIAL, PLANE MATERIAL,
// PLANTS MATERIAL, GLOW MATERIAL, GLOW BLUE, Skybox) on the Built-in Standard shader, which
// renders untextured/flat under URP. The pack includes a ready-made URP conversion
// (URP & HDRP/ArmoryURP.unitypackage) whose assets share the exact same GUIDs as the ones
// already in the project, so importing it overwrites them in place with URP/Lit versions —
// every prefab already placed in the scene picks it up automatically, no scene edits needed.
public static class ArmoryURPMaterialFix
{
    const string PackagePath = "Assets/Daniel Mistage/Stylized Fantasy Armory/URP & HDRP/ArmoryURP.unitypackage";
    const string RequestPath = "Assets/_RPG/Generated/ArmoryURPMaterialFix.generate";

    [MenuItem("RPG/World/Fix Stylized Armory URP Materials")]
    public static void Fix()
    {
        if (!File.Exists(Path.GetFullPath(PackagePath)))
        {
            Debug.LogError("[ArmoryURPMaterialFix] No se encontro el paquete: " + PackagePath);
            return;
        }

        AssetDatabase.ImportPackage(PackagePath, false);
        Debug.Log("[ArmoryURPMaterialFix] Materiales de Stylized Fantasy Armory convertidos a URP/Lit.");
    }

    [InitializeOnLoadMethod]
    static void RunRequestedFix()
    {
        EditorApplication.delayCall += TryRunRequestedFix;
    }

    static void TryRunRequestedFix()
    {
        string absoluteRequestPath = Path.GetFullPath(RequestPath);
        if (!File.Exists(absoluteRequestPath) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        File.Delete(absoluteRequestPath);
        string metaPath = absoluteRequestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        Fix();
    }
}

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SpawnVillageEnvironmentAudit
{
    const string ReportPath = "Temp/SpawnVillageEnvironmentAudit.txt";

    [MenuItem("RPG/World/Auditar SpawnVillage")]
    public static void Run()
    {
        StringBuilder report = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        report.AppendLine("ESCENA=" + scene.name);

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(
            FindObjectsInactive.Include);
        report.AppendLine("TERRENOS=" + terrains.Length);
        foreach (Terrain terrain in terrains)
            AuditTerrain(terrain, report);

        HashSet<Transform> loose = new HashSet<Transform>();
        HashSet<Transform> trees = new HashSet<Transform>();
        HashSet<Transform> structures = new HashSet<Transform>();
        int prefabRenderers = 0;
        int combinedStructures = 0;
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Include);
        foreach (Renderer renderer in renderers)
        {
            if (renderer.GetComponentInParent<Terrain>() != null) continue;
            if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                prefabRenderers++;
            Transform root = FindLogicalRoot(renderer.transform);
            string name = root.name.ToLowerInvariant();
            if (ContainsAny(name, "log", "trunk", "tronco", "table", "mesa",
                    "wood", "madera", "chair", "crate", "barrel", "bench"))
                loose.Add(root);
            if (ContainsAny(name, "tree", "arbol", "pine", "spruce", "fir",
                    "pino"))
                trees.Add(root);
            if (ContainsAny(name, "wall", "muralla", "fence", "palisade",
                    "barricade"))
            {
                structures.Add(root);
                if (renderer.bounds.size.magnitude > 18f)
                    combinedStructures++;
            }
        }

        report.AppendLine("RENDERERS_TOTAL=" + renderers.Length);
        report.AppendLine("RENDERERS_EN_PREFABS=" + prefabRenderers);
        report.AppendLine("OBJETOS_SUELTOS_CANDIDATOS=" + loose.Count);
        report.AppendLine("ARBOLES_GAMEOBJECT=" + trees.Count);
        report.AppendLine("PIEZAS_ESTRUCTURALES=" + structures.Count);
        report.AppendLine("ESTRUCTURAS_GRANDES_COMBINADAS=" +
                          combinedStructures);

        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("[SpawnVillageAudit]\n" + report);
        EditorUtility.DisplayDialog("Auditoría de SpawnVillage",
            "Auditoría terminada. El informe se guardó en:\n" + ReportPath,
            "Aceptar");
    }

    static void AuditTerrain(Terrain terrain, StringBuilder report)
    {
        TerrainData data = terrain.terrainData;
        int sampleCount = 0;
        float slopeSum = 0f;
        float maxSlope = 0f;
        int steep35 = 0;
        int steep55 = 0;
        const int grid = 64;
        for (int z = 0; z < grid; z++)
        for (int x = 0; x < grid; x++)
        {
            float slope = data.GetSteepness(x / (grid - 1f),
                z / (grid - 1f));
            slopeSum += slope;
            maxSlope = Mathf.Max(maxSlope, slope);
            if (slope >= 35f) steep35++;
            if (slope >= 55f) steep55++;
            sampleCount++;
        }
        report.AppendLine("TERRAIN_NAME=" + terrain.name);
        report.AppendLine("TERRAIN_SIZE=" + data.size);
        report.AppendLine("HEIGHTMAP_RESOLUTION=" + data.heightmapResolution);
        report.AppendLine("PENDIENTE_MEDIA=" +
                          (slopeSum / sampleCount).ToString("0.00"));
        report.AppendLine("PENDIENTE_MAXIMA=" + maxSlope.ToString("0.00"));
        report.AppendLine("PORCENTAJE_MAYOR_35=" +
                          (steep35 * 100f / sampleCount).ToString("0.00"));
        report.AppendLine("PORCENTAJE_MAYOR_55=" +
                          (steep55 * 100f / sampleCount).ToString("0.00"));
        report.AppendLine("CAPAS_TERRENO=" + data.terrainLayers.Length);
        report.AppendLine("PROTOTIPOS_DETALLE=" + data.detailPrototypes.Length);
        report.AppendLine("ARBOLES_TERRAIN=" + data.treeInstanceCount);
    }

    static Transform FindLogicalRoot(Transform child)
    {
        Transform current = child;
        while (current.parent != null &&
               current.parent.GetComponent<Terrain>() == null &&
               current.parent.GetComponent<Canvas>() == null)
        {
            string parent = current.parent.name.ToLowerInvariant();
            if (parent.Contains("environment") || parent.Contains("village") ||
                parent.Contains("props")) break;
            current = current.parent;
        }
        return current;
    }

    static bool ContainsAny(string value, params string[] terms)
    {
        foreach (string term in terms)
            if (value.Contains(term)) return true;
        return false;
    }
}
#endif

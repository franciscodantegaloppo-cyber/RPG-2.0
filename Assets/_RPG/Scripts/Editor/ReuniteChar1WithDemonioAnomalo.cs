#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ReuniteChar1WithDemonioAnomalo
{
    [MenuItem("RPG/Enemies/Reunite Char1 With Demonio Anomalo")]
    public static void Reunite()
    {
        DemonioAnomaloEncounterEffects anomaly = Object.FindAnyObjectByType<DemonioAnomaloEncounterEffects>(FindObjectsInactive.Include);
        if (anomaly == null)
        {
            Debug.LogError("[Char1] No se encontro el Demonio Anomalo en la escena.");
            return;
        }

        // The anomaly root is the map location where the imported model was first placed.
        // Using its Armature moved Char1 by the model's internal bone offset.
        Transform target = anomaly.transform;
        DemonioAnomaloBossSetup.RestoreOriginalMaterial(anomaly.gameObject);
        EditorUtility.SetDirty(anomaly.gameObject);

        SkinnedMeshRenderer[] meshes = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include);
        foreach (SkinnedMeshRenderer mesh in meshes)
        {
            if (mesh.gameObject.name != "char1") continue;
            CrabDemonBossAI owner = mesh.GetComponentInParent<CrabDemonBossAI>();
            if (owner == null || owner.gameObject == anomaly.gameObject) continue;

            Undo.RecordObject(owner.transform, "Reubicar Char1 junto al Demonio Anomalo");
            owner.transform.position = target.position;
            owner.transform.rotation = target.rotation;
            EditorUtility.SetDirty(owner.gameObject);
            Selection.activeGameObject = owner.gameObject;
            Debug.Log("[Char1] Reubicado en la posicion original del modelo Demonio Anomalo.");
            return;
        }

        Debug.LogWarning("[Char1] No se encontro un Char1 separado para reubicar.");
    }
}

[InitializeOnLoad]
static class ReuniteChar1WithDemonioAnomaloOnCompile
{
    const string SessionKey = "RPG.Char1RestoredToOriginalAnomalyPosition";

    static ReuniteChar1WithDemonioAnomaloOnCompile()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        EditorApplication.delayCall += ApplyOnce;
    }

    static void ApplyOnce()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        ReuniteChar1WithDemonioAnomalo.Reunite();
    }
}
#endif

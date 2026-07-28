#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class SwordSlashVisualCalibratorWindow : EditorWindow
{
    const string ProfilePath =
        "Assets/_RPG/Resources/VFX/SwordSlashes/SwordSlashVisualProfile.asset";

    enum HandleMode { Mover, Rotar, Escalar }

    SwordSlashVisualProfile profile;
    GameObject preview;
    Vector3 basePosition;
    Quaternion baseRotation;
    Vector3 baseScale;
    HandleMode handleMode = HandleMode.Rotar;

    [MenuItem("RPG/Tools/Calibrar Slash Visual")]
    static void Open()
    {
        GetWindow<SwordSlashVisualCalibratorWindow>("Calibrar Slash");
    }

    void OnEnable()
    {
        profile = LoadOrCreateProfile();
        SceneView.duringSceneGui += DrawSceneHandles;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= DrawSceneHandles;
        DestroyPreview();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Calibración visual del slash", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Entrá en Play, equipá una espada y creá la vista previa. Después mové, " +
            "rotá o escalá el efecto directamente en la vista Scene. Todo queda guardado.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button(preview == null
                    ? "Crear vista previa sobre la espada"
                    : "Recrear vista previa"))
                CreatePreview();
        }

        handleMode = (HandleMode)GUILayout.Toolbar((int)handleMode,
            new[] { "Mover", "Rotar", "Escalar" });

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Vector3Field("Desplazamiento", profile.localPositionOffset);
            EditorGUILayout.Vector3Field("Rotación", profile.rotationCorrection);
            EditorGUILayout.FloatField("Escala", profile.scaleMultiplier);
        }

        if (GUILayout.Button("Restablecer orientación original"))
        {
            Undo.RecordObject(profile, "Restablecer calibración del slash");
            profile.localPositionOffset = Vector3.zero;
            profile.rotationCorrection = Vector3.zero;
            profile.scaleMultiplier = 1f;
            SaveProfile();
            ApplyProfileToPreview();
        }

        if (preview != null && GUILayout.Button("Seleccionar el slash en Scene"))
        {
            Selection.activeGameObject = preview;
            SceneView.lastActiveSceneView?.FrameSelected();
        }
    }

    void DrawSceneHandles(SceneView sceneView)
    {
        if (preview == null || profile == null)
            return;

        EditorGUI.BeginChangeCheck();
        Vector3 position = preview.transform.position;
        Quaternion rotation = preview.transform.rotation;
        Vector3 scale = preview.transform.localScale;

        if (handleMode == HandleMode.Mover)
            position = Handles.PositionHandle(position, rotation);
        else if (handleMode == HandleMode.Rotar)
            rotation = Handles.RotationHandle(rotation, position);
        else
            scale = Handles.ScaleHandle(scale, position, rotation,
                HandleUtility.GetHandleSize(position));

        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(profile, "Calibrar slash visualmente");
        preview.transform.SetPositionAndRotation(position, rotation);
        preview.transform.localScale = scale;
        profile.localPositionOffset =
            Quaternion.Inverse(baseRotation) * (position - basePosition);
        profile.rotationCorrection =
            NormalizeEuler((Quaternion.Inverse(baseRotation) * rotation).eulerAngles);
        profile.scaleMultiplier = Mathf.Max(.1f,
            scale.x / Mathf.Max(.0001f, baseScale.x));
        SaveProfile();
        Repaint();
    }

    void CreatePreview()
    {
        DestroyPreview();
        WeaponSocket socket = null;
        foreach (WeaponSocket candidate in FindObjectsByType<WeaponSocket>())
        {
            if (!candidate.HasBladeGeometry)
                continue;
            socket = candidate;
            break;
        }

        if (socket == null)
        {
            EditorUtility.DisplayDialog("Calibrar Slash",
                "No encontré una espada equipada. Entrá en Play y equipá la espada que querés calibrar.",
                "Aceptar");
            return;
        }

        Vector3 savedPosition = profile.localPositionOffset;
        Vector3 savedRotation = profile.rotationCorrection;
        float savedScale = profile.scaleMultiplier;
        profile.localPositionOffset = Vector3.zero;
        profile.rotationCorrection = Vector3.zero;
        profile.scaleMultiplier = 1f;
        preview = StylizedSwordSlashVfx.Play(socket.transform, socket, 999, 1f, .55f, false);
        profile.localPositionOffset = savedPosition;
        profile.rotationCorrection = savedRotation;
        profile.scaleMultiplier = savedScale;
        if (preview == null)
            return;

        preview.name = "SwordSlash_VisualCalibration";
        preview.hideFlags = HideFlags.DontSave;
        basePosition = preview.transform.position;
        baseRotation = preview.transform.rotation;
        baseScale = preview.transform.localScale;
        foreach (ParticleSystem particles in preview.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            particles.Play(true);
        }

        ApplyProfileToPreview();
        Selection.activeGameObject = preview;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    void ApplyProfileToPreview()
    {
        if (preview == null || profile == null)
            return;
        preview.transform.position =
            basePosition + baseRotation * profile.localPositionOffset;
        preview.transform.rotation =
            baseRotation * Quaternion.Euler(profile.rotationCorrection);
        preview.transform.localScale =
            baseScale * Mathf.Max(.1f, profile.scaleMultiplier);
        SceneView.RepaintAll();
    }

    void DestroyPreview()
    {
        if (preview != null)
            DestroyImmediate(preview);
        preview = null;
    }

    static Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(NormalizeAngle(euler.x), NormalizeAngle(euler.y),
            NormalizeAngle(euler.z));
    }

    static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    void SaveProfile()
    {
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
    }

    static SwordSlashVisualProfile LoadOrCreateProfile()
    {
        SwordSlashVisualProfile result =
            AssetDatabase.LoadAssetAtPath<SwordSlashVisualProfile>(ProfilePath);
        if (result != null)
            return result;
        EnsureFolder("Assets/_RPG/Resources", "VFX");
        EnsureFolder("Assets/_RPG/Resources/VFX", "SwordSlashes");
        result = CreateInstance<SwordSlashVisualProfile>();
        AssetDatabase.CreateAsset(result, ProfilePath);
        AssetDatabase.SaveAssets();
        return result;
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif

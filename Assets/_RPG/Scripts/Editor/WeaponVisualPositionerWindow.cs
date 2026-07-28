#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class WeaponVisualPositionerWindow : EditorWindow
{
    enum ToolMode { Mover, Rotar }

    WeaponSocket socket;
    ToolMode toolMode;

    [MenuItem("RPG/Tools/Posicionar Espada Visualmente")]
    static void Open()
    {
        GetWindow<WeaponVisualPositionerWindow>("Posicionar Espada");
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += DrawSceneHandles;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= DrawSceneHandles;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Posición visual de la espada",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Entrá en Play, equipá la espada y presioná Detectar. Mové o rotá el " +
            "mango directamente en Scene; el ajuste se guarda en esa arma.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Detectar espada equipada"))
                DetectSocket();
        }

        if (socket == null || !socket.HasEquippedWeapon)
        {
            EditorGUILayout.HelpBox("Todavía no hay una espada detectada.",
                MessageType.Warning);
            return;
        }

        WeaponData data = socket.CurrentWeaponData;
        EditorGUILayout.LabelField("Arma",
            data != null ? data.weaponName : socket.EquippedWeaponGrip.name);
        toolMode = (ToolMode)GUILayout.Toolbar((int)toolMode,
            new[] { "Mover", "Rotar" });

        EditorGUILayout.Vector3Field("Posición local",
            socket.ActiveHandPositionOffset);
        EditorGUILayout.Vector3Field("Rotación local",
            socket.ActiveHandRotationOffset);

        if (GUILayout.Button("Seleccionar mango en Scene"))
        {
            Selection.activeTransform = socket.EquippedWeaponGrip;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        if (GUILayout.Button("Restablecer ajuste"))
            ApplyOffsets(new Vector3(.11f, .06f, .09f),
                new Vector3(289.52f, 32.8f, 107.87f));
    }

    void DetectSocket()
    {
        socket = null;
        foreach (WeaponSocket candidate in FindObjectsByType<WeaponSocket>())
        {
            if (!candidate.HasEquippedWeapon)
                continue;
            socket = candidate;
            break;
        }

        if (socket == null)
        {
            EditorUtility.DisplayDialog("Posicionar espada",
                "No encontré un arma equipada. Entrá en Play, equipá una espada y volvé a intentar.",
                "Aceptar");
            return;
        }

        Selection.activeTransform = socket.EquippedWeaponGrip;
        SceneView.lastActiveSceneView?.FrameSelected();
        Repaint();
    }

    void DrawSceneHandles(SceneView view)
    {
        if (socket == null || !socket.HasEquippedWeapon)
            return;

        Transform grip = socket.EquippedWeaponGrip;
        EditorGUI.BeginChangeCheck();
        Vector3 worldPosition = grip.position;
        Quaternion worldRotation = grip.rotation;
        if (toolMode == ToolMode.Mover)
            worldPosition = Handles.PositionHandle(worldPosition, worldRotation);
        else
            worldRotation = Handles.RotationHandle(worldRotation, worldPosition);

        if (!EditorGUI.EndChangeCheck())
            return;

        Transform parent = grip.parent;
        Vector3 localPosition = parent != null
            ? parent.InverseTransformPoint(worldPosition)
            : worldPosition;
        Quaternion localRotation = parent != null
            ? Quaternion.Inverse(parent.rotation) * worldRotation
            : worldRotation;
        ApplyOffsets(localPosition, NormalizeEuler(localRotation.eulerAngles));
    }

    void ApplyOffsets(Vector3 position, Vector3 rotation)
    {
        if (socket == null)
            return;

        WeaponData data = socket.CurrentWeaponData;
        if (data != null)
            Undo.RecordObject(data, "Posicionar espada visualmente");
        else
            Undo.RecordObject(socket, "Posicionar espada visualmente");

        socket.PreviewEquippedWeaponOffsets(position, rotation);
        if (data != null)
        {
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
        else
            EditorUtility.SetDirty(socket);

        SceneView.RepaintAll();
        Repaint();
    }

    static Vector3 NormalizeEuler(Vector3 value)
    {
        return new Vector3(Normalize(value.x), Normalize(value.y),
            Normalize(value.z));
    }

    static float Normalize(float value)
    {
        while (value > 180f) value -= 360f;
        while (value < -180f) value += 360f;
        return value;
    }
}
#endif

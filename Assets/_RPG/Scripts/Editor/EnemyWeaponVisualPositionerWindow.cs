#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Visual equivalent of WeaponVisualPositionerWindow for NPCs and enemies. It supports
// persistent weapons already parented to a hand (skeletons) in Edit Mode and temporary
// WeaponSocket weapons while an NPC is attacking in Play Mode.
public sealed class EnemyWeaponVisualPositionerWindow : EditorWindow
{
    enum ToolMode { Mover, Rotar }

    Transform enemyRoot;
    Transform weaponGrip;
    WeaponSocket socket;
    ToolMode toolMode;
    Vector3 detectedPosition;
    Vector3 detectedRotation;

    [MenuItem("RPG/Tools/Posicionar Arma de Enemigo")]
    static void Open() =>
        GetWindow<EnemyWeaponVisualPositionerWindow>("Arma de Enemigo");

    void OnEnable() => SceneView.duringSceneGui += DrawSceneHandles;
    void OnDisable() => SceneView.duringSceneGui -= DrawSceneHandles;

    void OnGUI()
    {
        EditorGUILayout.LabelField("Posicion visual de armas enemigas",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Selecciona el enemigo en la Jerarquia y pulsa Detectar. Para esqueletos " +
            "puedes ajustarlo directamente. Si el NPC esconde el arma, entra en Play, " +
            "espera a que ataque, pausa Unity y luego detectalo.",
            MessageType.Info);

        if (GUILayout.Button("Detectar arma del enemigo seleccionado"))
            DetectSelectedEnemy();

        if (weaponGrip == null)
        {
            EditorGUILayout.HelpBox(
                "No hay un arma detectada. Selecciona el enemigo o uno de sus hijos.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.ObjectField("Enemigo", enemyRoot, typeof(Transform), true);
        EditorGUILayout.ObjectField("Mango / arma", weaponGrip, typeof(Transform), true);
        toolMode = (ToolMode)GUILayout.Toolbar((int)toolMode,
            new[] { "Mover", "Rotar" });
        EditorGUILayout.Vector3Field("Posicion local", weaponGrip.localPosition);
        EditorGUILayout.Vector3Field("Rotacion local",
            NormalizeEuler(weaponGrip.localEulerAngles));

        if (socket != null && socket.CurrentWeaponData != null &&
            EditorApplication.isPlaying &&
            !AssetDatabase.Contains(socket.CurrentWeaponData))
            EditorGUILayout.HelpBox(
                "Esta arma fue creada temporalmente durante el ataque. El ajuste se ve " +
                "en vivo; copia los valores mostrados si necesitas aplicarlos a su prefab.",
                MessageType.Warning);
        else
            EditorGUILayout.HelpBox(
                "Los cambios se guardan en el Transform del arma o en su perfil WeaponData.",
                MessageType.None);

        if (GUILayout.Button("Seleccionar y enfocar arma"))
        {
            Selection.activeTransform = weaponGrip;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        if (GUILayout.Button("Restaurar posicion detectada"))
            ApplyOffsets(detectedPosition, detectedRotation);
    }

    void DetectSelectedEnemy()
    {
        weaponGrip = null;
        socket = null;
        Transform selected = Selection.activeTransform;
        if (selected == null)
        {
            ShowNotification(new GUIContent("Selecciona un enemigo."));
            return;
        }

        EnemyStats stats = selected.GetComponentInParent<EnemyStats>() ??
                           selected.GetComponentInChildren<EnemyStats>(true);
        enemyRoot = stats != null ? stats.transform : selected.root;

        SkeletonWeaponMarker skeletonWeapon =
            enemyRoot.GetComponentInChildren<SkeletonWeaponMarker>(true);
        if (skeletonWeapon != null)
            weaponGrip = skeletonWeapon.transform;

        socket = enemyRoot.GetComponent<WeaponSocket>() ??
                 enemyRoot.GetComponentInChildren<WeaponSocket>(true);
        if (weaponGrip == null && socket != null && socket.HasEquippedWeapon)
            weaponGrip = socket.EquippedWeaponGrip;

        if (weaponGrip == null)
        {
            foreach (Transform child in enemyRoot.GetComponentsInChildren<Transform>(true))
            {
                string lower = child.name.ToLowerInvariant();
                if ((lower.Contains("sword") || lower.Contains("weapon") ||
                     lower.Contains("espada")) &&
                    child.GetComponentInChildren<Renderer>(true) != null)
                {
                    weaponGrip = child;
                    break;
                }
            }
        }

        if (weaponGrip == null)
        {
            ShowNotification(new GUIContent(
                "No se encontro un arma visible. Pausa durante el ataque."));
            return;
        }

        detectedPosition = weaponGrip.localPosition;
        detectedRotation = NormalizeEuler(weaponGrip.localEulerAngles);
        Selection.activeTransform = weaponGrip;
        SceneView.lastActiveSceneView?.FrameSelected();
        Repaint();
    }

    void DrawSceneHandles(SceneView _)
    {
        if (weaponGrip == null) return;
        EditorGUI.BeginChangeCheck();
        Vector3 worldPosition = weaponGrip.position;
        Quaternion worldRotation = weaponGrip.rotation;
        if (toolMode == ToolMode.Mover)
            worldPosition = Handles.PositionHandle(worldPosition, worldRotation);
        else
            worldRotation = Handles.RotationHandle(worldRotation, worldPosition);
        if (!EditorGUI.EndChangeCheck()) return;

        Transform parent = weaponGrip.parent;
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
        if (weaponGrip == null) return;

        if (socket != null && weaponGrip == socket.EquippedWeaponGrip)
        {
            WeaponData data = socket.CurrentWeaponData;
            if (data != null) Undo.RecordObject(data, "Posicionar arma enemiga");
            socket.PreviewEquippedWeaponOffsets(position, rotation);
            if (data != null && AssetDatabase.Contains(data))
            {
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
        }
        else
        {
            Undo.RecordObject(weaponGrip, "Posicionar arma enemiga");
            weaponGrip.localPosition = position;
            weaponGrip.localRotation = Quaternion.Euler(rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(weaponGrip);
            EditorUtility.SetDirty(weaponGrip);
        }

        SceneView.RepaintAll();
        Repaint();
    }

    static Vector3 NormalizeEuler(Vector3 value) => new Vector3(
        Normalize(value.x), Normalize(value.y), Normalize(value.z));

    static float Normalize(float value)
    {
        while (value > 180f) value -= 360f;
        while (value < -180f) value += 360f;
        return value;
    }
}
#endif

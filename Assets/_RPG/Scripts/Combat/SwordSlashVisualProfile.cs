using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Combat/Sword Slash Visual Profile")]
public class SwordSlashVisualProfile : ScriptableObject
{
    [Tooltip("Desplazamiento visual relativo al plano real de la espada.")]
    public Vector3 localPositionOffset;

    [Tooltip("Rotación visual adicional del prefab del slash.")]
    public Vector3 rotationCorrection;

    [Min(0.1f)]
    [Tooltip("Escala adicional, conservando el tamaño calculado desde la hoja.")]
    public float scaleMultiplier = 1f;
}

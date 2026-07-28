using System.Collections;
using UnityEngine;

public class HouseDoor : MonoBehaviour, IInteractable
{
    [Header("Hinge")]
    [Tooltip("Offset local al pivot de la bisagra. X positivo = borde derecho, X negativo = borde izquierdo.")]
    [SerializeField] float hingeOffsetX = 0.55f;

    [Header("Swing")]
    [SerializeField] float openAngle = 90f;
    [SerializeField] float animDuration = 0.4f;

    [Header("Audio")]
    [SerializeField] AudioClip openSound;
    [SerializeField] AudioClip closeSound;

    bool isOpen;
    bool isAnimating;

    public string GetInteractionText() => isOpen ? "[E] Cerrar puerta" : "[E] Abrir puerta";
    public bool CanInteract(PlayerInteraction player) => !isAnimating;

    public void Interact(PlayerInteraction player)
    {
        if (isAnimating) return;
        StartCoroutine(AnimateDoor());
    }

    IEnumerator AnimateDoor()
    {
        isAnimating = true;
        AudioManager.Instance?.PlaySFX(isOpen ? closeSound : openSound);

        // Hinge point fijo en espacio mundo al inicio del giro
        Vector3 hingeWorld = transform.TransformPoint(new Vector3(hingeOffsetX, 0f, 0f));

        float targetAngle = isOpen ? -openAngle : openAngle;
        float elapsed = 0f;
        float rotated = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            float desired = targetAngle * t;
            float delta = desired - rotated;
            transform.RotateAround(hingeWorld, Vector3.up, delta);
            rotated = desired;
            yield return null;
        }

        isOpen = !isOpen;
        isAnimating = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 h = transform.TransformPoint(new Vector3(hingeOffsetX, 0f, 0f));
        Gizmos.DrawWireSphere(h, 0.05f);
        Gizmos.DrawLine(h + Vector3.down * 1f, h + Vector3.up * 1f);
    }
#endif
}

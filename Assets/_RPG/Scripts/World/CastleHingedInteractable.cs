using System.Collections;
using UnityEngine;

public class CastleHingedInteractable : MonoBehaviour, IInteractable
{
    public enum HingeAxis
    {
        LocalX,
        LocalY,
        LocalZ
    }

    [Header("Prompt")]
    [SerializeField] string closedText = "[E] Abrir";
    [SerializeField] string openText = "[E] Cerrar";

    [Header("Hinge")]
    [SerializeField] Vector3 localHingeOffset = new(-0.5f, 0f, 0f);
    [SerializeField] HingeAxis hingeAxis = HingeAxis.LocalY;

    [Header("Motion")]
    [SerializeField] float openAngle = 95f;
    [SerializeField] float animDuration = 0.55f;

    bool isOpen;
    bool isAnimating;

    public string GetInteractionText() => isOpen ? openText : closedText;
    public bool CanInteract(PlayerInteraction player) => !isAnimating;

    public void Interact(PlayerInteraction player)
    {
        if (!isAnimating)
        {
            StartCoroutine(Animate());
        }
    }

    IEnumerator Animate()
    {
        isAnimating = true;

        Vector3 hingeWorld = transform.TransformPoint(localHingeOffset);
        Vector3 axisWorld = hingeAxis switch
        {
            HingeAxis.LocalX => transform.right,
            HingeAxis.LocalZ => transform.forward,
            _ => transform.up
        };

        float targetAngle = isOpen ? -openAngle : openAngle;
        float elapsed = 0f;
        float rotated = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / animDuration));
            float desired = targetAngle * t;
            float delta = desired - rotated;
            transform.RotateAround(hingeWorld, axisWorld, delta);
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
        Vector3 hingeWorld = transform.TransformPoint(localHingeOffset);
        Gizmos.DrawWireSphere(hingeWorld, 0.08f);
        Gizmos.DrawLine(hingeWorld + Vector3.down, hingeWorld + Vector3.up);
    }
#endif
}

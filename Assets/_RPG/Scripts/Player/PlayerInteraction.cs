using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] float interactRadius = 2.5f;
    [SerializeField] LayerMask interactableMask;

    IInteractable currentTarget;
    HUDController hud;

    void Start()
    {
        hud = FindAnyObjectByType<HUDController>();

        // Si el mask no está configurado en el Inspector, usa layer "Interactable" (10)
        if (interactableMask.value == 0)
            interactableMask = 1 << 10;
    }

    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive()) return;

        IInteractable found = FindClosestInteractable();

        if (found != currentTarget)
        {
            currentTarget = found;
            hud?.ShowInteractionPrompt(found?.GetInteractionText());
        }

        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame && currentTarget != null && currentTarget.CanInteract(this))
            currentTarget.Interact(this);
    }

    IInteractable FindClosestInteractable()
    {
        // Búsqueda primaria: OverlapSphere con layer mask
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRadius, interactableMask);

        IInteractable best = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            // Busca IInteractable en el objeto o en su padre
            var interactable = hit.GetComponent<IInteractable>()
                            ?? hit.GetComponentInParent<IInteractable>();

            if (interactable == null || !interactable.CanInteract(this)) continue;

            float d = Vector3.Distance(transform.position, hit.transform.position);
            if (d < bestDist) { bestDist = d; best = interactable; }
        }

        if (best != null) return best;

        // Fallback: busca todos los IInteractable en la escena (por si el layer no está bien)
        var allInteractables = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);

        foreach (var mb in allInteractables)
        {
            if (mb is not IInteractable interactable) continue;
            if (!interactable.CanInteract(this)) continue;

            float d = Vector3.Distance(transform.position, mb.transform.position);
            if (d < interactRadius && d < bestDist)
            {
                bestDist = d;
                best = interactable;
            }
        }

        return best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}

using UnityEngine;

public sealed class HealingFountainInteractable : MonoBehaviour, IInteractable
{
    const int UsesPerDay = 3;

    [SerializeField, Min(0.05f)] float contactTolerance = 0.3f;

    TenkokuDayNightCycle dayNightCycle;
    Collider[] fountainColliders;
    PlayerInteraction cachedPlayer;
    Collider cachedPlayerCollider;
    int trackedWorldDay = int.MinValue;
    int usesToday;

    void Awake()
    {
        fountainColliders = GetComponentsInChildren<Collider>(true);
        FindClock();
        RefreshDay();
    }

    public string GetInteractionText()
    {
        RefreshDay();
        int remaining = Mathf.Max(0, UsesPerDay - usesToday);
        return remaining > 0
            ? "[E] Beber de la fuente  (" + remaining + "/3 hoy)"
            : "Fuente agotada por hoy";
    }

    public bool CanInteract(PlayerInteraction player)
    {
        if (player == null) return false;

        Collider playerCollider = ResolvePlayerCollider(player);
        Vector3 playerReference = playerCollider != null
            ? playerCollider.bounds.center
            : player.transform.position + Vector3.up * 0.9f;

        foreach (Collider fountainCollider in fountainColliders)
        {
            if (fountainCollider == null || !fountainCollider.enabled ||
                fountainCollider.isTrigger)
                continue;

            Vector3 pointOnFountain = SafeClosestPoint(
                fountainCollider, playerReference);
            Vector3 pointOnPlayer = playerCollider != null
                ? SafeClosestPoint(playerCollider, pointOnFountain)
                : playerReference;
            if ((pointOnFountain - pointOnPlayer).sqrMagnitude <=
                contactTolerance * contactTolerance)
                return true;
        }
        return false;
    }

    static Vector3 SafeClosestPoint(Collider collider, Vector3 position)
    {
        // Unity throws when ClosestPoint is called on a non-convex MeshCollider.
        // The fountain's detailed collision mesh is intentionally non-convex, so its bounds
        // provide a stable contact test without expanding the interaction radius.
        MeshCollider mesh = collider as MeshCollider;
        return mesh != null && !mesh.convex
            ? collider.bounds.ClosestPoint(position)
            : collider.ClosestPoint(position);
    }

    public void Interact(PlayerInteraction player)
    {
        if (player == null) return;
        RefreshDay();

        PlayerStats stats = player.GetComponent<PlayerStats>() ??
                            player.GetComponentInParent<PlayerStats>();
        if (stats == null) return;

        if (usesToday >= UsesPerDay)
        {
            ShowMessage("La fuente ya fue utilizada tres veces hoy. " +
                        "Su energía volverá con el próximo día.");
            return;
        }

        if (stats.CurrentHealth >= stats.MaxHealth - .01f)
        {
            ShowMessage("Ya tienes toda la vida. La energía de la fuente no fue consumida.");
            return;
        }

        stats.Heal(stats.MaxHealth);
        usesToday++;
        ShowMessage("La fuente restauró el 100% de tu vida. " +
                    "Usos restantes hoy: " + (UsesPerDay - usesToday) + ".");
    }

    void FindClock()
    {
        dayNightCycle = FindAnyObjectByType<TenkokuDayNightCycle>();
    }

    Collider ResolvePlayerCollider(PlayerInteraction player)
    {
        if (cachedPlayer == player && cachedPlayerCollider != null)
            return cachedPlayerCollider;

        cachedPlayer = player;
        cachedPlayerCollider = player.GetComponent<CharacterController>();
        if (cachedPlayerCollider == null)
            cachedPlayerCollider = player.GetComponent<Collider>();
        if (cachedPlayerCollider == null)
            cachedPlayerCollider = player.GetComponentInChildren<CapsuleCollider>();
        return cachedPlayerCollider;
    }

    void RefreshDay()
    {
        if (dayNightCycle == null) FindClock();
        int currentDay = dayNightCycle != null ? dayNightCycle.WorldDay : 0;
        if (trackedWorldDay == currentDay) return;
        trackedWorldDay = currentDay;
        usesToday = 0;
    }

    static void ShowMessage(string message)
    {
        MerchantDialoguePanel panel = MerchantDialoguePanel.EnsureRuntime();
        if (panel != null)
            panel.Show("Fuente de la aldea", message, "Cerrar", null);
    }
}

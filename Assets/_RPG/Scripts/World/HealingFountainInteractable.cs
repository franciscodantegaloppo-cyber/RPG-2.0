using UnityEngine;

public sealed class HealingFountainInteractable : MonoBehaviour, IInteractable
{
    const int UsesPerDay = 3;

    TenkokuDayNightCycle dayNightCycle;
    int trackedWorldDay = int.MinValue;
    int usesToday;

    void Awake()
    {
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
        return player != null;
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

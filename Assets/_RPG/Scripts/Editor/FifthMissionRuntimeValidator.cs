using UnityEditor;
using UnityEngine;

public static class FifthMissionRuntimeValidator
{
    [MenuItem("RPG/Quests/Validate Fifth Mission Runtime")]
    static void Validate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[FifthMissionValidation] Entr\u00e1 en Play Mode antes de ejecutar la validaci\u00f3n.");
            return;
        }

        QuestManager quests = QuestManager.Instance;
        InventoryManager inventory = InventoryManager.Instance;
        if (quests == null || inventory == null)
        {
            Debug.LogError("[FifthMissionValidation] Faltan QuestManager o InventoryManager.");
            return;
        }

        quests.TryGodJumpToPrimaryMission(5, out _);
        quests.StartFifthMission();
        Require(quests.MerchantIntroductionState == PrimaryQuestState.DefeatInsectoidCrabBoss,
            "La misi\u00f3n no avanz\u00f3 al objetivo del crab boss.");

        InsectoidCrabBossAI boss =
            Object.FindAnyObjectByType<InsectoidCrabBossAI>(FindObjectsInactive.Include);
        Require(boss != null, "No se encontr\u00f3 el Insectoid Crab Boss.");
        EnemyStats bossStats = boss.GetComponent<EnemyStats>();
        bossStats.TakeTrueDamage(bossStats.MaxHealth + 1f);
        Require(quests.MerchantIntroductionState == PrimaryQuestState.RetrieveDarkSaberChest,
            "La muerte del boss no liber\u00f3 el cofre.");

        FifthMissionSwordChest missionChest =
            Object.FindAnyObjectByType<FifthMissionSwordChest>(FindObjectsInactive.Include);
        Require(missionChest != null && missionChest.IsAvailable,
            "El cofre de la quinta misi\u00f3n no est\u00e1 disponible.");
        ChestContainer chest = missionChest.GetComponent<ChestContainer>();
        InventorySlot reward = chest.Slots.Find(slot =>
            slot?.instance?.template != null &&
            slot.instance.template.itemID == "dark_saber_reward");
        Require(reward != null && reward.instance.excellenceLevel >= 7,
            "El cofre no contiene el Sable Oscuro exe +7.");

        Require(inventory.AddItemInstance(reward.instance), "No se pudo transferir el sable al inventario.");
        chest.RemoveSlot(reward);
        Require(quests.MerchantIntroductionState == PrimaryQuestState.ReturnToNahueWithDarkSaber,
            "Tomar el sable no envi\u00f3 al jugador con Nahue.");
        Require(quests.EnchantDarkSaber(), "Nahue no pudo encantar el sable.");
        Require(reward.instance.darkEnergyEnchanted &&
                quests.MerchantIntroductionState == PrimaryQuestState.FifthQuestCompleted,
            "El encantamiento oscuro no se guard\u00f3 en el sable.");

        EquipmentManager.Instance?.Equip(reward.instance);
        Require(Object.FindAnyObjectByType<PlayerDarkEnergyAura>() != null,
            "El aura oscura del jugador no fue creada.");
        Debug.Log("[FifthMissionValidation] OK: boss, cofre, Sable Oscuro exe +7, Nahue, aura y energ\u00eda oscura validados.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new UnityException("[FifthMissionValidation] " + message);
    }
}

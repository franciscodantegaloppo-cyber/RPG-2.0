using UnityEngine;

// Own small local quest stage instead of extending the global QuestStage enum: Nahue's quest is
// independent of Tonio's skeleton/deer chain, it just waits for that chain to reach
// SkeletonReadyToTurnIn before it unlocks (see CanOfferQuest below).
public enum NahueStage { Locked, NotStarted, GoblinActive, GoblinReadyToTurnIn, Done }

public class NahueQuestGiver : MonoBehaviour, IInteractable
{
    const int EnchantmentDiamondCost = 10;
    [Header("Identidad")]
    [SerializeField] string npcName = "Elnahue";

    [Header("Mision (cazar goblins)")]
    [SerializeField] int goblinTarget = 8;
    [SerializeField] int goblinReward = 150;

    public NahueStage Stage { get; private set; } = NahueStage.Locked;
    public int GoblinKills { get; private set; }

    NPCWander wander;

    void Awake()
    {
        wander = GetComponent<NPCWander>();
        if (wander == null)
            wander = gameObject.AddComponent<NPCWander>();
        wander.Configure(speed: 0.65f, radius: 4f, step: 1.8f, minWait: 3f, maxWait: 6f);
        QuestNpcAttentionIcon icon = GetComponent<QuestNpcAttentionIcon>();
        if (icon == null) icon = gameObject.AddComponent<QuestNpcAttentionIcon>();
        icon.Configure(QuestNpcAttentionRole.Nahue);
    }

    void OnEnable() => EnemyStats.OnAnyDeath += HandleEnemyDeath;
    void OnDisable() => EnemyStats.OnAnyDeath -= HandleEnemyDeath;

    void Update()
    {
        // Not a per-frame poll target for CanOfferQuest - just keeps Stage in sync so the
        // interaction prompt reflects "locked" vs "have a quest" without requiring the player to
        // have already talked to Tonio first, in case they reach Nahue before Tonio's dialogue
        // event fires the transition.
        if (Stage == NahueStage.Locked && CanOfferQuest())
            Stage = NahueStage.NotStarted;
    }

    static bool CanOfferQuest()
    {
        QuestManager quests = QuestManager.Instance;
        return quests != null && quests.SkeletonState == QuestTaskState.Completed;
    }

    void HandleEnemyDeath(EnemyStats enemy)
    {
        if (Stage != NahueStage.GoblinActive || enemy == null)
            return;
        if (!enemy.gameObject.name.Contains("Goblin"))
            return;

        GoblinKills = Mathf.Min(GoblinKills + 1, goblinTarget);
        if (GoblinKills >= goblinTarget)
            Stage = NahueStage.GoblinReadyToTurnIn;
    }

    public string GetInteractionText() => "[E] Hablar con " + npcName;
    public bool CanInteract(PlayerInteraction player) => true;

    public void Interact(PlayerInteraction player)
    {
        wander?.PauseForInteraction();

        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);

        MerchantDialoguePanel panel = MerchantDialoguePanel.EnsureRuntime();
        if (panel == null)
        {
            wander?.ResumeWander();
            return;
        }

        QuestManager primary = QuestManager.Instance;
        // Reaching Nahue himself is authoritative. The original blood-trail end marker was
        // created at Nahue's old position while this NPC could keep wandering, so a player could
        // follow every stain, interact with the real NPC and still remain one state behind.
        // Advancing here also repairs already-running saves affected by that mismatch.
        if (primary != null &&
            primary.MerchantIntroductionState ==
                PrimaryQuestState.SeventhFollowBloodTrail)
            primary.ReachNahueThroughBloodTrail();

        if (primary != null &&
            primary.MerchantIntroductionState == PrimaryQuestState.SeventhTalkToNahue)
        {
            panel.Show(npcName,
                "Creo que estoy alucinando... Vi un monstruo gigante que rob\u00f3 el alma de Tonio. Desde entonces siento que algo dentro de m\u00ed intenta cambiar.",
                "Siguiente", () =>
                {
                    panel.Hide();
                    primary.BeginSeventhWindAftermath();
                    SeventhMissionSequence.Instance?.BeginNahueTransformation(this);
                });
            return;
        }
        if (primary != null &&
            primary.MerchantIntroductionState == PrimaryQuestState.ReturnToNahueWithDarkSaber)
        {
            ShowMissionEnchantmentOffer(panel, primary, player);
            return;
        }

        if (primary != null && primary.MerchantIntroductionState == PrimaryQuestState.TalkToNahueSecondQuest)
        {
            ShowSecondPrimaryDialogue(panel, primary);
            return;
        }

        if (primary != null &&
            primary.MerchantIntroductionState == PrimaryQuestState.FifthQuestCompleted &&
            EquipmentManager.Instance?.GetEquippedInstance(ItemType.Weapon) is ItemInstance offeredWeapon &&
            !offeredWeapon.darkEnergyEnchanted)
        {
            ShowGeneralEnchantmentOffer(panel, player);
            return;
        }

        switch (Stage)
        {
            case NahueStage.Locked:
                panel.Show(npcName,
                    "No tengo nada para ti todavia. Ayuda primero a Tonio con los esqueletos del cementerio.",
                    "Entendido",
                    () => wander?.ResumeWander());
                break;

            case NahueStage.NotStarted:
                panel.Show(npcName,
                    "Buen trabajo con los esqueletos. Ahora tengo un problema con goblins merodeando cerca del pueblo.\n\n" +
                    "Podrias eliminar a " + goblinTarget + " de ellos?",
                    "Aceptar mision",
                    () => { Stage = NahueStage.GoblinActive; GoblinKills = 0; wander?.ResumeWander(); });
                break;

            case NahueStage.GoblinActive:
                panel.Show(npcName,
                    "Llevas " + GoblinKills + "/" + goblinTarget + " goblins eliminados.\n\n" +
                    "Vuelve a hablarme cuando termines.",
                    "Entendido",
                    () => wander?.ResumeWander());
                break;

            case NahueStage.GoblinReadyToTurnIn:
                panel.Show(npcName,
                    "Excelente, el pueblo esta mas seguro gracias a ti.\n\n" +
                    "Aqui tienes tu recompensa: " + goblinReward + " monedas de oro.",
                    "Reclamar recompensa",
                    () =>
                    {
                        InventoryManager.Instance?.AddGold(goblinReward);
                        Stage = NahueStage.Done;
                        wander?.ResumeWander();
                    });
                break;

            default: // Done
                panel.Show(npcName,
                    "Gracias de nuevo por tu ayuda con los goblins.",
                    "Hasta luego",
                    () => wander?.ResumeWander());
                break;
        }
    }

    void ShowMissionEnchantmentOffer(MerchantDialoguePanel panel, QuestManager quests,
        PlayerInteraction player)
    {
        int diamonds = InventoryManager.Instance?.DiamondCount() ?? 0;
        panel.ShowChoices(npcName,
            "Esta misma energ\u00eda que me mantiene silente, silencia la bondad de los mortales, el orgullo y la valent\u00eda de algunos.\n\n" +
            "Puedo encantar el Sable Oscuro por " + EnchantmentDiamondCost +
            " diamantes. Tienes " + diamonds + ". \u00bfEst\u00e1s de acuerdo?",
            "Encantar - 10 diamantes", () =>
            {
                if (quests.EnchantDarkSaber(EnchantmentDiamondCost))
                {
                    DarkEnergyVfx.PlayEnchantBurst(
                        transform.position + Vector3.up * 1.15f, player.transform);
                    panel.Hide();
                    GameManager.Instance?.SetState(GameState.Exploration);
                    wander?.ResumeWander();
                    return;
                }

                panel.Show(npcName,
                    diamonds < EnchantmentDiamondCost
                        ? "No tienes suficientes diamantes. Necesitas 10 para realizar el encantamiento."
                        : "Debes traer contigo el Sable Oscuro para que pueda encantarlo.",
                    "Entendido", () => wander?.ResumeWander());
            },
            "Ahora no", () =>
            {
                panel.Hide();
                wander?.ResumeWander();
            });
    }

    void ShowGeneralEnchantmentOffer(MerchantDialoguePanel panel, PlayerInteraction player)
    {
        ItemInstance weapon = EquipmentManager.Instance?.GetEquippedInstance(ItemType.Weapon);
        if (weapon == null)
        {
            panel.Show(npcName, "Equipa el arma que deseas encantar y vuelve a hablar conmigo.",
                "Entendido", () => wander?.ResumeWander());
            return;
        }
        if (weapon.darkEnergyEnchanted)
        {
            panel.Show(npcName, weapon.DisplayName + " ya contiene energ\u00eda oscura.",
                "Entendido", () => wander?.ResumeWander());
            return;
        }

        int diamonds = InventoryManager.Instance?.DiamondCount() ?? 0;
        panel.ShowChoices(npcName,
            "Puedo envolver " + weapon.DisplayName +
            " con energ\u00eda oscura. El encantamiento cuesta " +
            EnchantmentDiamondCost + " diamantes. Tienes " + diamonds +
            ". \u00bfEst\u00e1s de acuerdo?",
            "Encantar - 10 diamantes", () =>
            {
                InventoryManager inventory = InventoryManager.Instance;
                if (inventory == null || inventory.DiamondCount() < EnchantmentDiamondCost)
                {
                    panel.Show(npcName,
                        "No tienes suficientes diamantes. Necesitas 10 para realizar el encantamiento.",
                        "Entendido", () => wander?.ResumeWander());
                    return;
                }
                if (!inventory.ConsumeDiamond(EnchantmentDiamondCost))
                    return;

                weapon.darkEnergyEnchanted = true;
                EquipmentManager.Instance?.RefreshEquippedWeaponVisual();
                inventory.NotifyInventoryChanged();
                DarkEnergyVfx.PlayEnchantBurst(
                    transform.position + Vector3.up * 1.15f, player.transform);
                panel.Hide();
                GameManager.Instance?.SetState(GameState.Exploration);
                wander?.ResumeWander();
            },
            "Cancelar", () =>
            {
                panel.Hide();
                wander?.ResumeWander();
            });
    }

    void ShowSecondPrimaryDialogue(MerchantDialoguePanel panel, QuestManager quests)
    {
        panel.Show(npcName,
            "Â¿Sabes lo que estoy viviendo? Â¿Lo que es tener la mirada perdida? Â¿Que la vida misma se presente ante mis ojos y de mÃ­ no exista respuesta?",
            "Siguiente", () => panel.Show("Frank",
                "El enclaustramiento mental puede tener muchÃ­simas etiologÃ­as. Pero todas comparten un mismo desenlace: la realidad deja de ser un hogar y se convierte en un eco. Los dÃ­as pasan, las personas hablan, el viento mueve los Ã¡rboles... y uno observa todo como si perteneciera a otro mundo. No porque el mundo haya cambiado, sino porque algo dentro de uno dejÃ³ de responder a su llamado.",
                "Siguiente", () => panel.Show(npcName,
                    "Blablabla... Por otro lado, Â¿por quÃ© hablas demasiado con Tonio? TÃº no conoces a Tonio. Es un dios en lamento. No porque gobierne sobre los hombres, sino porque carga un peso que ningÃºn hombre deberÃ­a soportar. Â¡El peso del dinero, jajaja! Necesito un favor de ti; si quieres, puedes persuadirlo para obtener un pedazo de la torta... al menos...",
                    "Siguiente", () => panel.Show("Frank",
                        "Realmente, quiÃ©n te entiende...",
                        "Siguiente", () => panel.Show(npcName,
                            "Â¿TÃº no eras el mÃ©dico?",
                            "Siguiente", () => panel.Show("Frank", "...", "Hablar con Tonio", () =>
                            {
                                quests.CompleteNahueSecondDialogue();
                                wander?.ResumeWander();
                            }))))));
    }
}

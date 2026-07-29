using UnityEngine;

public class TonioQuestGiver : MonoBehaviour, IInteractable
{
    [Header("Identidad")]
    [SerializeField] string npcName = "Tonio";
    NPCWander wander;
    TonioHouseRoutine houseRoutine;

    void Awake()
    {
        wander = GetComponent<NPCWander>();
        if (wander == null) wander = gameObject.AddComponent<NPCWander>();
        ConfigureIndoorController();
        wander.Configure(speed: .45f, radius: .75f, step: .45f, minWait: 3.5f, maxWait: 7f);
        wander.ConfigureReturnToSpawnChance(1f);
        // The generated interior floor is a walkable trigger, not a solid physical floor.
        // CharacterController gravity therefore sinks Tonio between ground corrections.
        // Keep the proven ground-projected movement and disable random wandering: the dedicated
        // house routine is now the only system allowed to move him.
        wander.UseTransformMovement(true);
        wander.SetMoving(false);
        houseRoutine = GetComponent<TonioHouseRoutine>();
        if (houseRoutine == null)
            houseRoutine = gameObject.AddComponent<TonioHouseRoutine>();
        EnsureInteractionTrigger();
        QuestNpcAttentionIcon icon = GetComponent<QuestNpcAttentionIcon>();
        if (icon == null) icon = gameObject.AddComponent<QuestNpcAttentionIcon>();
        icon.Configure(QuestNpcAttentionRole.Tonio);
    }

    public string GetInteractionText() => "[E] Hablar con " + npcName;
    public bool CanInteract(PlayerInteraction player) =>
        houseRoutine == null || !houseRoutine.IsRouteActive;

    public void Interact(PlayerInteraction player)
    {
        wander?.PauseForInteraction();
        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(dir);

        MerchantDialoguePanel panel = MerchantDialoguePanel.EnsureRuntime();
        QuestManager quests = QuestManager.Instance;
        if (panel == null || quests == null) { wander?.ResumeWander(); return; }

        if (quests.MerchantIntroductionState == PrimaryQuestState.SixthTalkToPetrifiedTonio)
        {
            SixthMissionSequence sequence = SixthMissionSequence.Instance;
            if (sequence != null)
                sequence.DiscoverPetrifiedVillage(transform, player.transform);
            else
                wander?.ResumeWander();
            return;
        }

        if (quests.MerchantIntroductionState == PrimaryQuestState.ReturnToTonioWithEquipment)
        {
            ShowGoblinPassageSpeechReadable(panel, quests);
            return;
        }

        if (quests.MerchantIntroductionState == PrimaryQuestState.ReturnToTonioAfterPassage)
        {
            panel.Show(npcName,
                "Señor, recuérdame y no permitas que olvide que existe esperanza en ti.",
                "Finalizar misión", () =>
                {
                    quests.CompleteFirstPrimaryQuest();
                    EndConversation();
                });
            return;
        }

        if (quests.MerchantIntroductionState == PrimaryQuestState.TalkToTonioAfterNahue)
        {
            ShowTonioAfterNahueReadable(panel, quests);
            return;
        }

        if (quests.MerchantIntroductionState == PrimaryQuestState.InvestigatePlagueTalkToTonio)
        {
            ShowPlagueInvestigationDialogue(panel, quests);
            return;
        }

        if (quests.MerchantIntroductionState == PrimaryQuestState.ReturnToTonioAfterTrial)
        {
            panel.Show(npcName,
                "Has enfrentado la peste en carne propia y regresaste con vida. Tu poder ya no es el de un novato.",
                "Recibir 5000 de experiencia", () =>
                {
                    quests.ClaimSecondPrimaryReward();
                    EndConversation();
                });
            return;
        }

        if (quests.MerchantIntroductionState == PrimaryQuestState.TalkToTonioFourthMission)
        {
            ShowFourthMissionDialogue(panel, quests);
            return;
        }

        if (quests.MerchantIntroductionState == PrimaryQuestState.ReturnToTonioWithDungeonKey)
        {
            panel.Show(npcName,
                "Conseguiste la llave. Has demostrado que puedes sobrevivir incluso entre los muertos.",
                "Recibir espada", () =>
                {
                    ItemData sword = Resources.Load<ItemData>("Items/TonioRewardSword");
                    quests.ClaimFourthMissionSword(sword);
                    EndConversation();
                });
            return;
        }

        if (quests.MerchantIntroductionState == PrimaryQuestState.TalkToTonioFifthMission)
        {
            panel.Show(npcName,
                "Creo que m\u00e1s all\u00e1 del pasaje goblin el herrero olvid\u00f3 una de sus espadas.",
                "Siguiente", () => panel.Show(npcName,
                    "La eliminaci\u00f3n de la peste no puede esperar.",
                    "Buscar la espada", () =>
                    {
                        quests.StartFifthMission();
                        EndConversation();
                    }));
            return;
        }

        panel.ShowChoices(npcName, "¿De qué quieres hablar?", "Cuéntame sobre esto que sucede",
            () => ShowWhatIsHappening(panel), "¿Quieres trabajo extra?", () => ShowExtraWork(panel, quests));
    }

    void ShowTonioAfterNahue(MerchantDialoguePanel panel, QuestManager quests)
    {
        panel.Show(npcName,
            "Â¡Ahh! Â¡Conociste al gran Nahue! Hay quienes luchan contra monstruos, pero Ã©l lucha contra el eco de su propia mente.",
            "Continuar el diÃ¡logo", () => panel.Show("Frank",
                "Â¡Jaja! Tienes razÃ³n. Tal vez por eso inspira temor, porque demuestra que un hombre puede seguir de pie incluso cuando hace mucho tiempo dejÃ³ de creer que el mundo fuera capaz de responderle.",
                "Siguiente", () => panel.Show(npcName,
                    "Â¡AsÃ­ es! Como Ã©l hay muchos, y debo adjudicarlo a la peste que antes te mencionÃ©. Me gustarÃ­a que comenzaras a forjar tus armas. Ve a la mesa de crafteo y aprende a utilizarla.",
                    "Ir a la mesa", () =>
                    {
                        quests.StartCraftingTraining();
                        EndConversation();
                    })));
    }

    void ShowPlagueInvestigationDialogue(MerchantDialoguePanel panel, QuestManager quests)
    {
        panel.Show(npcName, "Â¿CÃ³mo te ha ido?", "Siguiente", () => panel.Show("Frank",
            "Â¡Quiero que me hables sobre la peste!", "Siguiente", () => panel.Show(npcName,
                "Veo que te ha ido bien y has aumentado tu poder...", "Siguiente", () => panel.Show("Frank",
                    "Deja de dar vueltas...", "Siguiente", () => panel.Show(npcName,
                        "Necesito que comiences experimentÃ¡ndolo en carne propia. DirÃ­gete hacia la estatua a las afueras del pueblo.",
                        "Ir a la estatua", () =>
                        {
                            quests.SendPlayerToKingGoblinStatue();
                            EndConversation();
                        })))));
    }

    void ShowGoblinPassageSpeech(MerchantDialoguePanel panel, QuestManager quests)
    {
        panel.Show(npcName,
            "Cuántas veces, Señor, me encuentro abatido, ahogado en una desesperanza que no me deja avanzar hacia donde debo ir, hacia ti, hacia la santidad que es la eternidad junto a ti.",
            "Siguiente", () => panel.Show(npcName,
                "Muchos son los motivos que tengo para sufrir, pero mayores son los que tengo para culminar con esta peste: los malditos ¡Goblins!",
                "Siguiente", () => panel.Show(npcName,
                    "Necesito que consigas las runas y diamantes que esconden al final del pasaje.",
                    "Aceptar misión", () =>
                    {
                        quests.StartGoblinPassageInvestigation();
                        EndConversation();
                    })));
    }

    void ShowWhatIsHappeningLegacy(MerchantDialoguePanel panel)
    {
        panel.Show(npcName,
            "Te juro Frank que estar demasiado consciente es una enfermedad, una verdadera auténtica enfermedad.",
            "Siguiente", () => panel.Show(npcName,
                "Es como si me hubieran cortado con tijeras y separado de todo el mundo. Si mañana me mataran, no me importaría lo más mínimo.",
                "Cerrar", EndConversation));
    }

    void ShowExtraWork(MerchantDialoguePanel panel, QuestManager quests)
    {
        panel.ShowChoices(npcName,
            "Tengo dos trabajos opcionales. Puedes aceptar uno o ambos; no bloquean la misión principal.",
            "Cazar 100 esqueletos", () => ShowSkeletonJob(panel, quests),
            "Cazar 3 ciervos", () => ShowDeerJob(panel, quests));
    }

    void ShowSkeletonJob(MerchantDialoguePanel panel, QuestManager quests)
    {
        switch (quests.SkeletonState)
        {
            case QuestTaskState.Available:
                panel.Show(npcName,
                    "Los esqueletos del cementerio nos tienen aterrorizados. Elimina a 100.\n\n" +
                    "Trabajo secundario opcional. Recompensa: " + QuestManager.SkeletonReward + " monedas.",
                    "Aceptar trabajo", () => { quests.StartSkeletonQuest(); EndConversation(); });
                break;
            case QuestTaskState.Active:
                panel.Show(npcName, "Llevas " + quests.SkeletonKills + "/" + QuestManager.SkeletonTarget +
                    " esqueletos eliminados.", "Entendido", EndConversation);
                break;
            case QuestTaskState.ReadyToTurnIn:
                panel.Show(npcName, "Excelente trabajo: 100 esqueletos eliminados.\n\nRecompensa: " +
                    QuestManager.SkeletonReward + " monedas.", "Reclamar recompensa",
                    () => { quests.ClaimSkeletonReward(); EndConversation(); });
                break;
            default:
                panel.Show(npcName, "Ya completaste el trabajo de los esqueletos.", "Cerrar", EndConversation);
                break;
        }
    }

    void ShowDeerJob(MerchantDialoguePanel panel, QuestManager quests)
    {
        switch (quests.DeerState)
        {
            case QuestTaskState.Available:
                panel.Show(npcName,
                    "Hay demasiados ciervos comiéndose las cosechas. Caza a 3.\n\n" +
                    "Trabajo secundario opcional. Recompensa: " + QuestManager.DeerReward + " monedas.",
                    "Aceptar trabajo", () => { quests.StartDeerQuest(); EndConversation(); });
                break;
            case QuestTaskState.Active:
                panel.Show(npcName, "Llevas " + quests.DeerKills + "/" + QuestManager.DeerTarget +
                    " ciervos cazados.", "Entendido", EndConversation);
                break;
            case QuestTaskState.ReadyToTurnIn:
                panel.Show(npcName, "Perfecto, ya no tenemos que preocuparnos por los ciervos.\n\nRecompensa: " +
                    QuestManager.DeerReward + " monedas.", "Reclamar recompensa",
                    () => { quests.ClaimDeerReward(); EndConversation(); });
                break;
            default:
                panel.Show(npcName, "Ya completaste el trabajo de los ciervos.", "Cerrar", EndConversation);
                break;
        }
    }

    void ShowTonioAfterNahueReadable(MerchantDialoguePanel panel, QuestManager quests)
    {
        string[] speakers = { npcName, "Frank", "Frank", npcName, npcName };
        string[] lines =
        {
            "\u00a1Ahh! \u00a1Conociste al gran Nahue! Hay quienes luchan contra monstruos, pero \u00e9l lucha contra el eco de su propia mente.",
            "\u00a1Jaja! Tienes raz\u00f3n. Tal vez por eso inspira temor.",
            "Demuestra que un hombre puede seguir de pie incluso cuando hace mucho tiempo dej\u00f3 de creer que el mundo fuera capaz de responderle.",
            "\u00a1As\u00ed es! Como \u00e9l hay muchos, y debo adjudicarlo a la peste que antes te mencion\u00e9.",
            "Me gustar\u00eda que comenzaras a forjar tus armas. Ve a la mesa de crafteo y aprende a utilizarla."
        };
        ShowLinearSequence(panel, speakers, lines, 0, "Ir a la mesa", () =>
        {
            quests.StartCraftingTraining();
            EndConversation();
        });
    }

    void ShowGoblinPassageSpeechReadable(MerchantDialoguePanel panel, QuestManager quests)
    {
        string[] speakers = { npcName, npcName, npcName, npcName, npcName };
        string[] lines =
        {
            "Cu\u00e1ntas veces, Se\u00f1or, me encuentro abatido...",
            "Ahogado en una desesperanza que no me deja avanzar hacia donde debo ir, hacia ti, hacia la santidad que es la eternidad junto a ti.",
            "Muchos son los motivos que tengo para sufrir.",
            "Pero mayores son los que tengo para culminar con esta peste: los malditos \u00a1Goblins!",
            "Necesito que consigas las runas y diamantes que esconden al final del pasaje."
        };
        ShowLinearSequence(panel, speakers, lines, 0, "Aceptar misi\u00f3n", () =>
        {
            quests.StartGoblinPassageInvestigation();
            EndConversation();
        });
    }

    void ShowWhatIsHappening(MerchantDialoguePanel panel)
    {
        string[] speakers = { npcName, npcName, npcName, npcName };
        string[] lines =
        {
            "Te juro, Frank, que estar demasiado consciente es una enfermedad.",
            "Una verdadera, aut\u00e9ntica enfermedad.",
            "Es como si me hubieran cortado con tijeras y separado de todo el mundo.",
            "Si ma\u00f1ana me mataran, no me importar\u00eda lo m\u00e1s m\u00ednimo."
        };
        ShowLinearSequence(panel, speakers, lines, 0, "Cerrar", EndConversation);
    }

    void ShowLinearSequence(MerchantDialoguePanel panel, string[] speakers, string[] lines,
        int index, string finalButton, System.Action onFinished)
    {
        bool last = index >= lines.Length - 1;
        panel.Show(speakers[index], lines[index], last ? finalButton : "Siguiente", () =>
        {
            if (last) onFinished?.Invoke();
            else ShowLinearSequence(panel, speakers, lines, index + 1, finalButton, onFinished);
        });
    }

    void ShowFourthMissionDialogue(MerchantDialoguePanel panel, QuestManager quests)
    {
        string[] speakers = { npcName, "Frank", "Frank", npcName, npcName };
        string[] lines =
        {
            "Dime, \u00bfqu\u00e9 has encontrado?",
            "La estatua tiene tres relieves en su corona que interpreto que ser\u00e1n diamantes.",
            "\u00bfD\u00f3nde puedo conseguirlos?",
            "Ve al cementerio. Necesito que elimines un esqueleto de rango elite para obtener la llave del calabozo.",
            "Pero cuidado: debes hacerlo de noche."
        };
        ShowLinearSequence(panel, speakers, lines, 0, "Ir al cementerio", () =>
        {
            quests.StartEliteSkeletonHunt();
            EndConversation();
        });
    }

    void EndConversation() => wander?.ResumeWander();

    void ConfigureIndoorController()
    {
        CharacterController controller = GetComponent<CharacterController>();
        if (controller == null) return;
        controller.radius = Mathf.Min(controller.radius, .24f);
        controller.skinWidth = Mathf.Max(controller.skinWidth, .04f);
        controller.stepOffset = Mathf.Min(Mathf.Max(controller.stepOffset, .22f), .35f);
        controller.slopeLimit = Mathf.Max(controller.slopeLimit, 50f);
    }

    void EnsureInteractionTrigger()
    {
        CapsuleCollider trigger = GetComponent<CapsuleCollider>();
        if (trigger == null) trigger = gameObject.AddComponent<CapsuleCollider>();
        trigger.isTrigger = true;
        trigger.radius = .75f;
        trigger.height = 2f;
        trigger.center = new Vector3(0f, 1f, 0f);
    }
}

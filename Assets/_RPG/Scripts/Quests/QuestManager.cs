using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Kept for compatibility with older NPC gates. New quest UI uses the independent states below.
public enum QuestStage
{
    NotStarted, SkeletonActive, SkeletonReadyToTurnIn,
    DeerOffered, DeerActive, DeerReadyToTurnIn, AllDone
}

public enum QuestTaskState { Available, Active, ReadyToTurnIn, Completed }
public enum PrimaryQuestState
{
    GetNoviceEquipment,
    ReturnToTonioWithEquipment,
    InvestigateGoblinPassage,
    LootGoblinPassageChest,
    ReturnToTonioAfterPassage,
    TalkToNahueSecondQuest,
    TalkToTonioAfterNahue,
    UpgradeWeaponAtCraftingTable,
    PostCraftReflection,
    InvestigatePlagueTalkToTonio,
    GoToKingGoblinStatue,
    StatueTrialActive,
    ReturnToTonioAfterTrial,
    SecondQuestCompleted,
    ThirdMissionReflectionPending,
    ReturnToStatueForReliefs,
    TalkToTonioFourthMission,
    HuntEliteSkeletonAtNight,
    ReturnToTonioWithDungeonKey,
    FourthQuestCompleted,
    TalkToTonioFifthMission,
    RetrieveForgottenSwordChest,
    FifthQuestCompleted,
    DefeatInsectoidCrabBoss,
    RetrieveDarkSaberChest,
    ReturnToNahueWithDarkSaber,
    SixthMissionWarningPending,
    SixthTalkToPetrifiedTonio,
    SixthFindRedDiamond,
    SixthQuestCompleted,
    SeventhUseRedDiamond,
    SeventhFindTonio,
    SeventhFollowBloodTrail,
    SeventhTalkToNahue,
    SeventhWindAftermath,
    SeventhQuestCompleted,
    // Appended to preserve serialized numeric values from existing save games.
    SeventhHuntDemonAnomaly
}

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    public const int SkeletonTarget = 100;
    public const int DeerTarget = 3;
    public const int SkeletonReward = 100;
    public const int DeerReward = 200;

    public QuestTaskState SkeletonState { get; private set; } = QuestTaskState.Available;
    public QuestTaskState DeerState { get; private set; } = QuestTaskState.Available;
    public PrimaryQuestState MerchantIntroductionState { get; private set; } = PrimaryQuestState.GetNoviceEquipment;
    public bool HasDungeonKey { get; private set; }
    public int SkeletonKills { get; private set; }
    public int DeerKills { get; private set; }

    readonly List<GoblinSpawner> pausedGoblinSpawners = new List<GoblinSpawner>();
    Coroutine goblinSpawnerPauseRoutine;
    float goblinSpawnersResumeAt;

    // Compatibility for Nahue and any old scene logic that still reads the original sequence.
    public QuestStage Stage
    {
        get
        {
            if (SkeletonState == QuestTaskState.Active) return QuestStage.SkeletonActive;
            if (SkeletonState == QuestTaskState.ReadyToTurnIn) return QuestStage.SkeletonReadyToTurnIn;
            if (SkeletonState != QuestTaskState.Completed) return QuestStage.NotStarted;
            if (DeerState == QuestTaskState.Active) return QuestStage.DeerActive;
            if (DeerState == QuestTaskState.ReadyToTurnIn) return QuestStage.DeerReadyToTurnIn;
            if (DeerState == QuestTaskState.Completed) return QuestStage.AllDone;
            return QuestStage.DeerOffered;
        }
    }

    public event System.Action OnQuestChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("QuestManager").AddComponent<QuestManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable() => EnemyStats.OnAnyDeath += HandleEnemyDeath;
    void OnDisable() => EnemyStats.OnAnyDeath -= HandleEnemyDeath;

    void HandleEnemyDeath(EnemyStats enemy)
    {
        if (enemy == null) return;
        bool changed = false;

        if (SkeletonState == QuestTaskState.Active && enemy.Type == CreatureType.Undead)
        {
            SkeletonKills = Mathf.Min(SkeletonKills + 1, SkeletonTarget);
            if (SkeletonKills >= SkeletonTarget) SkeletonState = QuestTaskState.ReadyToTurnIn;
            changed = true;
        }

        if (DeerState == QuestTaskState.Active)
        {
            AnimalAI animal = enemy.GetComponent<AnimalAI>();
            if (animal != null && animal.Kind == AnimalKind.Deer)
            {
                DeerKills = Mathf.Min(DeerKills + 1, DeerTarget);
                if (DeerKills >= DeerTarget) DeerState = QuestTaskState.ReadyToTurnIn;
                changed = true;
            }
        }

        if (MerchantIntroductionState == PrimaryQuestState.DefeatInsectoidCrabBoss &&
            enemy.GetComponent<InsectoidCrabBossAI>() != null)
        {
            MerchantIntroductionState = PrimaryQuestState.RetrieveDarkSaberChest;
            changed = true;
        }

        if (changed) OnQuestChanged?.Invoke();
    }

    public void CompleteMerchantIntroduction()
    {
        if (MerchantIntroductionState != PrimaryQuestState.GetNoviceEquipment) return;
        MerchantIntroductionState = PrimaryQuestState.ReturnToTonioWithEquipment;
        OnQuestChanged?.Invoke();
    }

    public void StartGoblinPassageInvestigation()
    {
        if (MerchantIntroductionState != PrimaryQuestState.ReturnToTonioWithEquipment) return;
        MerchantIntroductionState = PrimaryQuestState.InvestigateGoblinPassage;
        OnQuestChanged?.Invoke();
        GoblinPassageIntroCinematic.PlayEntranceReveal();
    }

    public void ReachGoblinPassageEnd()
    {
        if (MerchantIntroductionState != PrimaryQuestState.InvestigateGoblinPassage) return;
        MerchantIntroductionState = PrimaryQuestState.LootGoblinPassageChest;
        OnQuestChanged?.Invoke();
    }

    public void NotifyGoblinChestLootTaken()
    {
        if (MerchantIntroductionState != PrimaryQuestState.LootGoblinPassageChest) return;
        MerchantIntroductionState = PrimaryQuestState.ReturnToTonioAfterPassage;
        SuspendGoblinSpawners(300f);
        OnQuestChanged?.Invoke();
    }

    public void CompleteFirstPrimaryQuest()
    {
        if (MerchantIntroductionState != PrimaryQuestState.ReturnToTonioAfterPassage) return;
        MerchantIntroductionState = PrimaryQuestState.TalkToNahueSecondQuest;
        PlayerStats player = FindAnyObjectByType<PlayerStats>();
        if (player != null) player.AddExperience(1000f);
        SuspendGoblinSpawners(300f);
        OnQuestChanged?.Invoke();
    }

    public void CompleteNahueSecondDialogue()
    {
        SetPrimaryState(PrimaryQuestState.TalkToNahueSecondQuest,
            PrimaryQuestState.TalkToTonioAfterNahue);
    }

    public void StartCraftingTraining()
    {
        SetPrimaryState(PrimaryQuestState.TalkToTonioAfterNahue,
            PrimaryQuestState.UpgradeWeaponAtCraftingTable);
    }

    public void NotifyWeaponCraftedAtTable()
    {
        if (MerchantIntroductionState != PrimaryQuestState.UpgradeWeaponAtCraftingTable) return;
        MerchantIntroductionState = PrimaryQuestState.PostCraftReflection;
        OnQuestChanged?.Invoke();
        StartCoroutine(ShowPostCraftReflection());
    }

    IEnumerator ShowPostCraftReflection()
    {
        yield return new WaitForSeconds(10f);
        if (MerchantIntroductionState != PrimaryQuestState.PostCraftReflection) yield break;

        PlayerStats player = FindAnyObjectByType<PlayerStats>();
        if (player != null)
            QuestOverheadThought.Show(player.transform,
                "Siento a\u00fan m\u00e1s poder... pero \u00bfqu\u00e9 es esa peste de la que tanto me habla? Los goblins eran f\u00e1ciles. Debo investigar a\u00fan m\u00e1s...",
                7f);

        MerchantIntroductionState = PrimaryQuestState.InvestigatePlagueTalkToTonio;
        OnQuestChanged?.Invoke();
    }

    public void SendPlayerToKingGoblinStatue()
    {
        SetPrimaryState(PrimaryQuestState.InvestigatePlagueTalkToTonio,
            PrimaryQuestState.GoToKingGoblinStatue);
    }

    public bool BeginStatueTrial()
    {
        if (MerchantIntroductionState != PrimaryQuestState.GoToKingGoblinStatue) return false;
        MerchantIntroductionState = PrimaryQuestState.StatueTrialActive;
        OnQuestChanged?.Invoke();
        return true;
    }

    public void FailStatueTrial()
    {
        SetPrimaryState(PrimaryQuestState.StatueTrialActive,
            PrimaryQuestState.GoToKingGoblinStatue);
    }

    public void CompleteStatueTrial()
    {
        SetPrimaryState(PrimaryQuestState.StatueTrialActive,
            PrimaryQuestState.ReturnToTonioAfterTrial);
    }

    public void ClaimSecondPrimaryReward()
    {
        if (MerchantIntroductionState != PrimaryQuestState.ReturnToTonioAfterTrial) return;
        MerchantIntroductionState = PrimaryQuestState.ThirdMissionReflectionPending;
        PlayerStats player = FindAnyObjectByType<PlayerStats>();
        if (player != null) player.AddExperience(5000f);
        OnQuestChanged?.Invoke();
        StartCoroutine(BeginThirdMissionAfterDelay());
    }

    IEnumerator BeginThirdMissionAfterDelay()
    {
        yield return new WaitForSeconds(10f);
        if (MerchantIntroductionState != PrimaryQuestState.ThirdMissionReflectionPending) yield break;
        MerchantDialoguePanel panel = MerchantDialoguePanel.EnsureRuntime();
        if (panel == null) yield break;
        panel.Show("Frank", "No entiendo lo que est\u00e1 sucediendo...", "Siguiente", () =>
            panel.Show("Frank", "Volver\u00e9 a revisar esa estatua.", "Ir a la estatua", () =>
            {
                MerchantIntroductionState = PrimaryQuestState.ReturnToStatueForReliefs;
                OnQuestChanged?.Invoke();
            }));
    }

    public void NotifyStatueReliefsInspected()
    {
        SetPrimaryState(PrimaryQuestState.ReturnToStatueForReliefs,
            PrimaryQuestState.TalkToTonioFourthMission);
    }

    public void StartEliteSkeletonHunt()
    {
        SetPrimaryState(PrimaryQuestState.TalkToTonioFourthMission,
            PrimaryQuestState.HuntEliteSkeletonAtNight);
    }

    public void NotifyDungeonKeyTaken()
    {
        if (MerchantIntroductionState != PrimaryQuestState.HuntEliteSkeletonAtNight) return;
        HasDungeonKey = true;
        MerchantIntroductionState = PrimaryQuestState.ReturnToTonioWithDungeonKey;
        OnQuestChanged?.Invoke();
    }

    public void ClaimFourthMissionSword(ItemData sword)
    {
        if (MerchantIntroductionState != PrimaryQuestState.ReturnToTonioWithDungeonKey) return;
        if (sword != null && InventoryManager.Instance != null)
            InventoryManager.Instance.AddItemInstance(new ItemInstance(sword));
        MerchantIntroductionState = PrimaryQuestState.TalkToTonioFifthMission;
        OnQuestChanged?.Invoke();
    }

    public void StartFifthMission()
    {
        SetPrimaryState(PrimaryQuestState.TalkToTonioFifthMission,
            PrimaryQuestState.DefeatInsectoidCrabBoss);
        FifthMissionPassageCollapse.CollapseNow();
    }

    public void NotifyDarkSaberTaken()
    {
        if (MerchantIntroductionState == PrimaryQuestState.RetrieveDarkSaberChest ||
            MerchantIntroductionState == PrimaryQuestState.RetrieveForgottenSwordChest)
            SetPrimaryState(MerchantIntroductionState,
                PrimaryQuestState.ReturnToNahueWithDarkSaber);
    }

    public bool EnchantDarkSaber()
    {
        if (MerchantIntroductionState != PrimaryQuestState.ReturnToNahueWithDarkSaber)
            return false;

        ItemInstance saber = FindOwnedDarkSaber();
        if (saber == null)
            return false;

        saber.darkEnergyEnchanted = true;
        EquipmentManager.Instance?.RefreshEquippedWeaponVisual();
        InventoryManager.Instance?.NotifyInventoryChanged();
        SetPrimaryState(PrimaryQuestState.ReturnToNahueWithDarkSaber,
            PrimaryQuestState.SixthMissionWarningPending);
        return true;
    }

    public bool EnchantDarkSaber(int diamondCost)
    {
        if (MerchantIntroductionState != PrimaryQuestState.ReturnToNahueWithDarkSaber)
            return false;
        ItemInstance saber = FindOwnedDarkSaber();
        if (saber == null || saber.darkEnergyEnchanted)
            return false;
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null || inventory.DiamondCount() < diamondCost ||
            !inventory.ConsumeDiamond(diamondCost))
            return false;
        return EnchantDarkSaber();
    }

    // Compatibility entry point for older scene objects.
    public void CompleteFifthMission()
    {
        NotifyDarkSaberTaken();
    }

    ItemInstance FindOwnedDarkSaber()
    {
        ItemInstance equipped = EquipmentManager.Instance?.GetEquippedInstance(ItemType.Weapon);
        if (IsDarkSaber(equipped))
            return equipped;

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
            return null;
        foreach (InventorySlot slot in inventory.Slots)
            if (slot != null && IsDarkSaber(slot.instance))
                return slot.instance;
        return null;
    }

    static bool IsDarkSaber(ItemInstance instance) =>
        instance?.template != null && instance.template.itemID == "dark_saber_reward";

    public bool IsDarkSaberEquipped()
    {
        return IsDarkSaber(EquipmentManager.Instance?.GetEquippedInstance(ItemType.Weapon));
    }

    public bool IsSixthMissionActive =>
        MerchantIntroductionState == PrimaryQuestState.SixthMissionWarningPending ||
        MerchantIntroductionState == PrimaryQuestState.SixthTalkToPetrifiedTonio ||
        MerchantIntroductionState == PrimaryQuestState.SixthFindRedDiamond;

    public void BeginSixthTonioObjective()
    {
        SetPrimaryState(PrimaryQuestState.SixthMissionWarningPending,
            PrimaryQuestState.SixthTalkToPetrifiedTonio);
    }

    public void BeginRedDiamondSearch()
    {
        SetPrimaryState(PrimaryQuestState.SixthTalkToPetrifiedTonio,
            PrimaryQuestState.SixthFindRedDiamond);
    }

    public void NotifyRedDiamondTaken()
    {
        SetPrimaryState(PrimaryQuestState.SixthFindRedDiamond,
            PrimaryQuestState.SixthQuestCompleted);
    }

    public void NotifyCrabDemonDefeatedWithRedDiamond()
    {
        if (MerchantIntroductionState != PrimaryQuestState.SixthQuestCompleted ||
            !HasInventoryItem("red_diamond_quest")) return;
        MerchantIntroductionState = PrimaryQuestState.SeventhUseRedDiamond;
        OnQuestChanged?.Invoke();
    }

    public bool UseRedDiamondForFireball()
    {
        if (MerchantIntroductionState != PrimaryQuestState.SeventhUseRedDiamond ||
            !HasInventoryItem("red_diamond_quest")) return false;
        PlayerStats player = FindAnyObjectByType<PlayerStats>();
        SkillTreeProgress tree = player != null
            ? player.GetComponent<SkillTreeProgress>() ??
              player.gameObject.AddComponent<SkillTreeProgress>()
            : null;
        tree?.GrantLevel("fire", 1);
        MerchantIntroductionState = PrimaryQuestState.SeventhFindTonio;
        OnQuestChanged?.Invoke();
        SeventhMissionSequence.Instance?.BeginTonioSearch();
        return true;
    }

    public void DiscoverBloodTrail()
    {
        SetPrimaryState(PrimaryQuestState.SeventhFindTonio,
            PrimaryQuestState.SeventhFollowBloodTrail);
    }

    public void ReachNahueThroughBloodTrail()
    {
        if (MerchantIntroductionState != PrimaryQuestState.SeventhFollowBloodTrail)
            return;
        MerchantIntroductionState = PrimaryQuestState.SeventhTalkToNahue;
        OnQuestChanged?.Invoke();
    }

    public void BeginSeventhWindAftermath()
    {
        SetPrimaryState(PrimaryQuestState.SeventhTalkToNahue,
            PrimaryQuestState.SeventhWindAftermath);
    }

    public void CompleteSeventhMission()
    {
        SetPrimaryState(PrimaryQuestState.SeventhHuntDemonAnomaly,
            PrimaryQuestState.SeventhQuestCompleted);
    }

    public void BeginSeventhAnomalyHunt()
    {
        SetPrimaryState(PrimaryQuestState.SeventhWindAftermath,
            PrimaryQuestState.SeventhHuntDemonAnomaly);
    }

    bool HasInventoryItem(string itemId)
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null) return false;
        foreach (InventorySlot slot in inventory.Slots)
            if (slot?.item != null && slot.item.itemID == itemId && slot.quantity > 0)
                return true;
        return false;
    }

    void SetPrimaryState(PrimaryQuestState expected, PrimaryQuestState next)
    {
        if (MerchantIntroductionState != expected) return;
        MerchantIntroductionState = next;
        OnQuestChanged?.Invoke();
    }

    // Debug navigation for god mode. It intentionally changes quest progression only: inventory,
    // equipment and already-earned rewards stay untouched so testing a later mission is safe.
    public bool TryGodJumpToPrimaryMission(int missionNumber, out string missionName)
    {
        PrimaryQuestState target;
        switch (missionNumber)
        {
            case 1:
                target = PrimaryQuestState.GetNoviceEquipment;
                missionName = "El primer equipamiento";
                break;
            case 2:
                target = PrimaryQuestState.TalkToNahueSecondQuest;
                missionName = "El origen de la peste";
                break;
            case 3:
                target = PrimaryQuestState.ThirdMissionReflectionPending;
                missionName = "Los relieves de la estatua";
                break;
            case 4:
                target = PrimaryQuestState.TalkToTonioFourthMission;
                missionName = "La llave del calabozo";
                break;
            case 5:
                target = PrimaryQuestState.TalkToTonioFifthMission;
                missionName = "La espada olvidada";
                break;
            case 6:
                // Mission 5 has already ended at this point: the crab was defeated
                // and its chest delivered the Dark Saber. Mission 6 begins by taking
                // that still-unenchanted weapon to Nahue.
                if (!PrepareMissionSixDarkSaber())
                {
                    missionName =
                        "No se pudo entregar el Sable Oscuro: libera un espacio del inventario.";
                    return false;
                }
                target = PrimaryQuestState.ReturnToNahueWithDarkSaber;
                missionName = "Encantar el Sable Oscuro con Nahue";
                break;
            case 7:
                if (!HasInventoryItem("red_diamond_quest"))
                {
                    ItemData diamond = Resources.Load<ItemData>("Items/RedDiamond");
                    if (diamond == null || InventoryManager.Instance == null ||
                        !InventoryManager.Instance.AddItem(diamond, 1))
                    {
                        missionName = "No se pudo entregar el Diamante Rojo.";
                        return false;
                    }
                }
                // Jumping to mission seven represents the completed red-diamond encounter:
                // the real pickup is already owned and the Crab Demon has already died.
                // Close any encounter that happened to be running during a debug test so its
                // boss, forced night, fog and wind cannot leak into the next mission.
                SixthMissionSequence.Instance?.
                    CompleteRedDiamondEncounterForMissionJump();
                target = PrimaryQuestState.SeventhUseRedDiamond;
                missionName = "El alma robada";
                break;
            default:
                missionName = null;
                return false;
        }

        KingGoblinPlagueStatue statue = FindAnyObjectByType<KingGoblinPlagueStatue>(FindObjectsInactive.Include);
        statue?.CancelTrialForMissionJump();
        MerchantDialoguePanel.Instance?.Hide();
        MerchantIntroductionState = target;
        if (missionNumber <= 4) HasDungeonKey = false;
        OnQuestChanged?.Invoke();
        if (missionNumber == 3) StartCoroutine(BeginThirdMissionAfterDelay());
        if (missionNumber == 7) SeventhMissionSequence.Instance?.PrepareMissionSeven();
        return true;
    }

    bool PrepareMissionSixDarkSaber()
    {
        ItemInstance saber = FindOwnedDarkSaber();
        if (saber == null)
        {
            ItemData template = null;
            foreach (ItemData item in Resources.LoadAll<ItemData>("Items"))
            {
                if (item != null && item.itemID == "dark_saber_reward")
                {
                    template = item;
                    break;
                }
            }

            if (template == null || InventoryManager.Instance == null)
                return false;

            saber = new ItemInstance(template)
            {
                excellenceLevel = 7,
                extraAttackPercent = 70f,
                darkEnergyEnchanted = false
            };
            if (!InventoryManager.Instance.AddItemInstance(saber))
                return false;
        }
        else
        {
            // Make the debug jump deterministic even if a previous test had already
            // enchanted or partially modified this same reward.
            saber.excellenceLevel = Mathf.Max(7, saber.excellenceLevel);
            saber.extraAttackPercent = Mathf.Max(70f, saber.extraAttackPercent);
            saber.darkEnergyEnchanted = false;
            InventoryManager.Instance?.NotifyInventoryChanged();
        }

        EquipmentManager.Instance?.RefreshEquippedWeaponVisual();
        FifthMissionPassageCollapse.CollapseNow();
        return true;
    }

    void SuspendGoblinSpawners(float duration)
    {
        goblinSpawnersResumeAt = Mathf.Max(goblinSpawnersResumeAt, Time.time + duration);
        GoblinSpawner[] found = FindObjectsByType<GoblinSpawner>(FindObjectsInactive.Include);
        foreach (GoblinSpawner spawner in found)
        {
            if (spawner == null || pausedGoblinSpawners.Contains(spawner)) continue;
            if (!spawner.enabled) continue;
            pausedGoblinSpawners.Add(spawner);
            spawner.enabled = false;
        }

        if (goblinSpawnerPauseRoutine == null)
            goblinSpawnerPauseRoutine = StartCoroutine(ResumeGoblinSpawnersWhenReady());

        Debug.Log("QuestManager: " + pausedGoblinSpawners.Count +
                  " spawners goblin suspendidos. Reactivación prevista dentro de 5 minutos.");
    }

    IEnumerator ResumeGoblinSpawnersWhenReady()
    {
        while (Time.time < goblinSpawnersResumeAt)
            yield return new WaitForSeconds(Mathf.Max(.1f, goblinSpawnersResumeAt - Time.time));

        foreach (GoblinSpawner spawner in pausedGoblinSpawners)
            if (spawner != null) spawner.enabled = true;

        pausedGoblinSpawners.Clear();
        goblinSpawnerPauseRoutine = null;
        Debug.Log("QuestManager: spawners goblin reactivados y funcionando normalmente.");
    }

    public void StartSkeletonQuest()
    {
        if (SkeletonState != QuestTaskState.Available) return;
        SkeletonState = QuestTaskState.Active;
        SkeletonKills = 0;
        OnQuestChanged?.Invoke();
    }

    public void ClaimSkeletonReward()
    {
        if (SkeletonState != QuestTaskState.ReadyToTurnIn) return;
        InventoryManager.Instance?.AddGold(SkeletonReward);
        SkeletonState = QuestTaskState.Completed;
        OnQuestChanged?.Invoke();
    }

    public void StartDeerQuest()
    {
        if (DeerState != QuestTaskState.Available) return;
        DeerState = QuestTaskState.Active;
        DeerKills = 0;
        OnQuestChanged?.Invoke();
    }

    public void ClaimDeerReward()
    {
        if (DeerState != QuestTaskState.ReadyToTurnIn) return;
        InventoryManager.Instance?.AddGold(DeerReward);
        DeerState = QuestTaskState.Completed;
        OnQuestChanged?.Invoke();
    }
}

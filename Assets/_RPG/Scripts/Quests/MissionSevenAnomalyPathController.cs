using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Playable final act of mission seven. It keeps the player on the old-man island,
/// blocks the return to Boat Stain and completes only after the Demon Anomaly dies.
/// Everything is created at runtime so existing scene placement is preserved.
/// </summary>
public sealed class MissionSevenAnomalyPathController : MonoBehaviour
{
    Transform player;
    PlayerStats playerStats;
    PlayerWaterBreathing water;
    EnemyStats anomalyStats;
    GameObject barrier;
    Vector3 checkpointPosition;
    Quaternion checkpointRotation;
    bool burningDeath;
    bool waitingForRespawn;
    bool completed;

    public static MissionSevenAnomalyPathController Begin(Transform target)
    {
        MissionSevenAnomalyPathController existing =
            FindAnyObjectByType<MissionSevenAnomalyPathController>();
        if (existing == null)
        {
            GameObject root = new GameObject(
                "Mission7_AnomalyPathController");
            existing = root.AddComponent<MissionSevenAnomalyPathController>();
        }
        existing.Configure(target);
        return existing;
    }

    void Configure(Transform target)
    {
        player = target;
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }
        if (player == null) return;

        playerStats = player.GetComponent<PlayerStats>();
        water = player.GetComponent<PlayerWaterBreathing>();
        checkpointPosition = player.position;
        checkpointRotation = player.rotation;
        completed = false;
        BuildFireBarrier();
        BindAnomaly();
    }

    void OnDestroy()
    {
        if (anomalyStats != null)
            anomalyStats.OnDeath -= HandleAnomalyDeath;
    }

    void Update()
    {
        if (completed) return;
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) Configure(found.transform);
            return;
        }

        if (anomalyStats == null)
            BindAnomaly();

        if (!burningDeath && !waitingForRespawn &&
            water != null && water.IsBodyInWater &&
            playerStats != null && !playerStats.IsDead)
        {
            burningDeath = true;
            waitingForRespawn = true;
            CreateBurningWaterEffect(player);
            QuestOverheadThought.Show(player,
                "El agua est\u00e1 ardiendo... no puedo escapar por aqu\u00ed.",
                2.4f);
            playerStats.ForceEnvironmentalDeath();
        }

        if (waitingForRespawn && playerStats != null &&
            !playerStats.IsDead)
        {
            ReturnToMissionCheckpoint();
            waitingForRespawn = false;
            burningDeath = false;
        }
    }

    void BindAnomaly()
    {
        DemonioAnomaloEncounterEffects[] effects =
            FindObjectsByType<DemonioAnomaloEncounterEffects>(
                FindObjectsInactive.Include);
        float closest = float.PositiveInfinity;
        EnemyStats selected = null;
        foreach (DemonioAnomaloEncounterEffects effect in effects)
        {
            if (effect == null) continue;
            EnemyStats stats = effect.GetComponentInParent<EnemyStats>(true) ??
                               effect.GetComponentInChildren<EnemyStats>(true);
            if (stats == null) continue;
            float distance = player != null
                ? (stats.transform.position - player.position).sqrMagnitude
                : 0f;
            if (distance >= closest) continue;
            closest = distance;
            selected = stats;
        }

        if (selected == null)
        {
            foreach (EnemyStats stats in FindObjectsByType<EnemyStats>(
                         FindObjectsInactive.Include))
            {
                if (stats == null ||
                    stats.name.IndexOf("Anomalo",
                        StringComparison.OrdinalIgnoreCase) < 0 &&
                    stats.name.IndexOf("Anomaly",
                        StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                selected = stats;
                break;
            }
        }

        if (selected == null || selected == anomalyStats) return;
        if (anomalyStats != null)
            anomalyStats.OnDeath -= HandleAnomalyDeath;
        anomalyStats = selected;
        anomalyStats.gameObject.SetActive(true);
        anomalyStats.OnDeath += HandleAnomalyDeath;
    }

    void BuildFireBarrier()
    {
        if (barrier != null) Destroy(barrier);
        Vector3 villageDirection = FindVillageDirection();
        Vector3 center = player.position + villageDirection * 8f;
        if (Physics.Raycast(center + Vector3.up * 40f, Vector3.down,
                out RaycastHit ground, 100f, ~0,
                QueryTriggerInteraction.Ignore))
            center.y = ground.point.y;

        barrier = new GameObject("Mission7_Firewall_To_BoatStain");
        barrier.transform.SetPositionAndRotation(center,
            Quaternion.LookRotation(villageDirection, Vector3.up));
        BoxCollider blocker = barrier.AddComponent<BoxCollider>();
        blocker.center = new Vector3(0f, 2.4f, 0f);
        blocker.size = new Vector3(18f, 5.2f, 1.25f);

        GameObject fireRoot = new GameObject("FireVisuals");
        fireRoot.transform.SetParent(barrier.transform, false);
        for (int i = -9; i <= 9; i++)
            BuildFireColumn(fireRoot.transform,
                new Vector3(i, .08f, UnityEngine.Random.Range(-.22f, .22f)));

        QuestOverheadThought.Show(player,
            "La salida a Boat Stain est\u00e1 bloqueada. Debo atravesar el sendero.",
            6f);
    }

    Vector3 FindVillageDirection()
    {
        Transform destination = null;
        try
        {
            GameObject spawn = GameObject.FindGameObjectWithTag("SpawnPoint");
            if (spawn != null) destination = spawn.transform;
        }
        catch (UnityException) { }

        if (destination == null)
        {
            foreach (Transform candidate in FindObjectsByType<Transform>(
                         FindObjectsInactive.Include))
            {
                if (candidate == null ||
                    candidate.name.IndexOf("Tonio",
                        StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                destination = candidate;
                break;
            }
        }

        Vector3 direction = destination != null
            ? destination.position - player.position
            : -player.forward;
        direction.y = 0f;
        return direction.sqrMagnitude > .01f
            ? direction.normalized
            : Vector3.back;
    }

    static void BuildFireColumn(Transform parent, Vector3 localPosition)
    {
        GameObject column = new GameObject("FireColumn",
            typeof(ParticleSystem));
        column.transform.SetParent(parent, false);
        column.transform.localPosition = localPosition;
        ParticleSystem particles = column.GetComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.7f, 5.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(.55f, 1.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, .08f, .01f, .84f),
            new Color(1f, .48f, .025f, .62f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 34f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 9f;
        shape.radius = .38f;
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, .72f, .12f), 0f),
                new GradientColorKey(new Color(1f, .04f, .01f), .55f),
                new GradientColorKey(new Color(.16f, .01f, .025f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(.82f, .12f),
                new GradientAlphaKey(.52f, .7f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;
        ParticleSystemRenderer renderer =
            column.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Shader shader = Shader.Find(
                            "Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Particles/Standard Unlit");
        if (shader != null)
        {
            Material material = new Material(shader)
            {
                name = "Mission7_Firewall_Material",
                color = Color.white
            };
            renderer.sharedMaterial = material;
        }
        particles.Play(true);
    }

    static void CreateBurningWaterEffect(Transform target)
    {
        GameObject root = new GameObject("Mission7_BurningWaterDeath");
        root.transform.SetParent(target, false);
        root.transform.localPosition = Vector3.zero;
        for (int i = 0; i < 5; i++)
            BuildFireColumn(root.transform,
                new Vector3(UnityEngine.Random.Range(-.35f, .35f),
                    UnityEngine.Random.Range(0f, 1.35f),
                    UnityEngine.Random.Range(-.3f, .3f)));
        Destroy(root, 3.8f);
    }

    void ReturnToMissionCheckpoint()
    {
        CharacterController controller =
            player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.SetPositionAndRotation(checkpointPosition,
            checkpointRotation);
        if (controller != null) controller.enabled = true;
    }

    void HandleAnomalyDeath()
    {
        if (completed) return;
        completed = true;
        if (anomalyStats != null)
            anomalyStats.OnDeath -= HandleAnomalyDeath;

        GiveBoneSword();
        RemoveDarkSaberEnchantment();
        MissionSevenPlayerDarkAura.RemoveFrom(player);
        TenkokuDayNightCycle cycle =
            FindAnyObjectByType<TenkokuDayNightCycle>();
        cycle?.SetCurrentHour(9f);
        BuildConstructionBlockade();
        QuestManager.Instance?.CompleteSeventhMission();
        QuestOverheadThought.Show(player,
            "Boat Stain est\u00e1 en construcci\u00f3n. Tendr\u00e9 que seguir otro camino...",
            8f);
        enabled = false;
    }

    static void GiveBoneSword()
    {
        ItemData boneSword = null;
        foreach (ShopEntry entry in BlacksmithShopPanel.CreateRuntimeCatalog())
        {
            if (entry?.item == null ||
                entry.item.itemID != "free_1h_sword_3_1") continue;
            boneSword = entry.item;
            break;
        }
        if (boneSword == null || InventoryManager.Instance == null) return;
        if (!InventoryManager.Instance.AddItem(boneSword))
        {
            // Quest rewards must never vanish just because all ordinary slots
            // happen to be occupied at the instant the boss dies.
            InventoryManager.Instance.Slots.Add(
                new InventorySlot(boneSword, 1));
            InventoryManager.Instance.NotifyInventoryChanged();
        }
    }

    static void RemoveDarkSaberEnchantment()
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null)
        {
            foreach (InventorySlot slot in inventory.Slots)
                if (slot?.instance != null &&
                    IsDarkSaber(slot.instance))
                    slot.instance.darkEnergyEnchanted = false;
            inventory.NotifyInventoryChanged();
        }

        EquipmentManager equipment = EquipmentManager.Instance;
        ItemInstance equipped =
            equipment?.GetEquippedInstance(ItemType.Weapon);
        if (IsDarkSaber(equipped))
            equipped.darkEnergyEnchanted = false;
        equipment?.RefreshEquippedWeaponVisual();
    }

    static bool IsDarkSaber(ItemInstance instance)
    {
        ItemData item = instance?.template;
        if (item == null) return false;
        string id = item.itemID ?? string.Empty;
        string name = item.itemName ?? string.Empty;
        return id.IndexOf("dark_saber",
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               id.IndexOf("sable_oscuro",
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               id == "free_1h_sword_2_2" ||
               name.IndexOf("Sable Oscuro",
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void BuildConstructionBlockade()
    {
        if (barrier == null) return;
        barrier.name = "BoatStain_ConstructionBlockade";
        Transform fire = barrier.transform.Find("FireVisuals");
        if (fire != null) Destroy(fire.gameObject);
        BoxCollider blocker = barrier.GetComponent<BoxCollider>();
        if (blocker != null)
        {
            blocker.center = new Vector3(0f, 1.7f, 0f);
            blocker.size = new Vector3(34f, 3.6f, 3.2f);
        }

        Material wood = CreateMaterial(new Color(.24f, .105f, .025f));
        Material stone = CreateMaterial(new Color(.18f, .16f, .15f));
        for (int i = -8; i <= 8; i++)
        {
            GameObject post = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            post.name = "ConstructionPost";
            post.transform.SetParent(barrier.transform, false);
            post.transform.localPosition =
                new Vector3(i * 2f, 1.45f, 0f);
            post.transform.localRotation =
                Quaternion.Euler(0f, 0f, i % 2 == 0 ? 5f : -5f);
            post.transform.localScale = new Vector3(.24f, 3f, .38f);
            post.GetComponent<Renderer>().sharedMaterial = wood;
            Destroy(post.GetComponent<Collider>());

            GameObject rock = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            rock.name = "ConstructionRock";
            rock.transform.SetParent(barrier.transform, false);
            rock.transform.localPosition =
                new Vector3(i * 2f, .42f, .25f);
            rock.transform.localScale =
                new Vector3(1.35f, .82f, 1.18f);
            rock.GetComponent<Renderer>().sharedMaterial = stone;
            Destroy(rock.GetComponent<Collider>());
        }
        AddConstructionSign();
    }

    void AddConstructionSign()
    {
        GameObject canvasObject = new GameObject(
            "BoatStainConstructionSign", typeof(Canvas));
        canvasObject.transform.SetParent(barrier.transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 3.8f, 0f);
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * .012f;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;
        RectTransform canvasRect =
            canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(760f, 90f);

        GameObject labelObject = new GameObject("Label",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(canvasObject.transform, false);
        TextMeshProUGUI label =
            labelObject.GetComponent<TextMeshProUGUI>();
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax =
            Vector2.zero;
        label.text =
            "BOAT STAIN EN CONSTRUCCI\u00d3N\nSIGUE EL OTRO SENDERO";
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = 34f;
        label.color = new Color(1f, .72f, .28f);
    }

    static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                        Shader.Find("Standard");
        Material material = new Material(shader)
        {
            color = color
        };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        return material;
    }
}

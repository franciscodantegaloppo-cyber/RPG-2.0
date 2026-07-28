using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KingGoblinPlagueStatue : MonoBehaviour, IInteractable
{
    [SerializeField] GameObject[] goblinPrefabs;
    [SerializeField] int goblinCount = 10;
    [SerializeField] int starredGoblinCount = 2;
    [SerializeField] float spawnRadius = 10f;
    [SerializeField] float trialSeconds = 120f;
    [SerializeField] float redNightSeconds = 30f;

    readonly List<GameObject> trialGoblins = new List<GameObject>();
    readonly List<GoblinSpawner> pausedSpawners = new List<GoblinSpawner>();
    Coroutine activeRoutine;
    PlayerStats player;
    int kills;
    float remaining;

    bool environmentCaptured;
    bool oldFogEnabled;
    FogMode oldFogMode;
    Color oldFogColor;
    float oldFogStart;
    float oldFogEnd;
    Color oldAmbient;
    Camera affectedCamera;
    float oldFarClip;
    Light sun;
    Color oldSunColor;
    float oldSunIntensity;
    TenkokuDayNightCycle dayNight;
    bool dayNightWasEnabled;
    GameObject timerPanel;
    TextMeshProUGUI timerText;

    void Awake()
    {
        QuestNpcAttentionIcon icon = GetComponent<QuestNpcAttentionIcon>();
        if (icon == null) icon = gameObject.AddComponent<QuestNpcAttentionIcon>();
        icon.Configure(QuestNpcAttentionRole.KingGoblinStatue);
        if (GetComponent<KingGoblinIntroCinematic>() == null)
            gameObject.AddComponent<KingGoblinIntroCinematic>();
        if (GetComponent<KingGoblinStatueLevelMarker>() == null)
            gameObject.AddComponent<KingGoblinStatueLevelMarker>();
    }

    void OnDisable()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
            CleanupTrial(false);
        }
    }

    public string GetInteractionText()
    {
        QuestManager quests = QuestManager.Instance;
        if (quests != null && quests.MerchantIntroductionState == PrimaryQuestState.ReturnToStatueForReliefs)
            return "[E] Examinar relieves de la corona";
        return activeRoutine == null ? "[E] Iniciar prueba de la peste" : "Prueba de la peste en curso";
    }

    public bool CanInteract(PlayerInteraction interaction)
    {
        QuestManager quests = QuestManager.Instance;
        return activeRoutine == null && quests != null &&
               (quests.MerchantIntroductionState == PrimaryQuestState.GoToKingGoblinStatue ||
                quests.MerchantIntroductionState == PrimaryQuestState.ReturnToStatueForReliefs);
    }

    public void Interact(PlayerInteraction interaction)
    {
        if (!CanInteract(interaction)) return;
        if (QuestManager.Instance.MerchantIntroductionState == PrimaryQuestState.ReturnToStatueForReliefs)
        {
            PlagueCrownAltarUI.Open();
            return;
        }
        if (!QuestManager.Instance.BeginStatueTrial()) return;
        player = interaction != null ? interaction.GetComponent<PlayerStats>() : FindAnyObjectByType<PlayerStats>();
        activeRoutine = StartCoroutine(RunTrial());
    }

    public void CancelTrialForMissionJump()
    {
        if (activeRoutine == null) return;
        StopCoroutine(activeRoutine);
        activeRoutine = null;
        CleanupTrial(true);
    }

    IEnumerator RunTrial()
    {
        kills = 0;
        remaining = trialSeconds;
        CaptureAndApplyRedNight();
        PauseNormalGoblinSpawners();
        CreateTimerUI();
        EnemyStats.OnAnyDeath += HandleEnemyDeath;
        PlayerStats.GlobalOnDeath += HandlePlayerDeath;

        if (!SpawnTrialGoblins())
        {
            QuestOverheadThought.Show(player != null ? player.transform : transform,
                "La estatua no pudo invocar a los goblins. Intenta nuevamente.", 4f);
            QuestManager.Instance?.FailStatueTrial();
            CleanupTrial(true);
            yield break;
        }

        float redRemaining = redNightSeconds;
        while (remaining > 0f && kills < goblinCount && player != null && !player.IsDead)
        {
            remaining -= Time.deltaTime;
            redRemaining -= Time.deltaTime;
            if (redRemaining <= 0f && environmentCaptured)
                RestoreEnvironment();
            UpdateTimerUI();
            yield return null;
        }

        bool won = kills >= goblinCount;
        if (won)
        {
            QuestManager.Instance?.CompleteStatueTrial();
            QuestOverheadThought.Show(player != null ? player.transform : transform,
                "Has superado la prueba de la peste. Regresa con Tonio.", 5f);
        }
        else
        {
            QuestManager.Instance?.FailStatueTrial();
            if (player != null && !player.IsDead)
                QuestOverheadThought.Show(player.transform,
                    "La peste ha vencido. Puedes volver a activar la estatua.", 5f);
        }
        CleanupTrial(true);
    }

    bool SpawnTrialGoblins()
    {
        if (goblinPrefabs == null || goblinPrefabs.Length == 0 || player == null) return false;
        Terrain terrain = Terrain.activeTerrain;
        for (int i = 0; i < goblinCount; i++)
        {
            float angle = (Mathf.PI * 2f * i / goblinCount) + Random.Range(-.16f, .16f);
            float radius = spawnRadius;
            Vector3 position = player.transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (terrain != null)
                position.y = terrain.SampleHeight(position) + terrain.transform.position.y + .08f;
            else if (Physics.Raycast(position + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 160f, ~0, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y + .08f;

            GameObject prefab = goblinPrefabs[i % goblinPrefabs.Length];
            if (prefab == null) continue;
            GameObject goblin = Instantiate(prefab, position, Quaternion.LookRotation(player.transform.position - position));
            goblin.name = "PlagueTrialGoblin_" + (i + 1);
            PlagueTrialGoblin marker = goblin.AddComponent<PlagueTrialGoblin>();
            marker.Owner = this;
            trialGoblins.Add(goblin);
            if (i < starredGoblinCount) MakeStarred(goblin);
        }
        return trialGoblins.Count == goblinCount;
    }

    void MakeStarred(GameObject goblin)
    {
        EnemyStats stats = goblin.GetComponent<EnemyStats>();
        stats?.ApplyStatMultiplier(2f, 2f);
        stats?.ApplyRewardMultiplier(2f);

        GameObject star = new GameObject("PlagueEliteStar", typeof(MeshFilter), typeof(MeshRenderer), typeof(BillboardToCamera));
        star.transform.SetParent(goblin.transform, false);
        Renderer[] renderers = goblin.GetComponentsInChildren<Renderer>(true);
        float localHeight = 2.6f;
        if (renderers.Length > 0)
        {
            float top = renderers[0].bounds.max.y;
            foreach (Renderer renderer in renderers) top = Mathf.Max(top, renderer.bounds.max.y);
            localHeight = goblin.transform.InverseTransformPoint(new Vector3(goblin.transform.position.x, top + .45f, goblin.transform.position.z)).y;
        }
        star.transform.localPosition = Vector3.up * localHeight;
        star.transform.localScale = Vector3.one * .38f;
        star.GetComponent<MeshFilter>().sharedMesh = BuildStarMesh();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        Color gold = new Color(1f, .78f, .03f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", gold);
        if (material.HasProperty("_Color")) material.SetColor("_Color", gold);
        star.GetComponent<MeshRenderer>().sharedMaterial = material;
        EliteEnemyAura.Ensure(goblin);
    }

    void HandleEnemyDeath(EnemyStats enemy)
    {
        if (enemy == null) return;
        PlagueTrialGoblin marker = enemy.GetComponent<PlagueTrialGoblin>();
        if (marker == null || marker.Owner != this || marker.Counted) return;
        marker.Counted = true;
        kills++;
        UpdateTimerUI();
    }

    void HandlePlayerDeath() { remaining = 0f; }

    void CaptureAndApplyRedNight()
    {
        oldFogEnabled = RenderSettings.fog;
        oldFogMode = RenderSettings.fogMode;
        oldFogColor = RenderSettings.fogColor;
        oldFogStart = RenderSettings.fogStartDistance;
        oldFogEnd = RenderSettings.fogEndDistance;
        oldAmbient = RenderSettings.ambientLight;
        affectedCamera = Camera.main;
        if (affectedCamera != null) oldFarClip = affectedCamera.farClipPlane;
        sun = RenderSettings.sun;
        if (sun != null) { oldSunColor = sun.color; oldSunIntensity = sun.intensity; }
        dayNight = FindAnyObjectByType<TenkokuDayNightCycle>(FindObjectsInactive.Include);
        if (dayNight != null) { dayNightWasEnabled = dayNight.enabled; dayNight.enabled = false; }
        environmentCaptured = true;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.15f, .006f, .012f);
        RenderSettings.fogStartDistance = 2.5f;
        RenderSettings.fogEndDistance = 10f;
        RenderSettings.ambientLight = new Color(.17f, .012f, .018f);
        if (affectedCamera != null) affectedCamera.farClipPlane = Mathf.Max(11f, affectedCamera.nearClipPlane + 1f);
        if (sun != null) { sun.color = new Color(.8f, .08f, .04f); sun.intensity = .2f; }
    }

    void RestoreEnvironment()
    {
        if (!environmentCaptured) return;
        RenderSettings.fog = oldFogEnabled;
        RenderSettings.fogMode = oldFogMode;
        RenderSettings.fogColor = oldFogColor;
        RenderSettings.fogStartDistance = oldFogStart;
        RenderSettings.fogEndDistance = oldFogEnd;
        RenderSettings.ambientLight = oldAmbient;
        if (affectedCamera != null) affectedCamera.farClipPlane = oldFarClip;
        if (sun != null) { sun.color = oldSunColor; sun.intensity = oldSunIntensity; }
        if (dayNight != null) dayNight.enabled = dayNightWasEnabled;
        environmentCaptured = false;
    }

    void PauseNormalGoblinSpawners()
    {
        foreach (GoblinSpawner spawner in FindObjectsByType<GoblinSpawner>(FindObjectsInactive.Exclude))
        {
            if (spawner == null || !spawner.enabled) continue;
            pausedSpawners.Add(spawner);
            spawner.enabled = false;
        }
    }

    void CleanupTrial(bool destroyRemaining)
    {
        EnemyStats.OnAnyDeath -= HandleEnemyDeath;
        PlayerStats.GlobalOnDeath -= HandlePlayerDeath;
        RestoreEnvironment();
        if (destroyRemaining)
            foreach (GameObject goblin in trialGoblins) if (goblin != null) Destroy(goblin);
        trialGoblins.Clear();
        foreach (GoblinSpawner spawner in pausedSpawners) if (spawner != null) spawner.enabled = true;
        pausedSpawners.Clear();
        if (timerPanel != null) Destroy(timerPanel);
        timerPanel = null;
        timerText = null;
        activeRoutine = null;
    }

    void CreateTimerUI()
    {
        Canvas canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null) return;
        timerPanel = new GameObject("PlagueTrialTimer", typeof(RectTransform), typeof(Image), typeof(Canvas));
        timerPanel.transform.SetParent(canvas.transform, false);
        RectTransform rect = timerPanel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);
        rect.sizeDelta = new Vector2(340f, 66f);
        Image image = timerPanel.GetComponent<Image>();
        Sprite frame = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        if (frame != null) { image.sprite = frame; image.type = Image.Type.Sliced; image.color = new Color(1f, 1f, 1f, .76f); }
        else image.color = new Color(.09f, .015f, .02f, .76f);
        Canvas top = timerPanel.GetComponent<Canvas>(); top.overrideSorting = true; top.sortingOrder = 1200;

        GameObject textObject = new GameObject("TimerText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(timerPanel.transform, false);
        timerText = textObject.GetComponent<TextMeshProUGUI>();
        timerText.fontSize = 15f; timerText.enableAutoSizing = true;
        timerText.fontSizeMin = 11f; timerText.fontSizeMax = 15f;
        timerText.textWrappingMode = TextWrappingModes.NoWrap;
        timerText.lineSpacing = 0f; timerText.alignment = TextAlignmentOptions.Center;
        timerText.color = new Color(1f, .78f, .25f); timerText.fontStyle = FontStyles.Bold;
        RectTransform tr = timerText.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(22f, 8f); tr.offsetMax = new Vector2(-22f, -8f);
        UpdateTimerUI();
    }

    void UpdateTimerUI()
    {
        if (timerText == null) return;
        int seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));
        timerText.text = "PRUEBA DE LA PESTE   " + kills + "/" + goblinCount +
                         "\nTiempo: " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }

    static Mesh BuildStarMesh()
    {
        var vertices = new List<Vector3> { Vector3.zero };
        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
            float radius = i % 2 == 0 ? 1f : .42f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
        }
        var triangles = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            int b = i + 1, c = (i + 1) % 10 + 1;
            triangles.Add(0); triangles.Add(b); triangles.Add(c);
            triangles.Add(0); triangles.Add(c); triangles.Add(b);
        }
        Mesh mesh = new Mesh { name = "PlagueEliteStar" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
}

public class PlagueTrialGoblin : MonoBehaviour
{
    public KingGoblinPlagueStatue Owner { get; set; }
    public bool Counted { get; set; }
}

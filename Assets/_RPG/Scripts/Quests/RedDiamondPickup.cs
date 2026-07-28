using UnityEngine;

public sealed class RedDiamondPickup : MonoBehaviour, IInteractable
{
    [SerializeField] ItemData redDiamondItem;
    [SerializeField] float rotationSpeed = 28f;
    [SerializeField] float hoverAmplitude = .16f;
    [SerializeField] float hoverSpeed = 1.65f;

    Vector3 baseLocalPosition;
    Light redLight;
    Renderer[] renderers;
    Collider[] colliders;
    ParticleSystem aura;
    bool presentationVisible;
    Vector3 lastHoverPosition;
    bool hasAppliedHover;

    void Awake()
    {
        baseLocalPosition = transform.localPosition;
        redLight = GetComponentInChildren<Light>(true);
        aura = BuildAura();
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        QuestNpcAttentionIcon icon = GetComponent<QuestNpcAttentionIcon>();
        if (icon == null) icon = gameObject.AddComponent<QuestNpcAttentionIcon>();
        icon.Configure(QuestNpcAttentionRole.RedDiamond);
        SetPresentationVisible(false);
    }

    void OnEnable()
    {
        // The transform authored in SpawnVillage is the source of truth. This object may sit
        // on a roof, bridge or dungeon prop, so no terrain/collider query is allowed to move it.
        baseLocalPosition = transform.localPosition;
        lastHoverPosition = baseLocalPosition;
        hasAppliedHover = false;
        if (QuestManager.Instance != null &&
            QuestManager.Instance.IsSixthMissionActive)
            SetPresentationVisible(true);
    }

    void Update()
    {
        // It exists visibly from the moment mission six starts. Interaction remains locked
        // until the objective actually changes to SixthFindRedDiamond.
        bool shouldBeVisible = QuestManager.Instance != null &&
            QuestManager.Instance.IsSixthMissionActive;
        if (shouldBeVisible != presentationVisible)
            SetPresentationVisible(shouldBeVisible);
        if (!shouldBeVisible) return;

        // If the diamond is moved manually during Play, retain that new position instead of
        // snapping it back to the Awake position on the next hover frame.
        if (hasAppliedHover)
        {
            Vector3 externalDelta = transform.localPosition - lastHoverPosition;
            if (externalDelta.sqrMagnitude > .000001f)
                baseLocalPosition += externalDelta;
        }

        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        transform.localPosition = baseLocalPosition +
            Vector3.up * (Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude);
        lastHoverPosition = transform.localPosition;
        hasAppliedHover = true;
        if (redLight != null)
            redLight.intensity = 4.2f + (Mathf.Sin(Time.time * 3.4f) + 1f) * 1.25f;
    }

    public string GetInteractionText() => "[E] Tomar Diamante Rojo";

    // Called by SixthMissionSequence as a self-repair path when an old save or an
    // inactive scene object prevented Update from making the quest item visible.
    public void EnsureQuestPresentation()
    {
        SetPresentationVisible(true);
    }

    // Kept as a compatibility entry point for old scene/editor setup scripts. Runtime quest
    // code deliberately does not call it: a manually placed red diamond must never move.
    public void SnapToTerrain()
    {
        baseLocalPosition = transform.localPosition;
        lastHoverPosition = baseLocalPosition;
        hasAppliedHover = false;
    }

    public bool CanInteract(PlayerInteraction player)
    {
        return QuestManager.Instance != null &&
               QuestManager.Instance.IsSixthMissionActive;
    }

    public void Interact(PlayerInteraction player)
    {
        if (!CanInteract(player) || redDiamondItem == null ||
            InventoryManager.Instance == null)
            return;

        // Saves created while mission six was in its warning/Tonio phase could already see the
        // diamond but were rejected here. Reaching and interacting with the real quest object
        // now promotes those two preliminary states to the pickup objective.
        if (QuestManager.Instance.MerchantIntroductionState ==
            PrimaryQuestState.SixthMissionWarningPending)
            QuestManager.Instance.BeginSixthTonioObjective();
        if (QuestManager.Instance.MerchantIntroductionState ==
            PrimaryQuestState.SixthTalkToPetrifiedTonio)
            QuestManager.Instance.BeginRedDiamondSearch();

        if (!InventoryManager.Instance.AddItem(redDiamondItem, 1))
        {
            MerchantDialoguePanel.EnsureRuntime()?.Show(
                "Diamante Rojo",
                "No tienes espacio libre en el inventario.",
                "Cerrar", null);
            return;
        }

        Vector3 summonOrigin = transform.position;
        QuestManager.Instance.NotifyRedDiamondTaken();
        // Hide the diamond before resolving the ground so its own collider cannot be mistaken
        // for the surface beneath the summoned boss.
        gameObject.SetActive(false);
        SixthMissionSequence.Instance?.BeginRedDiamondEncounter(
            player != null ? player.transform : null, summonOrigin);
    }

    void SetPresentationVisible(bool visible)
    {
        presentationVisible = visible;
        if (renderers != null)
            foreach (Renderer renderer in renderers)
                if (renderer != null) renderer.enabled = visible;
        if (colliders != null)
            foreach (Collider collider in colliders)
                if (collider != null) collider.enabled = visible;
        if (redLight != null) redLight.enabled = visible;
        if (aura != null)
        {
            if (visible) aura.Play(true);
            else aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    ParticleSystem BuildAura()
    {
        GameObject auraObject = new GameObject("RedDiamond_WindFireAura");
        auraObject.transform.SetParent(transform, false);
        auraObject.transform.localPosition = Vector3.up * .32f;
        ParticleSystem particles = auraObject.AddComponent<ParticleSystem>();

        var main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.04f, .14f);
        main.startSize = new ParticleSystem.MinMaxCurve(.012f, .034f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, .09f, .01f, .22f),
            new Color(1f, .48f, .035f, .42f));
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = particles.emission;
        emission.rateOverTime = 55f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .48f;
        shape.radiusThickness = .28f;

        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(.08f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);
        velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f);
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(1.65f);
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
        velocity.radial = new ParticleSystem.MinMaxCurve(.045f);

        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = .12f;
        noise.frequency = .65f;
        noise.scrollSpeed = .18f;

        var colorLifetime = particles.colorOverLifetime;
        colorLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, .12f, .01f), 0f),
                new GradientColorKey(new Color(1f, .52f, .04f), .52f),
                new GradientColorKey(new Color(.65f, .01f, .005f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(.42f, .22f),
                new GradientAlphaKey(.28f, .72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorLifetime.color = fade;

        ParticleSystemRenderer particleRenderer =
            auraObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.lengthScale = 3.2f;
        particleRenderer.velocityScale = .16f;
        Material material = WindVisualEffect.CreateWindStreakMaterial();
        if (material != null)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(1f, .15f, .015f, .55f));
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", new Color(1f, .15f, .015f, .55f));
            particleRenderer.material = material;
        }
        return particles;
    }
}

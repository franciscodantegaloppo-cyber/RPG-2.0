using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Coordinates a warned five-minute super-gust, physical village debris and next-day
// restoration. Large static structures bend first; lighter loose props are released sooner.
public sealed class ExtremeWindEventController : MonoBehaviour
{
    public static ExtremeWindEventController Instance { get; private set; }
    const float EffectRadius = 60f;
    const int MaxAffectedProps = 420;
    const int MaxNearbyColliders = 2048;
    const int MaxUprootedTrees = 48;
    const int MaxWallDebris = 180;
    const float ActivationInterval = .055f;

    sealed class PropSnapshot
    {
        public Transform transform;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 scale;
        public Rigidbody body;
        public bool bodyCreated;
        public bool originalKinematic;
        public bool originalUseGravity;
        public RigidbodyConstraints originalConstraints;
        public bool structural;
        public bool heavyStructure;
        public bool activated;
        public float releaseProgress;
        public float breakProgress;
        public float size;
        public string lowerName;
        public bool terrainTree;
        public Collider fallbackCollider;
        public WindDynamicColliderAdapter colliderAdapter;
    }

    sealed class TerrainWindSnapshot
    {
        public Terrain terrain;
        public TerrainCollider terrainCollider;
        public TerrainData originalData;
        public TerrainData runtimeData;
    }

    sealed class FountainWaterState
    {
        public Renderer renderer;
        public bool rendererEnabled;
        public ParticleSystem particles;
        public bool particlesWerePlaying;
    }

    readonly List<PropSnapshot> affected = new List<PropSnapshot>();
    readonly List<TerrainWindSnapshot> affectedTerrains =
        new List<TerrainWindSnapshot>();
    readonly HashSet<Transform> capturedRoots = new HashSet<Transform>();
    readonly List<FountainWaterState> fountainWater =
        new List<FountainWaterState>();
    readonly List<GameObject> spawnedWallDebris = new List<GameObject>();
    readonly Collider[] nearbyColliders = new Collider[MaxNearbyColliders];
    CanvasGroup warningGroup;
    TextMeshProUGUI warningText;
    ParticleSystem flyingLeaves;
    TenkokuDayNightCycle cycle;
    int restorationDay = -1;
    Coroutine eventRoutine;
    PlayerStats playerStats;
    float nextPlayerLookup;
    float nextPropActivation;
    float activeGustDuration = 300f;
    Vector3 eventCentre;
    Transform fountainRoot;
    public bool EventActive { get; private set; }
    public event System.Action ExtremeGustEnded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<ExtremeWindEventController>(
                FindObjectsInactive.Include) != null) return;
        new GameObject("ExtremeWindEventController")
            .AddComponent<ExtremeWindEventController>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildWarning();
        BuildFlyingLeaves();
    }

    void Update()
    {
        if (cycle == null) cycle = FindAnyObjectByType<TenkokuDayNightCycle>();
        if (playerStats == null && Time.unscaledTime >= nextPlayerLookup)
        {
            nextPlayerLookup = Time.unscaledTime + 1f;
            playerStats = FindAnyObjectByType<PlayerStats>();
        }

        if (restorationDay >= 0 && cycle != null &&
            cycle.WorldDay > restorationDay)
            RestoreVillage();

        if (!EventActive || WindManager.Instance == null) return;
        ApplyContinuousWind();
        UpdateFlyingLeaves();
    }

    public void StartExtremeGust(float warningSeconds = 10f,
        float durationSeconds = 300f)
    {
        if (eventRoutine != null) StopCoroutine(eventRoutine);
        if (affected.Count > 0 || affectedTerrains.Count > 0)
            RestoreVillage();
        eventRoutine = StartCoroutine(RunEvent(warningSeconds, durationSeconds));
    }

    public bool ToggleExtremeGust()
    {
        bool running = eventRoutine != null || EventActive ||
                       (WindManager.Instance != null &&
                        WindManager.Instance.IsExtremeGust);
        if (running)
        {
            StopExtremeGustNow();
            return false;
        }

        StartExtremeGust(10f, 300f);
        return true;
    }

    public void StopExtremeGustNow()
    {
        bool wasRunning = eventRoutine != null || EventActive ||
                          (WindManager.Instance != null &&
                           WindManager.Instance.IsExtremeGust);
        if (eventRoutine != null)
        {
            StopCoroutine(eventRoutine);
            eventRoutine = null;
        }
        warningGroup?.gameObject.SetActive(false);
        WindManager.Instance?.StopExtremeGust();
        if (flyingLeaves != null)
            flyingLeaves.Stop(true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        EventActive = false;
        if (wasRunning)
            ExtremeGustEnded?.Invoke();
    }

    IEnumerator RunEvent(float warningSeconds, float durationSeconds)
    {
        EventActive = false;
        float remaining = warningSeconds;
        warningGroup.alpha = 1f;
        warningGroup.gameObject.SetActive(true);
        while (remaining > 0f)
        {
            float progress = 1f - remaining / Mathf.Max(.01f, warningSeconds);
            WindManager.Instance?.PreviewExtremeGust(progress);
            warningText.text = "<b>ALERTA DE R\u00c1FAGA EXTREMA</b>\n" +
                               "Busca refugio en una casa  " +
                               Mathf.CeilToInt(remaining) + " s";
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
        warningGroup.gameObject.SetActive(false);

        activeGustDuration = Mathf.Max(1f, durationSeconds);
        DisableFountainWater();
        CaptureVillageProps();
        EventActive = true;
        StartCoroutine(DiscoverColliderlessProps(eventCentre));
        restorationDay = cycle != null ? cycle.WorldDay : 0;
        WindManager.Instance?.BeginExtremeGust(durationSeconds);
        if (flyingLeaves != null)
        {
            ParticleSystem.EmissionModule emission = flyingLeaves.emission;
            emission.enabled = true;
            flyingLeaves.Play(true);
        }

        float end = Time.time + durationSeconds;
        while (Time.time < end && WindManager.Instance != null &&
               WindManager.Instance.IsExtremeGust)
            yield return null;

        WindManager.Instance?.StopExtremeGust();
        if (flyingLeaves != null)
            flyingLeaves.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        EventActive = false;
        eventRoutine = null;
        ExtremeGustEnded?.Invoke();
    }

    void CaptureVillageProps()
    {
        affected.Clear();
        affectedTerrains.Clear();
        capturedRoots.Clear();
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        eventCentre = playerObject != null
            ? playerObject.transform.position
            : Vector3.zero;
        int capturedTrees = 0;
        int colliderCount = Physics.OverlapSphereNonAlloc(eventCentre,
            EffectRadius, nearbyColliders, ~0, QueryTriggerInteraction.Ignore);
        // Structures are captured first so abundant grass and loose props can
        // never consume the limit before every nearby roof and house wall.
        for (int capturePass = 0;
             capturePass < 2 && affected.Count < MaxAffectedProps;
             capturePass++)
        {
            for (int colliderIndex = 0;
                 colliderIndex < colliderCount &&
                 affected.Count < MaxAffectedProps;
                 colliderIndex++)
            {
                Collider candidate = nearbyColliders[colliderIndex];
                if (candidate == null) continue;
                Renderer renderer = FindRendererForCollider(candidate);
                if (renderer == null ||
                    renderer.GetComponentInParent<Terrain>() != null)
                    continue;
                Transform root = FindMovableRoot(renderer.transform);
                HealingFountainInteractable fountainOwner =
                    renderer.GetComponentInParent<HealingFountainInteractable>();
                if (fountainOwner != null)
                    root = fountainOwner.transform;
                if (root == null || root.CompareTag("Player") ||
                    root.GetComponentInChildren<EnemyStats>() != null ||
                    IsCharacterOrCreature(root) ||
                    capturedRoots.Contains(root))
                    continue;

                string name = HierarchyName(root);
                bool fountain = fountainOwner != null ||
                                ContainsAny(name, "fountain", "fuente");
                if (IsFountainWaterRenderer(renderer, fountain))
                {
                    HideWaterRenderer(renderer);
                    continue;
                }
                if (HasInteractable(root) && !fountain) continue;
                bool tree = ContainsAny(name, "tree", "arbol", "pine",
                    "spruce", "fir", "pino");
                bool roofPiece = IsRoofPiece(name);
                bool wallPiece = IsWallPiece(name);
                bool housePiece = IsHousePiece(name);
                bool woodFramePiece = housePiece && IsWoodFramePiece(name);
                bool structure = roofPiece || wallPiece || housePiece ||
                    woodFramePiece || ContainsAny(name, "fence", "palisade",
                        "barricade", "building", "edificio", "tower", "torre",
                        "castle", "castillo", "fountain", "fuente");
                if ((capturePass == 0) != structure) continue;
                bool heavyStructure = fountain || ContainsAny(name, "wall",
                    "muralla", "tower", "torre", "castle", "castillo",
                    "gate", "porton");
            // Only colliders close to the player are considered. This avoids scanning
            // and activating the complete village/forest when a gust begins.
                Vector3 flatOffset = root.position - eventCentre;
                flatOffset.y = 0f;
                if (flatOffset.sqrMagnitude > EffectRadius * EffectRadius)
                    continue;
                if (tree && capturedTrees >= MaxUprootedTrees) continue;

                Bounds bounds = renderer.bounds;
                float size = bounds.size.magnitude;
                if (size > 18f)
                {
                    structure = true;
                    heavyStructure = true;
                }

                Rigidbody body = root.GetComponent<Rigidbody>();
                bool originalKinematic = body != null && body.isKinematic;
                float releaseProgress;
                if (structure)
                    releaseProgress = Random.Range(0f, .012f);
                else if (tree)
                    releaseProgress = Random.Range(0f, .045f);
                else
                    releaseProgress = Random.Range(0f, .085f);

                affected.Add(new PropSnapshot
                {
                transform = root,
                position = root.position,
                rotation = root.rotation,
                localPosition = root.localPosition,
                localRotation = root.localRotation,
                scale = root.localScale,
                body = body,
                bodyCreated = false,
                originalKinematic = originalKinematic,
                originalUseGravity = body == null || body.useGravity,
                originalConstraints = body != null
                    ? body.constraints
                    : RigidbodyConstraints.None,
                structural = structure,
                heavyStructure = heavyStructure,
                activated = false,
                releaseProgress = Mathf.Clamp01(releaseProgress),
                breakProgress = structure
                    ? fountain
                        ? Random.Range(.025f, .06f)
                        : roofPiece
                            ? Random.Range(.005f, .035f)
                            : wallPiece || woodFramePiece
                                ? Random.Range(.04f, .19f)
                                : Random.Range(.055f, .198f)
                    : 0f,
                size = size,
                lowerName = name
                });
                capturedRoots.Add(root);
                if (tree) capturedTrees++;
            }
        }
        affected.Sort((a, b) =>
            a.releaseProgress.CompareTo(b.releaseProgress));
        CaptureTerrainTrees(eventCentre, ref capturedTrees);
        nextPropActivation = Time.time;
    }

    void CaptureTerrainTrees(Vector3 centre, ref int capturedTrees)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null ||
                capturedTrees >= MaxUprootedTrees) break;
            TerrainData original = terrain.terrainData;
            Bounds terrainBounds = new Bounds(
                terrain.transform.position + original.size * .5f,
                original.size);
            Vector3 nearest = terrainBounds.ClosestPoint(centre);
            Vector3 terrainOffset = nearest - centre;
            terrainOffset.y = 0f;
            if (terrainOffset.sqrMagnitude >
                EffectRadius * EffectRadius) continue;
            TreeInstance[] trees = original.treeInstances;
            if (trees == null || trees.Length == 0) continue;

            List<int> selected = new List<int>();
            for (int i = 0;
                 i < trees.Length && capturedTrees < MaxUprootedTrees;
                 i++)
            {
                TreeInstance tree = trees[i];
                Vector3 world = terrain.transform.position +
                    Vector3.Scale(tree.position, original.size);
                Vector3 flat = world - centre;
                flat.y = 0f;
                if (flat.sqrMagnitude > EffectRadius * EffectRadius) continue;
                if (tree.prototypeIndex < 0 ||
                    tree.prototypeIndex >= original.treePrototypes.Length)
                    continue;
                GameObject prefab =
                    original.treePrototypes[tree.prototypeIndex].prefab;
                if (prefab == null) continue;
                selected.Add(i);
                capturedTrees++;
            }
            if (selected.Count == 0) continue;

            TerrainData runtimeData = Instantiate(original);
            runtimeData.name = original.name + " (Extreme Wind Runtime)";
            HashSet<int> selectedSet = new HashSet<int>(selected);
            List<TreeInstance> remaining =
                new List<TreeInstance>(trees.Length - selected.Count);
            for (int i = 0; i < trees.Length; i++)
                if (!selectedSet.Contains(i)) remaining.Add(trees[i]);
            runtimeData.SetTreeInstances(remaining.ToArray(), true);

            TerrainCollider terrainCollider =
                terrain.GetComponent<TerrainCollider>();
            terrain.terrainData = runtimeData;
            if (terrainCollider != null)
                terrainCollider.terrainData = runtimeData;
            affectedTerrains.Add(new TerrainWindSnapshot
            {
                terrain = terrain,
                terrainCollider = terrainCollider,
                originalData = original,
                runtimeData = runtimeData
            });

            foreach (int index in selected)
            {
                TreeInstance tree = trees[index];
                GameObject prefab =
                    original.treePrototypes[tree.prototypeIndex].prefab;
                Vector3 world = terrain.transform.position +
                    Vector3.Scale(tree.position, original.size);
                GameObject treeObject = Instantiate(prefab, world,
                    Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f));
                treeObject.name = prefab.name + " (Wind Uprooted)";
                treeObject.transform.localScale = Vector3.Scale(
                    treeObject.transform.localScale,
                    new Vector3(tree.widthScale, tree.heightScale,
                        tree.widthScale));
                Renderer renderer =
                    treeObject.GetComponentInChildren<Renderer>();
                float size = renderer != null
                    ? renderer.bounds.size.magnitude
                    : 8f;
                capturedRoots.Add(treeObject.transform);
                affected.Add(new PropSnapshot
                {
                    transform = treeObject.transform,
                    position = world,
                    rotation = treeObject.transform.rotation,
                    localPosition = treeObject.transform.localPosition,
                    localRotation = treeObject.transform.localRotation,
                    scale = treeObject.transform.localScale,
                    body = treeObject.GetComponent<Rigidbody>(),
                    originalKinematic = true,
                    originalUseGravity = true,
                    originalConstraints = RigidbodyConstraints.None,
                    structural = false,
                    activated = false,
                    terrainTree = true,
                    releaseProgress = Random.Range(0f, .045f),
                    size = size,
                    lowerName = treeObject.name.ToLowerInvariant()
                });
            }
        }
        affected.Sort((a, b) =>
            a.releaseProgress.CompareTo(b.releaseProgress));
    }

    IEnumerator DiscoverColliderlessProps(Vector3 centre)
    {
        Scene scene = SceneManager.GetActiveScene();
        Stack<Transform> pending = new Stack<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
            pending.Push(root.transform);

        const int transformsPerFrame = 90;
        while (pending.Count > 0 && EventActive &&
               affected.Count < MaxAffectedProps)
        {
            int budget = transformsPerFrame;
            while (budget-- > 0 && pending.Count > 0)
            {
                Transform current = pending.Pop();
                for (int i = 0; i < current.childCount; i++)
                    pending.Push(current.GetChild(i));

                Renderer renderer = current.GetComponent<Renderer>();
                if (renderer == null ||
                    renderer.GetComponentInParent<Terrain>() != null) continue;
                Transform root = FindMovableRoot(current);
                HealingFountainInteractable fountainOwner =
                    renderer.GetComponentInParent<HealingFountainInteractable>();
                if (fountainOwner != null)
                    root = fountainOwner.transform;
                if (root == null || capturedRoots.Contains(root) ||
                    root.CompareTag("Player") ||
                    root.GetComponentInChildren<EnemyStats>() != null ||
                    IsCharacterOrCreature(root))
                    continue;

                Vector3 flat = root.position - centre;
                flat.y = 0f;
                if (flat.sqrMagnitude > EffectRadius * EffectRadius) continue;
                string name = HierarchyName(root);
                bool fountain = fountainOwner != null ||
                                ContainsAny(name, "fountain", "fuente");
                if (IsFountainWaterRenderer(renderer, fountain))
                {
                    HideWaterRenderer(renderer);
                    continue;
                }
                if (HasInteractable(root) && !fountain) continue;
                bool roofPiece = IsRoofPiece(name);
                bool wallPiece = IsWallPiece(name);
                bool housePiece = IsHousePiece(name);
                bool woodFramePiece = housePiece && IsWoodFramePiece(name);
                bool structure = roofPiece || wallPiece || housePiece ||
                    woodFramePiece || ContainsAny(name, "fence", "palisade",
                        "barricade", "building", "edificio", "tower", "torre",
                        "castle", "castillo", "fountain", "fuente");
                bool heavyStructure = fountain || ContainsAny(name, "wall",
                    "muralla", "tower", "torre", "castle", "castillo",
                    "gate", "porton");

                float size = renderer.bounds.size.magnitude;
                if (size > 18f)
                {
                    structure = true;
                    heavyStructure = true;
                }
                float release = structure
                    ? Random.Range(0f, .012f)
                    : Random.Range(0f, .085f);
                Rigidbody existingBody = root.GetComponent<Rigidbody>();
                affected.Add(new PropSnapshot
                {
                    transform = root,
                    position = root.position,
                    rotation = root.rotation,
                    localPosition = root.localPosition,
                    localRotation = root.localRotation,
                    scale = root.localScale,
                    body = existingBody,
                    originalKinematic =
                        existingBody != null && existingBody.isKinematic,
                    originalUseGravity =
                        existingBody == null || existingBody.useGravity,
                    originalConstraints = existingBody != null
                        ? existingBody.constraints
                        : RigidbodyConstraints.None,
                    structural = structure,
                    heavyStructure = heavyStructure,
                    activated = false,
                    releaseProgress = release,
                    breakProgress = structure
                        ? fountain
                            ? Random.Range(.025f, .06f)
                            : roofPiece
                                ? Random.Range(.005f, .035f)
                                : wallPiece || woodFramePiece
                                    ? Random.Range(.04f, .19f)
                                    : Random.Range(.055f, .198f)
                        : 0f,
                    size = size,
                    lowerName = name
                });
                capturedRoots.Add(root);
            }
            yield return null;
        }
        affected.Sort((a, b) =>
            a.releaseProgress.CompareTo(b.releaseProgress));
    }

    void ApplyContinuousWind()
    {
        Vector3 direction = WindManager.Instance.WindDirection;
        float strength = WindManager.Instance.CurrentStrength01;
        float elapsedFactor = 1f -
            Mathf.Clamp01(WindManager.Instance.ExtremeGustRemaining /
                          activeGustDuration);
        if (Time.time >= nextPropActivation)
        {
            foreach (PropSnapshot pending in affected)
            {
                if (pending.activated ||
                    pending.releaseProgress > elapsedFactor) continue;
                ActivateProp(pending);
                nextPropActivation = Time.time + ActivationInterval;
                break;
            }
        }

        foreach (PropSnapshot entry in affected)
        {
            if (entry.transform == null || !entry.activated) continue;
            if (entry.structural)
            {
                float localProgress = Mathf.InverseLerp(
                    entry.releaseProgress, 1f, elapsedFactor);
                float bend = Mathf.Lerp(2f, 14f, Mathf.Clamp01(strength / 2f));
                Quaternion target = entry.rotation *
                    Quaternion.AngleAxis(bend * localProgress,
                        Vector3.Cross(Vector3.up, direction).normalized);
                entry.transform.rotation = Quaternion.Slerp(
                    entry.transform.rotation, target, Time.deltaTime * .3f);
                // Each modular wall/roof piece starts at a different moment and can
                // only collapse after enduring the gust for a long time.
                if (elapsedFactor >= entry.breakProgress - .08f)
                {
                    entry.transform.rotation = Quaternion.RotateTowards(
                        entry.transform.rotation,
                        entry.rotation * Quaternion.Euler(0f, 0f, 78f),
                        Time.deltaTime * 5f);
                    // Modular pieces finally tear loose one by one. Entire combined
                    // buildings remain bent instead of becoming giant projectiles.
                    if (elapsedFactor >= entry.breakProgress)
                    {
                        if (IsWallPiece(entry.lowerName))
                            SpawnWallLogDebris(entry, direction);
                        MakeDynamic(entry, true);
                        entry.structural = false;
                    }
                }
                continue;
            }
            if (entry.body == null) continue;
            entry.body.isKinematic = false;
            entry.body.useGravity = true;
            entry.body.constraints = RigidbodyConstraints.None;
            entry.body.WakeUp();
            float difficulty = Mathf.Max(1f, Mathf.Sqrt(entry.body.mass));
            float push = entry.heavyStructure ? 70f : 210f;
            entry.body.AddForce(direction * (push * strength / difficulty),
                ForceMode.Acceleration);
            entry.body.AddTorque(Random.insideUnitSphere *
                ((entry.heavyStructure ? 24f : 36f) * strength / difficulty),
                ForceMode.Acceleration);
            // Gravity remains dominant even while the horizontal gust is pushing.
            entry.body.AddForce(Vector3.down * 4f, ForceMode.Acceleration);

            bool tree = entry.terrainTree || ContainsAny(entry.lowerName,
                "tree", "arbol", "pine", "spruce", "fir", "pino");
            float maxSpeed = entry.heavyStructure ? 9f : tree ? 18f : 28f;
            entry.body.linearVelocity = Vector3.ClampMagnitude(
                entry.body.linearVelocity, maxSpeed);
            entry.body.angularVelocity = Vector3.ClampMagnitude(
                entry.body.angularVelocity, tree ? 5f : 10f);
            if (tree && entry.transform.position.y > entry.position.y + 25f)
            {
                Vector3 capped = entry.transform.position;
                capped.y = entry.position.y + 25f;
                entry.transform.position = capped;
                Vector3 falling = entry.body.linearVelocity;
                falling.y = Mathf.Min(falling.y, -4f);
                entry.body.linearVelocity = falling;
            }
            if (!Physics.Raycast(entry.transform.position, Vector3.down,
                    1.5f, ~0, QueryTriggerInteraction.Ignore))
                entry.body.AddForce(Vector3.down * 8f,
                    ForceMode.Acceleration);
        }
    }

    void ActivateProp(PropSnapshot entry)
    {
        if (entry == null || entry.transform == null || entry.activated) return;
        entry.activated = true;
        if (entry.structural) return;
        MakeDynamic(entry, false);
    }

    void MakeDynamic(PropSnapshot entry, bool structuralPiece)
    {
        if (entry.body != null && !entry.body.isKinematic) return;
        entry.colliderAdapter =
            entry.transform.GetComponent<WindDynamicColliderAdapter>() ??
            entry.transform.gameObject.AddComponent<WindDynamicColliderAdapter>();
        entry.colliderAdapter.Prepare();

        bool tree = entry.terrainTree || ContainsAny(entry.lowerName,
            "tree", "arbol", "pine", "spruce", "fir", "pino");
        if (!HasEnabledSolidCollider(entry.transform))
        {
            Renderer renderer = entry.transform.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Vector3 lossy = entry.transform.lossyScale;
                if (tree)
                {
                    CapsuleCollider capsule = entry.transform.gameObject
                        .AddComponent<CapsuleCollider>();
                    capsule.direction = 1;
                    capsule.center = entry.transform.InverseTransformPoint(
                        renderer.bounds.center);
                    capsule.height = renderer.bounds.size.y /
                        Mathf.Max(.001f, Mathf.Abs(lossy.y));
                    capsule.radius = Mathf.Max(.12f,
                        Mathf.Min(renderer.bounds.size.x,
                            renderer.bounds.size.z) * .12f /
                        Mathf.Max(.001f,
                            Mathf.Max(Mathf.Abs(lossy.x),
                                Mathf.Abs(lossy.z))));
                    entry.fallbackCollider = capsule;
                }
                else
                {
                    BoxCollider box = entry.transform.gameObject
                        .AddComponent<BoxCollider>();
                    box.center = entry.transform.InverseTransformPoint(
                        renderer.bounds.center);
                    box.size = new Vector3(
                        renderer.bounds.size.x /
                        Mathf.Max(.001f, Mathf.Abs(lossy.x)),
                        renderer.bounds.size.y /
                        Mathf.Max(.001f, Mathf.Abs(lossy.y)),
                        renderer.bounds.size.z /
                        Mathf.Max(.001f, Mathf.Abs(lossy.z)));
                    entry.fallbackCollider = box;
                }
            }
        }
        if (entry.body == null)
        {
            entry.body = entry.transform.gameObject.AddComponent<Rigidbody>();
            entry.bodyCreated = true;
        }

        entry.body.isKinematic = false;
        entry.body.useGravity = true;
        entry.body.constraints = RigidbodyConstraints.None;
        entry.body.mass = Mathf.Clamp(entry.size * entry.size *
            (tree ? 1.15f :
                entry.heavyStructure ? 3.5f :
                structuralPiece ? 1.4f : .65f), 2f, 900f);
        entry.body.linearDamping = .12f;
        entry.body.angularDamping = .25f;
        entry.body.interpolation = RigidbodyInterpolation.Interpolate;
        entry.body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

    }

    static bool HasEnabledSolidCollider(Transform root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            if (collider != null && collider.enabled && !collider.isTrigger)
                return true;
        return false;
    }

    void RestoreVillage()
    {
        foreach (PropSnapshot entry in affected)
        {
            if (entry.transform == null) continue;
            if (entry.terrainTree)
            {
                Destroy(entry.transform.gameObject);
                continue;
            }
            if (!entry.activated) continue;
            if (entry.body != null)
            {
                entry.body.linearVelocity = Vector3.zero;
                entry.body.angularVelocity = Vector3.zero;
                entry.body.isKinematic = true;
            }
            entry.transform.localPosition = entry.localPosition;
            entry.transform.localRotation = entry.localRotation;
            entry.transform.localScale = entry.scale;
            if (entry.bodyCreated && entry.body != null)
                Destroy(entry.body);
            else if (entry.body != null)
            {
                entry.body.isKinematic = entry.originalKinematic;
                entry.body.useGravity = entry.originalUseGravity;
                entry.body.constraints = entry.originalConstraints;
            }
            if (entry.fallbackCollider != null)
                Destroy(entry.fallbackCollider);
            if (entry.colliderAdapter != null)
            {
                entry.colliderAdapter.Restore();
                Destroy(entry.colliderAdapter);
            }
        }
        foreach (TerrainWindSnapshot terrainEntry in affectedTerrains)
        {
            if (terrainEntry.terrain != null)
                terrainEntry.terrain.terrainData = terrainEntry.originalData;
            if (terrainEntry.terrainCollider != null)
                terrainEntry.terrainCollider.terrainData =
                    terrainEntry.originalData;
            if (terrainEntry.runtimeData != null)
                Destroy(terrainEntry.runtimeData);
        }
        affected.Clear();
        affectedTerrains.Clear();
        capturedRoots.Clear();
        foreach (GameObject debris in spawnedWallDebris)
            if (debris != null) Destroy(debris);
        spawnedWallDebris.Clear();
        RestoreFountainWater();
        restorationDay = -1;
    }

    static Transform FindMovableRoot(Transform child)
    {
        Transform current = child;
        Transform namedPiece = null;
        while (current.parent != null && current.parent.GetComponent<Terrain>() == null &&
               current.parent.GetComponent<Canvas>() == null)
        {
            string currentName = current.name.ToLowerInvariant();
            if (IsRoofPiece(currentName) || IsWallPiece(currentName) ||
                ContainsAny(currentName, "beam", "viga", "plank", "board",
                    "tabla", "door", "puerta", "fence", "palisade", "tree",
                    "arbol", "trunk", "tronco", "log", "timber", "madera",
                    "wood", "post", "poste", "crate", "barrel", "table",
                    "chair", "bench"))
                namedPiece = current;
            string parentName = current.parent.name.ToLowerInvariant();
            if (parentName.Contains("environment") ||
                parentName.Contains("village") ||
                parentName.Contains("props")) break;
            current = current.parent;
        }
        // If no meaningful module name exists, keep the renderer itself as the
        // smallest physical piece instead of throwing the whole parent building.
        return namedPiece != null ? namedPiece : child;
    }

    static Renderer FindRendererForCollider(Collider collider)
    {
        if (collider == null) return null;
        Renderer renderer = collider.GetComponent<Renderer>() ??
                            collider.GetComponentInChildren<Renderer>();
        Transform current = collider.transform.parent;
        int depth = 0;
        while (renderer == null && current != null && depth++ < 8)
        {
            renderer = current.GetComponent<Renderer>() ??
                       current.GetComponentInChildren<Renderer>(true);
            current = current.parent;
        }
        return renderer;
    }

    static bool ContainsAny(string value, params string[] words)
    {
        foreach (string word in words)
            if (value.Contains(word)) return true;
        return false;
    }

    static bool IsRoofPiece(string name)
    {
        return ContainsAny(name, "roof", "roofs", "techo", "tejado",
            "cubierta", "thatch", "shingle", "tile", "teja", "rafter",
            "cabio", "purlin", "correa", "bargeboard", "fascia", "eave",
            "alero", "gable", "hastial", "dormer", "buhardilla", "ridge",
            "cumbrera", "chimney", "chimenea");
    }

    static bool IsWallPiece(string name)
    {
        return ContainsAny(name, "wall", "walls", "muralla", "pared",
            "fence", "palisade", "barricade", "gate", "porton");
    }

    static bool IsHousePiece(string name)
    {
        return ContainsAny(name, "house", "casa", "cabin", "cabana",
            "cabaña", "hut", "choza", "building", "edificio");
    }

    static bool IsWoodFramePiece(string name)
    {
        return ContainsAny(name, "log", "tronco", "timber", "madera",
            "wood", "beam", "viga", "post", "poste", "plank", "board",
            "tabla", "frame", "marco", "support", "soporte");
    }

    void SpawnWallLogDebris(PropSnapshot entry, Vector3 windDirection)
    {
        if (entry == null || entry.transform == null ||
            spawnedWallDebris.Count >= MaxWallDebris)
            return;

        Renderer source = entry.transform.GetComponentInChildren<Renderer>();
        Bounds bounds = source != null
            ? source.bounds
            : new Bounds(entry.transform.position,
                Vector3.one * Mathf.Max(1f, entry.size * .35f));
        Material material = source != null && source.sharedMaterial != null
            ? source.sharedMaterial
            : null;
        int count = Mathf.Clamp(Mathf.RoundToInt(
            Mathf.Max(bounds.size.x, bounds.size.z) * .65f), 2, 5);
        count = Mathf.Min(count, MaxWallDebris - spawnedWallDebris.Count);

        for (int i = 0; i < count; i++)
        {
            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "Wind_WallLog_Debris";
            float length = Random.Range(.8f, 1.8f);
            float radius = Random.Range(.07f, .13f);
            log.transform.position = bounds.center + new Vector3(
                Random.Range(-bounds.extents.x, bounds.extents.x),
                Random.Range(-bounds.extents.y * .6f, bounds.extents.y * .6f),
                Random.Range(-bounds.extents.z, bounds.extents.z));
            Vector3 axis = (windDirection + Random.insideUnitSphere * .45f)
                .normalized;
            if (axis.sqrMagnitude < .01f) axis = Vector3.right;
            log.transform.rotation =
                Quaternion.FromToRotation(Vector3.up, axis);
            log.transform.localScale = new Vector3(radius, length * .5f,
                radius);

            Renderer logRenderer = log.GetComponent<Renderer>();
            if (logRenderer != null && material != null)
                logRenderer.sharedMaterial = material;
            Rigidbody body = log.AddComponent<Rigidbody>();
            body.mass = Random.Range(1.5f, 4f);
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;
            body.AddForce((windDirection * Random.Range(8f, 15f) +
                           Vector3.up * Random.Range(1f, 4f)),
                ForceMode.VelocityChange);
            body.AddTorque(Random.insideUnitSphere * 8f,
                ForceMode.VelocityChange);
            spawnedWallDebris.Add(log);
        }
    }

    static string HierarchyName(Transform current)
    {
        string result = "";
        int depth = 0;
        while (current != null && depth++ < 7)
        {
            result += " " + current.name.ToLowerInvariant();
            current = current.parent;
        }
        return result;
    }

    static bool HasInteractable(Transform root)
    {
        foreach (MonoBehaviour behaviour in
                 root.GetComponentsInParent<MonoBehaviour>(true))
            if (behaviour is IInteractable) return true;
        foreach (MonoBehaviour behaviour in
                 root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is IInteractable) return true;
        return false;
    }

    static bool IsCharacterOrCreature(Transform root)
    {
        if (root == null) return true;
        if (root.GetComponentInParent<PlayerStats>() != null ||
            root.GetComponentInParent<EnemyStats>() != null)
            return true;
        Animator animator = root.GetComponentInParent<Animator>();
        if (animator != null && animator.avatar != null &&
            animator.avatar.isHuman)
            return true;
        string name = root.root.name.ToLowerInvariant();
        return ContainsAny(name, "player", "npc", "tonio", "nahue",
            "merchant", "herrero", "viejo", "goblin", "skeleton",
            "esqueleto", "enemy", "boss", "demon", "crab", "ciervo",
            "deer", "chicken", "gallina", "dog", "perro");
    }

    void DisableFountainWater()
    {
        RestoreFountainWater();
        HealingFountainInteractable fountain =
            FindAnyObjectByType<HealingFountainInteractable>(
                FindObjectsInactive.Include);
        if (fountain == null) return;
        fountainRoot = fountain.transform;

        foreach (ParticleSystem particles in
                 fountainRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            fountainWater.Add(new FountainWaterState
            {
                particles = particles,
                particlesWerePlaying = particles.isPlaying
            });
            particles.Stop(true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach (Renderer renderer in
                 fountainRoot.GetComponentsInChildren<Renderer>(true))
            if (IsFountainWaterRenderer(renderer, true))
                HideWaterRenderer(renderer);

        // The circular water surface is a separate scene object rather than a
        // child of the fountain prefab, so explicitly remove every nearby water
        // renderer as soon as the gust begins.
        foreach (Renderer renderer in
                 FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            if (renderer == null) continue;
            Vector3 flat = renderer.bounds.center - fountainRoot.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 15f * 15f) continue;
            if (IsFountainWaterRenderer(renderer, false))
                HideWaterRenderer(renderer);
        }
    }

    bool IsFountainWaterRenderer(Renderer renderer, bool insideFountain)
    {
        if (renderer == null) return false;
        bool nearFountain = insideFountain;
        if (!nearFountain && fountainRoot != null)
        {
            Vector3 flat = renderer.bounds.center - fountainRoot.position;
            flat.y = 0f;
            nearFountain = flat.sqrMagnitude <= 8f * 8f;
        }
        if (!nearFountain) return false;

        string objectName = renderer.name.ToLowerInvariant();
        if (ContainsAny(objectName, "water", "agua", "liquid", "splash",
                "stream", "chorro"))
            return true;
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material == null) continue;
            string materialName = material.name.ToLowerInvariant();
            string shaderName = material.shader != null
                ? material.shader.name.ToLowerInvariant()
                : "";
            if (ContainsAny(materialName, "water", "agua", "liquid") ||
                ContainsAny(shaderName, "water", "liquid"))
                return true;
        }
        return false;
    }

    void HideWaterRenderer(Renderer renderer)
    {
        if (renderer == null) return;
        foreach (FountainWaterState state in fountainWater)
            if (state.renderer == renderer) return;
        fountainWater.Add(new FountainWaterState
        {
            renderer = renderer,
            rendererEnabled = renderer.enabled
        });
        renderer.enabled = false;
    }

    void RestoreFountainWater()
    {
        foreach (FountainWaterState state in fountainWater)
        {
            if (state.renderer != null)
                state.renderer.enabled = state.rendererEnabled;
            if (state.particles != null && state.particlesWerePlaying)
                state.particles.Play(true);
        }
        fountainWater.Clear();
        fountainRoot = null;
    }

    void BuildWarning()
    {
        GameObject canvasGo = new GameObject("ExtremeWindWarningCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3900;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("Warning", typeof(RectTransform),
            typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(canvasGo.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.32f, .76f);
        rect.anchorMax = new Vector2(.68f, .9f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = panel.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        warningGroup = panel.GetComponent<CanvasGroup>();

        GameObject textGo = new GameObject("Text", typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textGo.transform.SetParent(panel.transform, false);
        warningText = textGo.GetComponent<TextMeshProUGUI>();
        warningText.alignment = TextAlignmentOptions.Center;
        warningText.fontSize = 25f;
        warningText.color = new Color(1f, .76f, .3f);
        warningText.outlineWidth = .18f;
        RectTransform textRect = warningText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 12f);
        textRect.offsetMax = new Vector2(-18f, -12f);
        panel.SetActive(false);
    }

    void BuildFlyingLeaves()
    {
        GameObject leaves = new GameObject("ExtremeGust_FlyingLeaves",
            typeof(ParticleSystem));
        leaves.transform.SetParent(transform, false);
        flyingLeaves = leaves.GetComponent<ParticleSystem>();
        flyingLeaves.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = flyingLeaves.main;
        main.loop = true;
        main.duration = 8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(.025f, .115f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 3600;

        ParticleSystem.EmissionModule emission = flyingLeaves.emission;
        emission.rateOverTime = 260f;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = flyingLeaves.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 12f, 30f);

        ParticleSystem.NoiseModule noise = flyingLeaves.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(1.3f, 3.8f);
        noise.frequency = .34f;
        noise.scrollSpeed = 1.4f;

        ParticleSystem.RotationOverLifetimeModule rotation =
            flyingLeaves.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-7f, 7f);

        ParticleSystem.CollisionModule collision = flyingLeaves.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.dampen = .92f;
        collision.bounce = 0f;
        collision.lifetimeLoss = 0f;
        collision.collidesWith = ~0;
        collision.maxCollisionShapes = 256;

        ParticleSystemRenderer renderer =
            leaves.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = .06f;
        renderer.lengthScale = 1.25f;
        renderer.sortingFudge = 2f;
        Material leafMaterial = Resources.Load<Material>(
            "NatureFX/FlyingLeaves");
        if (leafMaterial != null)
            renderer.sharedMaterial = leafMaterial;
    }

    void UpdateFlyingLeaves()
    {
        if (flyingLeaves == null || WindManager.Instance == null)
            return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            flyingLeaves.transform.position =
                player.transform.position + Vector3.up * 4f;

        Vector3 velocity = WindManager.Instance.WindDirection *
                           Mathf.Lerp(18f, 34f,
                               Mathf.Clamp01(WindManager.Instance.CurrentStrength01 / 2f));
        ParticleSystem.VelocityOverLifetimeModule module =
            flyingLeaves.velocityOverLifetime;
        module.enabled = true;
        module.space = ParticleSystemSimulationSpace.World;
        // All axes deliberately use constant curves to avoid Unity's mixed-curve warning.
        module.x = new ParticleSystem.MinMaxCurve(velocity.x);
        module.y = new ParticleSystem.MinMaxCurve(1.4f);
        module.z = new ParticleSystem.MinMaxCurve(velocity.z);
    }
}

public sealed class WindDynamicColliderAdapter : MonoBehaviour
{
    readonly List<MeshCollider> disabledMeshes = new List<MeshCollider>();
    readonly List<BoxCollider> temporaryBoxes = new List<BoxCollider>();
    bool prepared;

    public void Prepare()
    {
        if (prepared) return;
        prepared = true;
        foreach (MeshCollider meshCollider in
                 GetComponentsInChildren<MeshCollider>(true))
        {
            if (meshCollider == null || !meshCollider.enabled ||
                meshCollider.convex) continue;
            BoxCollider box = meshCollider.gameObject.AddComponent<BoxCollider>();
            if (meshCollider.sharedMesh != null)
            {
                box.center = meshCollider.sharedMesh.bounds.center;
                box.size = meshCollider.sharedMesh.bounds.size;
            }
            box.isTrigger = meshCollider.isTrigger;
            box.sharedMaterial = meshCollider.sharedMaterial;
            meshCollider.enabled = false;
            disabledMeshes.Add(meshCollider);
            temporaryBoxes.Add(box);
        }
    }

    public void Restore()
    {
        foreach (MeshCollider meshCollider in disabledMeshes)
            if (meshCollider != null) meshCollider.enabled = true;
        foreach (BoxCollider box in temporaryBoxes)
            if (box != null) Destroy(box);
        disabledMeshes.Clear();
        temporaryBoxes.Clear();
        prepared = false;
    }
}

public sealed class WindShelterDetector : MonoBehaviour
{
    public static bool IsPlayerSheltered { get; private set; }
    float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.GetComponent<WindShelterDetector>() == null)
            player.AddComponent<WindShelterDetector>();
    }

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + .2f;
        IsPlayerSheltered = HasHouseRoof();
    }

    bool HasHouseRoof()
    {
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up,
            Vector3.up, 12f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            string name = hit.collider.transform.root.name.ToLowerInvariant();
            if (name.Contains("house") || name.Contains("casa") ||
                name.Contains("building") || name.Contains("inn") ||
                name.Contains("tent") || name.Contains("carpa"))
                return true;
        }
        return false;
    }

    void OnDisable() => IsPlayerSheltered = false;
}

using System.Collections;
using UnityEngine;

// Gives Tonio a controlled indoor/outdoor routine. The route is derived from the nearest
// HouseDoor and staircase belonging to that same house, so moving House_8 keeps the path valid.
[RequireComponent(typeof(NPCWander))]
public sealed class TonioHouseRoutine : MonoBehaviour
{
    [Header("Rutina")]
    [SerializeField] float firstDepartureDelay = 35f;
    [SerializeField] float departureIntervalMin = 70f;
    [SerializeField] float departureIntervalMax = 115f;
    [SerializeField] float outsideWaitMin = 7f;
    [SerializeField] float outsideWaitMax = 14f;
    [SerializeField] float routeSpeed = .5f;
    [SerializeField] float interactionLockDistance = 5f;

    NPCWander wander;
    HouseDoor door;
    Transform houseRoot;
    Renderer staircase;
    Bounds staircaseBounds;
    Transform player;
    Vector3 home;
    bool routeActive;
    bool lastWalkSucceeded;

    public bool IsRouteActive => routeActive;
    public bool IsInsideHouse
    {
        get
        {
            if (door == null || staircase == null)
                return true;

            Vector3 outsideDirection = FlatDirection(
                door.transform.position, staircaseBounds.center);
            Vector3 fromDoor = transform.position - door.transform.position;
            fromDoor.y = 0f;

            // A small margin keeps Tonio peaceful while he is standing in the doorway.
            return Vector3.Dot(fromDoor, outsideDirection) <= .12f;
        }
    }

    void Awake()
    {
        wander = GetComponent<NPCWander>();
    }

    IEnumerator Start()
    {
        ResolveHouseRoute();
        PlaceAtSafeInteriorHome();
        yield return new WaitForSeconds(firstDepartureDelay);

        while (true)
        {
            if (CanDepart())
                yield return WalkOutsideAndReturn();

            yield return new WaitForSeconds(Random.Range(
                departureIntervalMin, departureIntervalMax));
        }
    }

    bool CanDepart()
    {
        if (routeActive || door == null || staircase == null)
            return false;
        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState != GameState.Exploration)
            return false;
        if (TonioIsCurrentObjective())
            return false;

        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }

        // Do not make Tonio walk away while the player is approaching him for a quest.
        return player == null ||
               HorizontalDistance(transform.position, player.position) >
               interactionLockDistance;
    }

    void ResolveHouseRoute()
    {
        float bestDoorDistance = float.PositiveInfinity;
        foreach (HouseDoor candidate in
                 FindObjectsByType<HouseDoor>(FindObjectsInactive.Include))
        {
            float distance = HorizontalDistance(transform.position,
                candidate.transform.position);
            if (distance >= bestDoorDistance)
                continue;
            bestDoorDistance = distance;
            door = candidate;
        }

        if (door == null || bestDoorDistance > 7f)
        {
            door = null;
            Debug.LogWarning("[TonioHouseRoutine] No se encontro una puerta cercana a Tonio.");
            return;
        }

        houseRoot = FindHouseRoot(door.transform);
        float bestStairDistance = float.PositiveInfinity;
        Renderer[] candidates = houseRoot != null
            ? houseRoot.GetComponentsInChildren<Renderer>(true)
            : FindObjectsByType<Renderer>(FindObjectsInactive.Include);

        foreach (Renderer candidate in candidates)
        {
            string lower = candidate.name.ToLowerInvariant();
            if (!lower.Contains("stair") && !lower.Contains("step"))
                continue;

            float distance = HorizontalDistance(door.transform.position,
                candidate.bounds.center);
            if (distance >= bestStairDistance)
                continue;
            bestStairDistance = distance;
            staircase = candidate;
        }

        if (staircase == null)
        {
            Debug.LogWarning("[TonioHouseRoutine] La casa de Tonio no tiene escalera detectable.");
            return;
        }

        // Modular houses build one staircase from several separately named/rendered steps.
        // Treat the nearby pieces as a single route instead of measuring only the top step.
        staircaseBounds = staircase.bounds;
        foreach (Renderer candidate in candidates)
        {
            string lower = candidate.name.ToLowerInvariant();
            if (!lower.Contains("stair") && !lower.Contains("step"))
                continue;
            if (HorizontalDistance(candidate.bounds.center,
                    staircase.bounds.center) > 3.5f)
                continue;
            staircaseBounds.Encapsulate(candidate.bounds);
        }
    }

    void PlaceAtSafeInteriorHome()
    {
        if (door == null || staircase == null)
        {
            home = transform.position;
            return;
        }

        // The staircase is necessarily outside the entrance. Move in the exact opposite
        // direction and align with the centre of the doorway, avoiding the lateral wall where
        // Tonio's old scene position was authored.
        Vector3 outsideDirection = FlatDirection(
            door.transform.position, staircaseBounds.center);
        Vector3 safeInterior = door.transform.position -
                               outsideDirection * 1.5f;
        safeInterior.y = transform.position.y;

        CharacterController controller = GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;
        transform.position = safeInterior;
        if (controllerWasEnabled)
            controller.enabled = true;

        GetComponent<GroundSnapOnStart>()?.SnapNow();
        home = transform.position;
    }

    IEnumerator WalkOutsideAndReturn()
    {
        routeActive = true;
        wander.PauseForInteraction();

        Vector3 doorPosition = door.transform.position;
        Vector3 towardHome = FlatDirection(doorPosition, home);
        Vector3 outsideDirection = -towardHome;
        Vector3 insideDoor = doorPosition + towardHome * .72f;
        Vector3 outsideDoor = doorPosition - towardHome * .62f;

        Vector3 stairCenter = staircaseBounds.center;
        float stairHalfLength = Mathf.Max(.45f,
            ProjectedHorizontalExtent(staircaseBounds, outsideDirection));
        Vector3 stairTop = stairCenter - outsideDirection *
            Mathf.Max(.18f, stairHalfLength * .82f);
        Vector3 stairQuarter = Vector3.Lerp(stairTop,
            stairCenter + outsideDirection * stairHalfLength, .33f);
        Vector3 stairThreeQuarter = Vector3.Lerp(stairTop,
            stairCenter + outsideDirection * stairHalfLength, .72f);
        Vector3 stairBottom = stairCenter + outsideDirection *
            (stairHalfLength + .18f);
        Vector3 terrainPoint = FindTerrainBeyondStairs(
            stairBottom, outsideDirection);
        Vector3 yardPoint = terrainPoint + outsideDirection * .85f;
        float upperY = home.y;
        float terrainY = terrainPoint.y;
        stairTop.y = upperY;
        stairQuarter.y = Mathf.Lerp(upperY, terrainY, .30f);
        stairThreeQuarter.y = Mathf.Lerp(upperY, terrainY, .72f);
        stairBottom.y = Mathf.Lerp(upperY, terrainY, .92f);
        yardPoint.y = terrainY;

        bool completed = true;
        yield return WalkTo(insideDoor, 8f);
        completed = lastWalkSucceeded;
        if (completed)
        {
            Face(doorPosition);
            door.OpenForNpc();
            yield return WaitForDoor();
            yield return WalkTo(outsideDoor, 6f);
            completed = lastWalkSucceeded;
        }
        if (completed)
        {
            yield return WalkHeightRoute(new[]
            {
                stairTop, stairQuarter, stairThreeQuarter,
                stairBottom, terrainPoint
            }, 16f);
            completed = lastWalkSucceeded;
        }
        if (completed)
        {
            yield return WalkTo(yardPoint, 6f);
            completed = lastWalkSucceeded;
        }

        if (completed)
        {
            wander.StopCombatMove();
            yield return WaitWhileAvailable(Random.Range(
                outsideWaitMin, outsideWaitMax));
            yield return WalkTo(terrainPoint, 7f);
            completed = lastWalkSucceeded;
            if (completed)
            {
                yield return WalkHeightRoute(new[]
                {
                    terrainPoint, stairBottom, stairThreeQuarter,
                    stairQuarter, stairTop
                }, 16f);
                completed = lastWalkSucceeded;
            }
            if (completed)
            {
                yield return WalkTo(outsideDoor, 6f);
                completed = lastWalkSucceeded;
            }
            if (completed)
            {
                yield return WalkTo(insideDoor, 6f);
                completed = lastWalkSucceeded;
            }
            if (completed)
            {
                yield return WalkTo(home, 8f);
                completed = lastWalkSucceeded;
            }
        }

        wander.StopCombatMove();
        if (door != null)
        {
            door.CloseForNpc();
            yield return WaitForDoor();
        }
        if (!completed)
            RestoreHomePosition();
        routeActive = false;
        wander.ResumeWander();
    }

    Vector3 FindTerrainBeyondStairs(Vector3 start, Vector3 outsideDirection)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
            return start + outsideDirection * 1.4f;

        // Some generated house-floor triggers extend beyond the visible doorway. Search until
        // the raycast resolves the actual Terrain rather than stopping on that invisible ledge.
        Vector3 best = start + outsideDirection * 1.4f;
        for (float distance = .25f; distance <= 6f; distance += .35f)
        {
            Vector3 candidate = start + outsideDirection * distance;
            float terrainY = terrain.SampleHeight(candidate) +
                             terrain.transform.position.y;
            candidate.y = Mathf.Max(transform.position.y, terrainY);

            if (GroundUtility.TryGetGround(candidate, transform,
                    out GroundUtility.GroundHit ground, 5f, 10f) &&
                (ground.IsTerrain || Mathf.Abs(ground.y - terrainY) <= .12f))
            {
                candidate.y = terrainY;
                return candidate;
            }
            best = new Vector3(candidate.x, terrainY, candidate.z);
        }
        return best;
    }

    IEnumerator WaitForDoor()
    {
        float timeout = 1.5f;
        while (door != null && door.IsAnimating && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator WaitWhileAvailable(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (GameManager.Instance == null ||
                GameManager.Instance.CurrentState == GameState.Exploration)
                elapsed += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator WalkTo(Vector3 destination, float timeout)
    {
        float elapsed = 0f;
        while (HorizontalDistance(transform.position, destination) > .18f &&
               elapsed < timeout)
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameState.Exploration)
            {
                wander.StopCombatMove();
                yield return null;
                continue;
            }

            Vector3 direction = destination - transform.position;
            direction.y = 0f;
            wander.MoveForCombat(direction, routeSpeed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        wander.StopCombatMove();
        lastWalkSucceeded = elapsed < timeout;
    }

    IEnumerator WalkHeightRoute(Vector3[] waypoints, float timeout)
    {
        float elapsed = 0f;
        for (int index = 0; index < waypoints.Length; index++)
        {
            Vector3 destination = waypoints[index];
            while ((HorizontalDistance(transform.position, destination) > .12f ||
                    Mathf.Abs(transform.position.y - destination.y) > .08f) &&
                   elapsed < timeout)
            {
                if (GameManager.Instance != null &&
                    GameManager.Instance.CurrentState != GameState.Exploration)
                {
                    wander.StopCombatMove();
                    yield return null;
                    continue;
                }

                Vector3 direction = destination - transform.position;
                direction.y = 0f;
                float horizontalRemaining = Mathf.Max(.12f,
                    HorizontalDistance(transform.position, destination));
                float verticalSpeed = routeSpeed *
                    Mathf.Max(.35f, Mathf.Abs(destination.y -
                        transform.position.y) / horizontalRemaining);
                wander.MoveForRoute(direction, routeSpeed,
                    destination.y, verticalSpeed);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed >= timeout)
                break;
        }

        wander.StopCombatMove();
        lastWalkSucceeded = elapsed < timeout;
    }

    void Face(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > .001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    bool TonioIsCurrentObjective()
    {
        QuestManager quests = QuestManager.Instance;
        if (quests == null)
            return false;

        switch (quests.MerchantIntroductionState)
        {
            case PrimaryQuestState.ReturnToTonioWithEquipment:
            case PrimaryQuestState.ReturnToTonioAfterPassage:
            case PrimaryQuestState.TalkToTonioAfterNahue:
            case PrimaryQuestState.InvestigatePlagueTalkToTonio:
            case PrimaryQuestState.ReturnToTonioAfterTrial:
            case PrimaryQuestState.TalkToTonioFourthMission:
            case PrimaryQuestState.ReturnToTonioWithDungeonKey:
            case PrimaryQuestState.TalkToTonioFifthMission:
            case PrimaryQuestState.SixthTalkToPetrifiedTonio:
            case PrimaryQuestState.SeventhFindTonio:
                return true;
            default:
                return false;
        }
    }

    void RestoreHomePosition()
    {
        CharacterController controller = GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled)
            controller.enabled = false;
        transform.position = home;
        if (wasEnabled)
            controller.enabled = true;
    }

    static Transform FindHouseRoot(Transform current)
    {
        while (current != null)
        {
            if (current.name.StartsWith("House_", System.StringComparison.Ordinal))
                return current;
            current = current.parent;
        }
        return null;
    }

    static Vector3 FlatDirection(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        direction.y = 0f;
        return direction.sqrMagnitude > .001f
            ? direction.normalized
            : Vector3.forward;
    }

    static float ProjectedHorizontalExtent(Bounds bounds, Vector3 direction)
    {
        direction.y = 0f;
        direction.Normalize();
        return Mathf.Abs(direction.x) * bounds.extents.x +
               Mathf.Abs(direction.z) * bounds.extents.z;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}

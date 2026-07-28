using ithappy.Animals_FREE;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Same shape as SkeletonSpawner/GoblinSpawner (invisible at runtime, gizmo sphere only in the
// Scene view, tries to add one deer every spawnInterval seconds while the local population is
// below the cap), but spawns a passive/timid animal via AnimalAI instead of a combat enemy.
public class DeerSpawner : MonoBehaviour
{
    [SerializeField] GameObject deerPrefab;
    [SerializeField] float spawnRadius = 10f;
    [SerializeField] int maxDeerInRadius = 3;
    [SerializeField] float spawnInterval = 50f;

    float nextSpawnTime;
    Terrain terrain;
    bool prefabResolved;

    const string FallbackPrefabPath = "Assets/ithappy/Animals_FREE/Prefabs/Deer_001.prefab";

    void Awake()
    {
        terrain = Terrain.activeTerrain;
        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
    }

    void Update()
    {
        if (Time.time < nextSpawnTime)
            return;

        nextSpawnTime = Time.time + spawnInterval;
        TrySpawn();
    }

    void TrySpawn()
    {
        ResolvePrefabIfNeeded();
        if (deerPrefab == null)
            return;
        if (CountDeerInRadius() >= maxDeerInRadius)
            return;

        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        Vector3 position = transform.position + new Vector3(offset.x, 0f, offset.y);
        if (terrain != null)
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 0.05f;

        GameObject instance = Instantiate(deerPrefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        instance.name = "Animal_Deer_Spawned";
        ConfigureDeer(instance);
    }

    // Mirrors AnimalSetup.ConfigureAnimal's Deer config (health/attack/detection/flee/wander/
    // walk/run) so spawned deer behave identically to the hand-placed one.
    static void ConfigureDeer(GameObject deer)
    {
        deer.tag = "Enemy";
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            deer.layer = enemyLayer;
            foreach (Transform child in deer.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = enemyLayer;
        }

        if (deer.GetComponent<MovePlayerInput>() is MovePlayerInput input)
            Destroy(input);

        CreatureMover mover = deer.GetComponent<CreatureMover>();
        if (mover == null)
            mover = deer.AddComponent<CreatureMover>();
        SetSerialized(mover, "m_WalkSpeed", 3f);
        SetSerialized(mover, "m_RunSpeed", 10f);
        SetSerialized(mover, "m_RotateSpeed", 220f);
        SetSerialized(mover, "m_Space", (int)Space.World);

        CharacterController cc = deer.GetComponent<CharacterController>();
        if (cc == null)
            cc = deer.AddComponent<CharacterController>();
        cc.stepOffset = Mathf.Min(cc.height * 0.45f, 0.45f);
        cc.slopeLimit = 45f;

        EnemyStats stats = deer.GetComponent<EnemyStats>();
        if (stats == null)
            stats = deer.AddComponent<EnemyStats>();
        SetSerialized(stats, "maxHealth", 50f);
        SetSerialized(stats, "attack", 3f);
        SetSerialized(stats, "xpReward", 18);
        SetSerialized(stats, "minGoldDrop", 1);
        SetSerialized(stats, "maxGoldDrop", 6);

        AnimalAI ai = deer.GetComponent<AnimalAI>();
        if (ai == null)
            ai = deer.AddComponent<AnimalAI>();
        SetSerialized(ai, "animalKind", (int)AnimalKind.Deer);
        SetSerialized(ai, "temperament", (int)AnimalTemperament.Timid);
        SetSerialized(ai, "detectionRadius", 8f);
        SetSerialized(ai, "fleeRadius", 5.5f);
        SetSerialized(ai, "leashRadius", 16f);
        SetSerialized(ai, "wanderRadius", 13f);
        SetSerialized(ai, "stepDistance", 4f);
        SetSerialized(ai, "attackRadius", 1.25f);
        SetSerialized(ai, "attackCooldown", 2.2f);
        SetSerialized(ai, "lungeSeconds", 0.25f);
        SetSerialized(ai, "canAttack", false);
        SetSerialized(ai, "playerMask", LayerMask.GetMask("Player"));

        foreach (Collider collider in deer.GetComponentsInChildren<Collider>(true))
            collider.isTrigger = false;
    }

#if UNITY_EDITOR
    static void SetSerialized(Object target, string propertyName, float value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop != null) prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, int value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            if (prop.propertyType == SerializedPropertyType.Enum)
                prop.enumValueIndex = value;
            else
                prop.intValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, bool value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop != null) prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
#else
    static void SetSerialized(Object target, string propertyName, float value) { }
    static void SetSerialized(Object target, string propertyName, int value) { }
    static void SetSerialized(Object target, string propertyName, bool value) { }
#endif

    void ResolvePrefabIfNeeded()
    {
        if (prefabResolved)
            return;
        prefabResolved = true;

        if (deerPrefab != null)
            return;

#if UNITY_EDITOR
        deerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackPrefabPath);
#endif
    }

    int CountDeerInRadius()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, spawnRadius);
        int count = 0;
        foreach (Collider hit in hits)
        {
            AnimalAI ai = hit.GetComponentInParent<AnimalAI>();
            if (ai != null && ai.Kind == AnimalKind.Deer)
                count++;
        }
        return count;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.25f);
        Gizmos.DrawSphere(transform.position, spawnRadius);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}

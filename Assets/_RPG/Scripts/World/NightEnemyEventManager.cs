using TMPro;
using UnityEngine;

public class NightEnemyEventManager : MonoBehaviour
{
    public static NightEnemyEventManager Instance { get; private set; }
    public static bool IsNightNow
    {
        get
        {
            TenkokuDayNightCycle cycle = FindAnyObjectByType<TenkokuDayNightCycle>();
            if (cycle == null) return false;
            float hour = cycle.CurrentHour;
            return hour >= 18f || hour < 6f;
        }
    }

    bool wasNight;
    bool rolledThisNight;
    public bool IsBloodRainActive { get; private set; }
    ParticleSystem bloodRain;
    float nextWorldRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance == null) new GameObject("NightEnemyEventManager").AddComponent<NightEnemyEventManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        wasNight = IsNightNow;
        ApplyNightSpawnerBoost(wasNight);
        if (wasNight) RollBloodRain();
    }

    void Update()
    {
        bool night = IsNightNow;
        if (night != wasNight)
        {
            wasNight = night;
            ApplyNightSpawnerBoost(night);
            if (night) { rolledThisNight = false; RollBloodRain(); }
            else StopBloodRain();
        }
        if (bloodRain != null && Camera.main != null)
        {
            PlayerStats player = FindAnyObjectByType<PlayerStats>();
            if (player != null) bloodRain.transform.position = player.transform.position + Vector3.up * 12f;
        }
        if (Time.unscaledTime >= nextWorldRefresh)
        {
            nextWorldRefresh = Time.unscaledTime + 2f;
            ApplyNightSpawnerBoost(night);
            if (IsBloodRainActive)
                foreach (EnemyStats enemy in FindObjectsByType<EnemyStats>(FindObjectsInactive.Exclude))
                    ApplySpawnModifiers(enemy.gameObject);
        }
    }

    void RollBloodRain()
    {
        if (rolledThisNight) return;
        rolledThisNight = true;
        if (Random.value < .05f) StartBloodRain();
    }

    void ApplyNightSpawnerBoost(bool enabled)
    {
        foreach (GoblinSpawner spawner in FindObjectsByType<GoblinSpawner>(FindObjectsInactive.Include)) spawner.SetNightSpawnBoost(enabled);
        foreach (SkeletonSpawner spawner in FindObjectsByType<SkeletonSpawner>(FindObjectsInactive.Include)) spawner.SetNightSpawnBoost(enabled);
    }

    public void ApplySpawnModifiers(GameObject enemy)
    {
        if (!IsBloodRainActive || enemy == null || enemy.GetComponent<BloodRainEnemyBuff>() != null) return;
        enemy.AddComponent<BloodRainEnemyBuff>();
        enemy.GetComponent<EnemyStats>()?.SetBloodRainBuff(true);
        enemy.GetComponent<EnemyAI>()?.SetBloodRainSpeedBuff(true);
    }

    void StartBloodRain()
    {
        IsBloodRainActive = true;
        foreach (EnemyStats enemy in FindObjectsByType<EnemyStats>(FindObjectsInactive.Exclude)) ApplySpawnModifiers(enemy.gameObject);
        CreateRain();
        PlayerStats player = FindAnyObjectByType<PlayerStats>();
        if (player != null) QuestOverheadThought.Show(player.transform,
            "La noche sangra... los enemigos se han vuelto mucho m\u00e1s poderosos.", 6f);
    }

    void StopBloodRain()
    {
        if (!IsBloodRainActive) return;
        IsBloodRainActive = false;
        foreach (BloodRainEnemyBuff marker in FindObjectsByType<BloodRainEnemyBuff>(FindObjectsInactive.Include))
        {
            if (marker == null) continue;
            marker.GetComponent<EnemyStats>()?.SetBloodRainBuff(false);
            marker.GetComponent<EnemyAI>()?.SetBloodRainSpeedBuff(false);
            Destroy(marker);
        }
        if (bloodRain != null) Destroy(bloodRain.gameObject);
        bloodRain = null;
    }

    void CreateRain()
    {
        GameObject go = new GameObject("BloodRainVFX", typeof(ParticleSystem));
        DontDestroyOnLoad(go);
        bloodRain = go.GetComponent<ParticleSystem>();
        var main = bloodRain.main; main.loop=true; main.startLifetime=1.7f; main.startSpeed=18f;
        main.startSize=new ParticleSystem.MinMaxCurve(.025f,.07f); main.startColor=new Color(.55f,.005f,.012f,.72f);
        main.maxParticles=3500; main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=bloodRain.emission; emission.rateOverTime=900f;
        var shape=bloodRain.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(28f,.3f,28f);
        var velocity=bloodRain.velocityOverLifetime; velocity.enabled=true; velocity.y=-14f;
        ParticleSystemRenderer renderer=go.GetComponent<ParticleSystemRenderer>();
        Shader shader=Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
        Material material=new Material(shader); if(material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",new Color(.6f,0f,.015f,.75f));
        renderer.sharedMaterial=material; bloodRain.Play();
    }
}

public class BloodRainEnemyBuff : MonoBehaviour { }

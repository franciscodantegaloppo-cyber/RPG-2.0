using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(EnemyStats))]
public class KingGoblinHealthBar : MonoBehaviour
{
    [SerializeField] Texture frameTexture;
    [SerializeField] GameObject healthBarModel;
    [SerializeField] Vector3 localOffset = new Vector3(0f, 3.25f, 0f);
    [SerializeField] Vector3 modelScale = new Vector3(0.95f, 0.95f, 0.95f);
    [SerializeField] Vector3 modelRotation = new Vector3(0f, 180f, 0f);
    [SerializeField] Vector3 redFillOffset = new Vector3(0f, -0.03f, -0.04f);
    [SerializeField] Vector2 redFillSize = new Vector2(1.58f, 0.34f);
    [SerializeField] bool alwaysVisible = true;

    EnemyStats stats;
    Transform root;
    Transform redFill;
    MeshRenderer[] renderers;
    Camera mainCamera;
    Vector3 redFillFullScale;
    Vector3 redFillFullPosition;

    const string FallbackModelPath = "Assets/_Recovery/king goblin health bar/Meshy_AI_Quiero_que_hagas_una__0710213526_texture.obj";

    void Awake()
    {
        stats = GetComponent<EnemyStats>();
        localOffset = new Vector3(0f, 3.25f, 0f);
        redFillOffset = new Vector3(0f, -0.03f, -0.04f);
        redFillSize = new Vector2(1.58f, 0.34f);
        Build();
    }

    void OnEnable()
    {
        if (stats == null)
            stats = GetComponent<EnemyStats>();
        stats.OnHealthChanged += HandleHealthChanged;
        stats.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (stats == null)
            return;
        stats.OnHealthChanged -= HandleHealthChanged;
        stats.OnDeath -= HandleDeath;
    }

    void Build()
    {
        GameObject rootGo = new GameObject("KingGoblinHealthBar");
        rootGo.transform.SetParent(transform, false);
        rootGo.transform.localPosition = localOffset;
        rootGo.transform.localScale = modelScale;
        root = rootGo.transform;

        GameObject model = ResolveModel();
        if (model != null)
        {
            GameObject instance = Instantiate(model, root);
            instance.name = "ImportedKingGoblinHealthBar";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(modelRotation);
            instance.transform.localScale = Vector3.one;
        }

        redFill = BuildRedFill(root).transform;
        redFillFullScale = redFill.localScale;
        redFillFullPosition = redFill.localPosition;
        renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        SetVisible(alwaysVisible);

        RefreshFill(stats != null ? stats.CurrentHealth : 1f, stats != null ? stats.MaxHealth : 1f);
    }

    void HandleHealthChanged(float current, float max)
    {
        RefreshFill(current, max);
        SetVisible(true);
    }

    void HandleDeath()
    {
        SetVisible(false);
    }

    void Update()
    {
        if (root == null)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera != null)
            root.rotation = Quaternion.LookRotation(root.position - mainCamera.transform.position, Vector3.up);
    }

    void RefreshFill(float current, float max)
    {
        if (redFill == null)
            return;

        float percent = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        Vector3 scale = redFillFullScale;
        scale.x *= percent;
        redFill.localScale = scale;
        redFill.localPosition = redFillFullPosition + Vector3.left * (redFillFullScale.x - scale.x) * 0.5f;
    }

    GameObject BuildRedFill(Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "RedHealthFill";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = redFillOffset;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(redFillSize.x, redFillSize.y, 1f);

        Collider col = go.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        Material mat = new Material(shader);
        Color violet = new Color(0.40f, 0.06f, 0.72f, 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", violet);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", violet);
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        CreateVioletBand(go.transform, "DeepViolet", new Color(0.12f, 0.005f, 0.28f, 1f), -0.115f, 0.075f);
        CreateVioletBand(go.transform, "MiddleViolet", new Color(0.52f, 0.11f, 0.88f, 1f), 0f, 0.06f);
        CreateVioletBand(go.transform, "LavenderHighlight", new Color(0.86f, 0.55f, 1f, 1f), 0.105f, 0.038f);
        return go;
    }

    static void CreateVioletBand(Transform parent, string bandName, Color color, float y, float height)
    {
        GameObject band = GameObject.CreatePrimitive(PrimitiveType.Quad);
        band.name = bandName;
        band.transform.SetParent(parent, false);
        band.transform.localPosition = new Vector3(0f, y, -0.006f);
        band.transform.localScale = new Vector3(0.98f, height, 1f);
        Collider collider = band.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        MeshRenderer renderer = band.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        Material material = new Material(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    GameObject ResolveModel()
    {
        if (healthBarModel != null)
            return healthBarModel;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(FallbackModelPath);
#else
        return null;
#endif
    }

    void SetVisible(bool visible)
    {
        if (renderers == null)
            return;

        foreach (MeshRenderer meshRenderer in renderers)
            if (meshRenderer != null)
                meshRenderer.enabled = visible;
    }
}

using UnityEngine;

// Same E-to-pick-up pattern as WorldItemPickup (diamonds/runes), but adds gold directly to
// InventoryManager's counter instead of an inventory slot - a coin is never itself an item you
// see in the bag, per the request.
public class GoldCoinPickup : MonoBehaviour, IInteractable
{
    [SerializeField] int goldValue = 10;
    [SerializeField] float spinDegreesPerSecond = 150f;
    [SerializeField] float hoverHeight = 0.08f;
    [SerializeField] float hoverFrequency = 2.2f;

    float baseHeight;
    float hoverPhase;

    void Start()
    {
        baseHeight = transform.position.y;
        hoverPhase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.World);
        Vector3 position = transform.position;
        position.y = baseHeight + Mathf.Sin(Time.time * hoverFrequency + hoverPhase) * hoverHeight;
        transform.position = position;
    }

    public string GetInteractionText() => "[E] Recoger " + goldValue + " de oro";

    public void SetGoldValue(int value) => goldValue = value;

    public bool CanInteract(PlayerInteraction player) => true;

    public void Interact(PlayerInteraction player)
    {
        InventoryManager.Instance?.AddGold(goldValue);
        Destroy(gameObject);
    }

    public static GameObject BuildPrefab(GameObject coinModel, float scale, int goldValue, Material material = null)
    {
        GameObject go = Object.Instantiate(coinModel);
        go.name = "GoldCoinPickup";
        // Imported FBX models can have a non-unit authored root scale. Replacing it (rather
        // than multiplying it) shrinks the Meshy coin to a few millimetres despite the requested
        // visual size.
        go.transform.localScale = go.transform.localScale * scale;

        if (material != null)
            foreach (Renderer coinRenderer in go.GetComponentsInChildren<Renderer>(true))
                coinRenderer.sharedMaterial = material;

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0) go.layer = interactableLayer;

        // Coins are visual-only pickups. PlayerInteraction has a component-and-distance fallback,
        // so they do not need physics colliders (which can interfere with camera obstruction).
        foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }

        Light light = go.GetComponent<Light>();
        if (light == null) light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.85f, 0.2f);
        light.range = 2.2f;
        light.intensity = 1f;

        GoldCoinPickup pickup = go.GetComponent<GoldCoinPickup>();
        if (pickup == null) pickup = go.AddComponent<GoldCoinPickup>();
        pickup.goldValue = goldValue;

        return go;
    }

    // Kept independent of project assets so enemy gold always has a tangible, interactable
    // model even in a build where a Resources prefab was omitted or lost its mesh reference.
    public static GameObject CreateFallbackCoin(Vector3 position, int goldValue)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "GoldCoinPickup_Fallback";
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
        go.transform.localScale = new Vector3(0.26f, 0.045f, 0.26f);
        Object.Destroy(go.GetComponent<Collider>());

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0) go.layer = interactableLayer;

        Renderer renderer = go.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (renderer != null && shader != null)
        {
            Material material = new Material(shader);
            Color gold = new Color(1f, 0.68f, 0.06f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", gold);
            if (material.HasProperty("_Color")) material.SetColor("_Color", gold);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.82f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.7f);
            renderer.material = material;
        }

        GoldCoinPickup pickup = go.AddComponent<GoldCoinPickup>();
        pickup.goldValue = goldValue;
        return go;
    }
}

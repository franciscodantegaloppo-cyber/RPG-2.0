using UnityEngine;

public class DungeonKeyPickup : MonoBehaviour, IInteractable
{
    float baseY;

    public static void Create(Vector3 position)
    {
        GameObject root = new GameObject("DungeonKey_EliteSkeletonDrop");
        root.transform.position = position + Vector3.up * .45f;
        root.layer = LayerMask.NameToLayer("Interactable");
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.isTrigger = true; collider.radius = .42f; collider.height = 1.15f;
        root.AddComponent<DungeonKeyPickup>();
        QuestNpcAttentionIcon icon = root.AddComponent<QuestNpcAttentionIcon>();
        icon.Configure(QuestNpcAttentionRole.DungeonKeyPickup);
        BuildKeyVisual(root.transform);
    }

    void Awake() => baseY = transform.position.y;
    void Update()
    {
        transform.Rotate(0f, 58f * Time.deltaTime, 0f, Space.World);
        Vector3 p = transform.position; p.y = baseY + Mathf.Sin(Time.time * 2.8f) * .1f; transform.position = p;
    }

    public string GetInteractionText() => "[E] Recoger llave del calabozo";
    public bool CanInteract(PlayerInteraction player) => QuestManager.Instance != null &&
        QuestManager.Instance.MerchantIntroductionState == PrimaryQuestState.HuntEliteSkeletonAtNight;
    public void Interact(PlayerInteraction player)
    {
        if (!CanInteract(player)) return;
        QuestManager.Instance.NotifyDungeonKeyTaken();
        Destroy(gameObject);
    }

    static void BuildKeyVisual(Transform root)
    {
        Material gold = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        gold.color = new Color(1f, .56f, .06f);
        if (gold.HasProperty("_Metallic")) gold.SetFloat("_Metallic", .82f);
        if (gold.HasProperty("_Smoothness")) gold.SetFloat("_Smoothness", .72f);
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ring.name = "KeyRing"; ring.transform.SetParent(root, false);
        ring.transform.localPosition = new Vector3(0f,.34f,0f); ring.transform.localRotation = Quaternion.Euler(90f,0f,0f); ring.transform.localScale = new Vector3(.34f,.09f,.34f);
        GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder); hole.name = "KeyRingDarkInset"; hole.transform.SetParent(root, false);
        hole.transform.localPosition = new Vector3(0f,.34f,-.07f); hole.transform.localRotation = Quaternion.Euler(90f,0f,0f); hole.transform.localScale = new Vector3(.16f,.10f,.16f);
        Material dark = new Material(gold); dark.color = new Color(.06f,.025f,.005f); hole.GetComponent<Renderer>().sharedMaterial = dark;
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube); shaft.name="KeyShaft"; shaft.transform.SetParent(root,false); shaft.transform.localPosition=new Vector3(0f,-.1f,0f); shaft.transform.localScale=new Vector3(.12f,.72f,.12f);
        GameObject toothA = GameObject.CreatePrimitive(PrimitiveType.Cube); toothA.transform.SetParent(root,false); toothA.transform.localPosition=new Vector3(.13f,-.42f,0f); toothA.transform.localScale=new Vector3(.32f,.13f,.12f);
        GameObject toothB = GameObject.CreatePrimitive(PrimitiveType.Cube); toothB.transform.SetParent(root,false); toothB.transform.localPosition=new Vector3(.1f,-.25f,0f); toothB.transform.localScale=new Vector3(.25f,.11f,.12f);
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>()) if (renderer.gameObject != hole) renderer.sharedMaterial=gold;
        foreach (Collider collider in root.GetComponentsInChildren<Collider>()) if (collider.transform != root) Destroy(collider);
        root.localScale = Vector3.one * .85f;
        Light light = root.gameObject.AddComponent<Light>(); light.type=LightType.Point; light.color=new Color(1f,.45f,.05f); light.range=3f; light.intensity=1.6f; light.shadows=LightShadows.None;
    }
}

public class QuestEliteSkeletonMarker : MonoBehaviour
{
    EnemyStats stats;
    void Awake() { stats=GetComponent<EnemyStats>(); if(stats!=null) stats.OnDeath += DropKey; }
    void OnDestroy() { if(stats!=null) stats.OnDeath -= DropKey; }
    void DropKey() { DungeonKeyPickup.Create(transform.position); }
}

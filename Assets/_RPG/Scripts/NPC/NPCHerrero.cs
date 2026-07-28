using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NPCHerrero : MonoBehaviour, IInteractable
{
    [Header("Tienda")]
    [SerializeField] List<ShopEntry> shopItems = new List<ShopEntry>();

    [Header("Identidad")]
    [SerializeField] string npcName = "Herrero";
    [SerializeField] string greetText = "Bienvenido a mi herreria. Que arma te puedo forjar?";
    [SerializeField] RuntimeAnimatorController walkController;
    [SerializeField] Material blacksmithMaterial;

    NPCWander wander;
    bool appearanceReady;

    void Awake()
    {
        wander = GetComponent<NPCWander>();
        if (wander == null)
            wander = gameObject.AddComponent<NPCWander>();

        wander.Configure(speed: 0.75f, radius: 5f, step: 2.1f, minWait: 2.5f, maxWait: 5.5f);
        ApplyBlacksmithIdentity();
    }

    public string GetInteractionText() => $"[E] Hablar con {npcName}";
    public bool CanInteract(PlayerInteraction player) => true;

    public void Interact(PlayerInteraction player)
    {
        wander?.PauseForInteraction();

        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);

        var shop = BlacksmithShopPanel.Instance;
        if (shop == null)
            shop = EnsureRuntimeShopPanel();

        if (shop != null)
        {
            shop.Show(shopItems, () => wander?.ResumeWander());
        }
        else
        {
            FindAnyObjectByType<HUDController>()?.ShowDialogue(greetText);
            wander?.ResumeWander();
        }
    }

    BlacksmithShopPanel EnsureRuntimeShopPanel()
    {
        // FindAnyObjectByType<Canvas>() grabs whichever canvas Unity happens to enumerate first,
        // which since EnemyHealthBar started creating one small WorldSpace canvas per enemy is no
        // longer reliably the HUD - see InventoryUI.FindReusableCanvas() for the full story.
        Canvas canvas = InventoryUI.FindReusableCanvas();
        if (canvas == null)
        {
            var canvasObject = new GameObject("HUDCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        var panelObject = new GameObject("BlacksmithShopPanel", typeof(RectTransform));
        panelObject.transform.SetParent(canvas.transform, false);
        panelObject.SetActive(false);
        return panelObject.AddComponent<BlacksmithShopPanel>();
    }

    // Used to force a flat brown tint (and a crude cube "apron") onto every renderer here every
    // time Play mode started, undoing the GanzSe model's own textured armor pieces set up via
    // RPG/Fix Herrero Armor (GanzSe Low Poly) - that's why the fix looked correct in the Editor
    // (Edit mode never runs Awake) but reverted to solid brown the moment you pressed Play. The
    // GanzSe model's own SkinnedMeshRenderers already carry real armor textures now, so identity
    // setup here is just the walk controller.
    void ApplyBlacksmithIdentity()
    {
        if (appearanceReady) return;
        appearanceReady = true;

#if UNITY_EDITOR
        if (walkController == null)
            walkController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_RPG/Animations/MerchantWalk2.controller");
#endif

        var anim = GetComponentInChildren<Animator>(true);
        if (anim != null)
        {
            if (walkController != null)
                anim.runtimeAnimatorController = walkController;
            anim.applyRootMotion = false;
        }
    }
}

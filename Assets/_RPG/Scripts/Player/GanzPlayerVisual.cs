using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GanzPlayerVisual : MonoBehaviour
{
    const string PrefabPath = "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Modular Character/GanzSe Free Modular Character Update 1_1.prefab";

    readonly Dictionary<string, SkinnedMeshRenderer> renderersByName = new Dictionary<string, SkinnedMeshRenderer>();
    readonly Dictionary<ItemType, SkinnedMeshRenderer> equippedRenderers = new Dictionary<ItemType, SkinnedMeshRenderer>();
    readonly List<Renderer> hiddenOriginalRenderers = new List<Renderer>();

    GameObject visualRoot;
    Animator sourceAnimator;
    Animator visualAnimator;
    bool initialized;

    public Animator VisualAnimator
    {
        get
        {
            EnsureInitialized();
            return visualAnimator;
        }
    }

    void Awake()
    {
        EnsureInitialized();
    }

    public bool EnsureInitialized()
    {
        if (initialized)
            return visualRoot != null;

        initialized = true;
        sourceAnimator = GetComponent<Animator>();
        GameObject prefab = LoadGanzPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("GanzPlayerVisual: no se encontro el prefab modular Ganz.");
            return false;
        }

        visualRoot = Instantiate(prefab, transform);
        visualRoot.name = "Ganz_Player_Visual";
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;
        visualRoot.transform.localScale = Vector3.one;

        visualAnimator = visualRoot.GetComponent<Animator>();
        if (visualAnimator != null && sourceAnimator != null)
        {
            visualAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            visualAnimator.applyRootMotion = false;
            visualAnimator.updateMode = sourceAnimator.updateMode;
            visualAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // The clips carry animation events (Hit, FootL/R, WeaponSwitch...) that PlayerAnimatorBridge
        // handles on the Player root. This second Animator instance plays the same clips but lives
        // on a different GameObject, so those events have no receiver here and log an error every
        // time they fire. Real gameplay logic still runs correctly via the source animator's copy
        // of the same events - this sink only exists to silence the duplicate, receiver-less ones.
        if (visualRoot.GetComponent<GanzVisualAnimationEventSink>() == null)
            visualRoot.AddComponent<GanzVisualAnimationEventSink>();

        HideOriginalRenderers();
        CacheGanzRenderers();
        SetNudeBase();
        return true;
    }

    public bool ApplyItem(ItemData item)
    {
        if (item == null || item.itemType == ItemType.Weapon || item.itemType == ItemType.Shield || item.itemType == ItemType.Consumable)
            return false;
        if (!EnsureInitialized())
            return false;

        Unequip(item.itemType);

        string targetName = GetRendererName(item);
        SkinnedMeshRenderer renderer = FindRenderer(targetName);
        if (renderer == null)
        {
            Debug.LogWarning("GanzPlayerVisual: no encontre pieza skinned para " + item.itemName + " (" + targetName + ").");
            return false;
        }

        renderer.enabled = true;
        equippedRenderers[item.itemType] = renderer;
        return true;
    }

    public void Unequip(ItemType type)
    {
        if (!EnsureInitialized())
            return;

        if (equippedRenderers.TryGetValue(type, out SkinnedMeshRenderer renderer) && renderer != null)
            renderer.enabled = false;
        equippedRenderers.Remove(type);
    }

    void HideOriginalRenderers()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (visualRoot != null && renderer.transform.IsChildOf(visualRoot.transform))
                continue;
            if (renderer.GetComponentInParent<Canvas>() != null)
                continue;

            renderer.enabled = false;
            hiddenOriginalRenderers.Add(renderer);
        }
    }

    void CacheGanzRenderers()
    {
        renderersByName.Clear();
        foreach (SkinnedMeshRenderer renderer in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string key = NormalizeName(renderer.gameObject.name);
            if (!renderersByName.ContainsKey(key))
                renderersByName.Add(key, renderer);
        }
    }

    void SetNudeBase()
    {
        foreach (SkinnedMeshRenderer renderer in renderersByName.Values)
            renderer.enabled = false;

        EnableIfPresent("Base Character Mesh");
        EnableIfPresent("Eyes Type 1 Color 1");
        EnableIfPresent("Eyebrow Type 1 Color 1");
        EnableIfPresent("Nose Type 1");
        EnableIfPresent("Ears Type 1");
        EnableIfPresent("Hair Type 1 Color 1");
    }

    void EnableIfPresent(string rendererName)
    {
        SkinnedMeshRenderer renderer = FindRenderer(rendererName);
        if (renderer != null)
            renderer.enabled = true;
    }

    SkinnedMeshRenderer FindRenderer(string rawName)
    {
        string key = NormalizeName(rawName);
        if (renderersByName.TryGetValue(key, out SkinnedMeshRenderer exact))
            return exact;

        foreach (var pair in renderersByName)
            if (pair.Key.Contains(key) || key.Contains(pair.Key))
                return pair.Value;

        return null;
    }

    static string GetRendererName(ItemData item)
    {
        if (item.equipmentPrefab != null)
            return item.equipmentPrefab.name;

        return item.itemType switch
        {
            ItemType.Helmet => "Head Armor Type 1 Color 1",
            ItemType.Chest => "Chest Armor Type 1 Color 1",
            ItemType.Gloves => "Arm Armor Type 1 Color 1",
            ItemType.Legs => "Legs Armor Type 1 Color 1",
            ItemType.Boots => "Feet Armor Type 1 Color 1",
            _ => ""
        };
    }

    static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value
            .Replace("(Clone)", "")
            .Replace(" Part", "")
            .Trim()
            .ToLowerInvariant();
    }

    static GameObject LoadGanzPrefab()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#else
        return null;
#endif
    }
}

// Matches PlayerAnimatorBridge's animation event receiver methods so the visual character's
// own Animator instance has somewhere to deliver them. All no-ops: the actual gameplay effects
// (damage, weapon show/hide) already happen through PlayerAnimatorBridge's copy on Player root.
public class GanzVisualAnimationEventSink : MonoBehaviour
{
    void Hit() { }
    void FootL() { }
    void FootR() { }
    void Land() { }
    void Shoot() { }
    void WeaponSwitch() { }
    void OnAttackHit() { }
    void OnWeaponDrawn() { }
}

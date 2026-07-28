using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(RawImage))]
public class RuntimeItemPreviewUI : MonoBehaviour
{
    const float RenderInterval = 0.1f;
    const int MaxPreviewRendersPerFrame = 3;
    static int renderBudgetFrame = -1;
    static int rendersThisFrame;

    RawImage target;
    RenderTexture texture;
    Camera previewCamera;
    Light previewLight;
    Transform root;
    Transform modelPivot;
    GameObject model;
    Quaternion baseRotation;
    float previewFill = 2.08f;
    float nextRenderTime;

    public void Show(ItemData item)
    {
        Show(item, 2.08f);
    }

    public void Show(ItemData item, float fill)
    {
        Clear();
        GameObject prefab = GetPrefab(item);
        if (prefab == null && (item == null || item.icon == null))
            return;

        previewFill = Mathf.Clamp(fill, 1.6f, 2.48f);
        EnsureRig();
        // The same RawImage can temporarily be reused by InventoryUI for a normal drag.
        // Restore this preview's own render target every time Show is called.
        target.texture = texture;
        model = prefab != null ? Instantiate(prefab, modelPivot) : CreateSpriteCard(item.icon, modelPivot);
        model.name = "InventoryPreview_" + item.itemName;
        model.SetActive(true);
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            renderer.forceRenderingOff = false;
            renderer.gameObject.SetActive(true);
        }
        PreviewRenderIsolation.SetLayerRecursive(model);
        if (prefab != null)
            PreviewRenderIsolation.ApplyUnlitPreviewMaterials(model);
        modelPivot.localPosition = Vector3.zero;
        modelPivot.localRotation = Quaternion.identity;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        FitModel(model.transform, modelPivot, previewFill);

        // Every object uses its real principal axis: sword handle-to-tip,
        // armour head-to-feet, diamond point-to-point, etc. Rotation happens on
        // a separate pivot whose origin stays at the render bounds centre.
        Vector3 longitudinalAxis = PreviewSpinAxisUtility.FindLongitudinalAxis(model.transform);
        baseRotation = PreviewSpinAxisUtility.AlignLongitudinalAxisUp(longitudinalAxis);
        modelPivot.localRotation = baseRotation;
        target.enabled = true;

        // Rendering every newly rebuilt slot synchronously caused a large one-frame hitch in the
        // crafting table. Visible previews now share a small frame budget and fill progressively.
        nextRenderTime = Time.unscaledTime;
        if (texture != null && !texture.IsCreated()) texture.Create();
        if (previewCamera != null && AcquireRenderBudget()) previewCamera.Render();
    }

    void Awake()
    {
        target = GetComponent<RawImage>();
        target.enabled = false;
    }

    void OnDestroy()
    {
        Clear();
        if (previewCamera != null) Destroy(previewCamera.gameObject);
        if (previewLight != null) Destroy(previewLight.gameObject);
        if (root != null) Destroy(root.gameObject);
        if (texture != null) texture.Release();
    }

    void Update()
    {
        if (model == null || target == null || !target.isActiveAndEnabled || !target.gameObject.activeInHierarchy)
            return;

        float angle = Time.unscaledTime * 55f;
        modelPivot.localRotation = Quaternion.AngleAxis(angle, Vector3.up) * baseRotation;

        if (previewCamera == null || Time.unscaledTime < nextRenderTime || !AcquireRenderBudget())
            return;

        nextRenderTime = Time.unscaledTime + RenderInterval;
        previewCamera.Render();
    }

    static bool AcquireRenderBudget()
    {
        if (renderBudgetFrame != Time.frameCount)
        {
            renderBudgetFrame = Time.frameCount;
            rendersThisFrame = 0;
        }
        if (rendersThisFrame >= MaxPreviewRendersPerFrame)
            return false;
        rendersThisFrame++;
        return true;
    }

    void EnsureRig()
    {
        if (target == null)
            target = GetComponent<RawImage>();
        if (previewCamera != null)
            return;

        // Bag cells are visually small and gain nothing from a 256px render target. Large
        // crafting slots keep 256px; regular inventory thumbnails use one quarter the pixels.
        int resolution = previewFill >= 2.3f ? 256 : 128;
        texture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32);
        texture.name = "InventorySlot3DPreview";
        target.texture = texture;
        target.color = Color.white;

        root = new GameObject("InventorySlot3DPreviewRoot").transform;
        root.position = PreviewRenderIsolation.AllocateStagePosition();
        DontDestroyOnLoad(root.gameObject);

        modelPivot = new GameObject("CenteredModelPivot").transform;
        modelPivot.SetParent(root, false);

        GameObject camGO = new GameObject("InventorySlot3DPreviewCamera");
        camGO.transform.SetParent(root, false);
        previewCamera = camGO.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = 1.35f;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 20f;
        previewCamera.transform.localPosition = new Vector3(0f, 0.1f, -5f);
        previewCamera.transform.localRotation = Quaternion.identity;
        previewCamera.targetTexture = texture;
        // Camera.Render() is scheduled manually only for visible UI. Leaving this enabled made
        // every inventory/crafting icon render forever, even while its panel was closed.
        previewCamera.enabled = false;

        GameObject lightGO = new GameObject("InventorySlot3DPreviewLight");
        lightGO.transform.SetParent(root, false);
        previewLight = lightGO.AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 0f;
        previewLight.enabled = false;
        previewLight.transform.localRotation = Quaternion.Euler(45f, -30f, 0f);
        PreviewRenderIsolation.Configure(previewCamera, previewLight);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previous;
    }

    void Clear()
    {
        if (model != null)
        {
            model.SetActive(false);
            Destroy(model);
        }
        model = null;
        if (target != null)
            target.enabled = false;
    }

    public void ClearPreview()
    {
        Clear();
    }

    static GameObject GetPrefab(ItemData item)
    {
        if (item == null)
            return null;
        if (item.itemType == ItemType.Weapon && item.weaponData != null)
            return item.weaponData.weaponPrefab;
        return item.equipmentPrefab != null ? item.equipmentPrefab : LoadFallbackEquipmentPrefab(item.itemType);
    }

    static GameObject LoadFallbackEquipmentPrefab(ItemType type)
    {
#if UNITY_EDITOR
        string path = type switch
        {
            ItemType.Helmet => "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Head Armor Type 1 Color 1 Part.prefab",
            ItemType.Chest => "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Chest Armor Type 1 Color 1 Part.prefab",
            ItemType.Gloves => "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Arm Armor Type 1 Color 1 Part.prefab",
            ItemType.Legs => "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Legs Armor Type 1 Color 1 Part.prefab",
            ItemType.Boots => "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts/Feet Armor Type 1 Color 1 Part.prefab",
            _ => ""
        };
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }

    static GameObject CreateSpriteCard(Sprite sprite, Transform parent)
    {
        GameObject card = new GameObject("Sprite3DCard");
        card.transform.SetParent(parent, false);
        SpriteRenderer renderer = card.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return card;
    }

    static void FitModel(Transform modelRoot, Transform pivot, float targetSize)
    {
        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxSize > 0.01f)
            modelRoot.localScale *= targetSize / maxSize;

        // Recalculate after scaling, then move the MODEL relative to the stable
        // pivot. Moving the same transform that is later rotated does not change
        // its authored pivot; using a parent pivot genuinely centres the spin.
        renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        modelRoot.position += pivot.position - bounds.center;
    }
}

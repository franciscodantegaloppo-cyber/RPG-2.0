using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Bakes a small 3D-rendered thumbnail (weapon/armor model, lit and rotated) into a Sprite,
// once per ItemData, and caches it. Used by InventoryUI so bag/equipment slots show a real
// render of the item instead of plain text.
public static class ItemIconRenderer
{
    static readonly Dictionary<ItemData, Sprite> cache = new Dictionary<ItemData, Sprite>();

    static RenderTexture texture;
    static Camera camera;
    static Light light;
    static Transform root;

    public static Sprite GetIcon(ItemData item)
    {
        if (item == null) return null;
        if (item.icon != null) return item.icon;

        if (cache.TryGetValue(item, out Sprite cached) && cached != null)
            return cached;

        EnsureRig();

        GameObject prefab = item.itemType == ItemType.Weapon && item.weaponData != null
            ? item.weaponData.weaponPrefab
            : item.equipmentPrefab != null ? item.equipmentPrefab : LoadFallbackEquipmentPrefab(item.itemType);

        if (prefab == null)
            return null;

        GameObject model = Object.Instantiate(prefab, root);
        PreviewRenderIsolation.SetLayerRecursive(model);
        PreviewRenderIsolation.ApplyUnlitPreviewMaterials(model);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(18f, 35f, 0f);
        FitModel(model.transform);

        // This bake camera is needed only for this call; keeping it enabled afterwards made a
        // hidden 160px camera render forever once the first item icon had been requested.
        camera.enabled = true;
        camera.Render();
        camera.enabled = false;

        var tex2D = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = texture;
        tex2D.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        tex2D.Apply();
        RenderTexture.active = prevActive;

        // InventoryUI.Refresh() bakes ~30 icons back-to-back in a single frame. Object.Destroy
        // is deferred to end-of-frame, so the next GetIcon() call would instantiate its model
        // at the same shared rig position while this one is still alive - both get rendered
        // into that item's texture, showing up as extra overlapping geometry. Destroy it now.
        Object.DestroyImmediate(model);

        Sprite sprite = Sprite.Create(tex2D, new Rect(0, 0, tex2D.width, tex2D.height), new Vector2(0.5f, 0.5f));
        cache[item] = sprite;
        return sprite;
    }

    static void EnsureRig()
    {
        if (camera != null) return;

        texture = new RenderTexture(160, 160, 16, RenderTextureFormat.ARGB32);
        texture.name = "ItemIconRenderTexture";

        root = new GameObject("ItemIconRendererRoot").transform;
        root.position = new Vector3(4000f, 4000f, 4000f);
        Object.DontDestroyOnLoad(root.gameObject);

        var camGO = new GameObject("ItemIconRendererCamera");
        camGO.transform.SetParent(root, false);
        camera = camGO.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.13f, 0.145f, 0.15f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 1.4f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 20f;
        camera.transform.localPosition = new Vector3(0f, 0.15f, -5f);
        camera.transform.localRotation = Quaternion.identity;
        camera.targetTexture = texture;
        // Left enabled (matching BlacksmithShopPanel's WeaponPreviewRenderer, which is the
        // proven-working pattern in this project): under URP, calling Render() on a camera
        // that has never been part of the active pipeline update can produce a blank/white
        // frame. Camera.Render() below still forces the immediate synchronous bake we need.

        var lightGO = new GameObject("ItemIconRendererLight");
        lightGO.transform.SetParent(root, false);
        light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0f;
        light.enabled = false;
        lightGO.transform.localRotation = Quaternion.Euler(45f, -30f, 0f);
        PreviewRenderIsolation.Configure(camera, light);
        camera.enabled = false;
    }

    static void FitModel(Transform modelRoot)
    {
        var renderers = modelRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        modelRoot.position -= bounds.center - modelRoot.position;
        float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxSize > 0.01f)
            modelRoot.localScale *= 1.8f / maxSize;
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
}

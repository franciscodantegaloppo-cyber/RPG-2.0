using System.IO;
using UnityEditor;
using UnityEngine;

// Gives the health/stamina potions a real visual instead of a blank inventory icon: a tinted
// copy of a LowPolyMedievalPropsLite bottle prefab, captured via Unity's own asset-preview
// renderer and saved as a Sprite. The pack ships with no 2D icons of its own.
public static class PotionSetup
{
    const string HealthPotionPath = "Assets/_RPG/ScriptableObjects/Items/HealthPotion.asset";
    const string StaminaPotionPath = "Assets/_RPG/ScriptableObjects/Items/StaminaPotion.asset";

    const string HealthBottleSourcePath = "Assets/LowPolyMedievalPropsLite/Prefabs/Bottle_02.prefab";
    const string StaminaBottleSourcePath = "Assets/LowPolyMedievalPropsLite/Prefabs/Bottle_03.prefab";

    const string PrefabFolder = "Assets/_RPG/Prefabs/Items";
    const string MaterialFolder = "Assets/_RPG/Materials";
    const string IconFolder = "Assets/_RPG/Generated/Icons";

    static readonly Color HealthTint = new Color(0.75f, 0.08f, 0.08f);
    static readonly Color StaminaTint = new Color(0.1f, 0.55f, 0.85f);

    [MenuItem("RPG/Items/Setup Potions")]
    public static void Setup()
    {
        EnsureFolders();

        GameObject healthBottle = BuildTintedBottle(HealthBottleSourcePath, HealthTint, "HealthPotionBottle");
        GameObject staminaBottle = BuildTintedBottle(StaminaBottleSourcePath, StaminaTint, "StaminaPotionBottle");

        Sprite healthIcon = BuildIconSprite(healthBottle, "HealthPotionIcon");
        Sprite staminaIcon = BuildIconSprite(staminaBottle, "StaminaPotionIcon");

        ItemData health = EnsureItem(HealthPotionPath, "health_potion", "Pocion de Salud");
        health.icon = healthIcon != null ? healthIcon : health.icon;
        health.itemType = ItemType.Consumable;
        health.healAmount = 30f;
        health.stackable = true;
        health.maxStack = 10;
        health.description = "Restaura 30 puntos de salud al instante.";
        EditorUtility.SetDirty(health);

        ItemData stamina = EnsureItem(StaminaPotionPath, "stamina_potion", "Pocion de Estamina");
        stamina.icon = staminaIcon != null ? staminaIcon : stamina.icon;
        stamina.itemType = ItemType.Consumable;
        stamina.staminaCostReductionPercent = 30f;
        stamina.staminaRegenBoostPercent = 50f;
        stamina.buffDuration = 30f;
        stamina.stackable = true;
        stamina.maxStack = 10;
        stamina.description = "Reduce el costo de estamina y acelera su regeneracion durante 30s.";
        EditorUtility.SetDirty(stamina);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PotionSetup] Health/Stamina potions configured with icons from LowPolyMedievalPropsLite.");
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Prefabs"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/_RPG/Prefabs", "Items");
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/_RPG", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/_RPG/Generated"))
            AssetDatabase.CreateFolder("Assets/_RPG", "Generated");
        if (!AssetDatabase.IsValidFolder(IconFolder))
            AssetDatabase.CreateFolder("Assets/_RPG/Generated", "Icons");
    }

    static GameObject BuildTintedBottle(string sourcePath, Color tint, string name)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null)
        {
            Debug.LogError("[PotionSetup] Source bottle not found: " + sourcePath);
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = name;

        Renderer renderer = instance.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            Material tinted = new Material(renderer.sharedMaterial);
            tinted.name = name + "_Mat";
            if (tinted.HasProperty("_BaseColor"))
                tinted.SetColor("_BaseColor", tint);
            else if (tinted.HasProperty("_Color"))
                tinted.SetColor("_Color", tint);

            string matPath = MaterialFolder + "/" + tinted.name + ".mat";
            Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null)
                AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(tinted, matPath);
            renderer.sharedMaterial = tinted;
        }

        string prefabPath = PrefabFolder + "/" + name + ".prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
        return saved;
    }

    // AssetPreview.GetAssetPreview() renders asynchronously across editor frames and just
    // returns a "still loading" placeholder texture until it's ready - polling it in a
    // Thread.Sleep loop never works because sleeping the main thread also blocks the preview
    // renderer itself. A manually-created EditorSceneManager preview scene doesn't reliably
    // resolve URP shaders either (renders solid magenta - the "no SRP context" error color).
    // Rendering in the real active scene, far off to the side, reliably picks up the correct
    // URP pipeline context since it's exactly how any normal camera in this project renders.
    static Sprite BuildIconSprite(GameObject prefab, string iconName)
    {
        if (prefab == null)
            return null;

        const int Size = 128;
        Vector3 offset = new Vector3(800f, 800f, 800f);
        GameObject instance = null;
        GameObject lightGo = null;
        GameObject camGo = null;
        RenderTexture rt = null;
        Texture2D tex = null;

        try
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(offset, Quaternion.Euler(15f, 200f, 0f));

            Renderer renderer = instance.GetComponentInChildren<Renderer>(true);
            Bounds bounds = renderer != null ? renderer.bounds : new Bounds(offset, Vector3.one * 0.2f);
            float radius = Mathf.Max(0.05f, bounds.extents.magnitude);

            lightGo = new GameObject("IconPreviewLight");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            lightGo.transform.position = offset;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            camGo = new GameObject("IconPreviewCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographic = true;
            cam.orthographicSize = radius * 1.25f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = radius * 10f + 1f;
            cam.cullingMask = ~0;
            camGo.transform.position = bounds.center - Vector3.forward * (radius * 4f) + Vector3.up * radius * 0.2f;
            camGo.transform.LookAt(bounds.center);

            rt = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
        }
        finally
        {
            if (instance != null) Object.DestroyImmediate(instance);
            if (lightGo != null) Object.DestroyImmediate(lightGo);
            if (camGo != null) Object.DestroyImmediate(camGo);
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
        }

        if (tex == null)
        {
            Debug.LogWarning("[PotionSetup] Could not render a preview for " + prefab.name);
            return null;
        }

        byte[] png = tex.EncodeToPNG();
        string pngPath = IconFolder + "/" + iconName + ".png";
        File.WriteAllBytes(pngPath, png);
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
    }

    static ItemData EnsureItem(string path, string id, string displayName)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }

        item.itemID = id;
        item.itemName = displayName;
        return item;
    }
}

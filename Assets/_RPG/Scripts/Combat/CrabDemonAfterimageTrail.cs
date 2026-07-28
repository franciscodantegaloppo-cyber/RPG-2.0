using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Captures the crab's real animated pose while it moves and leaves short-lived blue silhouettes.
// Skinned meshes are baked per sample, so every ghost is the actual pose from that frame.
[DisallowMultipleComponent]
public class CrabDemonAfterimageTrail : MonoBehaviour
{
    [SerializeField, Range(.04f, .2f)] float sampleInterval = .085f;
    [SerializeField, Range(.2f, 1f)] float lifetime = .52f;
    [SerializeField, Range(.03f, .35f)] float startingAlpha = .17f;
    [SerializeField] float minimumMovement = .018f;
    [SerializeField, Range(2, 12)] int maximumGhosts = 7;

    readonly Queue<GameObject> ghosts = new Queue<GameObject>();
    Vector3 previousPosition;
    Quaternion previousRotation;
    float nextSampleTime;
    bool initialized;

    void LateUpdate()
    {
        if (!initialized)
        {
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            initialized = true;
            return;
        }

        float distance = Vector3.Distance(transform.position, previousPosition);
        float rotation = Quaternion.Angle(transform.rotation, previousRotation);
        previousPosition = transform.position;
        previousRotation = transform.rotation;

        if (Time.time < nextSampleTime ||
            (distance < minimumMovement && rotation < 1.5f))
            return;

        nextSampleTime = Time.time + sampleInterval;
        CapturePose();
    }

    void CapturePose()
    {
        GameObject root = new GameObject("CrabDemon_BlueAfterimage");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Material material = CreateGhostMaterial();
        bool hasVisual = false;

        foreach (SkinnedMeshRenderer source in
                 GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (!CanGhost(source))
                continue;

            Mesh baked = new Mesh { name = source.name + "_AfterimagePose" };
            source.BakeMesh(baked);
            CreateGhostPart(root.transform, source.transform, baked, material, true);
            hasVisual = true;
        }

        foreach (MeshRenderer source in GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!CanGhost(source))
                continue;
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                continue;

            CreateGhostPart(root.transform, source.transform, filter.sharedMesh,
                material, false);
            hasVisual = true;
        }

        if (!hasVisual)
        {
            Destroy(material);
            Destroy(root);
            return;
        }

        CrabDemonAfterimageFade fade = root.AddComponent<CrabDemonAfterimageFade>();
        fade.Configure(material, lifetime, startingAlpha);
        ghosts.Enqueue(root);
        while (ghosts.Count > maximumGhosts)
        {
            GameObject oldest = ghosts.Dequeue();
            if (oldest != null)
                Destroy(oldest);
        }
    }

    static bool CanGhost(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            return false;
        string lower = renderer.name.ToLowerInvariant();
        return !lower.Contains("health") && !lower.Contains("bar") &&
               !lower.Contains("aura") && !lower.Contains("particle") &&
               !lower.Contains("fire") && !lower.Contains("shadow");
    }

    static void CreateGhostPart(Transform parent, Transform source, Mesh mesh,
        Material material, bool ownsMesh)
    {
        GameObject part = new GameObject(source.name + "_Ghost");
        part.transform.SetParent(parent, false);
        part.transform.SetPositionAndRotation(source.position, source.rotation);
        part.transform.localScale = source.lossyScale;

        MeshFilter filter = part.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        if (ownsMesh)
        {
            CrabDemonAfterimageMeshOwner owner =
                part.AddComponent<CrabDemonAfterimageMeshOwner>();
            owner.mesh = mesh;
        }
    }

    Material CreateGhostMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            name = "CrabDemon_BlueAfterimage_Material",
            renderQueue = (int)RenderQueue.Transparent
        };
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        SetColor(material, new Color(.12f, .48f, 1f, startingAlpha));
        return material;
    }

    internal static void SetColor(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    void OnDisable()
    {
        while (ghosts.Count > 0)
        {
            GameObject ghost = ghosts.Dequeue();
            if (ghost != null)
                Destroy(ghost);
        }
        initialized = false;
    }
}

public class CrabDemonAfterimageFade : MonoBehaviour
{
    Material material;
    float duration;
    float initialAlpha;
    float elapsed;

    public void Configure(Material value, float seconds, float alpha)
    {
        material = value;
        duration = Mathf.Max(.05f, seconds);
        initialAlpha = alpha;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        float alpha = initialAlpha * (1f - Mathf.SmoothStep(0f, 1f, progress));
        CrabDemonAfterimageTrail.SetColor(material,
            new Color(.1f, .42f, 1f, alpha));
        if (progress >= 1f)
            Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}

public class CrabDemonAfterimageMeshOwner : MonoBehaviour
{
    public Mesh mesh;
    void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }
}

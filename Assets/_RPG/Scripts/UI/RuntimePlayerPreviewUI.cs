using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class RuntimePlayerPreviewUI : MonoBehaviour, IBeginDragHandler, IDragHandler,
    IEndDragHandler, IScrollHandler
{
    readonly Dictionary<Transform, Transform> transformMap = new Dictionary<Transform, Transform>();
    readonly List<KeyValuePair<Transform, Transform>> posePairs = new List<KeyValuePair<Transform, Transform>>();

    RawImage target;
    RenderTexture texture;
    Camera previewCamera;
    Transform stageRoot;
    Transform spinPivot;
    Transform poseRoot;
    Transform sourcePlayer;
    GameObject clonedCharacter;
    Quaternion baseRotation = Quaternion.Euler(0f, 180f, 0f);
    int visualSignature;
    float nextRenderTime;
    float manualYaw;
    bool manualDragging;

    void Awake()
    {
        target = GetComponent<RawImage>();
        target.enabled = false;
    }

    public void Show(Transform player)
    {
        if (player == null) return;
        EnsureRig();
        if (texture != null && !texture.IsCreated())
            texture.Create();
        target.texture = texture;
        int signature = CalculateVisualSignature(player);
        if (clonedCharacter == null || sourcePlayer != player || signature != visualSignature)
        {
            visualSignature = signature;
            RebuildCharacter(player);
        }
        target.enabled = true;
        previewCamera.Render();
        nextRenderTime = Time.unscaledTime + 0.066f;
    }

    void Update()
    {
        if (clonedCharacter == null || sourcePlayer == null || target == null ||
            !target.isActiveAndEnabled || !target.gameObject.activeInHierarchy) return;

        foreach (KeyValuePair<Transform, Transform> pair in posePairs)
        {
            if (pair.Key == null || pair.Value == null || pair.Key == sourcePlayer) continue;
            pair.Value.localPosition = pair.Key.localPosition;
            pair.Value.localRotation = pair.Key.localRotation;
            pair.Value.localScale = pair.Key.localScale;
            pair.Value.gameObject.SetActive(pair.Key.gameObject.activeSelf);
        }

        float automaticYaw = manualDragging ? 0f : Time.unscaledTime * 18f;
        spinPivot.localRotation =
            Quaternion.AngleAxis(automaticYaw + manualYaw, Vector3.up) * baseRotation;
        if (previewCamera != null && Time.unscaledTime >= nextRenderTime)
        {
            nextRenderTime = Time.unscaledTime + 0.066f;
            previewCamera.Render();
        }
    }

    void EnsureRig()
    {
        if (previewCamera != null) return;
        if (target == null) target = GetComponent<RawImage>();

        texture = new RenderTexture(320, 480, 16, RenderTextureFormat.ARGB32)
        {
            name = "InventoryPlayerMirror"
        };
        target.texture = texture;
        target.color = Color.white;

        stageRoot = new GameObject("InventoryPlayerPreviewStage").transform;
        stageRoot.position = PreviewRenderIsolation.AllocateStagePosition();
        DontDestroyOnLoad(stageRoot.gameObject);

        spinPivot = new GameObject("PlayerSpinPivot").transform;
        spinPivot.SetParent(stageRoot, false);
        poseRoot = new GameObject("PlayerPoseRoot").transform;
        poseRoot.SetParent(spinPivot, false);

        GameObject cameraObject = new GameObject("InventoryPlayerPreviewCamera");
        cameraObject.transform.SetParent(stageRoot, false);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = 2.15f;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 20f;
        previewCamera.transform.localPosition = new Vector3(0f, 0.05f, -7f);
        previewCamera.transform.localRotation = Quaternion.identity;
        previewCamera.targetTexture = texture;
        previewCamera.enabled = false;
        PreviewRenderIsolation.Configure(previewCamera, null);
    }

    static int CalculateVisualSignature(Transform player)
    {
        unchecked
        {
            int hash = 17;
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                // Particle/trail renderers from equipped spell auras are VFX, not character
                // meshes. They intentionally have no MeshFilter and must not participate in the
                // inventory mannequin signature.
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    mesh = skin.sharedMesh;
                }
                else if (renderer is MeshRenderer)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null) continue;
                    mesh = filter.sharedMesh;
                }
                else
                {
                    continue;
                }

                hash = hash * 31 + renderer.GetEntityId().GetHashCode();
                hash = hash * 31 + (mesh != null ? mesh.GetEntityId().GetHashCode() : 0);
            }
            return hash;
        }
    }

    void RebuildCharacter(Transform player)
    {
        if (clonedCharacter != null)
        {
            clonedCharacter.SetActive(false);
            Destroy(clonedCharacter);
        }

        sourcePlayer = player;
        transformMap.Clear();
        posePairs.Clear();
        // FitCharacter scales and offsets this root. Without resetting it, every equipment
        // change applied another scale/offset over the previous one until the mannequin left
        // the preview camera completely.
        poseRoot.localPosition = Vector3.zero;
        poseRoot.localRotation = Quaternion.identity;
        poseRoot.localScale = Vector3.one;
        spinPivot.localRotation = baseRotation;
        clonedCharacter = CloneTransformTree(player, poseRoot, true).gameObject;
        CopyRenderers();
        PreviewRenderIsolation.SetLayerRecursive(clonedCharacter);
        PreviewRenderIsolation.ApplyUnlitPreviewMaterials(clonedCharacter);
        FitCharacter();
    }

    Transform CloneTransformTree(Transform source, Transform parent, bool root)
    {
        GameObject cloneObject = new GameObject(source.name);
        cloneObject.SetActive(source.gameObject.activeSelf);
        Transform clone = cloneObject.transform;
        clone.SetParent(parent, false);
        clone.localPosition = root ? Vector3.zero : source.localPosition;
        clone.localRotation = root ? Quaternion.identity : source.localRotation;
        clone.localScale = source.localScale;
        transformMap[source] = clone;
        posePairs.Add(new KeyValuePair<Transform, Transform>(source, clone));

        foreach (Transform child in source)
            CloneTransformTree(child, clone, false);
        return clone;
    }

    void CopyRenderers()
    {
        foreach (KeyValuePair<Transform, Transform> pair in transformMap)
        {
            MeshRenderer sourceMeshRenderer = pair.Key.GetComponent<MeshRenderer>();
            MeshFilter sourceFilter = pair.Key.GetComponent<MeshFilter>();
            if (sourceMeshRenderer != null && sourceFilter != null)
            {
                pair.Value.gameObject.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer renderer = pair.Value.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceMeshRenderer.sharedMaterials;
                renderer.enabled = sourceMeshRenderer.enabled;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            SkinnedMeshRenderer sourceSkin = pair.Key.GetComponent<SkinnedMeshRenderer>();
            if (sourceSkin == null) continue;
            SkinnedMeshRenderer skin = pair.Value.gameObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = sourceSkin.sharedMesh;
            skin.sharedMaterials = sourceSkin.sharedMaterials;
            skin.localBounds = sourceSkin.localBounds;
            skin.updateWhenOffscreen = true;
            skin.enabled = sourceSkin.enabled;
            skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            skin.receiveShadows = false;

            Transform[] bones = new Transform[sourceSkin.bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bones[i] = sourceSkin.bones[i] != null && transformMap.TryGetValue(sourceSkin.bones[i], out Transform mapped)
                    ? mapped : pair.Value;
            skin.bones = bones;
            if (sourceSkin.rootBone != null && transformMap.TryGetValue(sourceSkin.rootBone, out Transform rootBone))
                skin.rootBone = rootBone;
        }
    }

    void FitCharacter()
    {
        List<Renderer> renderers = GetVisibleRenderers();
        if (renderers.Count == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
        float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        // Leave comfortable breathing room around the mannequin. At 3.55 the character filled
        // roughly 82% of the camera height and visually overwhelmed the inventory grid.
        if (maxSize > 0.01f) poseRoot.localScale *= 2.82f / maxSize;

        renderers = GetVisibleRenderers();
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
        poseRoot.position += spinPivot.position - bounds.center;
    }

    List<Renderer> GetVisibleRenderers()
    {
        List<Renderer> visible = new List<Renderer>();
        foreach (Renderer renderer in clonedCharacter.GetComponentsInChildren<Renderer>(false))
        {
            if (renderer.enabled)
                visible.Add(renderer);
        }
        return visible;
    }

    void OnDestroy()
    {
        if (clonedCharacter != null) Destroy(clonedCharacter);
        if (stageRoot != null) Destroy(stageRoot.gameObject);
        if (texture != null) texture.Release();
    }

    public void OnBeginDrag(PointerEventData eventData) => manualDragging = true;

    public void OnDrag(PointerEventData eventData)
    {
        manualYaw -= eventData.delta.x * .42f;
        if (previewCamera != null)
            previewCamera.Render();
    }

    public void OnEndDrag(PointerEventData eventData) => manualDragging = false;

    public void OnScroll(PointerEventData eventData)
    {
        if (previewCamera == null)
            return;
        previewCamera.orthographicSize = Mathf.Clamp(
            previewCamera.orthographicSize - eventData.scrollDelta.y * .12f, 1.45f, 3.1f);
        previewCamera.Render();
    }
}

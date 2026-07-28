using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public sealed class NewGameKingGoblinPortrait : MonoBehaviour
{
    const string ResourcePath = "Enemies/KingGoblinPortrait";
    const string EditorPrefabPath = "Assets/_RPG/Prefabs/Enemies/KingGoblinBoss.prefab";

    RawImage image;
    RenderTexture texture;
    Transform stage;
    Camera portraitCamera;
    GameObject model;
    Material softEdgeMaterial;

    void Awake()
    {
        image = GetComponent<RawImage>();
        image.raycastTarget = false;
        BuildPortrait();
    }

    void LateUpdate()
    {
        if (portraitCamera != null)
            portraitCamera.Render();
    }

    void BuildPortrait()
    {
        GameObject prefab = Resources.Load<GameObject>(ResourcePath);
#if UNITY_EDITOR
        if (prefab == null)
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(EditorPrefabPath);
#endif
        if (prefab == null)
        {
            image.enabled = false;
            return;
        }

        texture = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32)
        {
            name = "NewGame_KingGoblinFace",
            antiAliasing = 2,
            filterMode = FilterMode.Bilinear
        };
        texture.Create();
        image.texture = texture;
        image.color = new Color(1f, .82f, .72f, .76f);

        Shader softShader = Shader.Find("UI/SoftEdgeKingGoblinPortrait");
        if (softShader != null)
        {
            softEdgeMaterial = new Material(softShader)
            {
                name = "NewGame_KingGoblinFace_SoftEdges"
            };
            image.material = softEdgeMaterial;
        }

        stage = new GameObject("NewGameKingGoblinPortraitStage").transform;
        stage.position = PreviewRenderIsolation.AllocateStagePosition();
        model = Instantiate(prefab, stage);
        model.name = "KingGoblinFaceModel";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Canvas modelCanvas in model.GetComponentsInChildren<Canvas>(true))
            modelCanvas.enabled = false;
        foreach (ParticleSystem particles in model.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
        {
            animator.speed = 0f;
            animator.Update(0f);
        }
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            if (IsInterfaceRenderer(renderer.transform))
                renderer.enabled = false;

        PreviewRenderIsolation.SetLayerRecursive(model);
        Bounds bounds = CalculateBounds(model);
        Vector3 faceTarget = bounds.max - Vector3.up * bounds.size.y * .14f;
        float faceSize = Mathf.Clamp(bounds.size.y * .14f, .48f, 1.05f);
        Animator portraitAnimator = model.GetComponentInChildren<Animator>(true);
        if (portraitAnimator != null && portraitAnimator.isHuman)
        {
            Transform head = portraitAnimator.GetBoneTransform(HumanBodyBones.Head);
            Transform neck = portraitAnimator.GetBoneTransform(HumanBodyBones.Neck);
            if (head != null)
            {
                Vector3 headAxis = neck != null ? head.position - neck.position : Vector3.up;
                float headUnit = Mathf.Max(.08f, headAxis.magnitude);
                faceTarget = head.position + headAxis.normalized * headUnit * .42f;
                faceSize = Mathf.Clamp(
                    Mathf.Max(headUnit * 2.65f, bounds.size.y * .105f), .46f, 1.02f);
            }
        }

        GameObject cameraObject = new GameObject("KingGoblinPortraitCamera");
        cameraObject.transform.SetParent(stage, true);
        portraitCamera = cameraObject.AddComponent<Camera>();
        portraitCamera.clearFlags = CameraClearFlags.SolidColor;
        portraitCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        portraitCamera.cullingMask = PreviewRenderIsolation.PreviewMask;
        portraitCamera.orthographic = true;
        portraitCamera.orthographicSize = faceSize;
        portraitCamera.nearClipPlane = .05f;
        portraitCamera.farClipPlane = 30f;
        portraitCamera.targetTexture = texture;
        portraitCamera.enabled = false;

        Vector3 front = model.transform.forward;
        portraitCamera.transform.position = faceTarget + front * 9f;
        portraitCamera.transform.LookAt(faceTarget, Vector3.up);

        GameObject lightObject = new GameObject("KingGoblinPortraitLight");
        lightObject.transform.SetParent(stage, false);
        Light key = lightObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, .48f, .24f);
        key.intensity = 1.75f;
        key.cullingMask = PreviewRenderIsolation.PreviewMask;
        lightObject.transform.rotation = Quaternion.Euler(32f, 145f, 0f);

        GameObject rimObject = new GameObject("KingGoblinPortraitRim");
        rimObject.transform.SetParent(stage, false);
        Light rim = rimObject.AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.color = new Color(.5f, .12f, .82f);
        rim.intensity = .85f;
        rim.cullingMask = PreviewRenderIsolation.PreviewMask;
        rimObject.transform.rotation = Quaternion.Euler(18f, -35f, 0f);

        portraitCamera.Render();
    }

    static Bounds CalculateBounds(GameObject root)
    {
        var renderers = new List<Renderer>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            if (renderer.enabled && !(renderer is ParticleSystemRenderer) &&
                !(renderer is TrailRenderer) && !(renderer is LineRenderer))
                renderers.Add(renderer);
        if (renderers.Count == 0)
            return new Bounds(root.transform.position + Vector3.up, Vector3.one * 2f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Count; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static bool IsInterfaceRenderer(Transform candidate)
    {
        Transform current = candidate;
        while (current != null)
        {
            string objectName = current.name.ToLowerInvariant();
            if (objectName.Contains("healthbar") || objectName.Contains("health_bar") ||
                objectName.Contains("bossbar") || objectName.Contains("questattention") ||
                objectName.Contains("exclamation") || objectName.Contains("interactionprompt"))
                return true;
            current = current.parent;
        }
        return false;
    }

    void OnDestroy()
    {
        if (stage != null)
            Destroy(stage.gameObject);
        if (softEdgeMaterial != null)
            Destroy(softEdgeMaterial);
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }
}

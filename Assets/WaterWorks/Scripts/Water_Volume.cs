using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// WaterWorks volume pass for Unity 6.0 / URP 17.
/// URP 17 renders custom passes through RenderGraph, rather than the old
/// Configure/Execute RTHandle callbacks used by previous URP versions.
/// </summary>
public class Water_Volume : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        private const string PassName = "Water Volume Pass";
        private Material material;

        public void Setup(Material passMaterial)
        {
            material = passMaterial;
            // The effect samples the current camera colour, so it cannot be
            // rendered straight into the back buffer.
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
                return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.camera.cameraType == CameraType.Reflection)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogWarning("Water Volume Pass requires an intermediate colour texture and was skipped for the back buffer.");
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid())
                return;

            TextureDesc destinationDescription = renderGraph.GetTextureDesc(source);
            destinationDescription.name = "WaterWorks Temporary Colour Texture";
            destinationDescription.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDescription);

            RenderGraphUtils.BlitMaterialParameters parameters =
                new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
            renderGraph.AddBlitPass(parameters, PassName);

            // Make following URP passes use the processed colour texture.
            resourceData.cameraColor = destination;
        }
    }

    [System.Serializable]
    public class _Settings
    {
        public Material material;
        public RenderPassEvent renderPass = RenderPassEvent.AfterRenderingSkybox;
    }

    public _Settings settings = new _Settings();
    private CustomRenderPass scriptablePass;

    public override void Create()
    {
        if (settings.material == null)
            settings.material = Resources.Load<Material>("Water_Volume");

        scriptablePass = new CustomRenderPass
        {
            renderPassEvent = settings.renderPass
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (scriptablePass == null || settings.material == null)
            return;

        scriptablePass.Setup(settings.material);
        renderer.EnqueuePass(scriptablePass);
    }
}

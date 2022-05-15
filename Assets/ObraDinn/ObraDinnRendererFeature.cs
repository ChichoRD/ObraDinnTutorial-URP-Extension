using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ObraDinnRendererFeature : ScriptableRendererFeature
{
    private class ObraDinnRendererPass : ScriptableRenderPass
    {
        private readonly Material _dither;
        private readonly Material _threshold;

        private RenderTexture _large;
        private RenderTexture _main;
        
        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            _large = RenderTexture.GetTemporary(1640, 940, 0, RenderTextureFormat.ARGB32);
            _main = RenderTexture.GetTemporary(820, 470, 0, RenderTextureFormat.ARGB32);
            _large.filterMode = FilterMode.Bilinear;
            _main.filterMode = FilterMode.Bilinear;
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Vector3[] corners = new Vector3[4];
            Camera cam = renderingData.cameraData.camera;
            Transform camTransform = cam.transform;
            cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), cam.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, corners);

            for (int i = 0; i < 4; i++)
            {
                corners[i] = camTransform.TransformVector(corners[i]);
                corners[i].Normalize();
            }

            _dither.SetVector("_BL", corners[0]);
            _dither.SetVector("_TL", corners[1]);
            _dither.SetVector("_TR", corners[2]);
            _dither.SetVector("_BR", corners[3]);

            var colorTarget = renderingData.cameraData.renderer.cameraColorTarget;            
            CommandBuffer cmd = CommandBufferPool.Get();
                        
            using (new ProfilingScope(cmd, profilingSampler))
            {
                cmd.Blit(colorTarget, _large, _dither);
                cmd.Blit(_large, _main, _threshold);
            }
            
            cmd.Blit(_main, colorTarget);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            RenderTexture.ReleaseTemporary(_large);
            RenderTexture.ReleaseTemporary(_main);
        }

        public ObraDinnRendererPass(Material dither, Material threshold)
        {
            _dither = dither;
            _threshold = threshold;
        }
    }
    
    [System.Serializable]
    private class Settings
    {
        public Material dither;
        public Material threshold;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }
    
    [SerializeField] Settings _settings;
    ObraDinnRendererPass _ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {        
        _ScriptablePass = new ObraDinnRendererPass(_settings.dither, _settings.threshold);

        // Configures where the render pass should be injected.
        _ScriptablePass.renderPassEvent = _settings.renderPassEvent;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        bool validPass = _settings.dither != null && _settings.threshold != null; ;
        Debug.Assert(validPass, "Missing dither materials");
        if (!validPass) return;
        
        renderer.EnqueuePass(_ScriptablePass);
    }
}
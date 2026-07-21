using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class SobelEdgeFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public sealed class SobelSettings
    {
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
        public Shader sobelShader;
        [Range(0f, 1f)] public float edgeOnly = 0f;
        public Color edgeColor = Color.black;
        public Color backgroundColor = Color.white;
        [Range(0.5f, 5f)] public float edgeWidth = 1f;
        [Header("Edge Sensitivity")] [Range(0f, 5f)] public float colorSensitivity = 1f;
        [Range(0f, 20f)] public float depthSensitivity = 3f;
        [Header("Edge Quality")] [Range(0f, 0.5f)] public float edgeThreshold = 0.05f;
        [Range(1f, 30f)] public float edgeSharpness = 8f;
    }

    private sealed class SobelEdgePass : ScriptableRenderPass
    {
        private static readonly int PropEdgeOnly = Shader.PropertyToID("_EdgeOnly");
        private static readonly int PropEdgeColor = Shader.PropertyToID("_EdgeColor");
        private static readonly int PropBackgroundColor = Shader.PropertyToID("_BackgroundColor");
        private static readonly int PropEdgeWidth = Shader.PropertyToID("_EdgeWidth");
        private static readonly int PropColorSensitivity = Shader.PropertyToID("_ColorSensitivity");
        private static readonly int PropDepthSensitivity = Shader.PropertyToID("_DepthSensitivity");
        private static readonly int PropEdgeThreshold = Shader.PropertyToID("_EdgeThreshold");
        private static readonly int PropEdgeSharpness = Shader.PropertyToID("_EdgeSharpness");
        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");
        private static readonly int PropMainTexTexelSize = Shader.PropertyToID("_MainTex_TexelSize");

        private readonly SobelSettings _settings;
        private readonly Material _material;
        private RTHandle _cameraColorTarget;
        private RTHandle _temporaryColor;

        public SobelEdgePass(Material material, SobelSettings settings)
        {
            _material = material;
            _settings = settings;
            renderPassEvent = settings.passEvent;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void Setup()
        {
            renderPassEvent = _settings.passEvent;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            _cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref _temporaryColor, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp,
                name: "_SobelTempTex");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null || _temporaryColor == null || _cameraColorTarget == null)
                return;

            CameraData cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game)
                return;

            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            float width = Mathf.Max(1f, descriptor.width);
            float height = Mathf.Max(1f, descriptor.height);

            _material.SetFloat(PropEdgeOnly, _settings.edgeOnly);
            _material.SetColor(PropEdgeColor, _settings.edgeColor);
            _material.SetColor(PropBackgroundColor, _settings.backgroundColor);
            _material.SetFloat(PropEdgeWidth, _settings.edgeWidth);
            _material.SetFloat(PropColorSensitivity, _settings.colorSensitivity);
            _material.SetFloat(PropDepthSensitivity, _settings.depthSensitivity);
            _material.SetFloat(PropEdgeThreshold, _settings.edgeThreshold);
            _material.SetFloat(PropEdgeSharpness, _settings.edgeSharpness);

            CommandBuffer cmd = CommandBufferPool.Get("SobelEdge");
            Blitter.BlitCameraTexture(cmd, _cameraColorTarget, _temporaryColor);
            cmd.SetGlobalTexture(PropMainTex, _temporaryColor.nameID);
            cmd.SetGlobalVector(PropMainTexTexelSize, new Vector4(1f / width, 1f / height, width, height));
            Blitter.BlitCameraTexture(cmd, _temporaryColor, _cameraColorTarget, _material, 0);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _temporaryColor?.Release();
            _temporaryColor = null;
        }
    }

    public SobelSettings settings = new SobelSettings();

    private Material _material;
    private SobelEdgePass _pass;

    public override void Create()
    {
        Shader sobelShader = settings.sobelShader;
        if (sobelShader == null)
            sobelShader = Shader.Find("Unlit/URP_SobelOutline");

        if (sobelShader == null)
        {
            _material = null;
            _pass = null;
            Debug.LogWarning("[SobelEdgeFeature] Shader 'Unlit/URP_SobelOutline' was not found.");
            return;
        }

        settings.sobelShader = sobelShader;

        if (_material != null && _material.shader != sobelShader)
            CoreUtils.Destroy(_material);

        _material ??= CoreUtils.CreateEngineMaterial(sobelShader);
        _pass ??= new SobelEdgePass(_material, settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || _pass == null)
            return;

        _pass.Setup();
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        _pass = null;

        if (_material != null)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }
    }
}

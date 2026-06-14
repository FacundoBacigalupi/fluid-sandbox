// Usa el path de compatibilidad de ScriptableRenderPass (sin Render Graph).
// CS0618/CS0672 son esperadas en URP 17 al no migrar a RecordRenderGraph — funciona correctamente.
#pragma warning disable CS0618, CS0672

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// URP ScriptableRendererFeature que captura la profundidad de las esferas,
// aplica bilateral blur y reconstruye normales para el efecto de superficie líquida.
// Agregar en: URP Renderer Data → Add Renderer Feature → Fluid SSF Render Feature.
public class FluidSSFRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader ssfShader;
    }

    public Settings settings = new();
    FluidSSFPass _pass;

    public override void Create()
    {
        _pass = new FluidSSFPass(settings)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (FluidSSFController.Instance == null)   return;
        if (FluidSSFController.Instance.particleCount == 0) return;
        if (settings.ssfShader == null) return;
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing) => _pass?.Dispose();
}

// ─────────────────────────────────────────────────────────────────────────────

class FluidSSFPass : ScriptableRenderPass, System.IDisposable
{
    readonly FluidSSFRenderFeature.Settings _s;
    Material _mat;

    // RTs para el pipeline SSF
    RTHandle _depthRT;      // profundidad lineal de esferas (RFloat)
    RTHandle _depthBufRT;   // depth buffer real para rasterizar esferas
    RTHandle _blurHRT;      // bilateral blur H
    RTHandle _blurVRT;      // bilateral blur V  (= resultado final)

    // IDs de propiedades del shader
    static readonly int ID_WaterColor     = Shader.PropertyToID("_WaterColor");
    static readonly int ID_DeepColor      = Shader.PropertyToID("_DeepColor");
    static readonly int ID_Smoothness     = Shader.PropertyToID("_Smoothness");
    static readonly int ID_FresnelPow     = Shader.PropertyToID("_FresnelPow");
    static readonly int ID_Opacity        = Shader.PropertyToID("_Opacity");
    static readonly int ID_BlurRadius     = Shader.PropertyToID("_BlurRadius");
    static readonly int ID_BlurFalloff    = Shader.PropertyToID("_BlurFalloff");
    static readonly int ID_DepthScale     = Shader.PropertyToID("_DepthScale");
    static readonly int ID_FluidDepth     = Shader.PropertyToID("_FluidDepth");
    static readonly int ID_FluidBlurH     = Shader.PropertyToID("_FluidBlurH");
    static readonly int ID_FluidDepthBlur = Shader.PropertyToID("_FluidDepthBlur");

    const int PASS_DEPTH     = 0;
    const int PASS_BLUR_H    = 1;
    const int PASS_BLUR_V    = 2;
    const int PASS_COMPOSITE = 3;

    public FluidSSFPass(FluidSSFRenderFeature.Settings s) { _s = s; }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        var desc = renderingData.cameraData.cameraTargetDescriptor;

        // Textura RFloat para la profundidad capturada de las esferas
        var colorDesc = desc;
        colorDesc.colorFormat     = RenderTextureFormat.RFloat;
        colorDesc.depthBufferBits = 0;
        colorDesc.msaaSamples     = 1;
        RenderingUtils.ReAllocateIfNeeded(ref _depthRT,  colorDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_FluidDepth");
        RenderingUtils.ReAllocateIfNeeded(ref _blurHRT,  colorDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_FluidBlurH");
        RenderingUtils.ReAllocateIfNeeded(ref _blurVRT,  colorDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_FluidDepthBlur");

        // Depth buffer para la rasterización de esferas (necesario para Z-test)
        var depthDesc = desc;
        depthDesc.colorFormat     = RenderTextureFormat.Depth;
        depthDesc.depthBufferBits = 24;
        depthDesc.msaaSamples     = 1;
        RenderingUtils.ReAllocateIfNeeded(ref _depthBufRT, depthDesc, FilterMode.Point, TextureWrapMode.Clamp, name: "_FluidSphereDepth");
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var ctrl = FluidSSFController.Instance;
        if (ctrl == null || ctrl.particleCount == 0 || ctrl.sphereMesh == null) return;

        if (_mat == null)
        {
            if (_s.ssfShader == null) return;
            _mat = new Material(_s.ssfShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        // Actualizar propiedades del shader
        _mat.SetColor(ID_WaterColor,  ctrl.waterColor);
        _mat.SetColor(ID_DeepColor,   ctrl.deepColor);
        _mat.SetFloat(ID_Smoothness,  ctrl.smoothness);
        _mat.SetFloat(ID_FresnelPow,  ctrl.fresnelPow);
        _mat.SetFloat(ID_Opacity,     ctrl.opacity);
        _mat.SetFloat(ID_BlurRadius,  ctrl.blurRadius);
        _mat.SetFloat(ID_BlurFalloff, ctrl.blurFalloff);
        _mat.SetFloat(ID_DepthScale,  ctrl.depthScale);

        var cmd = CommandBufferPool.Get("FluidSSF");

        // ── 1. Captura de profundidad de partículas ───────────────────────────
        // SetRenderTarget(colorRT, depthRT) — el depthBufRT provee Z-test real
        cmd.SetRenderTarget(_depthRT.nameID, _depthBufRT.nameID);
        cmd.ClearRenderTarget(true, true, Color.clear, 1.0f);

        int n    = ctrl.particleCount;
        var mats = ctrl.instanceMatrices;
        for (int start = 0; start < n; start += 1023)
        {
            int count = Mathf.Min(1023, n - start);
            var slice = new Matrix4x4[count];
            System.Array.Copy(mats, start, slice, 0, count);
            cmd.DrawMeshInstanced(ctrl.sphereMesh, 0, _mat, PASS_DEPTH, slice, count);
        }

        // ── 2. Bilateral blur H ───────────────────────────────────────────────
        // Usar DrawProcedural (triángulo fullscreen) en lugar de cmd.Blit (deprecado en URP 17)
        _mat.SetTexture(ID_FluidDepth, _depthRT);
        cmd.SetRenderTarget(_blurHRT.nameID);
        cmd.DrawProcedural(Matrix4x4.identity, _mat, PASS_BLUR_H, MeshTopology.Triangles, 3);

        // ── 3. Bilateral blur V ───────────────────────────────────────────────
        _mat.SetTexture(ID_FluidBlurH, _blurHRT);
        cmd.SetRenderTarget(_blurVRT.nameID);
        cmd.DrawProcedural(Matrix4x4.identity, _mat, PASS_BLUR_V, MeshTopology.Triangles, 3);

        // ── 4. Composite sobre color de cámara ────────────────────────────────
        _mat.SetTexture(ID_FluidDepthBlur, _blurVRT);
        var colorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
        cmd.SetRenderTarget(colorTarget.nameID);
        cmd.DrawProcedural(Matrix4x4.identity, _mat, PASS_COMPOSITE, MeshTopology.Triangles, 3);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void OnCameraCleanup(CommandBuffer cmd) { }

    public void Dispose()
    {
        _depthRT?.Release();
        _depthBufRT?.Release();
        _blurHRT?.Release();
        _blurVRT?.Release();
        if (_mat != null) CoreUtils.Destroy(_mat);
    }
}

// Screen-Space Fluid Rendering — shader multi-pass.
// Pass 0 "DepthSphere"  : renderiza esferas de partículas, guarda profundidad lineal en canal R.
// Pass 1 "BlurH"        : bilateral blur horizontal (suaviza profundidad preservando bordes).
// Pass 2 "BlurV"        : bilateral blur vertical.
// Pass 3 "Composite"    : reconstruye normales desde gradiente de profundidad → Fresnel + especular.

Shader "FluidSandbox/FluidSSF"
{
    Properties
    {
        _WaterColor   ("Water Color",    Color)      = (0.05, 0.35, 0.8,  1)
        _DeepColor    ("Deep Color",     Color)      = (0.02, 0.10, 0.40, 1)
        _Smoothness   ("Smoothness",     Range(0,1)) = 0.92
        _FresnelPow   ("Fresnel Power",  Range(1,8)) = 3.5
        _Opacity      ("Opacity",        Range(0,1)) = 0.88
        _BlurRadius   ("Blur Radius",    Range(1,24))= 10
        _BlurFalloff  ("Blur Depth Falloff", Range(0.1,10)) = 2.0
        _DepthScale   ("Depth Scale",    Range(0.01,0.5)) = 0.08

        [HideInInspector] _FluidDepth      ("Fluid Depth",     2D) = "black" {}
        [HideInInspector] _FluidBlurH      ("Fluid Blur H",    2D) = "black" {}
        [HideInInspector] _FluidDepthBlur  ("Fluid Depth Blur",2D) = "black" {}
        [HideInInspector] _SceneColor      ("Scene Color",     2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        // ── Pass 0: DepthSphere ───────────────────────────────────────────────────
        // Renderiza esferas (meshes) y guarda profundidad lineal de vista en canal R.

        Pass
        {
            Name "DepthSphere"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Back
            ColorMask R

            HLSLPROGRAM
            #pragma vertex   vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct AttrD  { float4 posOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VaryD  { float4 posCS : SV_POSITION; float  eyeZ : TEXCOORD0;
                            UNITY_VERTEX_INPUT_INSTANCE_ID };

            VaryD vertDepth(AttrD IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                VaryD OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float3 posWS = TransformObjectToWorld(IN.posOS.xyz);
                OUT.posCS = TransformWorldToHClip(posWS);
                // Profundidad positiva en espacio de vista (cámara mira hacia -Z)
                OUT.eyeZ  = -TransformWorldToView(posWS).z;
                return OUT;
            }

            float fragDepth(VaryD IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                return IN.eyeZ; // profundidad lineal, float
            }
            ENDHLSL
        }

        // ── Pass 1: Bilateral Blur H ──────────────────────────────────────────────

        Pass
        {
            Name "BlurH"
            ZWrite Off ZTest Always Cull Off

            HLSLPROGRAM
            #pragma vertex   vertFS
            #pragma fragment fragBlurH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FluidDepth);  SAMPLER(sampler_FluidDepth);
            float4 _FluidDepth_TexelSize;
            float  _BlurRadius;
            float  _BlurFalloff;

            struct AttrFS { uint vid : SV_VertexID; };
            struct VaryFS { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            VaryFS vertFS(AttrFS IN)
            {
                VaryFS OUT;
                // Fullscreen triangle (no mesh needed)
                OUT.uv  = float2((IN.vid << 1) & 2, IN.vid & 2);
                OUT.pos = float4(OUT.uv * 2.0 - 1.0, 0.0, 1.0);
                // Flip V for DX/OpenGL platform difference
                #if UNITY_UV_STARTS_AT_TOP
                OUT.uv.y = 1.0 - OUT.uv.y;
                #endif
                return OUT;
            }

            float BilateralWeight(float centerD, float sampleD, float falloff)
            {
                float diff = centerD - sampleD;
                // Gaussiana espacial fija (sigma=1), peso de rango por diferencia de profundidad
                return exp(-diff * diff * falloff);
            }

            float fragBlurH(VaryFS IN) : SV_Target
            {
                float2 ts  = _FluidDepth_TexelSize.xy;
                float  cd  = SAMPLE_TEXTURE2D(_FluidDepth, sampler_FluidDepth, IN.uv).r;
                if (cd <= 0.0) return 0.0; // sin fluido

                float sum = 0.0, wSum = 0.0;
                int   R   = (int)_BlurRadius;
                for (int x = -R; x <= R; x++)
                {
                    float2 off = float2(x * ts.x, 0.0);
                    float  sd  = SAMPLE_TEXTURE2D(_FluidDepth, sampler_FluidDepth, IN.uv + off).r;
                    if (sd <= 0.0) continue;
                    float spatW = exp(-0.5 * x * x / (R * R * 0.25));
                    float rangW = BilateralWeight(cd, sd, _BlurFalloff);
                    float w     = spatW * rangW;
                    sum  += sd * w;
                    wSum += w;
                }
                return wSum > 0.0 ? sum / wSum : cd;
            }
            ENDHLSL
        }

        // ── Pass 2: Bilateral Blur V ──────────────────────────────────────────────

        Pass
        {
            Name "BlurV"
            ZWrite Off ZTest Always Cull Off

            HLSLPROGRAM
            #pragma vertex   vertFS
            #pragma fragment fragBlurV
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FluidBlurH);  SAMPLER(sampler_FluidBlurH);
            float4 _FluidBlurH_TexelSize;
            float  _BlurRadius;
            float  _BlurFalloff;

            struct AttrFS { uint vid : SV_VertexID; };
            struct VaryFS { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            VaryFS vertFS(AttrFS IN)
            {
                VaryFS OUT;
                OUT.uv  = float2((IN.vid << 1) & 2, IN.vid & 2);
                OUT.pos = float4(OUT.uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                OUT.uv.y = 1.0 - OUT.uv.y;
                #endif
                return OUT;
            }

            float fragBlurV(VaryFS IN) : SV_Target
            {
                float2 ts  = _FluidBlurH_TexelSize.xy;
                float  cd  = SAMPLE_TEXTURE2D(_FluidBlurH, sampler_FluidBlurH, IN.uv).r;
                if (cd <= 0.0) return 0.0;

                float sum = 0.0, wSum = 0.0;
                int   R   = (int)_BlurRadius;
                for (int y = -R; y <= R; y++)
                {
                    float2 off = float2(0.0, y * ts.y);
                    float  sd  = SAMPLE_TEXTURE2D(_FluidBlurH, sampler_FluidBlurH, IN.uv + off).r;
                    if (sd <= 0.0) continue;
                    float spatW = exp(-0.5 * y * y / (R * R * 0.25));
                    float rangW = exp(-(cd - sd) * (cd - sd) * _BlurFalloff);
                    float w     = spatW * rangW;
                    sum  += sd * w;
                    wSum += w;
                }
                return wSum > 0.0 ? sum / wSum : cd;
            }
            ENDHLSL
        }

        // ── Pass 3: Composite ─────────────────────────────────────────────────────
        // Reconstruye normales desde gradiente de profundidad → Fresnel + especular.

        Pass
        {
            Name "Composite"
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vertFS
            #pragma fragment fragComposite
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_FluidDepthBlur); SAMPLER(sampler_FluidDepthBlur);
            TEXTURE2D(_SceneColor);     SAMPLER(sampler_SceneColor);
            float4 _FluidDepthBlur_TexelSize;

            float4 _WaterColor;
            float4 _DeepColor;
            float  _Smoothness;
            float  _FresnelPow;
            float  _Opacity;
            float  _DepthScale;

            struct AttrFS { uint vid : SV_VertexID; };
            struct VaryFS { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            VaryFS vertFS(AttrFS IN)
            {
                VaryFS OUT;
                OUT.uv  = float2((IN.vid << 1) & 2, IN.vid & 2);
                OUT.pos = float4(OUT.uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                OUT.uv.y = 1.0 - OUT.uv.y;
                #endif
                return OUT;
            }

            float4 fragComposite(VaryFS IN) : SV_Target
            {
                float2 ts = _FluidDepthBlur_TexelSize.xy;
                float  d  = SAMPLE_TEXTURE2D(_FluidDepthBlur, sampler_FluidDepthBlur, IN.uv).r;

                // Solo donde hay fluido (profundidad > 0)
                clip(d - 0.001);

                // Reconstruir normal desde gradiente de profundidad (espacio de pantalla)
                float dR = SAMPLE_TEXTURE2D(_FluidDepthBlur, sampler_FluidDepthBlur, IN.uv + float2( ts.x, 0)).r;
                float dU = SAMPLE_TEXTURE2D(_FluidDepthBlur, sampler_FluidDepthBlur, IN.uv + float2(0,  ts.y)).r;
                float ddx = (dR > 0.001 ? dR : d) - d;
                float ddy = (dU > 0.001 ? dU : d) - d;
                // Normal en espacio de vista: tangentes [1,0,-ddx] × [0,1,-ddy]
                float3 N = normalize(float3(-ddx / ts.x * _DepthScale,
                                           -ddy / ts.y * _DepthScale,
                                            1.0));

                // Dirección de vista (espacio de pantalla → aproximación frontal)
                float3 V = float3(0, 0, 1);

                // Fresnel
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPow);

                // Color del fluido: mezcla profundidad superficial/profunda
                float depthT = saturate(d * 0.15);
                float4 fluidColor = lerp(_WaterColor, _DeepColor, depthT);

                // Especular (luz principal)
                float3 L     = normalize(_MainLightPosition.xyz);
                float3 H     = normalize(V + L);
                float  NdotH = saturate(dot(N, H));
                float  spec  = pow(NdotH, lerp(8.0, 512.0, _Smoothness)) * _MainLightColor.r;

                float3 finalColor = fluidColor.rgb + spec * 0.6 + fresnel * 0.25;
                float  alpha      = _Opacity * saturate(fluidColor.a + fresnel * 0.3);

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}

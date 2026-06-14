Shader "FluidSandbox/WaterParticle"
{
    Properties
    {
        _WaterColor ("Water Color",   Color)         = (0.08, 0.35, 0.75, 0.55)
        _RimColor   ("Rim Color",     Color)         = (0.55, 0.85, 1.0,  1.0)
        _Smoothness ("Smoothness",    Range(0,1))    = 0.92
        _FresnelPow ("Fresnel Power", Range(0.5,8))  = 2.5
        _SoftEdge   ("Soft Edge",     Range(0,0.8))  = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _WaterColor;
                float4 _RimColor;
                float  _Smoothness;
                float  _FresnelPow;
                float  _SoftEdge;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                ZERO_INITIALIZE(Varyings, output);
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs pi = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   ni = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = pi.positionCS;
                output.positionWS  = pi.positionWS;
                output.normalWS    = ni.normalWS;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 N    = normalize(input.normalWS);
                float3 V    = normalize(GetWorldSpaceViewDir(input.positionWS));
                float  NdotV = saturate(dot(N, V));

                float fresnel   = pow(1.0 - NdotV, _FresnelPow);
                float softAlpha = smoothstep(0.0, _SoftEdge + 0.001, NdotV);

                Light  light = GetMainLight();
                float  NdotL = saturate(dot(N, light.direction));
                float3 H     = normalize(light.direction + V);
                float  spec  = pow(saturate(dot(N, H)), _Smoothness * 128.0 + 1.0);

                float3 col = _WaterColor.rgb * (0.15 + 0.70 * NdotL) * light.color
                           + spec * light.color * _Smoothness
                           + fresnel * _RimColor.rgb;

                float alpha = lerp(_WaterColor.a, 1.0, fresnel * 0.5) * softAlpha;
                return float4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

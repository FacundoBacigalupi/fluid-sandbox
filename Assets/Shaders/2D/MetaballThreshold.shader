// Convierte la RenderTexture de blobs acumulados en el efecto metaball final.
// Pixels con valor > _Threshold se muestran con el color líquido.
// smoothstep genera un borde suave alrededor del umbral.
// Aplicado como material de un RawImage en un Canvas Screen Space Overlay.
Shader "FluidSandbox/MetaballThreshold"
{
    Properties
    {
        _MainTex    ("Blob RT",       2D)    = "black" {}
        _Threshold  ("Threshold",  Range(0.01, 1.5)) = 0.5
        _LiquidColor("Liquid Color",  Color) = (0.15, 0.5, 1, 0.95)
        _Softness   ("Edge Softness", Range(0.001, 0.15)) = 0.03
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull   Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _LiquidColor;
            float     _Threshold;
            float     _Softness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float val   = tex2D(_MainTex, i.uv).r;
                float alpha = smoothstep(_Threshold - _Softness,
                                         _Threshold + _Softness, val);
                return fixed4(_LiquidColor.rgb, alpha * _LiquidColor.a);
            }
            ENDCG
        }
    }
}

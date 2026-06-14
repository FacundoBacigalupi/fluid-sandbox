// Renderiza partículas como manchas suaves para el efecto metaball.
// Usa blending ADITIVO (Blend One One): varios blobs solapados suman sus valores.
// La cámara blob acumula estos valores en una RenderTexture,
// luego MetaballThreshold aplica el umbral para crear el efecto líquido.
Shader "FluidSandbox/BlobParticle"
{
    Properties
    {
        _MainTex  ("Texture",   2D)    = "white" {}
        _Color    ("Tint",      Color) = (1,1,1,1)
        _Intensity("Intensity", Float) = 0.7
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend  One One   // Aditivo: overlapping = suma de valores
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
            fixed4    _Color;
            float     _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };
            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float val = tex2D(_MainTex, i.uv).r * _Intensity;
                return fixed4(val, val, val, val) * i.color;
            }
            ENDCG
        }
    }
}

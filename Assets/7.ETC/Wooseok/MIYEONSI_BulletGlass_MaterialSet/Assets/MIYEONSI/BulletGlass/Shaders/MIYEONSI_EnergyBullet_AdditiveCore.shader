Shader "MIYEONSI/EnergyBullet/AdditiveCore"
{
    Properties
    {
        _Color ("Core Tint", Color) = (1, 0.11, 0.28, 1)
        _MainTex ("Core Sprite / Emission", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 20)) = 4.0
        _Alpha ("Alpha", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Intensity;
            half _Alpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed3 rgb = tex.rgb * _Color.rgb * _Intensity * i.color.rgb;
                fixed a = tex.a * _Color.a * _Alpha * i.color.a;
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}

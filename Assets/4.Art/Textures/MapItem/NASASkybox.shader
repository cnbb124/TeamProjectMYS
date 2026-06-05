Shader "Custom/NASASkybox"
{
   Properties
    {
        _MainTex ("Panoramic Texture (Equirectangular)", 2D) = "grey" {}
        
        [Header(Twinkling Settings)]
        _TwinkleSpeed ("Twinkle Speed", Range(0.1, 5.0)) = 2.0
        _TwinkleIntensity ("Twinkle Intensity", Range(0.0, 3.0)) = 2.0
        _TwinkleScale ("Twinkle Scale (Star Size Threshold)", Range(1.0, 500.0)) = 10.0
        
        [Header(Color Settings)]
        _Exposure ("Exposure", Range(0.0, 8.0)) = 1.0
        _Tint ("Tint Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _TwinkleSpeed;
            float _TwinkleIntensity;
            float _TwinkleScale;
            float _Exposure;
            float4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldDir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // 해시 함수 - 별 위치마다 고유한 랜덤값 생성
            float hash(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            // 3D 방향벡터 → equirectangular UV 변환
            float2 dirToEquirect(float3 dir)
            {
                float3 n = normalize(dir);
                float u = 0.5 + atan2(n.z, n.x) / (2.0 * 3.14159265);
                float v = 0.5 - asin(clamp(n.y, -1.0, 1.0)) / 3.14159265;
                return float2(u, v);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldDir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.worldDir);
                float2 uv = dirToEquirect(dir);
    
                float4 col = tex2D(_MainTex, uv);
    
                float2 texelSize = float2(1.0/4096.0, 1.0/2048.0);
                float4 colU = tex2D(_MainTex, uv + float2(0, texelSize.y));
                float4 colD = tex2D(_MainTex, uv - float2(0, texelSize.y));
                float4 colL = tex2D(_MainTex, uv + float2(texelSize.x, 0));
                float4 colR = tex2D(_MainTex, uv - float2(texelSize.x, 0));
    
                float avg = dot((colU + colD + colL + colR).rgb / 4.0, float3(0.299, 0.587, 0.114));
                float center = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float starMask = smoothstep(0.02, 0.1, center - avg);
    
                float2 cell = floor(uv * float2(1365.0, 682.0));
                float id = hash(cell);
                float speed = _TwinkleSpeed * (0.5 + id * 1.5);
                float phase = id * 6.2831;
                float twinkleVal = sin(_Time.y * speed + phase) * 0.5
                         + sin(_Time.y * speed * 1.7 + phase * 2.3) * 0.5;

                col.rgb *= 1.0 + twinkleVal * _TwinkleIntensity * 0.5 * starMask;
    
                col.rgb *= _Exposure * _Tint.rgb;
    
                return float4(col.rgb, 1.0);
            }
            ENDCG
        }
    }
    
    Fallback Off

}
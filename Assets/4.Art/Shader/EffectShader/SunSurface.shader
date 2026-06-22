// [SunSurface.shader]
// 빌트인 파이프라인용 태양 표면 셰이더.
// 노이즈로 표면이 일렁이고, 가장자리(Fresnel)가 코로나처럼 빛남.
//
// [사용법]
// 1. Assets > Create > Shader > Unlit Shader 만든 뒤 내용 교체, 또는 이 파일로 저장
// 2. Material 생성 후 이 셰이더(Custom/SunSurface) 지정
// 3. SunObject의 Quad/Sphere 메시에 이 머티리얼 적용
// 4. ★ Post Processing의 Bloom을 반드시 켜야 이글거림이 살아남 (Emission 강도 + Bloom)
//
// [주요 파라미터]
// _CoreColor    : 태양 중심 색 (HDR, 강도 높게)
// _EdgeColor    : 가장자리 코로나 색 (HDR)
// _NoiseScale   : 표면 노이즈 촘촘함
// _FlowSpeed    : 일렁임 속도
// _FresnelPow   : 가장자리 발광 두께
// _SpinSpeed    : 자전 속도 (표면이 U방향으로 흐름. 0이면 자전 없음)
// _Differential : 차등 자전 강도 (실제 태양처럼 적도는 빠르고 극지방은 느리게. 0이면 균일 자전)

Shader "Custom/SunSurface"
{
    Properties
    {
        [HDR] _CoreColor  ("Core Color", Color) = (1, 0.6, 0.1, 1)
        [HDR] _EdgeColor  ("Edge Color", Color) = (1, 0.3, 0.0, 1)
        _NoiseScale ("Noise Scale", Float)   = 4.0
        _FlowSpeed  ("Flow Speed", Float)    = 0.3
        _Distort    ("Surface Distort", Float)= 0.15
        _FresnelPow ("Fresnel Power", Float)  = 2.0
        _Intensity  ("Emission Intensity", Float) = 3.0
        _SpinSpeed    ("Spin Speed", Float)        = 0.05
        _Differential ("Differential Rotation", Float) = 0.4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir   : TEXCOORD2;
            };

            fixed4 _CoreColor;
            fixed4 _EdgeColor;
            float  _NoiseScale;
            float  _FlowSpeed;
            float  _Distort;
            float  _FresnelPow;
            float  _Intensity;
            float  _SpinSpeed;
            float  _Differential;

            // ── 해시 기반 그래디언트 노이즈 (텍스처 불필요) ──
            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453) * 2.0 - 1.0;
            }

            float gnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = dot(hash22(i + float2(0,0)), f - float2(0,0));
                float b = dot(hash22(i + float2(1,0)), f - float2(1,0));
                float c = dot(hash22(i + float2(0,1)), f - float2(0,1));
                float d = dot(hash22(i + float2(1,1)), f - float2(1,1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 0.5 + 0.5;
            }

            // 여러 옥타브 누적 (끓는 표면 느낌)
            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += amp * gnoise(p);
                    p *= 2.0;
                    amp *= 0.5;
                }
                return v;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _FlowSpeed;

                // ── 자전 (U방향 UV 스크롤) ──
                // 차등 자전: 적도(v=0.5)에서 가장 빠르고 극지방(v=0,1)에서 느림.
                // sin(v*PI) = 적도 1.0 → 극지방 0.0 곡선.
                float latitudeFactor = lerp(1.0, sin(i.uv.y * UNITY_PI), _Differential);
                float spinOffset = _Time.y * _SpinSpeed * latitudeFactor;

                // 서로 다른 속도의 노이즈 2겹으로 일렁임 (+ 자전 오프셋)
                float2 uv = i.uv * _NoiseScale;
                uv.x += spinOffset * _NoiseScale;
                float n1 = fbm(uv + float2(t, t * 0.7));
                float n2 = fbm(uv * 1.8 - float2(t * 0.5, t));

                // 표면 왜곡
                float2 distortUV = uv + (n1 - 0.5) * _Distort;
                float surface = fbm(distortUV + float2(t * 0.8, -t * 0.6));

                // 끓는 표면 밝기 (n1, n2, surface 혼합)
                float heat = saturate(n1 * 0.5 + n2 * 0.3 + surface * 0.4);

                // Fresnel — 가장자리 코로나 발광
                float fres = pow(1.0 - saturate(dot(i.worldNormal, i.viewDir)), _FresnelPow);

                // 중심색 ↔ 가장자리색 블렌딩
                fixed3 col = lerp(_CoreColor.rgb, _EdgeColor.rgb, heat);
                col += _EdgeColor.rgb * fres * 2.0; // 가장자리 발광 추가

                col *= _Intensity; // HDR 강도 (Bloom과 연동)

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
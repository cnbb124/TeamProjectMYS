// [UIFrostedGlass.shader]
// 빌트인 파이프라인용 UI 프로스티드 글래스(글래스모피즘) 셰이더.
// 패널 뒤 화면을 GrabPass로 캡쳐 → 블러 + 반투명 틴트 + 세로 그라데이션 + 가장자리 하이라이트.
//
// [필수 조건]
// ★ Canvas Render Mode = "Screen Space - Camera" 또는 "World Space" 여야 함.
//   (Screen Space - Overlay는 GrabPass가 뒤 화면을 캡쳐 못 해서 작동 안 함)
//
// [사용법]
// 1. Material 생성 → 이 셰이더(UI/FrostedGlass) 지정
// 2. 유리로 만들 UI Image의 Material 슬롯에 그 머티리얼 연결
//    (Image의 Source Image는 비워도 되고, 모서리 둥근 흰색 스프라이트면 모양이 그 형태로 잘림)
// 3. 머티리얼에서 블러/틴트/하이라이트 조절
//
// [파라미터]
// _TintColor   : 유리 색 (알파로 불투명도. 어두운 남색 + 알파 0.3~0.5 권장)
// _BlurSize    : 블러 강도 (클수록 더 흐림. 성능 ↑ 주의)
// _TopGlow     : 위쪽 밝기 보강 (유리 윗면 반사)
// _EdgeColor   : 가장자리 하이라이트 색
// _EdgePower   : 가장자리 하이라이트 두께

Shader "UI/FrostedGlass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (모양 마스크)", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (0.05, 0.08, 0.15, 0.4)
        _BlurSize  ("Blur Size", Range(0, 8)) = 3.0
        _TopGlow   ("Top Glow", Range(0, 1)) = 0.15
        _EdgeColor ("Edge Highlight", Color) = (0.5, 0.7, 1.0, 1.0)
        _EdgePower ("Edge Power", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        // 패널 뒤 화면 캡쳐 (이름 지정 → 같은 셰이더 쓰는 패널끼리 1번만 캡쳐 = 성능 절약)
        GrabPass { "_GlassGrabTex" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float4 grabPos  : TEXCOORD1;
                float4 color    : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _GlassGrabTex;
            float4    _GlassGrabTex_TexelSize;

            fixed4 _TintColor;
            float  _BlurSize;
            float  _TopGlow;
            fixed4 _EdgeColor;
            float  _EdgePower;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos     = UnityObjectToClipPos(v.vertex);
                o.uv      = v.uv;
                o.grabPos = ComputeGrabScreenPos(o.pos);
                o.color   = v.color;
                return o;
            }

            // 9탭 가우시안 근사 블러 (grab 텍스처를 스크린 UV로 샘플)
            fixed3 BlurredGrab(float4 grabPos)
            {
                float2 uv = grabPos.xy / grabPos.w;
                float2 px = _GlassGrabTex_TexelSize.xy * _BlurSize;

                fixed3 sum = fixed3(0,0,0);
                // 중심 + 8방향 (가중치 단순화)
                sum += tex2D(_GlassGrabTex, uv).rgb * 0.25;
                sum += tex2D(_GlassGrabTex, uv + float2( px.x,  0)).rgb * 0.125;
                sum += tex2D(_GlassGrabTex, uv + float2(-px.x,  0)).rgb * 0.125;
                sum += tex2D(_GlassGrabTex, uv + float2( 0,  px.y)).rgb * 0.125;
                sum += tex2D(_GlassGrabTex, uv + float2( 0, -px.y)).rgb * 0.125;
                sum += tex2D(_GlassGrabTex, uv + float2( px.x,  px.y)).rgb * 0.0625;
                sum += tex2D(_GlassGrabTex, uv + float2(-px.x,  px.y)).rgb * 0.0625;
                sum += tex2D(_GlassGrabTex, uv + float2( px.x, -px.y)).rgb * 0.0625;
                sum += tex2D(_GlassGrabTex, uv + float2(-px.x, -px.y)).rgb * 0.0625;
                return sum;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1) 뒤 화면 블러
                fixed3 glass = BlurredGrab(i.grabPos);

                // 2) 반투명 틴트 얹기
                glass = lerp(glass, _TintColor.rgb, _TintColor.a);

                // 3) 세로 그라데이션 — 위쪽 살짝 밝게 (유리 윗면 반사)
                glass += _TopGlow * i.uv.y;

                // 4) 가장자리 하이라이트 (UV 가장자리에 밝은 테)
                float edge = 0;
                edge = max(edge, smoothstep(_EdgePower, 0.0, i.uv.y));        // 아래
                edge = max(edge, smoothstep(1.0 - _EdgePower, 1.0, i.uv.y));  // 위
                edge = max(edge, smoothstep(_EdgePower, 0.0, i.uv.x));        // 좌
                edge = max(edge, smoothstep(1.0 - _EdgePower, 1.0, i.uv.x));  // 우
                glass += _EdgeColor.rgb * edge * _EdgeColor.a;

                // 5) 스프라이트 모양으로 마스킹 (둥근 모서리 등) + UI 정점 알파
                fixed maskAlpha = tex2D(_MainTex, i.uv).a * i.color.a;

                return fixed4(glass, maskAlpha);
            }
            ENDCG
        }
    }
}

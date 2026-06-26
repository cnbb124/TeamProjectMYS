// [UIMetal.shader]
// 빌트인 UI용 "금속 느낌" 셰이더 (패널 / 프레임 / 버튼 등).
// UI엔 실제 조명이 없으므로, 금속감은 다음 3요소로 흉내냄:
//   ① 세로 명암 그라데이션 (위 밝고 아래 어두운 원통형 반사)
//   ② 대각선 스페큘러 하이라이트 줄 (크롬 광택) — 정지 또는 천천히 흐름
//   ③ 브러시드 메탈 결 (한 방향 미세 줄무늬)
//
// [특징]
// - Image.color(정점 색)를 그대로 곱함 → 코드로 색 바꿔도 연동 유지
// - Canvas Mask/RectMask2D 안에서 정상 동작 (표준 UI 스텐실 포함)
// - GrabPass 안 씀 → 가벼움 (Overlay Canvas에서도 작동)
//
// [사용법]
// 1. Material 생성 → 셰이더 "UI/Metal" 지정
// 2. 패널/프레임 Image의 Material 슬롯에 연결
// 3. _BaseColor를 은색/금색 등 금속 색으로
//
// [파라미터]
// _BaseColor    : 금속 기본 색 (은=회색, 금=황색 등)
// _LightColor   : 위쪽 반사광 색 (보통 밝은 흰색)
// _ShadeStrength: 세로 명암 대비 (클수록 금속감 ↑)
// _StreakColor  : 스페큘러 줄 색
// _StreakPos    : 줄 위치 (0~1)
// _StreakWidth  : 줄 두께
// _StreakSpeed  : 줄 이동 속도 (0=정지)
// _BrushStrength: 브러시드 결 강도 (0=매끈한 금속)
// _BrushTiling  : 결 촘촘함

Shader "UI/Metal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseColor     ("Base Color", Color) = (0.7, 0.72, 0.78, 1)
        _LightColor    ("Light Color", Color) = (1, 1, 1, 1)
        _ShadeStrength ("Shade Strength", Range(0,1)) = 0.5
        _StreakColor   ("Streak Color", Color) = (1, 1, 1, 1)
        _StreakPos     ("Streak Pos", Range(0,1)) = 0.5
        _StreakWidth   ("Streak Width", Range(0.01,0.5)) = 0.12
        _StreakSpeed   ("Streak Speed", Range(0,2)) = 0.0
        _BrushStrength ("Brush Strength", Range(0,0.3)) = 0.05
        _BrushTiling   ("Brush Tiling", Range(1,400)) = 120

        // ── 표준 UI 스텐실 (Mask 지원) ──
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _BaseColor;
            fixed4    _LightColor;
            float     _ShadeStrength;
            fixed4    _StreakColor;
            float     _StreakPos;
            float     _StreakWidth;
            float     _StreakSpeed;
            float     _BrushStrength;
            float     _BrushTiling;
            float4    _ClipRect;

            // 한 줄 해시 (브러시드 결용)
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPos = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = TRANSFORM_TEX(v.uv, _MainTex);
                o.color  = v.color * _BaseColor; // Image.color × 금속 기본색
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed3 col = tex.rgb * i.color.rgb;

                // ① 세로 명암 그라데이션 (위 밝고 아래 어둡게) — 원통형 금속 반사
                float shade = lerp(1.0 - _ShadeStrength, 1.0 + _ShadeStrength * 0.5, i.uv.y);
                col *= shade;

                // ② 대각선 스페큘러 줄 (크롬 광택)
                float streakCoord = (i.uv.x + i.uv.y) * 0.5 + _Time.y * _StreakSpeed;
                float d = abs(frac(streakCoord) - _StreakPos);
                float streak = smoothstep(_StreakWidth, 0.0, d);
                col += _StreakColor.rgb * streak * 0.6;

                // ③ 브러시드 메탈 결 (세로 방향 미세 줄무늬)
                float brush = (hash11(floor(i.uv.x * _BrushTiling)) - 0.5) * _BrushStrength;
                col += brush;

                // 위쪽 반사광 살짝 가미
                col += _LightColor.rgb * pow(saturate(i.uv.y), 4.0) * 0.15;

                fixed alpha = tex.a * i.color.a;
                alpha *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}

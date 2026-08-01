// [UISoftGlow.shader]
// 빌트인 UI용 "부드러운 광택 + 발광" 셰이더 (게이지 바 / 패널 등).
// 평면적인 UI에 세로 광택(gloss)과 HDR 발광(glow)을 더해 촉촉하고 부드러운 느낌을 줌.
//
// [특징]
// - Image.color(정점 색)를 그대로 곱함 → HUDManager가 바꾸는 색(청록↔빨강) 연동 유지
// - 세로 그라데이션 광택: 위쪽이 살짝 밝게 (유리/젤 느낌)
// - HDR 발광(_Glow): Post Processing Bloom과 만나면 은은하게 번짐 (★Bloom 켜야 효과 삼)
// - Canvas Mask/RectMask2D 안에서도 정상 동작 (표준 UI 스텐실 포함)
// - OpenXR Single Pass Instanced 양안 렌더링 지원
//
// [사용법]
// 1. Material 생성 → 셰이더 "UI/SoftGlow" 지정
// 2. 게이지 Image(또는 패널)의 Material 슬롯에 연결
// 3. (게이지는 Image Type = Filled 그대로 사용 가능)
// 4. Bloom 켜면 발광이 살아남
//
// [파라미터]
// _GlossPower : 위쪽 광택 강도 (0이면 광택 없음)
// _GlossTint  : 광택 색 (보통 흰색)
// _Glow       : 발광 강도 (1=기본, 높을수록 Bloom에 밝게 번짐)
// _Softness   : 가장자리 알파 부드럽게 (0=또렷, 높을수록 흐릿한 경계)

Shader "UI/SoftGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color      ("Tint", Color) = (1,1,1,1)

        _GlossPower ("Gloss Power", Range(0,3)) = 1.0
        _GlossTint  ("Gloss Tint", Color) = (1,1,1,1)
        _Glow       ("Glow Intensity", Range(1,5)) = 1.6
        _Softness   ("Edge Softness", Range(0,0.5)) = 0.05

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
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _Color;
            fixed4    _GlossTint;
            float     _GlossPower;
            float     _Glow;
            float     _Softness;
            float4    _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPos = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = TRANSFORM_TEX(v.uv, _MainTex);
                o.color  = v.color * _Color; // Image.color(정점) × 머티리얼 틴트
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // 스프라이트 + UI 색
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                // 세로 광택 — 위쪽(uv.y=1) 살짝 밝게, 부드러운 곡선
                float gloss = pow(saturate(i.uv.y), 1.5) * _GlossPower;
                col.rgb += _GlossTint.rgb * gloss * 0.25;

                // 은은한 중앙 하이라이트 (가로 중앙이 살짝 밝게 → 젤 느낌)
                float centerGlow = (1.0 - abs(i.uv.y - 0.5) * 2.0) * 0.12;
                col.rgb += col.rgb * centerGlow;

                // HDR 발광 (Bloom 연동)
                col.rgb *= _Glow;

                // 가장자리 알파 부드럽게
                if (_Softness > 0.0)
                {
                    float edge = smoothstep(0.0, _Softness, i.uv.y)
                               * smoothstep(0.0, _Softness, 1.0 - i.uv.y);
                    col.a *= edge;
                }

                // UI 마스크(RectMask2D) 클립
                col.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);

                return col;
            }
            ENDCG
        }
    }
}

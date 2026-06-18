Shader "FX/Procedural Force Field"
{
    Properties
    {
        [Header(Mesh)]
        _MeshExpand("Mesh Expand", Range(1, 6)) = 1

        [Header(Field)]
        _BaseColor("Base Color", Color) = (0.10, 0.65, 1.00, 1)
        _RimColor("Rim Color", Color) = (0.55, 0.95, 1.00, 1)
        _Opacity("Opacity", Range(0, 1)) = 0.6
        _RimPower("Rim Power", Range(0.5, 10)) = 3.5
        _RimIntensity("Rim Intensity", Range(0, 10)) = 2.0

        [Header(Energy Bands)]
        _BandColor("Band Color", Color) = (0.65, 1.00, 1.00, 1)
        _BandTiling("Band Tiling", Range(0.1, 40)) = 10
        _BandSpeed("Band Speed", Range(-20, 20)) = 4
        _BandSharpness("Band Sharpness", Range(0.5, 12)) = 5
        _BandIntensity("Band Intensity", Range(0, 10)) = 1.6

        [Header(Noise)]
        _NoiseScale("Noise Scale", Range(0.25, 30)) = 6
        _NoiseSpeed("Noise Speed", Range(0, 10)) = 1.2
        _NoiseDistortion("Noise Distortion", Range(0, 0.35)) = 0.08

        [Header(Activation Reveal)]
        _RevealDuration("Reveal Duration", Range(0.05, 3)) = 0.45
        _RevealStart("Reveal Start", Range(0, 0.35)) = 0.06
        _RevealSoftness("Reveal Softness", Range(0.001, 0.35)) = 0.08
        _RevealEdgeColor("Reveal Edge Color", Color) = (0.85, 1.00, 1.00, 1)
        _RevealEdgeIntensity("Reveal Edge Intensity", Range(0, 20)) = 6.0
        _RevealEdgeAlpha("Reveal Edge Alpha", Range(0, 1)) = 0.25
        _RevealMaxDistance("Reveal Max Distance", Float) = 0.5

        [Header(Visibility)]
        _DefaultVisible("Default Visible", Range(0, 1)) = 1.0
        _ActivationReveal("Activation Reveal", Range(0, 1)) = 1.0
        _FieldVisibility("Field Visibility", Range(0, 1)) = 1.0

        [Header(Dissolve Fade)]
        _DissolveScale("Dissolve Scale", Range(0.1, 30)) = 7.0
        _DissolveWidth("Dissolve Width", Range(0.001, 0.35)) = 0.09
        _DissolveEdgeColor("Dissolve Edge Color", Color) = (0.65, 1.00, 1.00, 1)
        _DissolveEdgeIntensity("Dissolve Edge Intensity", Range(0, 20)) = 6.0
        _DissolveEdgeAlpha("Dissolve Edge Alpha", Range(0, 1)) = 0.25

        [Header(Impact)]
        _HitColor("Hit Color", Color) = (1.00, 1.00, 1.00, 1)
        _HitRingWidth("Hit Ring Width", Range(0, 1)) = 0.08
        _HitFalloff("Hit Falloff", Range(0.1, 12)) = 4.0
        _HitIntensity("Hit Intensity", Range(0, 20)) = 6.0
        _HitDuration("Hit Duration", Range(0.05, 3)) = 0.55

        [Header(Hit Shape)]
        _HexBlend("Hex Pattern Blend (0=원형 1=육각형)", Range(0, 1)) = 1.0
        _HexScale("Hex Scale", Range(0.1, 5)) = 1.0
        _RippleIntensity("Ripple Intensity (0=끔)", Range(0, 10)) = 3.0
        _RippleFreq("Ripple Frequency", Range(1, 20)) = 8.0
        _RippleSpeed("Ripple Speed", Range(1, 30)) = 15.0
        _RippleRadius("Ripple Radius", Range(0.5, 5)) = 2.0

        [Header(Deformation)]
        _HitPositionOS("Hit Position (Object)", Vector) = (0, 0, 0, 0)
        _BoundsExtentsOS("Bounds Extents (Object)", Vector) = (1, 1, 1, 0)
        _DeformStrength("Deform Strength", Range(0, 0.5)) = 0.12
        _DeformFrequency("Deform Frequency", Range(0.1, 40)) = 10
        _DeformDamping("Deform Damping", Range(0.1, 20)) = 7.5

        [Header(Impact Runtime(MPB))]
        _HitPosition("Hit Position (World)", Vector) = (0, 0, 0, 0)
        _HitTime("Hit Time", Float) = -9999
        _HitRadius("Hit Radius", Float) = 0.6
        _HitStrength("Hit Strength", Float) = 1.0
        _BoundsRadiusWS("Bounds Radius (World)", Float) = 1.0

        _HitPosition2("Hit Position 2 (World)", Vector) = (0, 0, 0, 0)
        _HitTime2("Hit Time 2", Float) = -9999
        _HitPositionOS2("Hit Position 2 (Object)", Vector) = (0, 0, 0, 0)

        _HitPosition3("Hit Position 3 (World)", Vector) = (0, 0, 0, 0)
        _HitTime3("Hit Time 3", Float) = -9999
        _HitPositionOS3("Hit Position 3 (Object)", Vector) = (0, 0, 0, 0)

        [Header(Team Color)]
        _ShieldTintColor("Shield Tint Color", Color) = (0.10, 0.65, 1.00, 1)
    }

        SubShader
        {
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
            Cull Back
            ZWrite Off
            Lighting Off
            Fog { Mode Off }
            Blend SrcAlpha OneMinusSrcAlpha

            CGINCLUDE
            #include "UnityCG.cginc"

            float _MeshExpand;

            fixed4 _BaseColor;
            fixed4 _RimColor;
            float _Opacity;
            float _RimPower;
            float _RimIntensity;

            fixed4 _BandColor;
            float _BandTiling;
            float _BandSpeed;
            float _BandSharpness;
            float _BandIntensity;

            float _NoiseScale;
            float _NoiseSpeed;
            float _NoiseDistortion;

            float _RevealDuration;
            float _RevealStart;
            float _RevealSoftness;
            float _RevealMaxDistance;
            fixed4 _RevealEdgeColor;
            float _RevealEdgeIntensity;
            float _RevealEdgeAlpha;

            float _DefaultVisible;
            float _ActivationReveal;
            float _FieldVisibility;

            float _DissolveScale;
            float _DissolveWidth;
            fixed4 _DissolveEdgeColor;
            float _DissolveEdgeIntensity;
            float _DissolveEdgeAlpha;

            fixed4 _HitColor;
            float _HitRingWidth;
            float _HitFalloff;
            float _HitIntensity;
            float _HitDuration;

            float _HexBlend;
            float _HexScale;
            float _RippleIntensity;
            float _RippleFreq;
            float _RippleSpeed;
            float _RippleRadius;

            float4 _HitPositionOS;
            float4 _BoundsExtentsOS;
            float _DeformStrength;
            float _DeformFrequency;
            float _DeformDamping;

            float4 _HitPosition;
            float _HitTime;
            float _HitRadius;
            float _HitStrength;
            float _BoundsRadiusWS;

            float4 _HitPosition2;
            float  _HitTime2;
            float4 _HitPositionOS2;

            float4 _HitPosition3;
            float  _HitTime3;
            float4 _HitPositionOS3;

            fixed4 _ShieldTintColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldN : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                float3 posOS : TEXCOORD4;
                float hitAge : TEXCOORD5;
                float hitMask : TEXCOORD6;
                float hitAge2 : TEXCOORD7;
                float hitMask2 : TEXCOORD8;
                float hitAge3 : TEXCOORD9;
                float hitMask3 : TEXCOORD10;
            };

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float Noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i + float2(0.0, 0.0));
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm2(float2 p)
            {
                float v = 0.0;
                float a = 0.5;

                for (int i = 0; i < 4; i++)
                {
                    v += Noise2(p) * a;
                    p *= 2.0;
                    a *= 0.5;
                }

                return v;
            }

            // 육각형 거리 함수 (2D)
            float HexDist(float2 p)
            {
                p = abs(p);
                return max(dot(p, normalize(float2(1.0, 1.732))), p.x);
            }

            // 피격 지점 기준 육각형/원형 블렌드 거리
            float HitShapeDist(float2 delta)
            {
                float2 scaled = delta * _HexScale;
                float circleDist = length(scaled);
                float hexDist    = HexDist(scaled);
                return lerp(circleDist, hexDist, _HexBlend);
            }

            // 피격 지점에서 십자/방사형 버스트
            float RadialBurst(float2 delta, float hitAge, float env)
            {
                float dist  = length(delta);
                if (dist < 0.0001) return 0.0;

                // 방사형 라인 (RippleFreq = 라인 수 × 2)
                float angle  = atan2(delta.y, delta.x);
                float rays   = abs(sin(angle * _RippleFreq));
                rays         = pow(rays, max(8.0 / max(_RippleFreq, 1.0), 1.0));

                // 피격 직후 바깥으로 퍼지며 사라짐
                float expand = saturate(hitAge * _RippleSpeed * 0.3);
                float ring   = 1.0 - abs(dist - expand * _RippleRadius) / max(_RippleRadius * 0.3, 0.001);
                ring         = saturate(ring);

                float falloff = saturate(1.0 - dist / max(_RippleRadius, 0.001));
                return rays * ring * falloff * env * _RippleIntensity;
            }

            float HitEnvelope(float hitAge)
            {
                float d = max(_HitDuration, 0.0001);
                float x = saturate(hitAge / d);
                float env = (1.0 - x);
                env *= env;
                return env;
            }

            float Ring01(float x, float center, float width)
            {
                float hw = width * 0.5;
                float a = smoothstep(center - hw, center, x);
                float b = 1.0 - smoothstep(center, center + hw, x);
                return saturate(a * b);
            }

            float ActivationMask(float3 worldPos, float3 hitPos, float hitAge, out float edgeWave)
            {
                float hasHit = step(-1000.0, _HitTime);

                float dur = max(_RevealDuration, 0.0001);
                float p = saturate(hitAge / dur);
                float fade = 1.0 - smoothstep(0.7, 1.0, p);

                // 충돌 지점에서의 거리
                float dist = distance(worldPos, hitPos);
                float maxR = max(_RevealMaxDistance, 0.0001);

                // 반경 안에만 보이고 퍼지지 않음
                float reveal = step(dist, maxR) * fade;

                float edge = smoothstep(maxR * 0.8, maxR, dist) * (1.0 - step(dist, maxR * 0.8));
                edgeWave = edge * fade * hasHit;

                return saturate(reveal * hasHit);
            }

            v2f vert(appdata v)
            {
                v2f o;

                float3 posOSRaw = v.vertex.xyz;

                float4 localPos = v.vertex;
                localPos.xyz *= max(_MeshExpand, 1.0);

                float4 worldPos4 = mul(unity_ObjectToWorld, localPos);
                float3 worldPos = worldPos4.xyz;

                float3 worldN = UnityObjectToWorldNormal(v.normal);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);

                float hitAge = _Time.y - _HitTime;
                float env = HitEnvelope(hitAge);

                float3 ext = max(_BoundsExtentsOS.xyz, float3(0.0001, 0.0001, 0.0001));
                float3 q = (posOSRaw - _HitPositionOS.xyz) / ext;
                float distN = length(q);

                float radiusN = max(_HitRadius, 0.0001);
                float hitMask = saturate(1.0 - distN / radiusN);
                hitMask = pow(hitMask, max(_HitFalloff, 0.0001));

                // Hit slot 2
                float hitAge2  = _Time.y - _HitTime2;
                float3 q2      = (posOSRaw - _HitPositionOS2.xyz) / ext;
                float distN2   = length(q2);
                float hitMask2 = pow(saturate(1.0 - distN2 / radiusN), max(_HitFalloff, 0.0001));

                // Hit slot 3
                float hitAge3  = _Time.y - _HitTime3;
                float3 q3      = (posOSRaw - _HitPositionOS3.xyz) / ext;
                float distN3   = length(q3);
                float hitMask3 = pow(saturate(1.0 - distN3 / radiusN), max(_HitFalloff, 0.0001));

                float2 np = worldPos.xz * _NoiseScale + _Time.y * _NoiseSpeed;
                float n = Fbm2(np);
                float wobble = (n - 0.5) * 2.0;

                float wave = sin(distN * _DeformFrequency - hitAge * 14.0 + wobble * 2.0);
                float damping = exp(-hitAge * _DeformDamping);
                float deform = wave * _DeformStrength * damping * env * hitMask * _HitStrength;

                float wave2   = sin(distN2 * _DeformFrequency - hitAge2 * 14.0 + wobble * 2.0);
                float damping2 = exp(-hitAge2 * _DeformDamping);
                deform += wave2 * _DeformStrength * damping2 * HitEnvelope(hitAge2) * hitMask2 * _HitStrength;

                float wave3   = sin(distN3 * _DeformFrequency - hitAge3 * 14.0 + wobble * 2.0);
                float damping3 = exp(-hitAge3 * _DeformDamping);
                deform += wave3 * _DeformStrength * damping3 * HitEnvelope(hitAge3) * hitMask3 * _HitStrength;

                float3 deformedWorldPos = worldPos + worldN * deform;

                o.worldPos = deformedWorldPos;
                o.worldN = worldN;
                o.viewDir = viewDir;
                o.uv = v.uv;

                o.posOS = posOSRaw;
                o.hitAge  = hitAge;
                o.hitMask = hitMask;
                o.hitAge2  = hitAge2;
                o.hitMask2 = hitMask2;
                o.hitAge3  = hitAge3;
                o.hitMask3 = hitMask3;

                o.pos = UnityWorldToClipPos(deformedWorldPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;

                float3 N = normalize(i.worldN);
                float3 V = normalize(i.viewDir);

                float fresnel = pow(1.0 - saturate(dot(N, V)), max(_RimPower, 0.0001));
                float3 rim = _RimColor.rgb * fresnel * _RimIntensity;

                float2 flowUv = i.worldPos.xz;
                float2 nUv = flowUv * _NoiseScale + t * _NoiseSpeed;
                float n = Fbm2(nUv);

                float2 distort = (float2(n, Fbm2(nUv + 19.31)) - 0.5) * _NoiseDistortion;
                float bandsPhase = (flowUv.y + distort.y) * _BandTiling + t * _BandSpeed + n * 2.5;
                float bandsRaw = 0.5 + 0.5 * sin(bandsPhase * 6.2831853);
                float bands = pow(saturate(bandsRaw), max(_BandSharpness, 0.0001));
                float3 bandCol = _BandColor.rgb * bands * _BandIntensity;

                float hitAge = i.hitAge;
                float env = HitEnvelope(hitAge);

                float3 ext = max(_BoundsExtentsOS.xyz, float3(0.0001, 0.0001, 0.0001));
                float radiusN = max(_HitRadius, 0.0001);

                // Hit slot 1
                float2 delta1 = (i.posOS.xz - _HitPositionOS.xz) / ext.xz;
                float  sd1    = HitShapeDist(delta1) / radiusN;
                float  d01    = saturate(sd1);
                float ring1   = Ring01(d01, 0.55, max(_HitRingWidth, 0.0001));
                float ripple1 = RadialBurst(delta1, hitAge, env);
                float hit1    = ring1 * 2.0 * env * i.hitMask + ripple1 * i.hitMask;

                // Hit slot 2
                float2 delta2 = (i.posOS.xz - _HitPositionOS2.xz) / ext.xz;
                float  sd2    = HitShapeDist(delta2) / radiusN;
                float  d02    = saturate(sd2);
                float  env2   = HitEnvelope(i.hitAge2);
                float ring2   = Ring01(d02, 0.55, max(_HitRingWidth, 0.0001));
                float ripple2 = RadialBurst(delta2, i.hitAge2, env2);
                float hit2    = ring2 * 2.0 * env2 * i.hitMask2 + ripple2 * i.hitMask2;

                // Hit slot 3
                float2 delta3 = (i.posOS.xz - _HitPositionOS3.xz) / ext.xz;
                float  sd3    = HitShapeDist(delta3) / radiusN;
                float  d03    = saturate(sd3);
                float  env3   = HitEnvelope(i.hitAge3);
                float ring3   = Ring01(d03, 0.55, max(_HitRingWidth, 0.0001));
                float ripple3 = RadialBurst(delta3, i.hitAge3, env3);
                float hit3    = ring3 * 2.0 * env3 * i.hitMask3 + ripple3 * i.hitMask3;

                float hit = saturate(max(hit1, max(hit2, hit3)));
                float3 hitCol = _HitColor.rgb * _ShieldTintColor.rgb * hit * _HitIntensity * _HitStrength;

                // 3개 슬롯 reveal 합산 — 각 피격 지점마다 독립적으로 쉴드 표시
                float edgeWave, edgeWave2, edgeWave3;
                float reveal1 = ActivationMask(i.worldPos, _HitPosition.xyz,  hitAge,    edgeWave);
                float reveal2 = ActivationMask(i.worldPos, _HitPosition2.xyz, i.hitAge2, edgeWave2);
                float reveal3 = ActivationMask(i.worldPos, _HitPosition3.xyz, i.hitAge3, edgeWave3);

                float hasHit = step(-1000.0, _HitTime);
                float reveal = saturate(_DefaultVisible);

                if (_ActivationReveal > 0.5 && hasHit > 0.5)
                {
                    float revealMask = saturate(reveal1 + reveal2 + reveal3);
                    if (_DefaultVisible > 0.5)
                        revealMask = 1.0;
                    reveal = revealMask;
                }

                float3 edgeCol = _RevealEdgeColor.rgb * saturate(edgeWave + edgeWave2 + edgeWave3) * _RevealEdgeIntensity;

                float visibility = saturate(_FieldVisibility);

                float2 dUv = i.worldPos.xz * _DissolveScale + t * 0.35;
                float dissolveN = Fbm2(dUv);

                float width = max(_DissolveWidth, 0.0001);
                float raw = 1.0 - smoothstep(visibility, min(visibility + width, 1.0), dissolveN);
                float fadeMask = visibility * raw;

                float edge = smoothstep(max(visibility - width, 0.0), visibility, dissolveN) * (1.0 - smoothstep(visibility, min(visibility + width, 1.0), dissolveN));
                edge *= (1.0 - visibility);

                float3 tint    = _ShieldTintColor.rgb;
                float3 baseCol = _BaseColor.rgb * tint * (0.55 + 0.45 * n);
                float3 tintedRim = rim * tint;
                float3 fieldCol = baseCol + bandCol * tint + tintedRim + hitCol + edgeCol;

                float totalMask = reveal;  // fadeMask 제거 - reveal만으로 충돌 지점 표시

                float3 dissolveEdgeCol = _DissolveEdgeColor.rgb * edge * _DissolveEdgeIntensity * visibility;
                float3 col = fieldCol * totalMask + dissolveEdgeCol * reveal;

                float alphaBase = saturate(_Opacity * (0.4 + 0.6 * fresnel) + hit * 0.3);
                float alpha = saturate(alphaBase * totalMask + edge * _DissolveEdgeAlpha * _Opacity * reveal * visibility);

                return fixed4(saturate(col), alpha);
            }
            ENDCG

            Pass
            {
                CGPROGRAM
                #pragma target 3.0
                #pragma vertex vert
                #pragma fragment frag
                ENDCG
            }
        }

            FallBack Off
}

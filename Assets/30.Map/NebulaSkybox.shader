Shader "Custom/NebulaSkybox"
{
    Properties
    {
        _NebulaTexture1 ("Nebula Texture 1", 2D) = "black" {}
        _NebulaColor1 ("Nebula Color 1", Color) = (0.5, 0, 1, 1)
        _NebulaTexture2 ("Nebula Texture 2", 2D) = "black" {}
        _NebulaColor2 ("Nebula Color 2", Color) = (1, 0.2, 0.2, 1)
        _NebulaTexture3 ("Nebula Texture 3", 2D) = "black" {}
        _NebulaColor3 ("Nebula Color 3", Color) = (0, 0.3, 1, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.5
        _StarDensity ("Star Density", Range(0, 500)) = 500
        _StarBrightness ("Star Brightness", Range(0, 2)) = 2.0

        _Sun1Position ("Sun1 Position", Vector) = (0.5, -0.3, 0.8, 0)
        _Sun1Color ("Sun1 Color", Color) = (1, 0.4, 0.1, 1)
        _Sun1Size ("Sun1 Size", Range(0, 0.1)) = 0.0002
        _Sun1Glow ("Sun1 Glow", Range(0, 0.5)) = 0.0003

        _Sun2Position ("Sun2 Position", Vector) = (-0.8, 0.3, 0.5, 0)
        _Sun2Color ("Sun2 Color", Color) = (0.3, 0.5, 1, 1)
        _Sun2Size ("Sun2 Size", Range(0, 0.1)) = 0.0001
        _Sun2Glow ("Sun2 Glow", Range(0, 0.5)) = 0.0002

        _Sun3Position ("Sun3 Position", Vector) = (0.2, 0.6, -0.7, 0)
        _Sun3Color ("Sun3 Color", Color) = (0.5, 0.2, 1, 1)
        _Sun3Size ("Sun3 Size", Range(0, 0.1)) = 0.0003
        _Sun3Glow ("Sun3 Glow", Range(0, 0.5)) = 0.0005

        _Sun4Position ("Sun4 Position", Vector) = (-0.3, -0.7, -0.6, 0)
        _Sun4Color ("Sun4 Color", Color) = (1, 0.8, 0.2, 1)
        _Sun4Size ("Sun4 Size", Range(0, 0.1)) = 0.0001
        _Sun4Glow ("Sun4 Glow", Range(0, 0.5)) = 0.0002

        _Sun5Position ("Sun5 Position", Vector) = (0.9, 0.1, -0.4, 0)
        _Sun5Color ("Sun5 Color", Color) = (0.8, 0.1, 0.1, 1)
        _Sun5Size ("Sun5 Size", Range(0, 0.1)) = 0.0002
        _Sun5Glow ("Sun5 Glow", Range(0, 0.5)) = 0.0004

        _Sun6Position ("Sun6 Position", Vector) = (-0.5, 0.5, 0.7, 0)
        _Sun6Color ("Sun6 Color", Color) = (0.2, 1, 0.8, 1)
        _Sun6Size ("Sun6 Size", Range(0, 0.1)) = 0.0001
        _Sun6Glow ("Sun6 Glow", Range(0, 0.5)) = 0.0002

        _Sun7Position ("Sun7 Position", Vector) = (0.7, -0.6, 0.3, 0)
        _Sun7Color ("Sun7 Color", Color) = (1, 0.5, 0.8, 1)
        _Sun7Size ("Sun7 Size", Range(0, 0.1)) = 0.0002
        _Sun7Glow ("Sun7 Glow", Range(0, 0.5)) = 0.0003

        _Sun8Position ("Sun8 Position", Vector) = (-0.2, -0.4, -0.9, 0)
        _Sun8Color ("Sun8 Color", Color) = (0.6, 0.8, 1, 1)
        _Sun8Size ("Sun8 Size", Range(0, 0.1)) = 0.0001
        _Sun8Glow ("Sun8 Glow", Range(0, 0.5)) = 0.0002

        _Sun9Position ("Sun9 Position", Vector) = (0.4, 0.8, -0.4, 0)
        _Sun9Color ("Sun9 Color", Color) = (1, 0.9, 0.5, 1)
        _Sun9Size ("Sun9 Size", Range(0, 0.1)) = 0.0002
        _Sun9Glow ("Sun9 Glow", Range(0, 0.5)) = 0.0003

        _Sun10Position ("Sun10 Position", Vector) = (-0.6, -0.2, 0.8, 0)
        _Sun10Color ("Sun10 Color", Color) = (0.4, 0.6, 1, 1)
        _Sun10Size ("Sun10 Size", Range(0, 0.1)) = 0.0001
        _Sun10Glow ("Sun10 Glow", Range(0, 0.5)) = 0.0002
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" }
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _NebulaTexture1, _NebulaTexture2, _NebulaTexture3;
            float4 _NebulaColor1, _NebulaColor2, _NebulaColor3;
            float _Intensity;
            float _StarDensity;
            float _StarBrightness;

            float4 _Sun1Position; float4 _Sun1Color; float _Sun1Size; float _Sun1Glow;
            float4 _Sun2Position; float4 _Sun2Color; float _Sun2Size; float _Sun2Glow;
            float4 _Sun3Position; float4 _Sun3Color; float _Sun3Size; float _Sun3Glow;
            float4 _Sun4Position; float4 _Sun4Color; float _Sun4Size; float _Sun4Glow;
            float4 _Sun5Position; float4 _Sun5Color; float _Sun5Size; float _Sun5Glow;
            float4 _Sun6Position; float4 _Sun6Color; float _Sun6Size; float _Sun6Glow;
            float4 _Sun7Position; float4 _Sun7Color; float _Sun7Size; float _Sun7Glow;
            float4 _Sun8Position; float4 _Sun8Color; float _Sun8Size; float _Sun8Glow;
            float4 _Sun9Position; float4 _Sun9Color; float _Sun9Size; float _Sun9Glow;
            float4 _Sun10Position; float4 _Sun10Color; float _Sun10Size; float _Sun10Glow;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 texcoord : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.vertex.xyz;
                return o;
            }

            float hash(float3 p)
            {
                p = frac(p * float3(443.8975, 397.2973, 491.1871));
                p += dot(p.zxy, p.yxz + 19.19);
                return frac(p.x * p.y * p.z);
            }

            float stars(float3 dir, float density)
            {
                float3 p = dir * density;
                float3 cell = floor(p);
                float h = hash(cell);
                float size = 0.02 + h * h * 0.07;
                float dist = length(frac(p) - 0.5);
                float brightness = hash(cell + 0.7);
                brightness = brightness * brightness;
                return smoothstep(size + 0.01, size, dist) * brightness;
            }

            fixed3 calcSun(float3 dir, float4 pos, float4 col, float size, float glow)
            {
                float3 sunDir = normalize(pos.xyz);
                float sunDot = dot(dir, sunDir);
                float sun = smoothstep(1.0 - size, 1.0, sunDot);
                float g = smoothstep(1.0 - size - glow, 1.0 - size, sunDot) * 0.3;
                return col.rgb * sun + col.rgb * g;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.texcoord);

                float2 uv1 = float2(atan2(dir.z, dir.x) / (2 * 3.14159) + 0.5, dir.y * 0.5 + 0.5);
                float2 uv2 = float2(atan2(dir.x, dir.y) / (2 * 3.14159) + 0.5, dir.z * 0.5 + 0.5);
                float2 uv3 = float2(atan2(dir.y, dir.z) / (2 * 3.14159) + 0.5, dir.x * 0.5 + 0.5);

                fixed4 n1 = tex2D(_NebulaTexture1, uv1) * _NebulaColor1;
                fixed4 n2 = tex2D(_NebulaTexture2, uv2) * _NebulaColor2;
                fixed4 n3 = tex2D(_NebulaTexture3, uv3) * _NebulaColor3;
                fixed4 nebula = (n1 + n2 + n3) * _Intensity;

                float s = stars(dir, _StarDensity) * _StarBrightness;

                fixed3 suns =
                    calcSun(dir, _Sun1Position, _Sun1Color, _Sun1Size, _Sun1Glow) +
                    calcSun(dir, _Sun2Position, _Sun2Color, _Sun2Size, _Sun2Glow) +
                    calcSun(dir, _Sun3Position, _Sun3Color, _Sun3Size, _Sun3Glow) +
                    calcSun(dir, _Sun4Position, _Sun4Color, _Sun4Size, _Sun4Glow) +
                    calcSun(dir, _Sun5Position, _Sun5Color, _Sun5Size, _Sun5Glow) +
                    calcSun(dir, _Sun6Position, _Sun6Color, _Sun6Size, _Sun6Glow) +
                    calcSun(dir, _Sun7Position, _Sun7Color, _Sun7Size, _Sun7Glow) +
                    calcSun(dir, _Sun8Position, _Sun8Color, _Sun8Size, _Sun8Glow) +
                    calcSun(dir, _Sun9Position, _Sun9Color, _Sun9Size, _Sun9Glow) +
                    calcSun(dir, _Sun10Position, _Sun10Color, _Sun10Size, _Sun10Glow);

                return fixed4(nebula.rgb + s + suns, 1);
            }
            ENDCG
        }
    }
}
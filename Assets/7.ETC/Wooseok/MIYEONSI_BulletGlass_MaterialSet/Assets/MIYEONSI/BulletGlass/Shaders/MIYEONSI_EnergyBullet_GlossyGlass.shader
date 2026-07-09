Shader "MIYEONSI/EnergyBullet/GlossyGlass"
{
    Properties
    {
        _Color ("Tint RGB / Alpha", Color) = (1, 0.07, 0.14, 0.36)
        _MainTex ("Albedo + Alpha", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _RoughnessMap ("Roughness Map", 2D) = "black" {}
        _EmissionMap ("Emission Map", 2D) = "black" {}
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 2.7
        _Smoothness ("Glass Smoothness", Range(0, 1)) = 0.94
        _AlphaBoost ("Alpha Boost", Range(0, 3)) = 1.15
        _FresnelColor ("Fresnel Color", Color) = (1, 0.18, 0.38, 1)
        _FresnelPower ("Fresnel Power", Range(0.2, 8)) = 2.1
        _FresnelIntensity ("Fresnel Emission", Range(0, 12)) = 3.4
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.65
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 300
        ZWrite Off
        Cull Back

        CGPROGRAM
        #pragma surface surf Standard alpha:fade keepalpha
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _RoughnessMap;
        sampler2D _EmissionMap;

        half4 _Color;
        half4 _FresnelColor;
        half _EmissionIntensity;
        half _Smoothness;
        half _AlphaBoost;
        half _FresnelPower;
        half _FresnelIntensity;
        half _NormalStrength;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedoAlpha = tex2D(_MainTex, IN.uv_MainTex);
            fixed3 unpacked = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            unpacked.xy *= _NormalStrength;
            o.Normal = normalize(unpacked);

            fixed rough = tex2D(_RoughnessMap, IN.uv_MainTex).r;
            fixed3 emissionTex = tex2D(_EmissionMap, IN.uv_MainTex).rgb;

            half fresnel = pow(1.0h - saturate(dot(normalize(IN.viewDir), o.Normal)), _FresnelPower);

            o.Albedo = albedoAlpha.rgb * _Color.rgb;
            o.Metallic = 0;
            o.Smoothness = saturate(_Smoothness * (1.0h - rough * 0.55h));
            o.Emission = emissionTex * _EmissionIntensity + _FresnelColor.rgb * fresnel * _FresnelIntensity;
            o.Alpha = saturate(albedoAlpha.a * _Color.a * _AlphaBoost + fresnel * 0.42h);
        }
        ENDCG
    }

    FallBack "Transparent/Diffuse"
}

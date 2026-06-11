  Shader "FleetRemake/Ship" {
    Properties {
      _MainTex ("Texture", 2D) = "white" {}
      _BumpMap ("Bumpmap", 2D) = "bump" {}
	  _EnginePower ("Engine Power", range(0,1)) = 1
	  _Color ("Ident Colour", Color) = (1,0,0)
    }

    SubShader {
      Tags { "RenderType" = "Opaque" }
      CGPROGRAM
      #pragma surface surf SimpleSpecular

	  sampler2D _MainTex;
      sampler2D _BumpMap;
	  float _EnginePower;
	  float4 _Color;

      half4 LightingSimpleSpecular (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten) {
          half3 h = normalize (lightDir + viewDir);

          half diff = max (0, dot (s.Normal, lightDir));

          float nh = max (0, dot (s.Normal, h));
          float spec = pow (nh, 8);

          half4 c;
          c.rgb = (s.Albedo * _LightColor0.rgb + _LightColor0.rgb * diff * spec) * (atten);
          c.a = s.Alpha;
          return c;
      }

      struct Input {
        float2 uv_MainTex;
        float2 uv_BumpMap;
		
      };


      void surf (Input IN, inout SurfaceOutput o) {
        o.Albedo = tex2D (_MainTex, IN.uv_MainTex).g;
        o.Normal = UnpackNormal (tex2D (_BumpMap, IN.uv_BumpMap));
		o.Emission = tex2D (_MainTex, IN.uv_MainTex).a + ((tex2D (_MainTex, IN.uv_MainTex).r + (tex2D (_MainTex, IN.uv_MainTex).b * 10 * _EnginePower)) * _Color);
      }
      ENDCG
    } 
    Fallback "Diffuse"
  }
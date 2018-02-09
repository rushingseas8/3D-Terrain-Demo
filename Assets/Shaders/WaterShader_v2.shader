Shader "Custom/WaterShader_v2"
{
	Properties
	{
		_Color ("Color", Color) = (1,1,1,1)
		_Color2 ("Color 2", Color) = (0,0,0,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_WaveHeight ("Wave Height", Float) = 1
		_WaveFreq ("Wave Frequency", Float) = 1
		_DeltaX ("Wave X Delta", Float) = 1
		_DeltaY ("Wave Y Delta", Float) = 0
		_DeltaZ ("Wave Z Delta", Float) = 0
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		LOD 100

		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows vertex:vert alpha:fade
		#pragma target 3.0

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
		};

		half _Glossiness;
		fixed4 _Color, _Color2;
		float _WaveHeight, _WaveFreq, _DeltaX, _DeltaY, _DeltaZ;

		void vert(inout appdata_full v)
		{
			v.vertex.xyz += v.normal.xyz * _WaveHeight * sin(_WaveFreq * (_Time[1] + (_DeltaX * v.texcoord.x) + (_DeltaY * v.texcoord.y) + (_DeltaZ * v.texcoord.z)));
		}

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * lerp(_Color, _Color2, 0.5 * sin(_Time[1]) + 0.5);
			o.Albedo = c.rgb;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}

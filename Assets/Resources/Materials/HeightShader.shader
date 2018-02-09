// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/HeightShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Low_Color ("Low color", Color) = (0,0,1,0)
		_High_Color ("High color", Color) = (1,0,0,0)
		_Scale_Height ("Max height", Float) = 1
		_Center ("Center", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct input_data
			{
				float4 pos : SV_POSITION;
				float4 col : TEXCOORD0;
			};

			fixed4 _Low_Color;
			fixed4 _High_Color;
			float _Scale_Height;
			float _Center;
			
			input_data vert (float4 vertexPos : POSITION)
			{
				input_data o;

				o.pos = UnityObjectToClipPos (vertexPos);
				o.col = mul(unity_ObjectToWorld, vertexPos);
				//o.col = lerp(_Low_Color, _High_Color, ((o.pos.y + (_Center * 2.0)) / _Scale_Height / 2.0) + 0.5);

				return o;
			}
			
			fixed4 frag (input_data i) : SV_Target
			{
				//return lerp(_Low_Color, _High_Color, ((i.col + (_Center * 2.0)) / _Scale_Height / 2.0) + 0.5);
				//return i.col;

				//return lerp(_Low_Color, _High_Color, (i.col.y - (_Center / 2.0)) / _Scale_Height);
				return lerp(_Low_Color, _High_Color, (i.col.y + (_Scale_Height / 2.0) + _Center) / _Scale_Height);
			}
			ENDCG
		}
	}
}

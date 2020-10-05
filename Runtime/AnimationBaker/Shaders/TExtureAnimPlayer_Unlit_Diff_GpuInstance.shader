// Wrote by Joe Rozek
// Commercial use -- yes
// Modification -- yes
// Distribution -- yes
// Private use -- yes
// YusufuCote@gmail.com

Shader "Unlit/TExtureAnimPlayer_Unlit_Diff_GpuInstance"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_PosTex("position texture", 2D) = "black"{}
		_NmlTex("normal texture", 2D) = "white"{}
		_DT("delta time", float) = 0
		_Length("animation length", Float) = 1
		[Toggle(ANIM_LOOP)] _Loop("loop", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
			#include "TextureAnimatorPlayerInstanced.cginc"
			#pragma multi_compile ___ ANIM_LOOP
			
			#pragma target 3.0

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				// float2 uv1 : TEXCOORD1;
				// float2 uv2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float3 normal : TEXCOORD1;

				// UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			v2f vert (appdata v, uint vid : SV_VertexID)
			{
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v);
				// UNITY_TRANSFER_INSTANCE_ID(v, o);
				float3 normal;
				float4 newVertexPos = TransFormVertex(vid, normal);
				o.vertex =  UnityObjectToClipPos(newVertexPos);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.normal = normal;

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				// UNITY_SETUP_INSTANCE_ID(i);
				half diff = dot(i.normal, float3(0,1,0))*0.5 + 0.5;
				half4 col = tex2D(_MainTex, i.uv);
				return diff * col;
			}
			ENDCG
		}
	}
	FallBack "Unlit/TExtureAnimPlayer_Unlit_Diff"
}

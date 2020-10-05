// Wrote by Joe Rozek
// Commercial use -- yes
// Modification -- yes
// Distribution -- yes
// Private use -- yes
// YusufuCote@gmail.com

Shader "Unlit/TExtureAnimPlayer_Unlit_Diff_GpuInstance FadeBetween"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_PosTex("position texture", 2D) = "black"{}
		_NmlTex("normal texture", 2D) = "white"{}
		_PosTex2("position texture", 2D) = "black"{}
		_NmlTex2("normal texture", 2D) = "white"{}
		_DT("delta time", float) = 0
		_Length("animation length", Float) = 1
		_Length2("animation length", Float) = 1
		_Fade ("Transition", Range(0,1)) = 0
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
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float3 normal : TEXCOORD1;

				// UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			sampler2D _MainTex, _PosTex2, _NmlTex2;
			float4 _MainTex_ST;
			float _Fade;
			UNITY_INSTANCING_BUFFER_START(Props3)
			UNITY_DEFINE_INSTANCED_PROP(float4, _PosTex2_TexelSize)
			UNITY_DEFINE_INSTANCED_PROP(float,_Length2)
			UNITY_INSTANCING_BUFFER_END(Props3)

			// In vertex Shader!
			float4 TransFormVertex2( uint vid : SV_VertexID, float time, out float3 normal)
			{		
				float x = (vid + 0.5) * UNITY_ACCESS_INSTANCED_PROP(Props3, _PosTex2_TexelSize.x);
				float y = time;
				float4 pos = tex2Dlod(_PosTex, float4(x, y, 0, 0));
				normal = tex2Dlod(_NmlTex, float4(x, y, 0, 0));
				normal = UnityObjectToWorldNormal(normal);
				return pos;
				// Don't forget to use
				// o.vertex = UnityObjectToClipPos(pos);
			}

			// In vertex Shader!
			float4 TransFormVertex1( uint vid : SV_VertexID, float t, out float3 normal)
			{		
				float x = (vid + 0.5) * UNITY_ACCESS_INSTANCED_PROP(Props2, _PosTex_TexelSize.x);
				float y = t;
				float4 pos = tex2Dlod(_PosTex2, float4(x, y, 0, 0));
				normal = tex2Dlod(_NmlTex2, float4(x, y, 0, 0));
				normal = UnityObjectToWorldNormal(normal);
				return pos;
				// Don't forget to use
				// o.vertex = UnityObjectToClipPos(pos);
			}

			v2f vert (appdata v, uint vid : SV_VertexID)
			{
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v);
				// UNITY_TRANSFER_INSTANCE_ID(v, o);
				float currentTIme = (_Time.y - UNITY_ACCESS_INSTANCED_PROP(Props2, _DT));

				float t1 = currentTIme / UNITY_ACCESS_INSTANCED_PROP(Props2, _Length);
				#if ANIM_LOOP
					t1 = fmod(t1, 1.0);
				#else
					t1 = saturate(t1);
				#endif	
				float t2 = currentTIme / UNITY_ACCESS_INSTANCED_PROP(Props3, _Length2);
				#if ANIM_LOOP
					t2 = fmod(t2, 1.0);
				#else
					t2 = saturate(t2);
				#endif	

				float3 normal1;
				float4 newVertexPos1 = TransFormVertex1(vid,t1, normal1);

				float3 normal2;
				float4 newVertexPos2 = TransFormVertex2(vid,t2, normal2);

				newVertexPos1 = lerp(newVertexPos1, newVertexPos2, _Fade);
				normal1 = lerp(normal1, normal2, _Fade);

				o.vertex =  UnityObjectToClipPos(newVertexPos1);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.normal = normal1;

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

#ifndef ANIMATION_BAKER_Instanced
    #define ANIMATION_BAKER_Instanced 1

    // Properties
    // {
        //     _MainTex ("Texture", 2D) = "white" {}
        //     _PosTex("position texture", 2D) = "black"{}
        //     _DT("delta time", float) = 0
        //     _Length("animation length", Float) = 1
        //     [Toggle(ANIM_LOOP)] _Loop("loop", Float) = 0
    // }

    // #pragma multi_compile ___ ANIM_LOOP

    sampler2D _PosTex, _NmlTex;

    UNITY_INSTANCING_BUFFER_START(Props2)
    UNITY_DEFINE_INSTANCED_PROP(float4, _PosTex_TexelSize)
    UNITY_DEFINE_INSTANCED_PROP(float,_Length)
    UNITY_DEFINE_INSTANCED_PROP(float, _DT)
    UNITY_INSTANCING_BUFFER_END(Props2)

    // v2f vert (appdata v, uint vid : SV_VertexID)
    
    // In vertex Shader!
    float4 TransFormVertex( uint vid : SV_VertexID, out float3 normal)
    {
        float t = (_Time.y - UNITY_ACCESS_INSTANCED_PROP(Props2, _DT)) / UNITY_ACCESS_INSTANCED_PROP(Props2, _Length);
        #if ANIM_LOOP
            t = fmod(t, 1.0);
        #else
            t = saturate(t);
        #endif			
        float x = (vid + 0.5) * UNITY_ACCESS_INSTANCED_PROP(Props2, _PosTex_TexelSize.x);
        float y = t;
        float4 pos = tex2Dlod(_PosTex, float4(x, y, 0, 0));
        normal = tex2Dlod(_NmlTex, float4(x, y, 0, 0));
        normal = UnityObjectToWorldNormal(normal);
        return pos;
        // Don't forget to use
        // o.vertex = UnityObjectToClipPos(pos);
    }
    
#endif
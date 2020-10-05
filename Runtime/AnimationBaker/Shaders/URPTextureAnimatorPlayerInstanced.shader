// When creating shaders for Universal Render Pipeline you can you the ShaderGraph which is super AWESOME!
// However, if you want to author shaders in shading language you can use this teamplate as a base.
// Please note, this shader does not necessarily match perfomance of the built-in URP Lit shader.
// This shader works with URP 7.1.x and above
/*
// Graph Properties
CBUFFER_START(UnityPerMaterial)
sampler2D _PosTex, _NmlTex;
CBUFFER_END

UNITY_INSTANCING_BUFFER_START(Props2)
UNITY_DEFINE_INSTANCED_PROP(float4, _PosTex_TexelSize)
UNITY_DEFINE_INSTANCED_PROP(float,_Length)
UNITY_DEFINE_INSTANCED_PROP(float, _DT)
UNITY_INSTANCING_BUFFER_END(Props2)

// v2f vert (appdata v, uint vid : SV_VertexID)

// In vertex Shader!
// float3 normalOC;
// float4 newVertexPos = TransFormVertex(vid, normalOC);
float4 TransFormVertex( uint vid : SV_VertexID, out float3 normalOC)
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
    normalOC = tex2Dlod(_NmlTex, float4(x, y, 0, 0)).xyz;
    return pos;
    // Don't forget to use
    // o.vertex = UnityObjectToClipPos(pos);
}
*/
Shader "Unlit/URPTextureAnimatorPlayerInstanced"
{
    Properties
    {
        [MainTexture] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainColor] _BaseMap("Base Map (RGB) Smoothness / Alpha (A)", 2D) = "white" {}

        _Cutoff("Alpha Clipping", Range(0.0, 1.0)) = 0.5

        _SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 0.5)
        _SpecGlossMap("Specular Map", 2D) = "white" {}
        [Enum(Specular Alpha,0,Albedo Alpha,1)] _SmoothnessSource("Smoothness Source", Float) = 0.0
        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0

        [HideInInspector] _BumpScale("Scale", Float) = 1.0
        [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}

        _EmissionColor("Emission Color", Color) = (0,0,0)
        [NoScaleOffset]_EmissionMap("Emission Map", 2D) = "white" {}

        // Blending state
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0

        [ToogleOff] _ReceiveShadows("Receive Shadows", Float) = 1.0

        // Editmode props
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0
        [HideInInspector] _Smoothness("SMoothness", Float) = 0.5

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Shininess("Smoothness", Float) = 0.0
        [HideInInspector] _GlossinessSource("GlossinessSource", Float) = 0.0
        [HideInInspector] _SpecSource("SpecularHighlights", Float) = 0.0
        [HideInInspektor] _Metallic (" Metalic value", Range(0.0, 1.0)) = 0.5

        _PosTex("position texture", 2D) = "black"{}
        _NmlTex("normal texture", 2D) = "white"{}
        _DT("delta time", float) = 0
        _Length("animation length", Float) = 1
        [Toggle(_ANIM_LOOP)]_Loop("loop", Float) = 0 // [Toggle(ANIM_LOOP)]
    }

    SubShader
    {
        // With SRP we introduce a new "RenderPipeline" tag in Subshader. This allows to create shaders
        // that can match multiple render pipelines. If a RenderPipeline tag is not set it will match
        // any render pipeline. In case you want your subshader to only run in LWRP set the tag to
        // "UniversalRenderPipeline"
        Tags 
        { 
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry+0"
            "PreviewType" = "Plane"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------
        // Forward pass. Shades GI, emission, fog and all lights in a single pass.
        // Compared to Builtin pipeline forward renderer, LWRP forward renderer will
        // render a scene with multiple lights with less drawcalls and less overdraw.
        Pass
        {
            Tags 
            { 
                "LightMode" = "UniversalForward"
            }
            LOD 250
            // Use same blending / depth states as Standard shader
            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]
            // Render State
            // Blend One Zero, One Zero
            // Cull Back
            ZTest LEqual
            // ZWrite On
            // Lighting Off
            // ColorMask: <None>
            
            
            HLSLPROGRAM
            
            // Pragmas
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma fragmentoption ARB_precision_hint_fastest
            
            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _ _SPECGLOSSMAP _SPECULAR_COLOR
            #pragma shader_feature _GLOSSINESS_FROM_BASE_ALPHA
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _EMISSION
            #pragma shader_feature _RECEIVE_SHADOWS_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile __ _ANIM_LOOP

            
            // Defines
            #define _NORMAL_DROPOFF_TS 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define ATTRIBUTES_NEED_TEXCOORD1
            #define VARYINGS_NEED_POSITION_WS 
            #define VARYINGS_NEED_NORMAL_WS
            #define VARYINGS_NEED_TANGENT_WS
            #define VARYINGS_NEED_VIEWDIRECTION_WS
            #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
            #define SHADERPASS_FORWARD
            #define BUMP_SCALE_NOT_SUPPORTED 1
            //DECLARE_VERTEX_SKINNING

            // Includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            #pragma vertex LitPassVertexSimple
            #pragma fragment LitPassFragmentSimple


            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _SpecColor;
            half4 _EmissionColor;
            half _Cutoff;
            half _Smoothness;
            half _Metallic;
            half _BumpScale;
            half _OcclusionStrength;
            CBUFFER_END

            sampler2D _PosTex, _NmlTex;

            UNITY_INSTANCING_BUFFER_START(Props2)
            UNITY_DEFINE_INSTANCED_PROP(float4, _PosTex_TexelSize)
            UNITY_DEFINE_INSTANCED_PROP(float,_Length)
            UNITY_DEFINE_INSTANCED_PROP(float, _DT)
            UNITY_INSTANCING_BUFFER_END(Props2)

            // v2f vert (appdata v, uint vid : SV_VertexID)

            // In vertex Shader!
            // float3 normalOS;
            // float4 newVertexPos = TransFormVertex(vid, normalOS);
            float4 TransFormVertex( uint vid : SV_VertexID, out float3 normalOS)
            {
                float t = (_Time.y - UNITY_ACCESS_INSTANCED_PROP(Props2, _DT)) / UNITY_ACCESS_INSTANCED_PROP(Props2, _Length);
                #ifdef _ANIM_LOOP
                    t = fmod(t, 1.0);
                #else
                    t = saturate(t);
                #endif			
                float x = (vid + 0.5) * UNITY_ACCESS_INSTANCED_PROP(Props2, _PosTex_TexelSize.x);
                float y = t;
                float4 pos = tex2Dlod(_PosTex, float4(x, y, 0, 0));
                normalOS = tex2Dlod(_NmlTex, float4(x, y, 0, 0)).xyz;
                return pos;
                // Don't forget to use or similar to transform object space to clip space
                // o.vertex = UnityObjectToClipPos(pos);
            }

            TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);

            half4 SampleSpecularSmoothness(half2 uv, half alpha, half4 specColor, TEXTURE2D_PARAM(specMap, sampler_specMap))
            {
                half4 specularSmoothness = half4(0.0h, 0.0h, 0.0h, 1.0h);
                #ifdef _SPECGLOSSMAP
                    specularSmoothness = SAMPLE_TEXTURE2D(specMap, sampler_specMap, uv) * specColor;
                #elif defined(_SPECULAR_COLOR)
                    specularSmoothness = specColor;
                #endif

                #ifdef _GLOSSINESS_FROM_BASE_ALPHA
                    specularSmoothness.a = exp2(10 * alpha + 1);
                #else
                    specularSmoothness.a = exp2(10 * specularSmoothness.a + 1);
                #endif

                return specularSmoothness;
            }

            struct Attributes
            {
                float4 positionOS    : POSITION;
                float3 normalOS      : NORMAL;
                float4 tangentOS     : TANGENT;
                float2 texcoord      : TEXCOORD0;
                float2 lightmapUV    : TEXCOORD1;
                float4 texcoord2    : TEXCOORD2;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv                       : TEXCOORD0;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);

                float3 posWS                    : TEXCOORD2;    // xyz: posWS

                #ifdef _NORMALMAP
                    float4 normal                   : TEXCOORD3;    // xyz: normal, w: viewDir.x
                    float4 tangent                  : TEXCOORD4;    // xyz: tangent, w: viewDir.y
                    float4 bitangent                : TEXCOORD5;    // xyz: bitangent, w: viewDir.z
                #else
                    float3  normal                  : TEXCOORD3;
                    float3 viewDir                  : TEXCOORD4;
                #endif

                half4 fogFactorAndVertexLight   : TEXCOORD6; // x: fogFactor, yzw: vertex light

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord              : TEXCOORD7;
                #endif

                float4 positionCS               : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                // UNITY_VERTEX_OUTPUT_STEREO // for VR
            };

            void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
            {
                inputData.positionWS = input.posWS;

                #ifdef _NORMALMAP
                    half3 viewDirWS = half3(input.normal.w, input.tangent.w, input.bitangent.w);
                    inputData.normalWS = TransformTangentToWorld(normalTS,
                    half3x3(input.tangent.xyz, input.bitangent.xyz, input.normal.xyz));
                #else
                    half3 viewDirWS = input.viewDir;
                    inputData.normalWS = input.normal;
                #endif

                inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
                viewDirWS = SafeNormalize(viewDirWS);

                inputData.viewDirectionWS = viewDirWS;

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    inputData.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                inputData.fogCoord = input.fogFactorAndVertexLight.x;
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
            }

            // Used in Standard (Simple Lighting) shader
            Varyings LitPassVertexSimple(Attributes input, uint vid : SV_VertexID)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                // UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output); // for VR

                float3 normalOS;
                input.positionOS = TransFormVertex(vid, normalOS);
                input.normalOS = normalOS;
                #ifdef _NORMALMAP
                    // calculate tangent from given normal

                #endif

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                half3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
                half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.posWS.xyz = vertexInput.positionWS;
                output.positionCS = vertexInput.positionCS;

                #ifdef _NORMALMAP
                    output.normal = half4(normalInput.normalWS, viewDirWS.x);
                    output.tangent = half4(normalInput.tangentWS, viewDirWS.y);
                    output.bitangent = half4(normalInput.bitangentWS, viewDirWS.z);
                #else
                    output.normal = NormalizeNormalPerVertex(normalInput.normalWS);
                    output.viewDir = viewDirWS;
                #endif

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normal.xyz, output.vertexSH);

                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                return output;
            }

            // Used for StandardSimpleLighting shader
            half4 LitPassFragmentSimple(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); // For VR

                float2 uv = input.uv;
                half4 diffuseAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
                half3 diffuse = diffuseAlpha.rgb * _BaseColor.rgb;

                half alpha = diffuseAlpha.a * _BaseColor.a;
                AlphaDiscard(alpha, _Cutoff);
                #ifdef _ALPHAPREMULTIPLY_ON
                    diffuse *= alpha;
                #endif

                half3 normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap));
                half3 emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
                half4 specular = SampleSpecularSmoothness(uv, alpha, _SpecColor, TEXTURE2D_ARGS(_SpecGlossMap, sampler_SpecGlossMap));
                half smoothness = specular.a;

                InputData inputData;
                InitializeInputData(input, normalTS, inputData);

                half4 color = UniversalFragmentBlinnPhong(inputData, diffuse, specular, smoothness, emission, alpha);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            };
            ENDHLSL
        }

        // ShadowCaster
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0
            #pragma fragmentoption ARB_precision_hint_fastest


            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _GLOSSINESS_FROM_BASE_ALPHA

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"


            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _SpecColor;
            half4 _EmissionColor;
            half _Cutoff;
            CBUFFER_END

            float3 _LightDirection;
            TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);

            half4 SampleSpecularSmoothness(half2 uv, half alpha, half4 specColor, TEXTURE2D_PARAM(specMap, sampler_specMap))
            {
                half4 specularSmoothness = half4(0.0h, 0.0h, 0.0h, 1.0h);
                #ifdef _SPECGLOSSMAP
                    specularSmoothness = SAMPLE_TEXTURE2D(specMap, sampler_specMap, uv) * specColor;
                #elif defined(_SPECULAR_COLOR)
                    specularSmoothness = specColor;
                #endif

                #ifdef _GLOSSINESS_FROM_BASE_ALPHA
                    specularSmoothness.a = exp2(10 * alpha + 1);
                #else
                    specularSmoothness.a = exp2(10 * specularSmoothness.a + 1);
                #endif

                return specularSmoothness;
            }

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
                return 0;
            }

            ENDHLSL
        }
        
        // Depth Only
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0
            #pragma fragmentoption ARB_precision_hint_fastest


            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _GLOSSINESS_FROM_BASE_ALPHA

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _SpecColor;
            half4 _EmissionColor;
            half _Cutoff;
            CBUFFER_END

            TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);

            half4 SampleSpecularSmoothness(half2 uv, half alpha, half4 specColor, TEXTURE2D_PARAM(specMap, sampler_specMap))
            {
                half4 specularSmoothness = half4(0.0h, 0.0h, 0.0h, 1.0h);
                #ifdef _SPECGLOSSMAP
                    specularSmoothness = SAMPLE_TEXTURE2D(specMap, sampler_specMap, uv) * specColor;
                #elif defined(_SPECULAR_COLOR)
                    specularSmoothness = specColor;
                #endif

                #ifdef _GLOSSINESS_FROM_BASE_ALPHA
                    specularSmoothness.a = exp2(10 * alpha + 1);
                #else
                    specularSmoothness.a = exp2(10 * specularSmoothness.a + 1);
                #endif

                return specularSmoothness;
            }

            struct Attributes
            {
                float4 position     : POSITION;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
                return 0;
            }
            ENDHLSL
        }
        
        // Meta - LighMaps only used in Editor
        Pass
        {
            Name "Meta"
            Tags{ "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaSimple

            #pragma shader_feature _EMISSION
            #pragma shader_feature _SPECGLOSSMAP

            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitMetaPass.hlsl"

            ENDHLSL
        }
    }

    // Uses a custom shader GUI to display settings. Re-use the same from Lit shader as they have the
    // same properties.
    CustomEditor "SimpleLitURPShaderGui"
}
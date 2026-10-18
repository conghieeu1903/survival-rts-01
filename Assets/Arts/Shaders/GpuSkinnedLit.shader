// GPU skinning từ bone texture cho kẻ địch ECS (Entities Graphics).
// Mỗi đỉnh gắn cứng vào một xương (chỉ số xương ở TEXCOORD1.x).
// Bone texture: mỗi hàng là một frame, mỗi xương chiếm 3 pixel = 3 hàng đầu của ma trận skin (bone * bindpose).
// _AnimData (riêng từng thực thể qua [MaterialProperty]): x = hàng frame A, y = hàng frame B, z = tỉ lệ nội suy A→B.
Shader "SurvivalRts/GpuSkinnedLit"
{
    Properties
    {
        _BaseColor("Màu nhân", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _BoneTex("Bone Texture", 2D) = "black" {}
        _AnimData("Anim Data (frameA, frameB, blend)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _AnimData;
        CBUFFER_END

        #ifdef UNITY_DOTS_INSTANCING_ENABLED
        UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
            UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DOTS_INSTANCED_PROP(float4, _AnimData)
        UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

        #define _BaseColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor)
        #define _AnimData  UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _AnimData)
        #endif

        TEXTURE2D(_BoneTex);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float4 color      : COLOR;
            float2 bone       : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3x4 LoadBone(int bone, int row)
        {
            int x = bone * 3;
            float4 r0 = LOAD_TEXTURE2D_LOD(_BoneTex, int2(x,     row), 0);
            float4 r1 = LOAD_TEXTURE2D_LOD(_BoneTex, int2(x + 1, row), 0);
            float4 r2 = LOAD_TEXTURE2D_LOD(_BoneTex, int2(x + 2, row), 0);
            return float3x4(r0, r1, r2);
        }

        // Biến đổi vị trí và pháp tuyến (trong không gian object) theo animation hiện tại.
        void Skin(inout float3 positionOS, inout float3 normalOS, float boneIndex)
        {
            float4 anim = _AnimData;
            int bone = (int)(boneIndex + 0.5);
            float3x4 a = LoadBone(bone, (int)(anim.x + 0.5));
            float3x4 b = LoadBone(bone, (int)(anim.y + 0.5));

            float4 p = float4(positionOS, 1.0);
            positionOS = lerp(mul(a, p), mul(b, p), anim.z);
            normalOS = normalize(lerp(mul((float3x3)a, normalOS), mul((float3x3)b, normalOS), anim.z));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                Skin(positionOS, normalOS, input.bone.x);

                output.positionWS = TransformObjectToWorld(positionOS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normalWS, light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
                half3 lighting = light.color * diffuse + SampleSH(normalWS);
                return half4(input.color.rgb * lighting, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                Skin(positionOS, normalOS, input.bone.x);

                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                Skin(positionOS, normalOS, input.bone.x);
                output.positionCS = TransformObjectToHClip(positionOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                Skin(positionOS, normalOS, input.bone.x);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}

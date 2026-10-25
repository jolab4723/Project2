// SW VFXTest 전용 툰 메시 셰이더(얼음 결정·파편 등 메시 파티클용, 임시 테스트 폴더 한정).
// - 주 광원 NdotL을 3단(그늘/중간/밝은 면)으로 끊고, 시선 기준 프레넬로 딱 끊긴 흰 림을 얹는다.
// - 파티클 정점 색으로 틴트·알파를 받고, 자체 발광을 더해 어두운 장면에서도 형태가 묻히지 않게 한다.
Shader "SW/VFXTest/ToonLit"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.2, 0.35, 0.6, 1)
        _BaseColor ("Mid Color", Color) = (0.45, 0.7, 1, 1)
        _HighColor ("Lit Color", Color) = (0.85, 0.95, 1, 1)
        _CelSteps ("Cel Thresholds (shadow, lit)", Vector) = (0.05, 0.55, 0, 0)
        [HDR] _RimColor ("Rim Color (HDR)", Color) = (1.5, 1.8, 2, 1)
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.62
        [HDR] _Emission ("Emission (HDR)", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "VFXTestToonLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half4 _BaseColor;
                half4 _HighColor;
                half4 _CelSteps;
                half4 _RimColor;
                half _RimThreshold;
                half4 _Emission;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();
                half ndl = dot(n, mainLight.direction);
                half3 col = ndl > _CelSteps.y ? _HighColor.rgb : (ndl > _CelSteps.x ? _BaseColor.rgb : _ShadowColor.rgb);
                half fres = 1.0h - saturate(dot(n, v));
                col += _RimColor.rgb * step(_RimThreshold, fres);
                col = col * input.color.rgb + _Emission.rgb;
                return half4(col, input.color.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

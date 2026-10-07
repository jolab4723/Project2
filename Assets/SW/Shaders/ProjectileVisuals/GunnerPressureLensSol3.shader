Shader "SW/ProjectileVisuals/PressureLensSol3"
{
    Properties
    {
        _BaseColor ("Lens Color", Color) = (0.18, 0.25, 0.36, 0.10)
        [HDR] _RimColor ("Cool White Rim", Color) = (0.86, 0.92, 1.0, 0.42)
        _RimPower ("Rim Power", Range(1.5, 8.0)) = 4.2
        _Opacity ("Opacity", Range(0.0, 1.0)) = 0.72
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PressureLens"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _RimPower;
                half _Opacity;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                half fresnel = pow(1.0h - saturate(abs(dot(normalWS, viewDirWS))), _RimPower);
                half3 color = _BaseColor.rgb * 0.34h + _RimColor.rgb * fresnel;
                half alpha = saturate((_BaseColor.a * 0.35h + _RimColor.a * fresnel) * _Opacity);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

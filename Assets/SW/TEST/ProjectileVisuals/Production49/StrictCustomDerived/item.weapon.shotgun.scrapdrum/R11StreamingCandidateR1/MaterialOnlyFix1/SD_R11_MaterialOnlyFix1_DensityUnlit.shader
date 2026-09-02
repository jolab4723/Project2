Shader "Production49/ScrapDrum/MaterialOnlyFix1DensityUnlit"
{
    Properties
    {
        [NoScaleOffset] _DensityMask("Axial density mask (linear R)", 2D) = "black" {}
        _DensityScale("Layer opacity/density", Range(0, 1)) = 0.62
        _EdgePower("View-normal edge falloff", Range(1, 4)) = 2
        _HotCenterZ("Hot center, candidate-local Z", Float) = 0.32
        _HotHalfWidthZ("Hot half-width, candidate-local Z", Float) = 0.42
        _HotRadiance("Hot center radiance (linear RGB)", Vector) = (2.4, 2.28, 2.04, 0)
        _WarmRadiance("Warm falloff radiance (linear RGB)", Vector) = (0.7, 0.18, 0.025, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "DensityEnergy"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            // RGB is explicitly premultiplied below, including all radiance.
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_DensityMask);
            SAMPLER(sampler_DensityMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _HotRadiance;
                float4 _WarmRadiance;
                float _DensityScale;
                float _EdgePower;
                float _HotCenterZ;
                float _HotHalfWidthZ;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float axialZ : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.axialZ = input.positionOS.z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float facing = saturate(dot(normalWS, viewWS));
                float edgeDensity = pow(facing, max(_EdgePower, 1.0));

                // Existing U follows each closed sweep from rear to nose; V is
                // circumference, NOT camera-facing width. Never paint a V stripe.
                float axialDensity = SAMPLE_TEXTURE2D(
                    _DensityMask, sampler_DensityMask,
                    float2(saturate(input.uv.x), 0.5)).r;
                float density = saturate(axialDensity * edgeDensity * _DensityScale);

                // Temperature is anchored in unchanged candidate-local +Z units,
                // so the small rear forks cannot inherit a repeated white stripe.
                float center = saturate(1.0 - abs(input.axialZ - _HotCenterZ)
                    / max(_HotHalfWidthZ, 0.0001));
                float hotWeight = center * center * (3.0 - 2.0 * center) * edgeDensity;
                float3 radiance = max(lerp(_WarmRadiance.rgb, _HotRadiance.rgb, hotWeight), 0.0);

                // One mask controls opacity AND every emitted RGB component.
                // density==0 returns (0,0,0,0): black/gray destination is unchanged.
                return half4(radiance * density, density);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

Shader "Production49/ScrapDrum/R12ParticleDensityUnlit"
{
    Properties
    {
        [NoScaleOffset] _DensityMask("Axial density (linear R)", 2D) = "black" {}
        _DensityScale("Density", Range(0,1)) = 0.7
        _EdgePower("Grazing falloff", Range(1,4)) = 1.25
        _HotCenter("Intrinsic UV.y hot center", Range(0,1)) = 0.65
        _HotWidth("Intrinsic hot half width", Range(0.01,1)) = 0.22
        _TailDensity("Subordinate trailing density", Range(0,1)) = 1
        _HotRadiance("Linear hot radiance", Vector) = (5,4.8,4.35,0)
        _WarmRadiance("Linear warm radiance", Vector) = (1.7,0.67,0.13,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Unlit"
               "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "ParticleDensityEnergy"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_DensityMask); SAMPLER(sampler_DensityMask);
            CBUFFER_START(UnityPerMaterial)
                float4 _HotRadiance, _WarmRadiance;
                float _DensityScale, _EdgePower, _HotCenter, _HotWidth;
                float _TailDensity;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float facing = abs(dot(N,V)); // Fix2 sign-invariant orientation.
                float edge = pow(saturate(facing), max(1.0,_EdgePower));
                float u = saturate(input.uv.x);
                float axial = SAMPLE_TEXTURE2D(_DensityMask,sampler_DensityMask,float2(u,0.5)).r;
                // Analytic endpoint guard guarantees zero at both tips regardless
                // of filtering or the copied mask's first/last texel footprint.
                axial *= smoothstep(0.0,0.035,u) * smoothstep(0.0,0.035,1.0-u);
                float tail = lerp(_TailDensity,1.0,smoothstep(0.20,0.48,input.uv.y));
                float particleAlpha = saturate(input.color.a);
                float density = saturate(axial * edge * tail * _DensityScale) * particleAlpha;
                // UV.y is authored intrinsic thermal position, not PS-generated
                // object/world position: moving/scaling a particle cannot move its heat.
                float h = saturate(1.0-abs(input.uv.y-_HotCenter)/max(_HotWidth,0.0001));
                h = h*h*(3.0-2.0*h);
                float3 radiance = max(lerp(_WarmRadiance.rgb,_HotRadiance.rgb,h),0.0);
                float3 particleTint = max(input.color.rgb,0.0);
                // Alpha is applied ONCE through density to both opacity and ALL RGB.
                // Zero COLOR.a => exact zero RGB/alpha on black AND gray; no specular path.
                return half4(radiance * particleTint * density, density);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

Shader "Project2/Gunner Projectile Flow VFX"
{
    Properties
    {
        [MainTexture] _BaseMap("Shape Texture", 2D) = "white" {}
        _MaskMap("Animated Flow Mask", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 0.91, 0.69, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (3, 2.73, 2.07, 1)
        _BaseScroll("Base Scroll XY", Vector) = (-0.25, 0, 0, 0)
        _MaskScroll("Mask Scroll XY", Vector) = (3, 0, 0, 0)
        _FlowTiling("Flow Tiling XY", Vector) = (1, 1, 0, 0)
        _MaskInfluence("Mask Influence", Range(0, 1)) = 0.85
        _FlowComplexity("Flow Complexity", Range(0, 1)) = 0.55
        _FlowContrast("Flow Contrast", Range(0.25, 4)) = 1.5
        _FlowSharpness("Flow Sharpness", Range(0.25, 5)) = 1.25
        _PulseSpeed("Pulse Speed", Range(0, 12)) = 3
        _PulseAmount("Pulse Amount", Range(0, 0.75)) = 0.15
        _VertexWave("Vertex Wave", Range(0, 0.15)) = 0.01
        _VertexWaveFrequency("Vertex Wave Frequency", Range(0.1, 20)) = 5
        _FresnelStrength("Fresnel Strength", Range(0, 4)) = 0.35
        _FresnelPower("Fresnel Power", Range(0.25, 8)) = 2.5
        _Opacity("Layer Opacity", Range(0, 1)) = 0.5
        _Intensity("Emission Intensity", Range(0, 5)) = 1
        _CoreWhiteness("Hot Core Whiteness", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ProjectileFlow"
            Tags { "LightMode" = "UniversalForward" }

            // Alpha blending preserves layered mesh detail; pure additive blending
            // turns complex projectile shells into a flat white silhouette.
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half3 normalWS : TEXCOORD1;
                half3 viewDirectionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                float4 _BaseScroll;
                float4 _MaskScroll;
                float4 _FlowTiling;
                half _MaskInfluence;
                half _FlowComplexity;
                half _FlowContrast;
                half _FlowSharpness;
                half _PulseSpeed;
                half _PulseAmount;
                half _VertexWave;
                half _VertexWaveFrequency;
                half _FresnelStrength;
                half _FresnelPower;
                half _Opacity;
                half _Intensity;
                half _CoreWhiteness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float wavePhase = dot(input.positionOS.xyz, float3(1.3, 2.1, 0.7)) * _VertexWaveFrequency;
                wavePhase += _Time.y * max(_PulseSpeed, 0.01h) * 2.0;
                float3 animatedPositionOS = input.positionOS.xyz;
                animatedPositionOS += input.normalOS * (sin(wavePhase) * _VertexWave * input.color.a);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(animatedPositionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap) * _FlowTiling.xy;
                output.color = input.color;
                output.normalWS = normalInputs.normalWS;
                output.viewDirectionWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float time = _Time.y;
                float2 baseUv = input.uv + _BaseScroll.xy * time;
                float2 maskUv = input.uv + _MaskScroll.xy * time;
                float2 secondaryMaskUv = float2(-input.uv.y, input.uv.x) * 1.37;
                secondaryMaskUv += _MaskScroll.yx * time * 0.63;

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUv);
                half4 maskA = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, maskUv);
                half4 maskB = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, secondaryMaskUv);

                half maskSimple = dot(maskA.rgb, half3(0.299h, 0.587h, 0.114h));
                half maskLayered = saturate(maskA.r * maskB.g + maskA.b * 0.45h + maskB.r * 0.20h);
                half flowMask = lerp(maskSimple, maskLayered, _FlowComplexity);
                flowMask = saturate((flowMask - 0.5h) * _FlowContrast + 0.5h);
                flowMask = pow(max(flowMask, 0.0001h), _FlowSharpness);

                // Reference textures vary between alpha-authored and luminance-authored masks.
                // Multiplying both channels preserves either convention and prevents the
                // transparent border of the hot-core texture from becoming a visible quad.
                half baseShape = baseSample.a * dot(baseSample.rgb, half3(0.299h, 0.587h, 0.114h));
                half shape = lerp(baseShape, baseShape * flowMask, _MaskInfluence);
                half effectiveFlow = lerp(0.5h, flowMask, _MaskInfluence);
                half pulse = 1.0h + sin(time * _PulseSpeed * 6.28318h + input.uv.x * 6.28318h) * _PulseAmount;

                half alpha = saturate(shape * input.color.a * _BaseColor.a * _Opacity);
                half3 normalWS = normalize(input.normalWS + half3(0.0001h, 0.0001h, 0.0001h));
                half3 viewDirectionWS = SafeNormalize(input.viewDirectionWS);
                half fresnel = pow(saturate(1.0h - abs(dot(normalWS, viewDirectionWS))), _FresnelPower);

                half emissionPeak = max(max(_EmissionColor.r, _EmissionColor.g), _EmissionColor.b);
                half3 hotEmission = lerp(_EmissionColor.rgb, emissionPeak.xxx, _CoreWhiteness);
                half3 surfaceDetail = baseSample.rgb * _BaseColor.rgb * (0.08h + effectiveFlow * 0.08h);
                half3 flowEmission = hotEmission * _Intensity * (0.20h + effectiveFlow * 0.35h);
                half3 finalColor = (surfaceDetail + flowEmission) * input.color.rgb * pulse;
                finalColor *= 1.0h + fresnel * _FresnelStrength;
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

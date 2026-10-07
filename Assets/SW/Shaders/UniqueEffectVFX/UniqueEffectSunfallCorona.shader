// CoronaBand UV: x = angle (0..1), y = normalized radial coordinate (0..1).
// The mesh spans radius 0.70..1.06; the authored texture's hot rim is at UV.y ~= 0.70.
Shader "SW/UniqueEffectVFX/SunfallCorona"
{
    Properties
    {
        [HDR] _TintColor ("Tint (HDR)", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _BaseMap ("Authored Orange Eclipse Band", 2D) = "white" {}
        [NoScaleOffset] _NoiseMap ("Tiled Corona Noise", 2D) = "white" {}
        _BandIntensity ("Orange Band Intensity", Range(0, 4)) = 1.7
        _CoreIntensity ("Hot Rim Intensity", Range(0, 8)) = 3.0
        _CoronaIntensity ("Flame Intensity", Range(0, 4)) = 2.4
        _CoronaSpeed ("Flame Speed", Range(0, 2)) = 0.55
        _Distortion ("Heat Shimmer (UV)", Range(0, 0.06)) = 0.022
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "SunfallCorona"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half _BandIntensity;
                half _CoreIntensity;
                half _CoronaIntensity;
                float _CoronaSpeed;
                float _Distortion;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = _Time.y * _CoronaSpeed;

                // Integer angular tiling keeps the ring seam continuous with Repeat noise.
                // Decode the shared sRGB noise approximately back to its authored range;
                // changing its importer would also change the other unique effects.
                half broad = pow(saturate(SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap,
                    float2(input.uv.x * 9.0 + t * 0.11, t * 0.23)).r), 1.0h / 2.2h);
                half flow = pow(saturate(SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap,
                    float2(input.uv.x * 29.0 - t * 0.18 + broad * 0.45,
                        input.uv.y * 2.6 - t * 0.85)).r), 1.0h / 2.2h);
                half detail = pow(saturate(SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap,
                    float2(input.uv.x * 71.0 + t * 0.27,
                        input.uv.y * 5.0 - t * 1.3)).r), 1.0h / 2.2h);

                // Keep the original orange body and its alpha. Noise never erodes this
                // foundation into gaps or replaces it with a thin procedural white line.
                float shimmer = ((broad - 0.5) * 0.7 + (flow - 0.5) * 0.3) * _Distortion;
                float2 bandUV = float2(input.uv.x + t * 0.025, saturate(input.uv.y + shimmer));
                half4 band = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, bandUV);
                half hotRim = smoothstep(0.40h, 0.75h, band.g);
                half bandAlpha = pow(saturate(band.a), 1.15h);
                half bandShimmer = lerp(0.95h, 1.20h, flow);
                half3 emission = band.rgb * bandAlpha * bandShimmer
                    * lerp(_BandIntensity, _CoreIntensity, hotRim);

                // Flow stretches radially into short, irregular tongues on both sides
                // of the authored hot rim. The original band stays visible underneath.
                float radius = lerp(0.70, 1.06, bandUV.y);
                float offset = radius - 0.952;
                half plume = smoothstep(0.18h, 0.82h, broad * 0.65h + flow * 0.35h);
                float flameWidth = offset < 0.0
                    ? lerp(0.035, 0.125, plume) : lerp(0.030, 0.100, plume);
                half envelope = pow(saturate(1.0 - abs(offset) / flameWidth), 1.25h);
                half strands = smoothstep(0.24h, 0.78h, flow * 0.75h + detail * 0.25h);
                half corona = envelope * (0.18h + strands * 0.82h);
                half gold = smoothstep(0.50h, 0.92h, flow) * envelope;
                half3 flameColor = lerp(half3(1.0h, 0.13h, 0.008h),
                    half3(1.0h, 0.48h, 0.055h), gold);
                emission += flameColor * corona * _CoronaIntensity;

                // Remain on the existing annulus: no new radius, centre fill or wall.
                half edgeFade = smoothstep(0.0, 0.012, input.uv.y)
                    * (1.0h - smoothstep(0.985, 1.0, input.uv.y));
                // Additive blending ignores output alpha: timeline fade must multiply RGB.
                half alpha = edgeFade * input.color.a * _TintColor.a;
                return half4(emission * input.color.rgb * _TintColor.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

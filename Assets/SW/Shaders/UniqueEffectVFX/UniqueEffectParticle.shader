// SW 수정: 고유효과 전용 파티클 셰이더.
// - 프리멀티플라이 알파 출력: 가산은 Blend One One, 반투명은 Blend One OneMinusSrcAlpha 로 같은 텍스처를 공유한다.
// - 파티클 정점 알파를 '페이드' 또는 '침식(디졸브) 진행도'로 사용해 가장자리가 부드럽게 흐려지는 대신 찢기듯 사라진다.
// - 회색 마스크를 그라디언트 램프로 색 매핑(백열 코어 → 포화색 → 어두운 가장자리)하고 HDR 배율로 Bloom을 받는다.
// - 파티클 StableRandom.x(TEXCOORD0.z)로 UV 스크롤 시작점을 흩어 같은 메시가 반복되어 보이지 않게 한다.
Shader "SW/UniqueEffectVFX/Particle"
{
    Properties
    {
        _BaseMap ("Mask / Color (RGBA)", 2D) = "white" {}
        [HDR] _TintColor ("Tint (HDR)", Color) = (1, 1, 1, 1)
        _MainScroll ("Main UV Scroll (xy)", Vector) = (0, 0, 0, 0)
        _NoiseMap ("Erosion Noise", 2D) = "white" {}
        _NoiseScroll ("Noise UV Scroll (xy)", Vector) = (0, 0, 0, 0)
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.5
        [Toggle] _Erode ("Vertex Alpha Drives Erosion", Float) = 0
        _ErosionSoftness ("Erosion Softness", Range(0.01, 1)) = 0.15
        [Toggle] _UseRamp ("Use Gradient Ramp", Float) = 0
        _RampMap ("Gradient Ramp (U = mask)", 2D) = "white" {}
        _RampBias ("Ramp Bias", Range(-0.5, 0.5)) = 0
        _AlphaPower ("Alpha Power", Range(0.3, 4)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
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
            Name "UniqueEffectParticle"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            TEXTURE2D(_RampMap); SAMPLER(sampler_RampMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NoiseMap_ST;
                half4 _TintColor;
                float4 _MainScroll;
                float4 _NoiseScroll;
                half _NoiseStrength;
                half _Erode;
                half _ErosionSoftness;
                half _UseRamp;
                half _RampBias;
                half _AlphaPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float3 uv : TEXCOORD0; // xy = UV, z = StableRandom.x (없으면 0)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;   // xy = 메인, zw = 노이즈
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                float t = _Time.y + input.uv.z * 17.0;
                output.uv.xy = TRANSFORM_TEX(input.uv.xy, _BaseMap) + _MainScroll.xy * t;
                output.uv.zw = TRANSFORM_TEX(input.uv.xy, _NoiseMap) + _NoiseScroll.xy * t + input.uv.z * 3.7;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.uv.zw).r;
                half mask = baseSample.a * lerp(1.0h, noise * 1.6h, _NoiseStrength);

                half alpha;
                if (_Erode > 0.5h)
                {
                    // 정점 알파 1 = 온전, 0 = 완전히 침식. 마스크가 약한 가장자리부터 찢기듯 사라진다.
                    half threshold = 1.0h - input.color.a;
                    alpha = saturate((mask - threshold) / _ErosionSoftness);
                }
                else
                {
                    alpha = saturate(mask) * input.color.a;
                }
                alpha = pow(saturate(alpha), _AlphaPower);

                half3 rgb = baseSample.rgb;
                if (_UseRamp > 0.5h)
                    rgb = SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, float2(saturate(mask + _RampBias), 0.5)).rgb;

                rgb *= input.color.rgb * _TintColor.rgb;
                alpha *= _TintColor.a;
                return half4(rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

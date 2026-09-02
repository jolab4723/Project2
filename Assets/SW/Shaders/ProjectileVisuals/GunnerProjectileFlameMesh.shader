Shader "Project2/Gunner Projectile Flame Mesh"
{
    Properties
    {
        [MainColor] _BaseColor("Surface Color", Color) = (0.22, 0.08, 0.015, 0.42)
        [HDR] _EmissionColor("Emission Color", Color) = (1.0, 0.42, 0.08, 1)
        _FlowSpeed("Forward Flow Speed", Range(0, 8)) = 2.4
        _NoiseScale("Flame Detail Scale", Range(0.5, 12)) = 4
        _WaveAmplitude("Mesh Wave", Range(0, 0.12)) = 0.025
        _PulseSpeed("Heat Pulse Speed", Range(0, 10)) = 3
        _Intensity("Emission Intensity", Range(0, 3)) = 0.9
        _Opacity("Opacity", Range(0, 1)) = 0.5
        _EdgeFade("Edge Fade", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FlameMesh"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
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
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                half _FlowSpeed;
                half _NoiseScale;
                half _WaveAmplitude;
                half _PulseSpeed;
                half _Intensity;
                half _Opacity;
                half _EdgeFade;
            CBUFFER_END

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(cell + float3(0, 0, 0));
                float n100 = Hash31(cell + float3(1, 0, 0));
                float n010 = Hash31(cell + float3(0, 1, 0));
                float n110 = Hash31(cell + float3(1, 1, 0));
                float n001 = Hash31(cell + float3(0, 0, 1));
                float n101 = Hash31(cell + float3(1, 0, 1));
                float n011 = Hash31(cell + float3(0, 1, 1));
                float n111 = Hash31(cell + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

            float FlameNoise(float3 p)
            {
                float value = ValueNoise(p) * 0.58;
                value += ValueNoise(p * 2.03 + 7.1) * 0.28;
                value += ValueNoise(p * 4.11 + 19.4) * 0.14;
                return value;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = input.positionOS.xyz;
                float travelPhase = positionOS.z * 5.7 - _Time.y * _FlowSpeed;
                float wave = sin(travelPhase + positionOS.x * 8.3) * 0.62;
                wave += sin(travelPhase * 1.71 + positionOS.y * 10.1) * 0.38;
                positionOS.xy += input.normalOS.xy * wave * _WaveAmplitude;

                VertexPositionInputs positions = GetVertexPositionInputs(positionOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.positionOS = positionOS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float forwardFlow = input.positionOS.z * _NoiseScale - _Time.y * _FlowSpeed;
                float3 noisePosition = float3(
                    input.positionOS.xy * _NoiseScale * 1.35,
                    forwardFlow);
                half noise = FlameNoise(noisePosition);
                half pulse = 0.9h + sin(forwardFlow * 1.8h + _Time.y * _PulseSpeed) * 0.1h;

                half3 viewDirectionWS = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                half3 normalWS = normalize(input.normalWS + half3(0.0001h, 0.0001h, 0.0001h));
                half rim = pow(saturate(1.0h - abs(dot(normalWS, viewDirectionWS))), 1.6h);
                half body = saturate(0.58h + noise * 0.72h);
                half edge = lerp(1.0h, 1.0h - rim * 0.72h, _EdgeFade);
                half alpha = saturate(_BaseColor.a * _Opacity * body * edge * input.color.a);

                half heat = smoothstep(0.35h, 0.88h, noise);
                half3 flameColor = lerp(_BaseColor.rgb, _EmissionColor.rgb, 0.48h + heat * 0.52h);
                flameColor *= _Intensity * pulse * (0.78h + body * 0.36h) * input.color.rgb;
                return half4(flameColor, alpha);
            }
            ENDHLSL
        }
    }
}

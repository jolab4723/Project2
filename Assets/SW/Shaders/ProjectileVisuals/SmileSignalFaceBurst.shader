Shader "Project2/Smile Signal Face Burst"
{
    Properties
    {
        _Intensity("Spark emission", Range(0, 4)) = 1.1
        _SparkSize("Spark radius", Range(0.001, 0.05)) = 0.010
        _Scatter("Breakup distance", Range(0, 1)) = 0.28
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            // 한 ParticleSystem의 Mesh 입자 하나가 얼굴을 구성하는 모든 불꽃 점을 함께 재생합니다.
            // Center, AgePercent, UV, SizeXY 순서의 사용자 지정 정점 스트림을 사용합니다.
            // UV.x의 정수는 불꽃 번호이고 UV.y의 정수는 원본 RGB565 색입니다. 소수는 불꽃 면의 모서리입니다.
            // 나이는 파티클에서 받으므로 풀 재사용이나 다시 재생할 때 전역 시간에 영향을 받지 않습니다.
            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float4 centerAge : TEXCOORD0;
                float4 uvSize : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _SparkSize;
                float _Scatter;
            CBUFFER_END

            float3 Random3(float n)
            {
                return frac(sin(n * float3(127.1, 311.7, 74.7) + float3(13.1, 7.2, 19.7)) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float age = saturate(input.centerAge.w);
                float id = floor(input.uvSize.x);
                float3 random = Random3(id + 1.0);
                float2 corner = float2(frac(input.uvSize.x) / 0.8, frac(input.uvSize.y) * 2.0) * 2.0 - 1.0;
                // 원본 얼굴 색은 UV에 담아 전달합니다. 파티클 렌더러의 색 변환에 영향을 받지 않습니다.
                float packed = floor(input.uvSize.y);
                half3 faceColor = half3(floor(packed / 2048.0) / 31.0,
                    fmod(floor(packed / 32.0), 64.0) / 63.0, fmod(packed, 32.0) / 31.0);
                float3 centerWS = TransformObjectToWorld(input.centerAge.xyz);
                float3 pointWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 offset = pointWS - centerWS;
                float3 right = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up = UNITY_MATRIX_I_V._m01_m11_m21;
                // 균일한 점 격자처럼 보이지 않도록 형상을 해치지 않는 작은 위치 차이를 줍니다.
                offset += (right * (random.x - 0.5) + up * (random.y - 0.5)) * 0.012 * max(input.uvSize.z, 0.001);

                // 중심에서 빠르게 터지고, 잠깐 얼굴이 읽힌 다음 점마다 다른 방향으로 흩어집니다.
                float expansion = 0.035 + 0.965 * (1.0 - pow(1.0 - saturate(age / 0.3), 2.0));
                float breakup = smoothstep(0.42, 0.95, age);
                float scale = max(input.uvSize.z, 0.001);
                float3 drift = ((random - 0.5) * 2.0 + normalize(offset + 0.0001) * 0.55) * _Scatter * scale;
                pointWS = centerWS + offset * (expansion + breakup * 0.18) + drift * breakup;
                pointWS.y -= breakup * breakup * 0.10 * scale;

                float fade = smoothstep(0.0, 0.07, age) * (1.0 - smoothstep(0.58 + random.z * 0.10, 0.88 + random.z * 0.10, age));
                float radius = _SparkSize * scale * lerp(1.0, 0.4, breakup) * (0.8 + random.x * 0.4);
                float2 direction = float2(dot(offset, right), dot(offset, up));
                direction = normalize(direction + 0.0001);
                float3 along = right * direction.x + up * direction.y;
                float3 across = right * direction.y - up * direction.x;
                float streak = 1.0 + (1.0 - smoothstep(0.08, 0.3, age)) * 3.0 + breakup * 1.2;
                pointWS += (across * corner.x + along * corner.y * streak) * radius;
                // 중심에 겹친 점들의 밝기를 낮춰 얼굴이 펼쳐지기 전에 하얀 덩어리가 되지 않게 합니다.
                fade *= expansion * expansion / streak;
                o.positionCS = TransformWorldToHClip(pointWS);
                o.uv = corner;
                o.color = half4(SRGBToLinear(faceColor), fade);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float d = dot(input.uv, input.uv);
                float soft = exp(-d * 4.0) * saturate(1.0 - d);
                float hot = exp(-d * 22.0);
                half3 color = input.color.rgb * (soft + hot * 0.35);
                return half4(color * input.color.a * _Intensity, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

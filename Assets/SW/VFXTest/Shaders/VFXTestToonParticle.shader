// SW VFXTest 전용 툰 파티클 셰이더(임시 테스트 폴더 한정). SW/UniqueEffectVFX/Particle을 바탕으로 다음을 더했다.
// - 셀 음영 3단: 마스크를 화면 기준 광원 방향으로 조금 옮겨 다시 읽고, 그 차이로 밝은 면/중간/그늘을 딱 끊어 나눈다.
//   UV 미분(ddx/ddy)으로 화면 방향을 UV 방향으로 바꾸므로 파티클 회전과 상관없이 늘 같은 쪽(왼쪽 위)이 밝다.
// - 방향 침식: 원본 UV를 지정 방향으로 투영한 기울기를 침식 기준에 섞어, 가장자리부터가 아니라 한쪽 끝부터 사라지게 한다.
// - 코어 발광: HDR 배율을 램프 상단(코어)에만 준다. 몸통은 램프 색(≤1)이라 블룸 임계값 아래에서 채도를 지키고,
//   코어만 임계값을 넘어 블룸을 받는다. 전체에 같은 배율을 곱하면 몸통까지 톤매핑에 눌려 하얗고 납작해진다.
// - 어두운 가장자리 띠: 침식 경계(또는 잘라 낸 형태 경계) 바로 안쪽을 어둡게 눌러 흰 코어 → 몸통 → 어두운 테두리 3단을 늘 보이게 한다.
//   램프의 어두운 끝은 마스크가 낮은 곳이라 원래는 투명해서 보이지 않았다.
// - 진행 드러남: 정점 스트림의 AgePercent(TEXCOORD0.w)로 지정 방향을 따라 형태가 차례로 나타나게 한다(베기 궤적 등).
// - 툰 림: 원통처럼 둥근 메시를 시선과 면이 이루는 각으로 2단 어둡게 끊어, 평평한 띠가 아니라 둥근 기둥으로 읽히게 한다.
Shader "SW/VFXTest/ToonParticle"
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
        _CelShade ("Cel Shade Amount", Range(0, 1)) = 0
        _CelOffset ("Cel Sample Offset (UV)", Range(0.005, 0.2)) = 0.05
        _CelLevels ("Cel Lit / Shadow / Threshold", Vector) = (1.3, 0.55, 0.06, 0)
        _LightDir2D ("Screen Light Dir (xy)", Vector) = (-0.6, 0.8, 0, 0)
        _ErodeDir ("Directional Erosion (dir xy, offset z, weight w)", Vector) = (1, 0, 0, 0)
        _CoreStart ("Core Emission Start (ramp U)", Range(0, 1)) = 0.6
        _CoreBoost ("Core Emission Boost", Float) = 1
        _EdgeFade ("Edge Fade (view angle power, 0 = off)", Range(0, 4)) = 0
        _DarkEdge ("Dark Edge (width x, rgb mul y, cutout z, fade on dissolve w)", Vector) = (0, 1, 0, 0)
        _Reveal ("Reveal (dir xy, offset z, end age w; w = 0 off)", Vector) = (1, 0, 0, 0)
        _RevealSoft ("Reveal Edge Softness", Range(0.005, 0.3)) = 0.12
        _ToonRim ("Toon Rim (outer th x, mul y, inner th z, mul w; x = 0 off)", Vector) = (0, 1, 0, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "VFXTestToonParticle"
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
                half _CelShade;
                float _CelOffset;
                half4 _CelLevels;
                float4 _LightDir2D;
                float4 _ErodeDir;
                half _CoreStart;
                half _CoreBoost;
                half _EdgeFade;
                half4 _DarkEdge;
                float4 _Reveal;
                half _RevealSoft;
                half4 _ToonRim;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float4 uv : TEXCOORD0; // xy = UV, z = StableRandom.x, w = AgePercent (없으면 0)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;   // xy = 메인, zw = 노이즈
                float3 uvRaw : TEXCOORD1; // xy = 스크롤·타일 전 UV(방향 침식 기준), z = AgePercent
                float3 normalWS : TEXCOORD2;
                float3 viewWS : TEXCOORD3;
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
                output.uvRaw = float3(input.uv.xy, input.uv.w);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(TransformObjectToWorld(input.positionOS.xyz));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.uv.zw).r;
                half mask = baseSample.a * lerp(1.0h, noise * 1.6h, _NoiseStrength);

                half alpha;
                half edgeDist = 1.0h; // 형태 경계까지의 마스크 거리(어두운 가장자리 띠 기준)
                if (_Erode > 0.5h)
                {
                    // 정점 알파 1 = 온전, 0 = 완전히 침식. 방향 가중치가 있으면 지정한 끝부터 먹어 들어간다.
                    half threshold = 1.0h - input.color.a;
                    half grad = saturate(dot(input.uvRaw.xy, _ErodeDir.xy) + _ErodeDir.z);
                    half erodeMask = lerp(mask, grad * saturate(mask * 8.0h), _ErodeDir.w);
                    // 노이즈가 마스크를 1 위로 올리면 정점 알파 0에서도 픽셀이 남아 마지막 칸에 잔상이 된다: 알파 0이면 확실히 지운다
                    alpha = saturate((erodeMask - threshold) / _ErosionSoftness) * step(0.002h, input.color.a);
                    edgeDist = erodeMask - threshold;
                }
                else if (_DarkEdge.z > 0.001h)
                {
                    // 잘라 낸 툰 경계: 반투명 번짐 대신 딱 끊긴 형태 + 경계 안쪽 어두운 띠
                    alpha = saturate((mask - _DarkEdge.z) / _ErosionSoftness) * input.color.a;
                    edgeDist = mask - _DarkEdge.z;
                }
                else
                {
                    alpha = saturate(mask) * input.color.a;
                }
                if (_Reveal.w > 0.001h)
                {
                    // 나이 0 → _Reveal.w 동안 방향 기울기가 낮은 쪽부터 차례로 나타난다.
                    half p = saturate(input.uvRaw.z / _Reveal.w) * 1.15h;
                    half g = saturate(dot(input.uvRaw.xy, _Reveal.xy) + _Reveal.z);
                    alpha *= saturate((p - g) / _RevealSoft); // 툰 진행 경계는 작게(기본 0.12는 화염 베기의 부드러운 머리)
                }
                alpha = pow(saturate(alpha), _AlphaPower);
                if (_EdgeFade > 0.001h)
                {
                    // 메시 가장자리(시선과 면이 나란한 곳)를 흐리게: 원통·물벽이 판이나 사발처럼 딱 잘려 보이지 않게. 양면 모두 같은 규칙.
                    half facing = abs(dot(normalize(input.normalWS), normalize(input.viewWS)));
                    alpha *= pow(saturate(facing), _EdgeFade);
                }

                half3 rgb = baseSample.rgb;
                half rampU = saturate(mask + _RampBias);
                if (_UseRamp > 0.5h)
                    rgb = SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, float2(rampU, 0.5)).rgb;
                rgb *= lerp(1.0h, _CoreBoost, smoothstep(_CoreStart, 1.0h, rampU));
                if (_ToonRim.x > 0.001h)
                {
                    // 가장자리일수록 시선과 면이 나란하다(facing → 0): 바깥 단·안쪽 단을 딱 끊어 곱한다
                    half facingR = abs(dot(normalize(input.normalWS), normalize(input.viewWS)));
                    rgb *= facingR < _ToonRim.x ? _ToonRim.y : (facingR < _ToonRim.z ? _ToonRim.w : 1.0h);
                }
                if (_DarkEdge.x > 0.001h)
                    rgb *= lerp(_DarkEdge.y, 1.0h, max(step(_DarkEdge.x, edgeDist), saturate((1.0h - input.color.a) * _DarkEdge.w))); // 경계 안쪽 폭 x만큼 툰 테두리(딱 끊김). w > 0이면 소멸(정점 알파 감소) 초반에 테두리를 걷어 낸다: 넓은 띠가 침식 경계에 한꺼번에 걸려 검은 줄로 보이지 않게

                if (_CelShade > 0.001h)
                {
                    // 화면 광원 방향(픽셀 단위)을 UV 방향으로 변환해 마스크를 한 번 더 읽는다.
                    float2 dir = ddx(input.uv.xy) * _LightDir2D.x + ddy(input.uv.xy) * _LightDir2D.y;
                    dir *= _CelOffset * rsqrt(max(dot(dir, dir), 1e-10));
                    half toward = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy + dir).a;
                    half d = baseSample.a - toward; // 양수: 광원 쪽으로 가면 형태가 끝남 = 밝은 면
                    half cell = d > _CelLevels.z ? _CelLevels.x : (d < -_CelLevels.z ? _CelLevels.y : 1.0h);
                    rgb *= lerp(1.0h, cell, _CelShade);
                }

                rgb *= input.color.rgb * _TintColor.rgb;
                alpha *= _TintColor.a;
                return half4(rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

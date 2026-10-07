Shader "Project2/Rifle Body Glow Shell"
{
    Properties
    {
        [HDR] _GlowColor("Glow Color", Color) = (1.4, 0.45, 0.12, 0.65)
        _OutlineWidth("Outline Width", Range(0.0005, 0.05)) = 0.008
        _Intensity("Glow Intensity", Range(0, 4)) = 1.25
        _RimPower("Rim Focus", Range(0.5, 8)) = 2.2
        _PulseSpeed("Pulse Speed", Range(0, 20)) = 3
        _PulseAmount("Pulse Amount", Range(0, 0.5)) = 0.08
        _Style("Element Style", Range(0, 3)) = 0
        _HaloScale("Outer Halo Scale", Range(1, 1.2)) = 1.06
        _OutlinePixels("Minimum Rim Width (pixels)", Range(0, 4)) = 1.6
        _HaloPixels("Minimum Halo Width (pixels)", Range(0, 6)) = 3.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            // 각 탄환의 원점을 기준으로 테두리를 넓히므로 메시를 미리 하나로 합치지 않습니다.
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "BodyGlowShell"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            Cull Front
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
                float _OutlineWidth;
                float _Intensity;
                float _RimPower;
                float _PulseSpeed;
                float _PulseAmount;
                float _Style;
                float _HaloScale;
                float _OutlinePixels;
                float _HaloPixels;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Expand from the baked mesh centre instead of along split vertex
                // normals. This keeps a clean, continuous toon band on hard-surface
                // bullets while letting each element control a readable thickness.
                float innerScale = 1.0 + _OutlineWidth;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz * innerScale);
                output.positionCS = positionInputs.positionCS;
                // A model-relative expansion becomes less than one pixel after the
                // WBH projectile scale and gameplay camera projection. Keep a small
                // visible rim in screen space. Smooth normals are stored on the
                // dedicated glow mesh; the solid projectile mesh is untouched.
                float4 baseCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 outlineNormalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalVS = mul((float3x3)UNITY_MATRIX_V, outlineNormalWS).xy;
                float2 directionVS = mul((float2x2)UNITY_MATRIX_P, normalVS) * _ScaledScreenParams.xy;
                float directionLength = length(directionVS);
                float2 pixelDirection = directionVS / max(directionLength, 0.0001);
                float2 expandedPixels = (output.positionCS.xy / max(output.positionCS.w, 0.0001)
                    - baseCS.xy / max(baseCS.w, 0.0001)) * (0.5 * _ScaledScreenParams.xy);
                float existingWidth = dot(expandedPixels, pixelDirection);
                float extraPixels = max(0.0, _OutlinePixels - existingWidth);
                output.positionCS.xy += pixelDirection * extraPixels
                    * (2.0 / _ScaledScreenParams.xy) * output.positionCS.w
                    * saturate(length(normalVS) * 20.0)
                    * step(0.0001, baseCS.w);

                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // Keep the projectile surface readable: only the silhouette edge emits.
                // A filled additive shell made pale/ice projectiles turn into flat white blobs.
                // 작은 탄환에서는 시선에 따른 감쇠가 너무 강하면 HDR 발광이 Bloom 기준 아래로 내려갑니다.
                // 본체 바깥에 노출된 테두리만 최소 밝기를 유지하며, 본체 표면은 깊이 검사로 보호합니다.
                float rim = 0.65 + 0.35 * pow(saturate(1.0 - abs(dot(normalWS, viewDirWS))), _RimPower);

                float phase = _Time.y * _PulseSpeed;
                float pulse = 1.0 + sin(phase) * _PulseAmount;
                float pattern = 1.0;
                float3 styleColor = _GlowColor.rgb;

                // Fire: two broad moving tongues break the rim into a hot, organic
                // lick. The brighter sections lean orange without bleaching the core.
                if (_Style > 0.5 && _Style < 1.5)
                {
                    float heatA = sin(dot(input.positionOS, float3(7.0, 11.0, 16.0)) - phase * 2.15);
                    float heatB = sin(dot(input.positionOS, float3(-13.0, 8.0, 9.0)) + phase * 1.45);
                    float lick = saturate(heatA * 0.55 + heatB * 0.35 + 0.58);
                    pattern = 0.52 + 0.58 * smoothstep(0.15, 0.92, lick);
                    styleColor = lerp(_GlowColor.rgb, float3(2.05, 0.38, 0.035), lick * 0.42);
                }
                // Ice: intersecting diagonal bands make compact frost-crystal facets.
                // They remain attached to the projectile because they use object space.
                else if (_Style > 1.5 && _Style < 2.5)
                {
                    float facets = abs(normalWS.x * 0.61 + normalWS.y * 0.83 + normalWS.z * 0.47);
                    float frostA = abs(sin((input.positionOS.x + input.positionOS.y) * 22.0 + phase * 0.30));
                    float frostB = abs(sin((input.positionOS.y - input.positionOS.z) * 19.0 - phase * 0.22));
                    float crystal = smoothstep(0.68, 0.96, max(frostA, frostB));
                    pattern = 0.62 + 0.30 * smoothstep(0.30, 0.76, facets) + crystal * 0.34;
                    styleColor = lerp(_GlowColor.rgb, float3(0.58, 1.72, 2.25), crystal * 0.38);
                    pulse = 0.95 + 0.05 * sin(phase * 0.48);
                }
                // Electric: sharp crossing bands resemble short yellow lightning
                // segments instead of a single smooth neon outline.
                else if (_Style > 2.5)
                {
                    float arcA = abs(sin(input.positionOS.z * 34.0 + input.positionOS.x * 25.0 - phase * 3.2));
                    float arcB = abs(sin(input.positionOS.y * 31.0 - input.positionOS.z * 19.0 + phase * 2.65));
                    float arc = pow(max(arcA, arcB), 8.0);
                    pattern = 0.34 + arc * 1.04;
                    styleColor = lerp(_GlowColor.rgb, float3(2.25, 1.82, 0.08), arc * 0.46);
                    pulse = 0.86 + 0.14 * sin(phase * 1.85);
                }

                float3 emission = styleColor * (_GlowColor.a * _Intensity * rim * pattern * pulse);
                return half4(emission, 1.0);
            }
            ENDHLSL
        }

        // A wider low-energy pass gives the outline a readable glow layer in the
        // gameplay camera without inflating or bleaching the projectile mesh.
        Pass
        {
            Name "BodyGlowHalo"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VertHalo
            #pragma fragment FragHalo
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
                float _OutlineWidth;
                float _Intensity;
                float _RimPower;
                float _PulseSpeed;
                float _PulseAmount;
                float _Style;
                float _HaloScale;
                float _OutlinePixels;
                float _HaloPixels;
            CBUFFER_END

            Varyings VertHalo(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // Scaling around the baked mesh center preserves a continuous
                // silhouette even when the source FBX has split hard-edge normals.
                // The outer pass starts beyond the crisp toon band. Coupling part
                // of the outline width here prevents a thin neon-wire appearance.
                float outerScale = _HaloScale + (_OutlineWidth * 0.65);
                float3 expandedOS = input.positionOS.xyz * outerScale;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(expandedOS);
                output.positionCS = positionInputs.positionCS;
                // A model-relative expansion becomes less than one pixel after the
                // WBH projectile scale and gameplay camera projection. Keep a small
                // visible rim in screen space. Smooth normals are stored on the
                // dedicated glow mesh; the solid projectile mesh is untouched.
                float4 baseCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 outlineNormalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalVS = mul((float3x3)UNITY_MATRIX_V, outlineNormalWS).xy;
                float2 directionVS = mul((float2x2)UNITY_MATRIX_P, normalVS) * _ScaledScreenParams.xy;
                float directionLength = length(directionVS);
                float2 pixelDirection = directionVS / max(directionLength, 0.0001);
                float2 expandedPixels = (output.positionCS.xy / max(output.positionCS.w, 0.0001)
                    - baseCS.xy / max(baseCS.w, 0.0001)) * (0.5 * _ScaledScreenParams.xy);
                float existingWidth = dot(expandedPixels, pixelDirection);
                float extraPixels = max(0.0, _HaloPixels - existingWidth);
                output.positionCS.xy += pixelDirection * extraPixels
                    * (2.0 / _ScaledScreenParams.xy) * output.positionCS.w
                    * saturate(length(normalVS) * 20.0)
                    * step(0.0001, baseCS.w);

                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 FragHalo(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim = 0.3 + 0.7 * pow(saturate(1.0 - abs(dot(normalWS, viewDirWS))), max(0.65, _RimPower * 0.72));
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * (_PulseAmount * 0.6);
                float styleEnergy = _Style > 2.5 ? 0.37 : (_Style > 0.5 && _Style < 1.5 ? 0.34 : 0.29);
                float stylePattern = 1.0;
                if (_Style > 0.5 && _Style < 1.5)
                {
                    float haloLick = sin(dot(input.positionOS, float3(5.0, 9.0, 13.0)) - _Time.y * _PulseSpeed * 1.35);
                    stylePattern = 0.68 + 0.42 * saturate(haloLick * 0.5 + 0.5);
                }
                else if (_Style > 1.5 && _Style < 2.5)
                {
                    float frostGlint = abs(sin((input.positionOS.x - input.positionOS.y + input.positionOS.z) * 17.0));
                    stylePattern = 0.74 + 0.32 * smoothstep(0.72, 0.98, frostGlint);
                }
                else if (_Style > 2.5)
                {
                    float haloArc = abs(sin(input.positionOS.z * 28.0 - input.positionOS.x * 21.0 - _Time.y * _PulseSpeed * 2.1));
                    stylePattern = 0.50 + 0.62 * pow(haloArc, 7.0);
                }
                float3 emission = _GlowColor.rgb * (_GlowColor.a * _Intensity * rim * pulse * styleEnergy * stylePattern);
                return half4(emission, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

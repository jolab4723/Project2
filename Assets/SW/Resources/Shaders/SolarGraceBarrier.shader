Shader "Project2/SolarGraceBarrier"
{
    Properties
    {
        _Color ("Barrier Color", Color) = (0.30, 0.80, 1.0, 1.0)
        _HitAt ("Last Hit Time", Float) = -100
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _HitAt;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
                float rim = pow(saturate(1.0 - abs(dot(normalize(input.normalWS), viewDirection))), 2.3);
                float2 grid = abs(sin((input.uv + float2(_Time.y * 0.012, 0)) * float2(95, 48)));
                float filaments = pow(saturate(max(grid.x, grid.y)), 36.0) * 0.10;
                float hit = exp(-5.0 * max(0.0, _Time.y - _HitAt));
                float alpha = saturate(0.045 + rim * 0.46 + filaments + hit * 0.22);
                float3 color = _Color.rgb * (0.75 + rim * 0.85 + hit * 0.8);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

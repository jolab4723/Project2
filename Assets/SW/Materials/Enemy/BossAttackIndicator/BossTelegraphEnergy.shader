Shader "SW/VFX/Boss Telegraph Energy"
{
 Properties {
 [HDR] _BaseColor ("Energy", Color) = (3,0.08,0.01,1)
 _Mode ("0 Ribbon / 1 Field / 2 Spark", Float) = 0
 _Progress ("Charge", Range(0,1)) = 0.65
 _Phase ("Time", Float) = 0
 _Opacity ("Opacity", Range(0,1)) = 1
 _Flow ("Flow strength", Range(0,1)) = 0
 }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
 Pass {
 Tags { "LightMode"="SRPDefaultUnlit" }
 Blend SrcAlpha One
 ZWrite Off
 ZTest LEqual
 Cull Off
 Offset -1, -1
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
 struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
 CBUFFER_START(UnityPerMaterial)
 half4 _BaseColor;
 float _Mode, _Progress, _Phase, _Opacity, _Flow;
 CBUFFER_END
 Varyings vert(Attributes v) {
 Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
 o.uv=v.uv; o.color=v.color; return o;
 }
 float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
 float noise(float2 p) {
 float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
 return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
 }
 half4 frag(Varyings i):SV_Target {
 float p=saturate(_Progress), a;
 float charge=lerp(.65,1.25,p);
 if (_Mode < .5) {
 float d=abs(i.uv.y*2-1);
 float aa=max(fwidth(d),.015);
 a=1-smoothstep(.24-aa,.24+aa,d);
 a+=.30*pow(saturate(1-d),2);
 float flow=.55+.45*pow(.5+.5*sin(i.uv.x*24-_Phase*7),3);
 a*=lerp(1,flow,_Flow)*i.color.a;
 } else if (_Mode < 1.5) {
 float2 q=(i.uv-.5)*2;
 float r=length(q);
 float n=noise(q*24+_Phase*.14)*noise(q*67-_Phase*.08);
 float edge=exp2(-pow((r-.955)*65,2));
 float sweep=exp2(-pow((r-lerp(.92,.09,p))*85,2));
 float2 grid=abs(frac(q*19)-.5);
 float gridline=1-smoothstep(.012,.026,min(grid.x,grid.y));
 a=(.015+.065*n+.018*gridline+.085*edge+.13*sweep)*(1-smoothstep(.96,1,r));
 a*=lerp(.45,1,p);
 } else {
 float2 q=(i.uv-.5)*2;
 a=exp2(-dot(q,q)*7)*i.color.a;
 }
 return half4(_BaseColor.rgb*charge, saturate(a*_BaseColor.a*_Opacity));
 }
 ENDHLSL
 }
 }
}

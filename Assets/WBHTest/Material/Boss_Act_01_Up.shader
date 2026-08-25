// Advanced Dissolve <https://u3d.as/16cX>
// Copyright (c) Amazing Assets <https://amazingassets.world>

Shader "Amazing Assets/Advanced Dissolve/Shader Graph/Boss_Act_01_Up"
{
Properties
{
//Advanced Dissolve Properties Start////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

//Cutout
[HideInInspector]                                                   _AdvancedDissolveCutoutStandardClip("", Range(0,1)) = 0.5

[HideInInspector]											        _AdvancedDissolveCutoutStandardMap1("", 2D) = "white" { }
[HideInInspector]											        _AdvancedDissolveCutoutStandardMap1Tiling("", Vector) = (1, 1, 1, 0)
[HideInInspector]											        _AdvancedDissolveCutoutStandardMap1Offset("", Vector) = (0, 0, 0, 0)
[HideInInspector]					                                _AdvancedDissolveCutoutStandardMap1Scroll("", Vector) = (0, 0, 0, 0)
[HideInInspector]											        _AdvancedDissolveCutoutStandardMap1Intensity("", Range(0, 1)) = 1
[HideInInspector][Enum(Red, 0, Green, 1, Blue, 2, Alpha, 3)]        _AdvancedDissolveCutoutStandardMap1Channel("", INT) = 3
[HideInInspector][AdvancedDissolveToggleFloat]				        _AdvancedDissolveCutoutStandardMap1Invert("", INT) = 0
[HideInInspector]											        _AdvancedDissolveCutoutStandardMap2("", 2D) = "white" { }
[HideInInspector]											        _AdvancedDissolveCutoutStandardMap2Tiling("", Vector) = (1, 1, 1, 0)
[HideInInspector]											        _AdvancedDissolveCutoutStandardMap2Offset("", Vector) = (0, 0, 0, 0)
[HideInInspector]					                                _AdvancedDissolveCutoutStandardMap2Scroll("", Vector) = (0, 0, 0, 0)
[HideInInspector]											        _AdvancedDissolveCutoutStandardMap2Intensity("", Range(0, 1)) = 1
[HideInInspector][Enum(Red, 0, Green, 1, Blue, 2, Alpha, 3)]        _AdvancedDissolveCutoutStandardMap2Channel("", INT) = 3
[HideInInspector][AdvancedDissolveToggleFloat]				        _AdvancedDissolveCutoutStandardMap2Invert("", INT) = 0
[HideInInspector]											        _AdvancedDissolveCutoutStandardMap3("", 2D) = "white" { }
[HideInInspector]											        _AdvancedDissolveCutoutStandardMap3Tiling("", Vector) = (1, 1, 1, 0)
[HideInInspector]											        _AdvancedDissolveCutoutStandardMap3Offset("", Vector) = (0, 0, 0, 0)
[HideInInspector]					                                _AdvancedDissolveCutoutStandardMap3Scroll("", Vector) = (0, 0, 0, 0)
[HideInInspector]											        _AdvancedDissolveCutoutStandardMap3Intensity("", Range(0, 1)) = 1
[HideInInspector][Enum(Red, 0, Green, 1, Blue, 2, Alpha, 3)]        _AdvancedDissolveCutoutStandardMap3Channel("", INT) = 3
[HideInInspector][AdvancedDissolveToggleFloat]				        _AdvancedDissolveCutoutStandardMap3Invert("", INT) = 0

[HideInInspector][Enum(Multiply, 0, Add, 1)]				        _AdvancedDissolveCutoutStandardMapsBlendType("", Float) = 0
[HideInInspector][Enum(World, 0, Local, 1)]					        _AdvancedDissolveCutoutStandardMapsTriplanarMappingSpace("", Float) = 0	
[HideInInspector][Enum(Constant, 0, Camera Relative, 1)]            _AdvancedDissolveCutoutStandardMapsScreenSpaceUVScale("", Float) = 0
[HideInInspector][AdvancedDissolveToggleFloat]				        _AdvancedDissolveCutoutStandardBaseInvert("", INT) = 0

//Geometric
[HideInInspector][AdvancedDissolveToggleFloat]			    	    _AdvancedDissolveCutoutGeometricInvert("", Float) = 0
[HideInInspector]										    	    _AdvancedDissolveCutoutGeometricNoise("", Float) = 0.1	

[HideInInspector][Enum(X, 0, Y, 1, Z, 2)]                           _AdvancedDissolveCutoutGeometricXYZAxis("", Float) = 0
[HideInInspector][Enum(Linear, 0, Symmetrical, 1)]                  _AdvancedDissolveCutoutGeometricXYZStyle("", Float) = 0 
[HideInInspector][Enum(World, 0, Local, 1)]                         _AdvancedDissolveCutoutGeometricXYZSpace("", Float) = 0	 
[HideInInspector]											        _AdvancedDissolveCutoutGeometricXYZRollout("", Float) = 0
[HideInInspector]											        _AdvancedDissolveCutoutGeometricXYZPosition("", Vector) = (0, 0, 0, 0)

[HideInInspector]										    	    _AdvancedDissolveCutoutGeometric1Position("", Vector) = (0,0,0,0)
[HideInInspector]										    	    _AdvancedDissolveCutoutGeometric1Normal("", Vector) = (1,0,0,0)
[HideInInspector]										    	    _AdvancedDissolveCutoutGeometric1Radius("", Float) = 1
[HideInInspector]										    	    _AdvancedDissolveCutoutGeometric1Height("", Float) = 1

[HideInInspector]										    	    _AdvancedDissolveCutoutGeometric2Position("", Vector) = (0,0,0,0)
[HideInInspector]									    		    _AdvancedDissolveCutoutGeometric2Normal("", Vector) = (1,0,0,0)
[HideInInspector]									    		    _AdvancedDissolveCutoutGeometric2Radius("", Float) = 1
[HideInInspector]									    		    _AdvancedDissolveCutoutGeometric2Height("", Float) = 1
 
[HideInInspector]									    		    _AdvancedDissolveCutoutGeometric3Position("", Vector) = (0,0,0,0)
[HideInInspector]									    		    _AdvancedDissolveCutoutGeometric3Normal("", Vector) = (1,0,0,0)
[HideInInspector]									    		    _AdvancedDissolveCutoutGeometric3Radius("", Float) = 1
[HideInInspector]										    	    _AdvancedDissolveCutoutGeometric3Height("", Float) = 1

[HideInInspector]										    	    _AdvancedDissolveCutoutGeometric4Position("", Vector) = (0,0,0,0)
[HideInInspector]											        _AdvancedDissolveCutoutGeometric4Normal("", Vector) = (1,0,0,0)
[HideInInspector]											        _AdvancedDissolveCutoutGeometric4Radius("", Float) = 1
[HideInInspector]											        _AdvancedDissolveCutoutGeometric4Height("", Float) = 1

//Edge
[HideInInspector]										    	    _AdvancedDissolveEdgeBaseWidthStandard("", Range(0,1)) = 0.1 
[HideInInspector]										    	    _AdvancedDissolveEdgeBaseWidthGeometric("", Range(0,1)) = 0.1 
[HideInInspector][Enum(Solid, 0, Smooth, 1, Smoother, 2)]           _AdvancedDissolveEdgeBaseShape("", INT) = 0
[HideInInspector][AdvancedDissolveColorRGB]  				        _AdvancedDissolveEdgeBaseColor("", Color) = (0,1,0,1)
[HideInInspector]											        _AdvancedDissolveEdgeBaseColorTransparency("", Range(0, 1)) = 1
[HideInInspector][AdvancedDissolveExponental]                       _AdvancedDissolveEdgeBaseColorIntensity("", Vector) = (0, 0, 0, 0)		

[HideInInspector][AdvancedDissolveColorRGB]					        _AdvancedDissolveEdgeAdditionalColor("", color) = (1, 0, 0, 1)
[HideInInspector]											        _AdvancedDissolveEdgeAdditionalColorTransparency("", Range(0, 1)) = 1
[HideInInspector][AdvancedDissolveExponental]			            _AdvancedDissolveEdgeAdditionalColorIntensity("", Vector) = (0, 0, 0, 0)
[HideInInspector]								                    _AdvancedDissolveEdgeAdditionalColorMap("", 2D) = "white" { }
[HideInInspector]					                                _AdvancedDissolveEdgeAdditionalColorMapTiling("", Vector) = (1, 1, 1, 0)
[HideInInspector]					                                _AdvancedDissolveEdgeAdditionalColorMapOffset("", Vector) = (0, 0, 0, 0)
[HideInInspector]					                                _AdvancedDissolveEdgeAdditionalColorMapScroll("", Vector) = (0, 0, 0, 0)
[HideInInspector][AdvancedDissolveToggleFloat]				        _AdvancedDissolveEdgeAdditionalColorMapReverse("", FLOAT) = 0
[HideInInspector]											        _AdvancedDissolveEdgeAdditionalColorMapMipmap("", Range(0, 10)) = 1	
[HideInInspector]											        _AdvancedDissolveEdgeAdditionalColorPhaseOffset("", FLOAT) = 0
[HideInInspector]											        _AdvancedDissolveEdgeAdditionalColorAlphaOffset("", Range(-1, 1)) = 0	
[HideInInspector][AdvancedDissolveToggleFloat]				        _AdvancedDissolveEdgeAdditionalColorClipInterpolation("", Float) = 0


[HideInInspector]								                    _AdvancedDissolveEdgeUVDistortionMap("", 2D) = "black" { }
[HideInInspector]					                                _AdvancedDissolveEdgeUVDistortionMapTiling("", Vector) = (1, 1, 1, 0)
[HideInInspector]					                                _AdvancedDissolveEdgeUVDistortionMapOffset("", Vector) = (0, 0, 0, 0)
[HideInInspector]					                                _AdvancedDissolveEdgeUVDistortionMapScroll("", Vector) = (0, 0, 0, 0)
[HideInInspector]				                                    _AdvancedDissolveEdgeUVDistortionStrength("", Float) = 0

[HideInInspector][AdvancedDissolvePositiveFloat]			        _AdvancedDissolveEdgeGIMetaPassMultiplier("", Float) = 1

//Keywords
[HideInInspector][AdvancedDissolveKeywordState]                     _AdvancedDissolveKeywordState("", INT) = 0
[HideInInspector][AdvancedDissolveKeywordCutoutStandardSource]      _AdvancedDissolveKeywordCutoutStandardSource("", INT) = 0
[HideInInspector][AdvancedDissolveKeywordCutoutStandardMappingType] _AdvancedDissolveKeywordCutoutStandardSourceMapsMappingType("", INT) = 0
[HideInInspector][AdvancedDissolveKeywordCutoutGeometricType]       _AdvancedDissolveKeywordCutoutGeometricType("", INT) = 0
[HideInInspector][AdvancedDissolveKeywordCutoutGeometricCount]      _AdvancedDissolveKeywordCutoutGeometricCount("", INT) = 0
[HideInInspector][AdvancedDissolveKeywordEdgeBaseSource]            _AdvancedDissolveKeywordEdgeBaseSource("", INT) = 0
[HideInInspector][AdvancedDissolveKeywordEdgeAdditionalColorSource] _AdvancedDissolveKeywordEdgeAdditionalColorSource("", INT) = 0
[HideInInspector][AdvancedDissolveKeywordEdgeUVDistortionSource]    _AdvancedDissolveKeywordEdgeUVDistortionSource("", INT) = 0
[HideInInspector][AdvancedDissolveKeywordGlobalControlID]           _AdvancedDissolveKeywordGlobalControlID("", INT) = 0

//BakedKeywords
[HideInInspector]                                                   _AdvancedDissolveBakedKeywords("", Vector) = (0,0,0,0)	

//Shader Graph
[HideInInspector]                                                   _AdvancedDissolveShaderGraphGUID("3c1b713fd13312f4d9d7653bfd06e100", float) = 0	

//Advanced Dissolve Properties End////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


_Roug("Roug", Range(0, 1)) = 0
_Base("Base", Color) = (0, 0, 0, 0)
[NoScaleOffset]_Custom_texture("Custom texture", 2D) = "white" {}
_tile("tile", Range(0, 20)) = 0
_color_or_texture("color or texture", Range(0, 1)) = 0
_emis("emis", Range(0, 1)) = 0
_emis_1("emis (1)", Color) = (0, 0, 0, 0)
_armor("armor", Color) = (0, 0, 0, 0)
_metal("metal", Color) = (0, 0, 0, 0)
_Steel("Steel", Color) = (0, 0, 0, 0)
_rocket("rocket", Color) = (0, 0, 0, 0)
_cilindr("cilindr", Color) = (0, 0, 0, 0)
_eye("eye", Color) = (0, 0, 0, 0)
_decal("decal", Color) = (0, 0, 0, 0)
_Curvature_1("Curvature 1", Range(0, 1)) = 0
_Curvature_1_C("Curvature 1  C", Color) = (0, 0, 0, 0)
_Curvature_2("Curvature 2", Range(0, 1)) = 0
_Curvature_2_C("Curvature 2 C", Color) = (0, 0, 0, 0)
[NonModifiableTextureData][NoScaleOffset]_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D("Texture2D", 2D) = "white" {}
[NonModifiableTextureData][NoScaleOffset]_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D("Texture2D", 2D) = "white" {}
[NonModifiableTextureData][NoScaleOffset]_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D("Texture2D", 2D) = "white" {}
[NonModifiableTextureData][NoScaleOffset]_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D("Texture2D", 2D) = "white" {}
[NonModifiableTextureData][Normal][NoScaleOffset]_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D("Texture2D", 2D) = "bump" {}
[NonModifiableTextureData][NoScaleOffset]_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D("Texture2D", 2D) = "white" {}
[NonModifiableTextureData][NoScaleOffset]_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D("Texture2D", 2D) = "white" {}
[HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
[HideInInspector]_QueueControl("_QueueControl", Float) = -1
[HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
[HideInInspector]_BUILTIN_QueueOffset("Float", Float) = 0
[HideInInspector]_BUILTIN_QueueControl("Float", Float) = -1
}
SubShader
{
Tags
{
"RenderPipeline"="UniversalPipeline"
"RenderType"="Opaque"
"UniversalMaterialType" = "Lit"
"Queue"="AlphaTest"
"DisableBatching"="False"
"ShaderGraphShader"="true"
"ShaderGraphTargetId"="UniversalLitSubTarget"
}
Pass
{
    Name "Universal Forward"
    Tags
    {
        "LightMode" = "UniversalForward"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On
AlphaToMask On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma multi_compile_instancing
#pragma instancing_options renderinglayer
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DYNAMICLIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
#pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
#pragma multi_compile _ REFLECTION_PROBE_ROTATION
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma multi_compile_fragment _ _LIGHT_LAYERS
#pragma multi_compile_fragment _ DEBUG_DISPLAY
#pragma multi_compile_fragment _ _LIGHT_COOKIES
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define VARYINGS_NEED_SHADOW_COORD
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_FORWARD
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion;
#endif
 float4 fogFactorAndVertexLight;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord;
#endif
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 ObjectSpacePosition;
 float3 WorldSpacePosition;
 float3 AbsoluteWorldSpacePosition;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV : INTERP0;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV : INTERP1;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP2;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion : INTERP3;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord : INTERP4;
#endif
 float4 tangentWS : INTERP5;
 float4 texCoord0 : INTERP6;
 float4 fogFactorAndVertexLight : INTERP7;
 float3 positionWS : INTERP8;
 float3 normalWS : INTERP9;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// unity-custom-func-begin
void AdvancedDissolveShaderGraphFunction_float(float2 UV, float3 PositionOS, float3 PositionWS, float3 PositionWS_Absolut, float3 NormalOS, float3 NormalWS, float Custom_Cutout, float4 Custom_Color, out float Value){
Value = 0;
}
// unity-custom-func-end

struct Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float
{
float3 ObjectSpaceNormal;
float3 WorldSpaceNormal;
float3 ObjectSpacePosition;
float3 WorldSpacePosition;
float3 AbsoluteWorldSpacePosition;
half4 uv0;
};

void SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float Vector1_9E44E7D0, float4 Color_d37717e22d9845eeb5507ed0b661e197, Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float IN, out float Out_3)
{
float4 _UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4 = IN.uv0;
float _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float = Vector1_9E44E7D0;
float4 _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4 = Color_d37717e22d9845eeb5507ed0b661e197;
float _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
AdvancedDissolveShaderGraphFunction_float((_UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4.xy), IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float, _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4, _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float);
Out_3 = _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
float Alpha;
float AlphaClipThreshold;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_R_4_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.r;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_G_5_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.g;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_B_6_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.b;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_A_7_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.a;
float4 _Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4 = _Base;
UnityTexture2D _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_Custom_texture);
float _Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float = _tile;
float2 _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float.xx), float2 (0, 0), _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2);
float4 _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.tex, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.samplerstate, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2) );
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_R_4_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.r;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_G_5_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.g;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_B_6_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.b;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_A_7_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.a;
float _Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float = _color_or_texture;
float4 _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4;
Unity_Lerp_float4(_Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4, _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4, (_Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float.xxxx), _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4);
float4 _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4 = _armor;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.r;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.g;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.b;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_A_7_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.a;
float4 _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4, _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float.xxxx), _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4);
float4 _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4 = _metal;
float4 _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4, _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float.xxxx), _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4);
float4 _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4 = _Steel;
float4 _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4, _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float.xxxx), _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4);
float4 _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4 = _rocket;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.r;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.g;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.b;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_A_7_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.a;
float4 _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4, _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float.xxxx), _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4);
float4 _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4 = _cilindr;
float4 _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4, _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float.xxxx), _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4);
float4 _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4 = _eye;
float4 _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4, _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float.xxxx), _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4);
float4 _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4 = _decal;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.r;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.g;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.b;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_A_7_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.a;
float4 _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4, _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4, (_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float.xxxx), _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4);
float4 _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4 = _Curvature_1_C;
float _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float = _Curvature_1;
float _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float, _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float, _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float);
float4 _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4, _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4, (_Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float.xxxx), _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4);
float4 _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4 = _Curvature_2_C;
float _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float = _Curvature_2;
float _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float, _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float, _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float);
float4 _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4, _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4, (_Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float.xxxx), _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4);
float4 _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4, _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4, _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4);
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4);
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_R_4_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.r;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_G_5_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.g;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_B_6_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.b;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_A_7_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.a;
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_R_4_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.r;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_G_5_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.g;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_B_6_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.b;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_A_7_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.a;
float _Property_1f990134a755435faf10a57f4d75685b_Out_0_Float = _emis;
float4 _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4, (_Property_1f990134a755435faf10a57f4d75685b_Out_0_Float.xxxx), _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4);
float4 _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4 = _emis_1;
float4 _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4, _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4, _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4);
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_R_4_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.r;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_G_5_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.g;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_B_6_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.b;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_A_7_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.a;
float _Property_67edbdce71854da594b303ad6e95e154_Out_0_Float = _Roug;
float _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_G_5_Float, _Property_67edbdce71854da594b303ad6e95e154_Out_0_Float, _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float);
float _Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float = float(1);
Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpaceNormal = IN.ObjectSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpaceNormal = IN.WorldSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpacePosition = IN.ObjectSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpacePosition = IN.WorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.AbsoluteWorldSpacePosition = IN.AbsoluteWorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.uv0 = IN.uv0;
float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float;
SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float(0), float4 (0, 0, 0, 1), _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float);
float _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
Unity_Add_float(_Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float, _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float);
surface.BaseColor = (_Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4.xyz);
surface.NormalTS = (_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.xyz);
surface.Emission = (_Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4.xyz);
surface.Metallic = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_B_6_Float;
surface.Smoothness = _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float;
surface.Occlusion = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_R_4_Float;
surface.Alpha = _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
surface.AlphaClipThreshold = float(0.5);


//UniversalForward
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.BaseColor, surface.Emission, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);


    output.WorldSpacePosition = input.positionWS;
    output.ObjectSpacePosition = TransformWorldToObject(input.positionWS);
    output.AbsoluteWorldSpacePosition = GetAbsolutePositionWS(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "GBuffer"
    Tags
    {
        "LightMode" = "UniversalGBuffer"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 4.5
#pragma exclude_renderers gles3 glcore
#pragma multi_compile_instancing
#pragma instancing_options renderinglayer
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DYNAMICLIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
#pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
#pragma multi_compile _ REFLECTION_PROBE_ROTATION
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
#pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
#pragma multi_compile_fragment _ DEBUG_DISPLAY
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define VARYINGS_NEED_SHADOW_COORD
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_GBUFFER
#define _FOG_FRAGMENT 1
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion;
#endif
 float4 fogFactorAndVertexLight;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord;
#endif
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 ObjectSpacePosition;
 float3 WorldSpacePosition;
 float3 AbsoluteWorldSpacePosition;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV : INTERP0;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV : INTERP1;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP2;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion : INTERP3;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord : INTERP4;
#endif
 float4 tangentWS : INTERP5;
 float4 texCoord0 : INTERP6;
 float4 fogFactorAndVertexLight : INTERP7;
 float3 positionWS : INTERP8;
 float3 normalWS : INTERP9;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// unity-custom-func-begin
void AdvancedDissolveShaderGraphFunction_float(float2 UV, float3 PositionOS, float3 PositionWS, float3 PositionWS_Absolut, float3 NormalOS, float3 NormalWS, float Custom_Cutout, float4 Custom_Color, out float Value){
Value = 0;
}
// unity-custom-func-end

struct Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float
{
float3 ObjectSpaceNormal;
float3 WorldSpaceNormal;
float3 ObjectSpacePosition;
float3 WorldSpacePosition;
float3 AbsoluteWorldSpacePosition;
half4 uv0;
};

void SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float Vector1_9E44E7D0, float4 Color_d37717e22d9845eeb5507ed0b661e197, Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float IN, out float Out_3)
{
float4 _UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4 = IN.uv0;
float _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float = Vector1_9E44E7D0;
float4 _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4 = Color_d37717e22d9845eeb5507ed0b661e197;
float _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
AdvancedDissolveShaderGraphFunction_float((_UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4.xy), IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float, _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4, _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float);
Out_3 = _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
float Alpha;
float AlphaClipThreshold;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_R_4_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.r;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_G_5_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.g;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_B_6_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.b;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_A_7_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.a;
float4 _Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4 = _Base;
UnityTexture2D _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_Custom_texture);
float _Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float = _tile;
float2 _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float.xx), float2 (0, 0), _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2);
float4 _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.tex, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.samplerstate, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2) );
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_R_4_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.r;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_G_5_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.g;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_B_6_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.b;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_A_7_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.a;
float _Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float = _color_or_texture;
float4 _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4;
Unity_Lerp_float4(_Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4, _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4, (_Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float.xxxx), _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4);
float4 _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4 = _armor;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.r;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.g;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.b;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_A_7_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.a;
float4 _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4, _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float.xxxx), _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4);
float4 _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4 = _metal;
float4 _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4, _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float.xxxx), _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4);
float4 _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4 = _Steel;
float4 _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4, _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float.xxxx), _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4);
float4 _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4 = _rocket;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.r;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.g;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.b;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_A_7_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.a;
float4 _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4, _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float.xxxx), _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4);
float4 _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4 = _cilindr;
float4 _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4, _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float.xxxx), _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4);
float4 _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4 = _eye;
float4 _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4, _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float.xxxx), _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4);
float4 _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4 = _decal;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.r;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.g;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.b;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_A_7_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.a;
float4 _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4, _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4, (_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float.xxxx), _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4);
float4 _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4 = _Curvature_1_C;
float _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float = _Curvature_1;
float _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float, _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float, _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float);
float4 _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4, _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4, (_Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float.xxxx), _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4);
float4 _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4 = _Curvature_2_C;
float _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float = _Curvature_2;
float _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float, _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float, _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float);
float4 _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4, _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4, (_Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float.xxxx), _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4);
float4 _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4, _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4, _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4);
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4);
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_R_4_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.r;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_G_5_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.g;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_B_6_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.b;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_A_7_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.a;
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_R_4_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.r;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_G_5_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.g;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_B_6_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.b;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_A_7_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.a;
float _Property_1f990134a755435faf10a57f4d75685b_Out_0_Float = _emis;
float4 _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4, (_Property_1f990134a755435faf10a57f4d75685b_Out_0_Float.xxxx), _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4);
float4 _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4 = _emis_1;
float4 _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4, _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4, _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4);
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_R_4_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.r;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_G_5_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.g;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_B_6_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.b;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_A_7_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.a;
float _Property_67edbdce71854da594b303ad6e95e154_Out_0_Float = _Roug;
float _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_G_5_Float, _Property_67edbdce71854da594b303ad6e95e154_Out_0_Float, _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float);
float _Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float = float(1);
Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpaceNormal = IN.ObjectSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpaceNormal = IN.WorldSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpacePosition = IN.ObjectSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpacePosition = IN.WorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.AbsoluteWorldSpacePosition = IN.AbsoluteWorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.uv0 = IN.uv0;
float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float;
SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float(0), float4 (0, 0, 0, 1), _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float);
float _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
Unity_Add_float(_Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float, _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float);
surface.BaseColor = (_Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4.xyz);
surface.NormalTS = (_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.xyz);
surface.Emission = (_Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4.xyz);
surface.Metallic = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_B_6_Float;
surface.Smoothness = _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float;
surface.Occlusion = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_R_4_Float;
surface.Alpha = _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
surface.AlphaClipThreshold = float(0.5);


//GBuffer
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.BaseColor, surface.Emission, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);


    output.WorldSpacePosition = input.positionWS;
    output.ObjectSpacePosition = TransformWorldToObject(input.positionWS);
    output.AbsoluteWorldSpacePosition = GetAbsolutePositionWS(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRGBufferPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "ShadowCaster"
    Tags
    {
        "LightMode" = "ShadowCaster"
    }

// Render State
Cull Back
ZTest LEqual
ZWrite On
ColorMask 0

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_SHADOWCASTER
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 ObjectSpacePosition;
 float3 WorldSpacePosition;
 float3 AbsoluteWorldSpacePosition;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float3 positionWS : INTERP1;
 float3 normalWS : INTERP2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

// unity-custom-func-begin
void AdvancedDissolveShaderGraphFunction_float(float2 UV, float3 PositionOS, float3 PositionWS, float3 PositionWS_Absolut, float3 NormalOS, float3 NormalWS, float Custom_Cutout, float4 Custom_Color, out float Value){
Value = 0;
}
// unity-custom-func-end

struct Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float
{
float3 ObjectSpaceNormal;
float3 WorldSpaceNormal;
float3 ObjectSpacePosition;
float3 WorldSpacePosition;
float3 AbsoluteWorldSpacePosition;
half4 uv0;
};

void SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float Vector1_9E44E7D0, float4 Color_d37717e22d9845eeb5507ed0b661e197, Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float IN, out float Out_3)
{
float4 _UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4 = IN.uv0;
float _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float = Vector1_9E44E7D0;
float4 _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4 = Color_d37717e22d9845eeb5507ed0b661e197;
float _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
AdvancedDissolveShaderGraphFunction_float((_UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4.xy), IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float, _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4, _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float);
Out_3 = _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float = float(1);
Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpaceNormal = IN.ObjectSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpaceNormal = IN.WorldSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpacePosition = IN.ObjectSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpacePosition = IN.WorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.AbsoluteWorldSpacePosition = IN.AbsoluteWorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.uv0 = IN.uv0;
float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float;
SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float(0), float4 (0, 0, 0, 1), _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float);
float _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
Unity_Add_float(_Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float, _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float);
surface.Alpha = _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
surface.AlphaClipThreshold = float(0.5);


//ShadowCaster
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale


    output.WorldSpacePosition = input.positionWS;
    output.ObjectSpacePosition = TransformWorldToObject(input.positionWS);
    output.AbsoluteWorldSpacePosition = GetAbsolutePositionWS(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "MotionVectors"
    Tags
    {
        "LightMode" = "MotionVectors"
    }

// Render State
Cull Back
ZTest LEqual
ZWrite On
ColorMask RG

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.5
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TEXCOORD0
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_MOTION_VECTORS
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 uv0 : TEXCOORD0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 ObjectSpacePosition;
 float3 WorldSpacePosition;
 float3 AbsoluteWorldSpacePosition;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float3 positionWS : INTERP1;
 float3 normalWS : INTERP2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

// unity-custom-func-begin
void AdvancedDissolveShaderGraphFunction_float(float2 UV, float3 PositionOS, float3 PositionWS, float3 PositionWS_Absolut, float3 NormalOS, float3 NormalWS, float Custom_Cutout, float4 Custom_Color, out float Value){
Value = 0;
}
// unity-custom-func-end

struct Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float
{
float3 ObjectSpaceNormal;
float3 WorldSpaceNormal;
float3 ObjectSpacePosition;
float3 WorldSpacePosition;
float3 AbsoluteWorldSpacePosition;
half4 uv0;
};

void SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float Vector1_9E44E7D0, float4 Color_d37717e22d9845eeb5507ed0b661e197, Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float IN, out float Out_3)
{
float4 _UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4 = IN.uv0;
float _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float = Vector1_9E44E7D0;
float4 _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4 = Color_d37717e22d9845eeb5507ed0b661e197;
float _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
AdvancedDissolveShaderGraphFunction_float((_UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4.xy), IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float, _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4, _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float);
Out_3 = _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float = float(1);
Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpaceNormal = IN.ObjectSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpaceNormal = IN.WorldSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpacePosition = IN.ObjectSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpacePosition = IN.WorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.AbsoluteWorldSpacePosition = IN.AbsoluteWorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.uv0 = IN.uv0;
float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float;
SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float(0), float4 (0, 0, 0, 1), _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float);
float _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
Unity_Add_float(_Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float, _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float);
surface.Alpha = _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
surface.AlphaClipThreshold = float(0.5);


//MotionVectors
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale


    output.WorldSpacePosition = input.positionWS;
    output.ObjectSpacePosition = TransformWorldToObject(input.positionWS);
    output.AbsoluteWorldSpacePosition = GetAbsolutePositionWS(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/MotionVectorPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "DepthOnly"
    Tags
    {
        "LightMode" = "DepthOnly"
    }

// Render State
Cull Back
ZTest LEqual
ZWrite On
ColorMask R

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 ObjectSpacePosition;
 float3 WorldSpacePosition;
 float3 AbsoluteWorldSpacePosition;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float3 positionWS : INTERP1;
 float3 normalWS : INTERP2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

// unity-custom-func-begin
void AdvancedDissolveShaderGraphFunction_float(float2 UV, float3 PositionOS, float3 PositionWS, float3 PositionWS_Absolut, float3 NormalOS, float3 NormalWS, float Custom_Cutout, float4 Custom_Color, out float Value){
Value = 0;
}
// unity-custom-func-end

struct Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float
{
float3 ObjectSpaceNormal;
float3 WorldSpaceNormal;
float3 ObjectSpacePosition;
float3 WorldSpacePosition;
float3 AbsoluteWorldSpacePosition;
half4 uv0;
};

void SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float Vector1_9E44E7D0, float4 Color_d37717e22d9845eeb5507ed0b661e197, Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float IN, out float Out_3)
{
float4 _UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4 = IN.uv0;
float _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float = Vector1_9E44E7D0;
float4 _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4 = Color_d37717e22d9845eeb5507ed0b661e197;
float _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
AdvancedDissolveShaderGraphFunction_float((_UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4.xy), IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float, _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4, _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float);
Out_3 = _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float = float(1);
Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpaceNormal = IN.ObjectSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpaceNormal = IN.WorldSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpacePosition = IN.ObjectSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpacePosition = IN.WorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.AbsoluteWorldSpacePosition = IN.AbsoluteWorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.uv0 = IN.uv0;
float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float;
SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float(0), float4 (0, 0, 0, 1), _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float);
float _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
Unity_Add_float(_Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float, _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float);
surface.Alpha = _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
surface.AlphaClipThreshold = float(0.5);


//DepthOnly
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale


    output.WorldSpacePosition = input.positionWS;
    output.ObjectSpacePosition = TransformWorldToObject(input.positionWS);
    output.AbsoluteWorldSpacePosition = GetAbsolutePositionWS(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "DepthNormals"
    Tags
    {
        "LightMode" = "DepthNormals"
    }

// Render State
Cull Back
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHNORMALS
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 ObjectSpacePosition;
 float3 WorldSpacePosition;
 float3 AbsoluteWorldSpacePosition;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 tangentWS : INTERP0;
 float4 texCoord0 : INTERP1;
 float3 positionWS : INTERP2;
 float3 normalWS : INTERP3;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

// unity-custom-func-begin
void AdvancedDissolveShaderGraphFunction_float(float2 UV, float3 PositionOS, float3 PositionWS, float3 PositionWS_Absolut, float3 NormalOS, float3 NormalWS, float Custom_Cutout, float4 Custom_Color, out float Value){
Value = 0;
}
// unity-custom-func-end

struct Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float
{
float3 ObjectSpaceNormal;
float3 WorldSpaceNormal;
float3 ObjectSpacePosition;
float3 WorldSpacePosition;
float3 AbsoluteWorldSpacePosition;
half4 uv0;
};

void SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float Vector1_9E44E7D0, float4 Color_d37717e22d9845eeb5507ed0b661e197, Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float IN, out float Out_3)
{
float4 _UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4 = IN.uv0;
float _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float = Vector1_9E44E7D0;
float4 _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4 = Color_d37717e22d9845eeb5507ed0b661e197;
float _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
AdvancedDissolveShaderGraphFunction_float((_UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4.xy), IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float, _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4, _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float);
Out_3 = _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 NormalTS;
float Alpha;
float AlphaClipThreshold;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4);
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_R_4_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.r;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_G_5_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.g;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_B_6_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.b;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_A_7_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.a;
float _Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float = float(1);
Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpaceNormal = IN.ObjectSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpaceNormal = IN.WorldSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpacePosition = IN.ObjectSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpacePosition = IN.WorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.AbsoluteWorldSpacePosition = IN.AbsoluteWorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.uv0 = IN.uv0;
float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float;
SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float(0), float4 (0, 0, 0, 1), _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float);
float _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
Unity_Add_float(_Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float, _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float);
surface.NormalTS = (_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.xyz);
surface.Alpha = _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
surface.AlphaClipThreshold = float(0.5);


//DepthNormals
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);


    output.WorldSpacePosition = input.positionWS;
    output.ObjectSpacePosition = TransformWorldToObject(input.positionWS);
    output.AbsoluteWorldSpacePosition = GetAbsolutePositionWS(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthNormalsOnlyPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "Meta"
    Tags
    {
        "LightMode" = "Meta"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma shader_feature _ EDITOR_VISUALIZATION
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_INSTANCEID
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_TEXCOORD2
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_META
#define _FOG_FRAGMENT 1
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 texCoord0;
 float4 texCoord1;
 float4 texCoord2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 ObjectSpacePosition;
 float3 WorldSpacePosition;
 float3 AbsoluteWorldSpacePosition;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float4 texCoord1 : INTERP1;
 float4 texCoord2 : INTERP2;
 float3 positionWS : INTERP3;
 float3 normalWS : INTERP4;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.texCoord1.xyzw = input.texCoord1;
output.texCoord2.xyzw = input.texCoord2;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.texCoord1 = input.texCoord1.xyzw;
output.texCoord2 = input.texCoord2.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_META_PASS
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// unity-custom-func-begin
void AdvancedDissolveShaderGraphFunction_float(float2 UV, float3 PositionOS, float3 PositionWS, float3 PositionWS_Absolut, float3 NormalOS, float3 NormalWS, float Custom_Cutout, float4 Custom_Color, out float Value){
Value = 0;
}
// unity-custom-func-end

struct Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float
{
float3 ObjectSpaceNormal;
float3 WorldSpaceNormal;
float3 ObjectSpacePosition;
float3 WorldSpacePosition;
float3 AbsoluteWorldSpacePosition;
half4 uv0;
};

void SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float Vector1_9E44E7D0, float4 Color_d37717e22d9845eeb5507ed0b661e197, Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float IN, out float Out_3)
{
float4 _UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4 = IN.uv0;
float _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float = Vector1_9E44E7D0;
float4 _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4 = Color_d37717e22d9845eeb5507ed0b661e197;
float _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
AdvancedDissolveShaderGraphFunction_float((_UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4.xy), IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float, _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4, _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float);
Out_3 = _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 Emission;
float Alpha;
float AlphaClipThreshold;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_R_4_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.r;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_G_5_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.g;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_B_6_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.b;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_A_7_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.a;
float4 _Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4 = _Base;
UnityTexture2D _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_Custom_texture);
float _Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float = _tile;
float2 _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float.xx), float2 (0, 0), _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2);
float4 _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.tex, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.samplerstate, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2) );
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_R_4_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.r;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_G_5_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.g;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_B_6_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.b;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_A_7_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.a;
float _Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float = _color_or_texture;
float4 _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4;
Unity_Lerp_float4(_Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4, _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4, (_Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float.xxxx), _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4);
float4 _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4 = _armor;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.r;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.g;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.b;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_A_7_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.a;
float4 _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4, _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float.xxxx), _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4);
float4 _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4 = _metal;
float4 _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4, _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float.xxxx), _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4);
float4 _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4 = _Steel;
float4 _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4, _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float.xxxx), _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4);
float4 _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4 = _rocket;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.r;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.g;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.b;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_A_7_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.a;
float4 _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4, _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float.xxxx), _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4);
float4 _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4 = _cilindr;
float4 _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4, _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float.xxxx), _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4);
float4 _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4 = _eye;
float4 _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4, _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float.xxxx), _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4);
float4 _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4 = _decal;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.r;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.g;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.b;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_A_7_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.a;
float4 _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4, _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4, (_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float.xxxx), _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4);
float4 _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4 = _Curvature_1_C;
float _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float = _Curvature_1;
float _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float, _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float, _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float);
float4 _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4, _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4, (_Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float.xxxx), _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4);
float4 _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4 = _Curvature_2_C;
float _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float = _Curvature_2;
float _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float, _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float, _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float);
float4 _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4, _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4, (_Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float.xxxx), _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4);
float4 _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4, _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4, _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4);
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_R_4_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.r;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_G_5_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.g;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_B_6_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.b;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_A_7_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.a;
float _Property_1f990134a755435faf10a57f4d75685b_Out_0_Float = _emis;
float4 _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4, (_Property_1f990134a755435faf10a57f4d75685b_Out_0_Float.xxxx), _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4);
float4 _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4 = _emis_1;
float4 _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4, _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4, _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4);
float _Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float = float(1);
Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpaceNormal = IN.ObjectSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpaceNormal = IN.WorldSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpacePosition = IN.ObjectSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpacePosition = IN.WorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.AbsoluteWorldSpacePosition = IN.AbsoluteWorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.uv0 = IN.uv0;
float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float;
SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float(0), float4 (0, 0, 0, 1), _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float);
float _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
Unity_Add_float(_Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float, _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float);
surface.BaseColor = (_Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4.xyz);
surface.Emission = (_Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4.xyz);
surface.Alpha = _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
surface.AlphaClipThreshold = float(0.5);


//Unknown
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.BaseColor, surface.Emission, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale


    output.WorldSpacePosition = input.positionWS;
    output.ObjectSpacePosition = TransformWorldToObject(input.positionWS);
    output.AbsoluteWorldSpacePosition = GetAbsolutePositionWS(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "SceneSelectionPass"
    Tags
    {
        "LightMode" = "SceneSelectionPass"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define SCENESELECTIONPASS 1
#define ALPHA_CLIP_THRESHOLD 1
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 ObjectSpacePosition;
 float3 WorldSpacePosition;
 float3 AbsoluteWorldSpacePosition;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float3 positionWS : INTERP1;
 float3 normalWS : INTERP2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

// unity-custom-func-begin
void AdvancedDissolveShaderGraphFunction_float(float2 UV, float3 PositionOS, float3 PositionWS, float3 PositionWS_Absolut, float3 NormalOS, float3 NormalWS, float Custom_Cutout, float4 Custom_Color, out float Value){
Value = 0;
}
// unity-custom-func-end

struct Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float
{
float3 ObjectSpaceNormal;
float3 WorldSpaceNormal;
float3 ObjectSpacePosition;
float3 WorldSpacePosition;
float3 AbsoluteWorldSpacePosition;
half4 uv0;
};

void SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float Vector1_9E44E7D0, float4 Color_d37717e22d9845eeb5507ed0b661e197, Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float IN, out float Out_3)
{
float4 _UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4 = IN.uv0;
float _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float = Vector1_9E44E7D0;
float4 _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4 = Color_d37717e22d9845eeb5507ed0b661e197;
float _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
AdvancedDissolveShaderGraphFunction_float((_UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4.xy), IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float, _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4, _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float);
Out_3 = _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float = float(1);
Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpaceNormal = IN.ObjectSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpaceNormal = IN.WorldSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpacePosition = IN.ObjectSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpacePosition = IN.WorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.AbsoluteWorldSpacePosition = IN.AbsoluteWorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.uv0 = IN.uv0;
float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float;
SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float(0), float4 (0, 0, 0, 1), _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float);
float _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
Unity_Add_float(_Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float, _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float);
surface.Alpha = _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
surface.AlphaClipThreshold = float(0.5);


//SceneSelectionPass
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale


    output.WorldSpacePosition = input.positionWS;
    output.ObjectSpacePosition = TransformWorldToObject(input.positionWS);
    output.AbsoluteWorldSpacePosition = GetAbsolutePositionWS(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "ScenePickingPass"
    Tags
    {
        "LightMode" = "Picking"
    }

// Render State
Cull Back

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define SCENEPICKINGPASS 1
#define ALPHA_CLIP_THRESHOLD 1
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 ObjectSpacePosition;
 float3 WorldSpacePosition;
 float3 AbsoluteWorldSpacePosition;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float3 positionWS : INTERP1;
 float3 normalWS : INTERP2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// unity-custom-func-begin
void AdvancedDissolveShaderGraphFunction_float(float2 UV, float3 PositionOS, float3 PositionWS, float3 PositionWS_Absolut, float3 NormalOS, float3 NormalWS, float Custom_Cutout, float4 Custom_Color, out float Value){
Value = 0;
}
// unity-custom-func-end

struct Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float
{
float3 ObjectSpaceNormal;
float3 WorldSpaceNormal;
float3 ObjectSpacePosition;
float3 WorldSpacePosition;
float3 AbsoluteWorldSpacePosition;
half4 uv0;
};

void SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float Vector1_9E44E7D0, float4 Color_d37717e22d9845eeb5507ed0b661e197, Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float IN, out float Out_3)
{
float4 _UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4 = IN.uv0;
float _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float = Vector1_9E44E7D0;
float4 _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4 = Color_d37717e22d9845eeb5507ed0b661e197;
float _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
AdvancedDissolveShaderGraphFunction_float((_UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4.xy), IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float, _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4, _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float);
Out_3 = _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float Alpha;
float AlphaClipThreshold;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_R_4_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.r;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_G_5_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.g;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_B_6_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.b;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_A_7_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.a;
float4 _Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4 = _Base;
UnityTexture2D _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_Custom_texture);
float _Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float = _tile;
float2 _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float.xx), float2 (0, 0), _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2);
float4 _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.tex, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.samplerstate, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2) );
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_R_4_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.r;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_G_5_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.g;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_B_6_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.b;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_A_7_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.a;
float _Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float = _color_or_texture;
float4 _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4;
Unity_Lerp_float4(_Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4, _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4, (_Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float.xxxx), _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4);
float4 _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4 = _armor;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.r;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.g;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.b;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_A_7_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.a;
float4 _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4, _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float.xxxx), _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4);
float4 _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4 = _metal;
float4 _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4, _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float.xxxx), _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4);
float4 _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4 = _Steel;
float4 _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4, _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float.xxxx), _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4);
float4 _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4 = _rocket;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.r;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.g;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.b;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_A_7_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.a;
float4 _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4, _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float.xxxx), _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4);
float4 _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4 = _cilindr;
float4 _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4, _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float.xxxx), _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4);
float4 _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4 = _eye;
float4 _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4, _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float.xxxx), _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4);
float4 _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4 = _decal;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.r;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.g;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.b;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_A_7_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.a;
float4 _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4, _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4, (_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float.xxxx), _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4);
float4 _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4 = _Curvature_1_C;
float _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float = _Curvature_1;
float _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float, _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float, _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float);
float4 _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4, _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4, (_Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float.xxxx), _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4);
float4 _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4 = _Curvature_2_C;
float _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float = _Curvature_2;
float _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float, _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float, _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float);
float4 _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4, _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4, (_Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float.xxxx), _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4);
float4 _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4, _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4, _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4);
float _Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float = float(1);
Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpaceNormal = IN.ObjectSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpaceNormal = IN.WorldSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpacePosition = IN.ObjectSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpacePosition = IN.WorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.AbsoluteWorldSpacePosition = IN.AbsoluteWorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.uv0 = IN.uv0;
float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float;
SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float(0), float4 (0, 0, 0, 1), _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float);
float _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
Unity_Add_float(_Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float, _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float);
surface.BaseColor = (_Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4.xyz);
surface.Alpha = _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
surface.AlphaClipThreshold = float(0.5);


//ScenePickingPass
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.BaseColor, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale


    output.WorldSpacePosition = input.positionWS;
    output.ObjectSpacePosition = TransformWorldToObject(input.positionWS);
    output.AbsoluteWorldSpacePosition = GetAbsolutePositionWS(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "Universal 2D"
    Tags
    {
        "LightMode" = "Universal2D"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_2D
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 ObjectSpacePosition;
 float3 WorldSpacePosition;
 float3 AbsoluteWorldSpacePosition;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float3 positionWS : INTERP1;
 float3 normalWS : INTERP2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// unity-custom-func-begin
void AdvancedDissolveShaderGraphFunction_float(float2 UV, float3 PositionOS, float3 PositionWS, float3 PositionWS_Absolut, float3 NormalOS, float3 NormalWS, float Custom_Cutout, float4 Custom_Color, out float Value){
Value = 0;
}
// unity-custom-func-end

struct Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float
{
float3 ObjectSpaceNormal;
float3 WorldSpaceNormal;
float3 ObjectSpacePosition;
float3 WorldSpacePosition;
float3 AbsoluteWorldSpacePosition;
half4 uv0;
};

void SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float Vector1_9E44E7D0, float4 Color_d37717e22d9845eeb5507ed0b661e197, Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float IN, out float Out_3)
{
float4 _UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4 = IN.uv0;
float _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float = Vector1_9E44E7D0;
float4 _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4 = Color_d37717e22d9845eeb5507ed0b661e197;
float _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
AdvancedDissolveShaderGraphFunction_float((_UV_0af11090dff4968fbefbff780ab3f959_Out_0_Vector4.xy), IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, _Property_2254a3efc4fcf082bc34b2ce5b131975_Out_0_Float, _Property_6d35f866e3e7457cb788755ca206532e_Out_0_Vector4, _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float);
Out_3 = _AdvancedDissolveShaderGraphFunctionCustomFunction_18f0160f9996fe8f938c567e2ad92b60_Value_7_Float;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float Alpha;
float AlphaClipThreshold;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_R_4_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.r;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_G_5_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.g;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_B_6_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.b;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_A_7_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.a;
float4 _Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4 = _Base;
UnityTexture2D _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_Custom_texture);
float _Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float = _tile;
float2 _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float.xx), float2 (0, 0), _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2);
float4 _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.tex, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.samplerstate, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2) );
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_R_4_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.r;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_G_5_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.g;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_B_6_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.b;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_A_7_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.a;
float _Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float = _color_or_texture;
float4 _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4;
Unity_Lerp_float4(_Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4, _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4, (_Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float.xxxx), _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4);
float4 _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4 = _armor;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.r;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.g;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.b;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_A_7_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.a;
float4 _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4, _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float.xxxx), _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4);
float4 _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4 = _metal;
float4 _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4, _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float.xxxx), _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4);
float4 _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4 = _Steel;
float4 _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4, _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float.xxxx), _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4);
float4 _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4 = _rocket;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.r;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.g;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.b;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_A_7_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.a;
float4 _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4, _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float.xxxx), _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4);
float4 _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4 = _cilindr;
float4 _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4, _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float.xxxx), _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4);
float4 _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4 = _eye;
float4 _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4, _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float.xxxx), _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4);
float4 _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4 = _decal;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.r;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.g;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.b;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_A_7_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.a;
float4 _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4, _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4, (_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float.xxxx), _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4);
float4 _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4 = _Curvature_1_C;
float _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float = _Curvature_1;
float _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float, _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float, _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float);
float4 _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4, _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4, (_Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float.xxxx), _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4);
float4 _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4 = _Curvature_2_C;
float _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float = _Curvature_2;
float _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float, _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float, _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float);
float4 _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4, _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4, (_Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float.xxxx), _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4);
float4 _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4, _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4, _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4);
float _Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float = float(1);
Bindings_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpaceNormal = IN.ObjectSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpaceNormal = IN.WorldSpaceNormal;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.ObjectSpacePosition = IN.ObjectSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.WorldSpacePosition = IN.WorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.AbsoluteWorldSpacePosition = IN.AbsoluteWorldSpacePosition;
_AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335.uv0 = IN.uv0;
float _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float;
SG_AdvancedDissolve_58cc1ed7edc36664e85cbe55fd29d527_float(float(0), float4 (0, 0, 0, 1), _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float);
float _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
Unity_Add_float(_Float_6cfe8a2028454827a77afec626fc06fb_Out_0_Float, _AdvancedDissolve_339423da0c0b4eca9985ef7a1ea30335_Out_3_Float, _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float);
surface.BaseColor = (_Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4.xyz);
surface.Alpha = _Add_84261bc9eb8f417dab06fa3a133ba296_Out_2_Float;
surface.AlphaClipThreshold = float(0.5);


//Universal2D
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.BaseColor, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale


    output.WorldSpacePosition = input.positionWS;
    output.ObjectSpacePosition = TransformWorldToObject(input.positionWS);
    output.AbsoluteWorldSpacePosition = GetAbsolutePositionWS(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBR2DPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
}
SubShader
{
Tags
{
// RenderPipeline: <None>
"RenderType"="Opaque"
"BuiltInMaterialType" = "Lit"
"Queue"="Geometry"
// DisableBatching: <None>
"ShaderGraphShader"="true"
"ShaderGraphTargetId"="BuiltInLitSubTarget"
}
Pass
{
    Name "BuiltIn Forward"
    Tags
    {
        "LightMode" = "ForwardBase"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma multi_compile_fwdbase
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _ADDITIONAL_OFF
#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile _ _SHADOWS_SOFT
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_FORWARD
#define BUILTIN_TARGET_API 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
 float4 fogFactorAndVertexLight;
 float4 shadowCoord;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 TangentSpaceNormal;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV : INTERP0;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP1;
#endif
 float4 tangentWS : INTERP2;
 float4 texCoord0 : INTERP3;
 float4 fogFactorAndVertexLight : INTERP4;
 float4 shadowCoord : INTERP5;
 float3 positionWS : INTERP6;
 float3 normalWS : INTERP7;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.shadowCoord.xyzw = input.shadowCoord;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.shadowCoord = input.shadowCoord.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_R_4_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.r;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_G_5_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.g;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_B_6_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.b;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_A_7_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.a;
float4 _Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4 = _Base;
UnityTexture2D _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_Custom_texture);
float _Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float = _tile;
float2 _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float.xx), float2 (0, 0), _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2);
float4 _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.tex, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.samplerstate, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2) );
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_R_4_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.r;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_G_5_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.g;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_B_6_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.b;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_A_7_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.a;
float _Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float = _color_or_texture;
float4 _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4;
Unity_Lerp_float4(_Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4, _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4, (_Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float.xxxx), _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4);
float4 _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4 = _armor;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.r;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.g;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.b;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_A_7_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.a;
float4 _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4, _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float.xxxx), _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4);
float4 _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4 = _metal;
float4 _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4, _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float.xxxx), _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4);
float4 _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4 = _Steel;
float4 _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4, _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float.xxxx), _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4);
float4 _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4 = _rocket;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.r;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.g;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.b;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_A_7_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.a;
float4 _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4, _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float.xxxx), _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4);
float4 _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4 = _cilindr;
float4 _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4, _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float.xxxx), _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4);
float4 _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4 = _eye;
float4 _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4, _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float.xxxx), _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4);
float4 _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4 = _decal;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.r;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.g;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.b;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_A_7_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.a;
float4 _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4, _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4, (_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float.xxxx), _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4);
float4 _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4 = _Curvature_1_C;
float _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float = _Curvature_1;
float _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float, _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float, _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float);
float4 _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4, _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4, (_Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float.xxxx), _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4);
float4 _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4 = _Curvature_2_C;
float _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float = _Curvature_2;
float _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float, _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float, _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float);
float4 _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4, _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4, (_Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float.xxxx), _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4);
float4 _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4, _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4, _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4);
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4);
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_R_4_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.r;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_G_5_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.g;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_B_6_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.b;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_A_7_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.a;
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_R_4_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.r;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_G_5_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.g;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_B_6_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.b;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_A_7_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.a;
float _Property_1f990134a755435faf10a57f4d75685b_Out_0_Float = _emis;
float4 _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4, (_Property_1f990134a755435faf10a57f4d75685b_Out_0_Float.xxxx), _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4);
float4 _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4 = _emis_1;
float4 _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4, _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4, _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4);
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_R_4_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.r;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_G_5_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.g;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_B_6_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.b;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_A_7_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.a;
float _Property_67edbdce71854da594b303ad6e95e154_Out_0_Float = _Roug;
float _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_G_5_Float, _Property_67edbdce71854da594b303ad6e95e154_Out_0_Float, _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float);
surface.BaseColor = (_Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4.xyz);
surface.NormalTS = (_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.xyz);
surface.Emission = (_Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4.xyz);
surface.Metallic = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_B_6_Float;
surface.Smoothness = _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float;
surface.Occlusion = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_R_4_Float;


//BuiltInForward
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.BaseColor, surface.Emission, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    



    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);



    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf

    result._ShadowCoord = varyings.shadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = varyings.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lmap.xy = varyings.lightmapUV;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf
    result.shadowCoord = surfVertex._ShadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = surfVertex.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lightmapUV = surfVertex.lmap.xy;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl"

ENDHLSL
}
Pass
{
    Name "BuiltIn ForwardAdd"
    Tags
    {
        "LightMode" = "ForwardAdd"
    }

// Render State
Blend SrcAlpha One, One One
ZWrite Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma multi_compile_fwdadd_fullshadows
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _ADDITIONAL_OFF
#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile _ _SHADOWS_SOFT
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_FORWARD_ADD
#define BUILTIN_TARGET_API 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
 float4 fogFactorAndVertexLight;
 float4 shadowCoord;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 TangentSpaceNormal;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV : INTERP0;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP1;
#endif
 float4 tangentWS : INTERP2;
 float4 texCoord0 : INTERP3;
 float4 fogFactorAndVertexLight : INTERP4;
 float4 shadowCoord : INTERP5;
 float3 positionWS : INTERP6;
 float3 normalWS : INTERP7;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.shadowCoord.xyzw = input.shadowCoord;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.shadowCoord = input.shadowCoord.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_R_4_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.r;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_G_5_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.g;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_B_6_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.b;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_A_7_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.a;
float4 _Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4 = _Base;
UnityTexture2D _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_Custom_texture);
float _Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float = _tile;
float2 _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float.xx), float2 (0, 0), _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2);
float4 _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.tex, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.samplerstate, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2) );
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_R_4_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.r;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_G_5_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.g;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_B_6_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.b;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_A_7_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.a;
float _Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float = _color_or_texture;
float4 _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4;
Unity_Lerp_float4(_Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4, _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4, (_Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float.xxxx), _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4);
float4 _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4 = _armor;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.r;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.g;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.b;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_A_7_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.a;
float4 _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4, _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float.xxxx), _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4);
float4 _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4 = _metal;
float4 _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4, _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float.xxxx), _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4);
float4 _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4 = _Steel;
float4 _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4, _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float.xxxx), _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4);
float4 _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4 = _rocket;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.r;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.g;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.b;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_A_7_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.a;
float4 _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4, _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float.xxxx), _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4);
float4 _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4 = _cilindr;
float4 _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4, _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float.xxxx), _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4);
float4 _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4 = _eye;
float4 _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4, _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float.xxxx), _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4);
float4 _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4 = _decal;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.r;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.g;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.b;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_A_7_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.a;
float4 _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4, _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4, (_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float.xxxx), _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4);
float4 _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4 = _Curvature_1_C;
float _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float = _Curvature_1;
float _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float, _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float, _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float);
float4 _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4, _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4, (_Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float.xxxx), _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4);
float4 _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4 = _Curvature_2_C;
float _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float = _Curvature_2;
float _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float, _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float, _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float);
float4 _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4, _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4, (_Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float.xxxx), _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4);
float4 _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4, _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4, _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4);
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4);
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_R_4_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.r;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_G_5_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.g;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_B_6_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.b;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_A_7_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.a;
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_R_4_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.r;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_G_5_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.g;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_B_6_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.b;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_A_7_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.a;
float _Property_1f990134a755435faf10a57f4d75685b_Out_0_Float = _emis;
float4 _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4, (_Property_1f990134a755435faf10a57f4d75685b_Out_0_Float.xxxx), _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4);
float4 _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4 = _emis_1;
float4 _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4, _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4, _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4);
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_R_4_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.r;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_G_5_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.g;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_B_6_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.b;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_A_7_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.a;
float _Property_67edbdce71854da594b303ad6e95e154_Out_0_Float = _Roug;
float _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_G_5_Float, _Property_67edbdce71854da594b303ad6e95e154_Out_0_Float, _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float);
surface.BaseColor = (_Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4.xyz);
surface.NormalTS = (_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.xyz);
surface.Emission = (_Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4.xyz);
surface.Metallic = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_B_6_Float;
surface.Smoothness = _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float;
surface.Occlusion = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_R_4_Float;


//BuiltInForwardAdd
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.BaseColor, surface.Emission, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    



    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);



    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf

    result._ShadowCoord = varyings.shadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = varyings.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lmap.xy = varyings.lightmapUV;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf
    result.shadowCoord = surfVertex._ShadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = surfVertex.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lightmapUV = surfVertex.lmap.xy;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRForwardAddPass.hlsl"

ENDHLSL
}
Pass
{
    Name "BuiltIn Deferred"
    Tags
    {
        "LightMode" = "Deferred"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 4.5
#pragma multi_compile_instancing
#pragma exclude_renderers nomrt
#pragma multi_compile_prepassfinal
#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _SHADOWS_SOFT
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
#pragma multi_compile _ _GBUFFER_NORMALS_OCT
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEFERRED
#define BUILTIN_TARGET_API 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
 float4 fogFactorAndVertexLight;
 float4 shadowCoord;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 TangentSpaceNormal;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV : INTERP0;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP1;
#endif
 float4 tangentWS : INTERP2;
 float4 texCoord0 : INTERP3;
 float4 fogFactorAndVertexLight : INTERP4;
 float4 shadowCoord : INTERP5;
 float3 positionWS : INTERP6;
 float3 normalWS : INTERP7;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.shadowCoord.xyzw = input.shadowCoord;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.shadowCoord = input.shadowCoord.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_R_4_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.r;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_G_5_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.g;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_B_6_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.b;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_A_7_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.a;
float4 _Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4 = _Base;
UnityTexture2D _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_Custom_texture);
float _Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float = _tile;
float2 _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float.xx), float2 (0, 0), _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2);
float4 _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.tex, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.samplerstate, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2) );
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_R_4_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.r;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_G_5_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.g;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_B_6_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.b;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_A_7_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.a;
float _Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float = _color_or_texture;
float4 _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4;
Unity_Lerp_float4(_Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4, _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4, (_Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float.xxxx), _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4);
float4 _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4 = _armor;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.r;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.g;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.b;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_A_7_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.a;
float4 _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4, _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float.xxxx), _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4);
float4 _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4 = _metal;
float4 _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4, _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float.xxxx), _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4);
float4 _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4 = _Steel;
float4 _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4, _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float.xxxx), _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4);
float4 _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4 = _rocket;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.r;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.g;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.b;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_A_7_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.a;
float4 _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4, _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float.xxxx), _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4);
float4 _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4 = _cilindr;
float4 _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4, _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float.xxxx), _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4);
float4 _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4 = _eye;
float4 _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4, _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float.xxxx), _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4);
float4 _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4 = _decal;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.r;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.g;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.b;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_A_7_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.a;
float4 _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4, _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4, (_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float.xxxx), _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4);
float4 _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4 = _Curvature_1_C;
float _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float = _Curvature_1;
float _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float, _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float, _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float);
float4 _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4, _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4, (_Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float.xxxx), _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4);
float4 _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4 = _Curvature_2_C;
float _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float = _Curvature_2;
float _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float, _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float, _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float);
float4 _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4, _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4, (_Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float.xxxx), _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4);
float4 _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4, _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4, _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4);
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4);
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_R_4_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.r;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_G_5_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.g;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_B_6_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.b;
float _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_A_7_Float = _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.a;
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_R_4_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.r;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_G_5_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.g;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_B_6_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.b;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_A_7_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.a;
float _Property_1f990134a755435faf10a57f4d75685b_Out_0_Float = _emis;
float4 _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4, (_Property_1f990134a755435faf10a57f4d75685b_Out_0_Float.xxxx), _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4);
float4 _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4 = _emis_1;
float4 _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4, _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4, _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4);
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_R_4_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.r;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_G_5_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.g;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_B_6_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.b;
float _SampleTexture2D_eb316884c098474f94cbfc21291558c0_A_7_Float = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_RGBA_0_Vector4.a;
float _Property_67edbdce71854da594b303ad6e95e154_Out_0_Float = _Roug;
float _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_G_5_Float, _Property_67edbdce71854da594b303ad6e95e154_Out_0_Float, _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float);
surface.BaseColor = (_Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4.xyz);
surface.NormalTS = (_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_RGBA_0_Vector4.xyz);
surface.Emission = (_Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4.xyz);
surface.Metallic = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_B_6_Float;
surface.Smoothness = _Multiply_0fff1e19213f43e8ada7a5d41b27479f_Out_2_Float;
surface.Occlusion = _SampleTexture2D_eb316884c098474f94cbfc21291558c0_R_4_Float;


//BuiltInDeferred
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.BaseColor, surface.Emission, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    



    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);



    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf

    result._ShadowCoord = varyings.shadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = varyings.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lmap.xy = varyings.lightmapUV;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf
    result.shadowCoord = surfVertex._ShadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = surfVertex.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lightmapUV = surfVertex.lmap.xy;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRDeferredPass.hlsl"

ENDHLSL
}
Pass
{
    Name "ShadowCaster"
    Tags
    {
        "LightMode" = "ShadowCaster"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On
ColorMask 0

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma multi_compile_shadowcaster
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_SHADOWCASTER
#define BUILTIN_TARGET_API 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;


//ShadowCaster
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"

ENDHLSL
}
Pass
{
    Name "DepthOnly"
    Tags
    {
        "LightMode" = "DepthOnly"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On
ColorMask 0

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define BUILTIN_TARGET_API 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;


//DepthOnly
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

ENDHLSL
}
Pass
{
    Name "Meta"
    Tags
    {
        "LightMode" = "Meta"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define VARYINGS_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_META
#define BUILTIN_TARGET_API 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_META_PASS
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 Emission;
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_R_4_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.r;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_G_5_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.g;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_B_6_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.b;
float _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_A_7_Float = _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4.a;
float4 _Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4 = _Base;
UnityTexture2D _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_Custom_texture);
float _Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float = _tile;
float2 _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_5e90b7651c6d4ba499eb1e615dc88d58_Out_0_Float.xx), float2 (0, 0), _TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2);
float4 _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.tex, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.samplerstate, _Property_2c2411017e6d4edbad1e31ded917d11f_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_7a00707e018b4fea967262ad88890a2d_Out_3_Vector2) );
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_R_4_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.r;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_G_5_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.g;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_B_6_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.b;
float _SampleTexture2D_6f0e37576f5a438781aa488def85a258_A_7_Float = _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4.a;
float _Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float = _color_or_texture;
float4 _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4;
Unity_Lerp_float4(_Property_6bc537ca4d3a4f54a60259e8b7d59f85_Out_0_Vector4, _SampleTexture2D_6f0e37576f5a438781aa488def85a258_RGBA_0_Vector4, (_Property_b764bfbd478b461794f28da038b9ef95_Out_0_Float.xxxx), _Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4);
float4 _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4 = _armor;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.r;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.g;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.b;
float _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_A_7_Float = _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_RGBA_0_Vector4.a;
float4 _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2cfd79fd983e49489704efb116aab95b_Out_3_Vector4, _Property_050ac6e722974618b321b29a38153f8d_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_R_4_Float.xxxx), _Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4);
float4 _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4 = _metal;
float4 _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_0659c0ca243b4f1385d5b7422b048f58_Out_3_Vector4, _Property_fd7a9cbe2b1348e8a0f237beea5c1cba_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_G_5_Float.xxxx), _Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4);
float4 _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4 = _Steel;
float4 _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_2c2bea5346924c67b3a8937da81e12b5_Out_3_Vector4, _Property_42ac6bbad280415693446b6be0b4b75e_Out_0_Vector4, (_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_B_6_Float.xxxx), _Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4);
float4 _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4 = _rocket;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.r;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.g;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.b;
float _SampleTexture2D_488dd7083251451f90afce59e05429a8_A_7_Float = _SampleTexture2D_488dd7083251451f90afce59e05429a8_RGBA_0_Vector4.a;
float4 _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_3071811658554a2aa004b1f87c5826ec_Out_3_Vector4, _Property_74c22e504f0844cda8167a3f62bd914d_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_R_4_Float.xxxx), _Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4);
float4 _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4 = _cilindr;
float4 _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_36642b6ef5344fbbaec773932a878a22_Out_3_Vector4, _Property_49796a0d09a04f5586d720770c7750dc_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_G_5_Float.xxxx), _Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4);
float4 _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4 = _eye;
float4 _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8536fc9ac4c6441ea915a98fcbe6c15c_Out_3_Vector4, _Property_d7161ccae2804a68947c30467f213fb7_Out_0_Vector4, (_SampleTexture2D_488dd7083251451f90afce59e05429a8_B_6_Float.xxxx), _Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4);
float4 _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4 = _decal;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.r;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.g;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.b;
float _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_A_7_Float = _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_RGBA_0_Vector4.a;
float4 _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_8da3bc36d2aa4dcc8c3ea9d6fb341134_Out_3_Vector4, _Property_a55f6458cf73436e94a57bfae8ae3fec_Out_0_Vector4, (_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_R_4_Float.xxxx), _Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4);
float4 _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4 = _Curvature_1_C;
float _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float = _Curvature_1;
float _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_G_5_Float, _Property_d94dce753a4d4fd495f00812d5af1ecd_Out_0_Float, _Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float);
float4 _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_dc299178cd7a422690c1ce8ddc08c186_Out_3_Vector4, _Property_30542ba33dd14677b250b4b32e02e77a_Out_0_Vector4, (_Multiply_3775d354989247b0a127878da2cc5584_Out_2_Float.xxxx), _Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4);
float4 _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4 = _Curvature_2_C;
float _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float = _Curvature_2;
float _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_B_6_Float, _Property_2ad4d7ba3d514e0b99af3ad44f0812c6_Out_0_Float, _Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float);
float4 _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4;
Unity_Lerp_float4(_Lerp_264f066335874f72953d38d974e53a17_Out_3_Vector4, _Property_071ba34bce7f478f9346a2439de9c9ea_Out_0_Vector4, (_Multiply_86c59f1b312a4cc6b21cc3df2bba4b32_Out_2_Float.xxxx), _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4);
float4 _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_RGBA_0_Vector4, _Lerp_cb68d601c8774134ad409c218c1d0bc4_Out_3_Vector4, _Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4);
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D).GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_R_4_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.r;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_G_5_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.g;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_B_6_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.b;
float _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_A_7_Float = _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4.a;
float _Property_1f990134a755435faf10a57f4d75685b_Out_0_Float = _emis;
float4 _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_RGBA_0_Vector4, (_Property_1f990134a755435faf10a57f4d75685b_Out_0_Float.xxxx), _Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4);
float4 _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4 = _emis_1;
float4 _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_885068c5add54628851ba9c24f6ad9e4_Out_2_Vector4, _Property_8dd505014849468b99b80ba3f17ef7dc_Out_0_Vector4, _Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4);
surface.BaseColor = (_Multiply_11748638395f43358c839ecba461e198_Out_2_Vector4.xyz);
surface.Emission = (_Multiply_11ccbc04d56c45ec89ee927d2342c648_Out_2_Vector4.xyz);


//Unknown
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.BaseColor, surface.Emission, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.texcoord2  = attributes.uv2;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl"

ENDHLSL
}
Pass
{
    Name "SceneSelectionPass"
    Tags
    {
        "LightMode" = "SceneSelectionPass"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SceneSelectionPass
#define BUILTIN_TARGET_API 1
#define SCENESELECTIONPASS 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;


//SceneSelectionPass
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

ENDHLSL
}
Pass
{
    Name "ScenePickingPass"
    Tags
    {
        "LightMode" = "Picking"
    }

// Render State
Cull Back

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS ScenePickingPass
#define BUILTIN_TARGET_API 1
#define SCENEPICKINGPASS 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties


//Advanced Dissolve Keywords Start///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#pragma shader_feature_local   _AD_STATE_ENABLED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_BASE_ALPHA				  _AD_CUTOUT_STANDARD_SOURCE_CUSTOM_MAP                     _AD_CUTOUT_STANDARD_SOURCE_TWO_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_THREE_CUSTOM_MAPS _AD_CUTOUT_STANDARD_SOURCE_USER_DEFINED
#pragma shader_feature_local _ _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_TRIPLANAR _AD_CUTOUT_STANDARD_SOURCE_MAPS_MAPPING_TYPE_SCREEN_SPACE
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_TYPE_XYZ						  _AD_CUTOUT_GEOMETRIC_TYPE_PLANE                           _AD_CUTOUT_GEOMETRIC_TYPE_SPHERE           _AD_CUTOUT_GEOMETRIC_TYPE_CUBE               _AD_CUTOUT_GEOMETRIC_TYPE_CAPSULE       _AD_CUTOUT_GEOMETRIC_TYPE_CONE_SMOOTH
#pragma shader_feature_local _ _AD_CUTOUT_GEOMETRIC_COUNT_TWO					      _AD_CUTOUT_GEOMETRIC_COUNT_THREE                          _AD_CUTOUT_GEOMETRIC_COUNT_FOUR
#pragma shader_feature_local _ _AD_EDGE_BASE_SOURCE_CUTOUT_STANDARD                   _AD_EDGE_BASE_SOURCE_CUTOUT_GEOMETRIC                     _AD_EDGE_BASE_SOURCE_ALL
#pragma shader_feature_local _ _AD_EDGE_ADDITIONAL_COLOR_BASE_COLOR                   _AD_EDGE_ADDITIONAL_COLOR_CUSTOM_MAP                      _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_MAP     _AD_EDGE_ADDITIONAL_COLOR_GRADIENT_COLOR     _AD_EDGE_ADDITIONAL_COLOR_USER_DEFINED
#pragma shader_feature_local _ _AD_GLOBAL_CONTROL_ID_ONE                              _AD_GLOBAL_CONTROL_ID_TWO                                 _AD_GLOBAL_CONTROL_ID_THREE                _AD_GLOBAL_CONTROL_ID_FOUR
//Advanced Dissolve Keywords End/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


#define ADVANCED_DISSOLVE_SHADER_GRAPH
#define ADVANCED_DISSOLVE_UNIVERSAL_RENDER_PIPELINE
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc"
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D_TexelSize;
float4 _SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D_TexelSize;
float _Roug;
float4 _Base;
float4 _Custom_texture_TexelSize;
float _tile;
float _color_or_texture;
float _emis;
float4 _emis_1;
float4 _rocket;
float4 _cilindr;
float4 _eye;
float4 _metal;
float4 _Steel;
float4 _armor;
float4 _decal;
float _Curvature_2;
float _Curvature_1;
float4 _Curvature_1_C;
float4 _Curvature_2_C;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_05f9125098024e5187f5e8c3500e6666_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_067e96f2a73842c4928ade1fe6061fc3_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_32f4d095d09949a0a20575d5634ab2c9_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_488dd7083251451f90afce59e05429a8_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_6178afb1f9ba4ffba5543ed193825917_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_bf052d02051d4b929da3e62268e635c4_Texture_1_Texture2D);
TEXTURE2D(_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
SAMPLER(sampler_SampleTexture2D_eb316884c098474f94cbfc21291558c0_Texture_1_Texture2D);
TEXTURE2D(_Custom_texture);
SAMPLER(sampler_Custom_texture);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
};



//Advanced Dissolve
#include "Assets/Resources_GoogleDrive/Tool/Amazing Assets/Advanced Dissolve/Shaders/cginc/Core.cginc"


SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;


//ScenePickingPass
AdvancedDissolveShaderGraph(IN.uv0.xy, IN.ObjectSpacePosition, IN.WorldSpacePosition, IN.AbsoluteWorldSpacePosition, IN.ObjectSpaceNormal, IN.WorldSpaceNormal, float(0), 1, surface.Alpha, surface.AlphaClipThreshold);


return surface;

}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

ENDHLSL
}
}
CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
CustomEditorForRenderPipeline "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInLitGUI" ""
//CustomEditorForRenderPipeline "UnityEditor.ShaderGraphLitGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
CustomEditorForRenderPipeline "AmazingAssets.AdvancedDissolve.Editor.ShaderGraph.ShaderGraphLitGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
FallBack "Hidden/Shader Graph/FallbackError"
}

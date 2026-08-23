// Made with Amplify Shader Editor v1.9.9
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Custom/Frame UI Shader Detailed Rotation"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        _TextureMask( "Texture Mask", 2D ) = "white" {}
        _Opacity( "Opacity", Float ) = 1
        _WindAllSpeed( "Wind All Speed", Float ) = 1
        _EmissiveIntensity( "Emissive Intensity", Float ) = 1
        _Mask_Power( "Mask_Power", Float ) = 1
        _Mask_Multiply( "Mask_Multiply", Float ) = 1
        [Header(Header(Rotate Texture))] _RotatingTexture( "Rotating Texture", 2D ) = "white" {}
        [Toggle( _INVERTROTATION_ON )] _InvertRotation( "Invert Rotation", Float ) = 0
        _RotationMaskPower( "Rotation Mask Power", Range( 1, 10 ) ) = 1.5
        _TimeSpeedRotation( "Time Speed Rotation", Range( 0, 5 ) ) = 1
        [Header(Noise Setting)] _NoisesOpacityBoost( "Noises Opacity Boost", Float ) = 1
        _Noises_Multiply( "Noises_Multiply", Float ) = 5
        _Noises_Power( "Noises_Power", Float ) = 1
        [Header(Header(Noise Texture 1))] _Noise_01_Texture( "Noise_01_Texture", 2D ) = "white" {}
        _Noise_01_Scale( "Noise_01_Scale", Vector ) = ( 0.8, 0.8, 0, 0 )
        _Noise_01_Speed( "Noise_01_Speed", Vector ) = ( 0.5, 0.5, 0, 0 )
        [Header(Noise Texture 2)] _Noise_02_Texture( "Noise_02_Texture", 2D ) = "white" {}
        _Noise_02_Scale( "Noise_02_Scale", Vector ) = ( 1, 1, 0, 0 )
        _Noise_02_Speed( "Noise_02_Speed", Vector ) = ( -0.2, 0.4, 0, 0 )
        [Header(Distortion Mask Texture)] _DistortionMaskTexture( "Distortion Mask Texture", 2D ) = "white" {}
        _DistortionMaskIntensity( "Distortion Mask Intensity", Float ) = 1
        _DistortionMaskScale( "Distortion Mask Scale", Vector ) = ( 1, 1, 0, 0 )
        [Header(Distortion Texture)] _NoiseDistortion_Texture( "NoiseDistortion_Texture", 2D ) = "white" {}
        _DistortionAmount( "Distortion Amount", Float ) = 1
        _NoiseDistortion_Scale( "NoiseDistortion_Scale", Vector ) = ( 1, 1, 0, 0 )
        _NoiseDistortion_Speed( "NoiseDistortion_Speed", Vector ) = ( -0.3, -0.3, 0, 0 )
        _FillColor( "Fill", Color ) = ( 0, 0, 0, 0 )
        _StrokeColor( "Stroke Color", Color ) = ( 1, 0, 0, 0 )
        [Enum(UnityEngine.Rendering.BlendMode)] _Src( "Src", Float ) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst( "Dst", Float ) = 1

    }

    SubShader
    {
		LOD 0

        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
        	Ref [_Stencil]
        	ReadMask [_StencilReadMask]
        	WriteMask [_StencilWriteMask]
        	Comp [_StencilComp]
        	Pass [_StencilOp]
        }


        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_Src] [_Dst]
        ColorMask [_ColorMask]

        
        Pass
        {
            Name "Default"
        CGPROGRAM
            #define ASE_VERSION 19900

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityShaderVariables.cginc"
            #define ASE_NEEDS_TEXTURE_COORDINATES0
            #define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
            #pragma shader_feature_local _INVERTROTATION_ON


            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4  mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
                
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            uniform float _Src;
            uniform float _Dst;
            uniform float4 _StrokeColor;
            uniform sampler2D _Noise_01_Texture;
            uniform float _WindAllSpeed;
            uniform float2 _Noise_01_Speed;
            uniform float2 _Noise_01_Scale;
            uniform sampler2D _NoiseDistortion_Texture;
            uniform float2 _NoiseDistortion_Speed;
            uniform float2 _NoiseDistortion_Scale;
            uniform float _DistortionAmount;
            uniform sampler2D _Noise_02_Texture;
            uniform float2 _Noise_02_Speed;
            uniform float2 _Noise_02_Scale;
            uniform float _Noises_Power;
            uniform float _Noises_Multiply;
            uniform float _NoisesOpacityBoost;
            uniform sampler2D _TextureMask;
            uniform sampler2D _DistortionMaskTexture;
            uniform float2 _DistortionMaskScale;
            uniform float _DistortionMaskIntensity;
            uniform sampler2D _RotatingTexture;
            uniform float _TimeSpeedRotation;
            uniform float _RotationMaskPower;
            uniform float _Opacity;
            uniform float _Mask_Power;
            uniform float _Mask_Multiply;
            uniform float4 _FillColor;
            uniform float _EmissiveIntensity;


            v2f vert(appdata_t v )
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                

                v.vertex.xyz +=  float3( 0, 0, 0 ) ;

                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                OUT.texcoord = v.texcoord;
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN ) : SV_Target
            {
                //Round up the alpha color coming from the interpolator (to 1.0/256.0 steps)
                //The incoming alpha could have numerical instability, which makes it very sensible to
                //HDR color transparency blend, when it blends with the world's texture.
                const half alphaPrecision = half(0xff);
                const half invAlphaPrecision = half(1.0/alphaPrecision);
                IN.color.a = round(IN.color.a * alphaPrecision)*invAlphaPrecision;

                float windSpeed819 = ( _WindAllSpeed * _Time.y );
                float2 texCoord832 = IN.texcoord.xy * float2( 1,1 ) + float2( 0,0 );
                float2 panner843 = ( windSpeed819 * _Noise_01_Speed + ( texCoord832 * _Noise_01_Scale ));
                float2 texCoord820 = IN.texcoord.xy * float2( 1,1 ) + float2( 0,0 );
                float2 panner824 = ( windSpeed819 * _NoiseDistortion_Speed + ( texCoord820 * _NoiseDistortion_Scale ));
                float Distortion839 = ( ( tex2D( _NoiseDistortion_Texture, panner824 ).r * 0.1 ) * _DistortionAmount );
                float2 texCoord830 = IN.texcoord.xy * float2( 1,1 ) + float2( 0,0 );
                float2 panner841 = ( windSpeed819 * _Noise_02_Speed + ( texCoord830 * _Noise_02_Scale ));
                float noises876 = saturate( ( pow( ( tex2D( _Noise_01_Texture, ( panner843 + Distortion839 ) ).r * tex2D( _Noise_02_Texture, ( panner841 + Distortion839 ) ).r ) , _Noises_Power ) * _Noises_Multiply ) );
                float2 texCoord849 = IN.texcoord.xy * float2( 1,1 ) + float2( 0,0 );
                float2 texCoord861 = IN.texcoord.xy * float2( 1,1 ) + float2( 0,0 );
                float2 texCoord913 = IN.texcoord.xy * float2( 1,1 ) + float2( 0,0 );
                float4 appendResult920 = (float4(( 1.0 - texCoord913.x ) , texCoord913.y , 0.0 , 0.0));
                #ifdef _INVERTROTATION_ON
                float4 staticSwitch924 = appendResult920;
                #else
                float4 staticSwitch924 = float4( texCoord913, 0.0 , 0.0 );
                #endif
                float mulTime919 = _Time.y * ( _TimeSpeedRotation * 3.0 );
                float cos943 = cos( mulTime919 );
                float sin943 = sin( mulTime919 );
                float2 rotator943 = mul( staticSwitch924.xy - float2( 0.5,0.5 ) , float2x2( cos943 , -sin943 , sin943 , cos943 )) + float2( 0.5,0.5 );
                float clampResult965 = clamp( pow( pow( tex2D( _RotatingTexture, rotator943 ).r , 1.1 ) , _RotationMaskPower ) , 0.0 , 1.0 );
                float temp_output_888_0 = ( ( noises876 * _NoisesOpacityBoost ) * saturate( ( pow( ( ( tex2D( _TextureMask, ( ( tex2D( _DistortionMaskTexture, ( texCoord849 * _DistortionMaskScale ) ).r * ( Distortion839 * _DistortionMaskIntensity ) ) + texCoord861 ) ).r * clampResult965 ) * _Opacity ) , _Mask_Power ) * _Mask_Multiply ) ) );
                float4 lerpResult899 = lerp( _StrokeColor , float4( 0,0,0,0 ) , temp_output_888_0);
                float4 lerpResult895 = lerp( lerpResult899 , _FillColor , temp_output_888_0);
                

                half4 color = ( lerpResult895 * _EmissiveIntensity );

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                color.rgb *= color.a;

                return color;
            }
        ENDCG
        }
    }
    CustomEditor "AmplifyShaderEditor.MaterialInspector"
	
	Fallback Off
}
/*ASEBEGIN
Version=19900
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;804;-1392,768;Inherit;False;786;417;Register Wind Speed;4;819;817;816;815;;0,0,0,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;815;-1344,816;Inherit;False;Property;_WindAllSpeed;Wind All Speed;2;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;816;-1344,1072;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;805;-681.0283,-158.5842;Inherit;False;2502.5;663.612;Heat Haze;12;839;829;828;827;826;825;824;823;822;821;820;818;;0,0,0,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;817;-1088,816;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;820;-473.0283,-14.58423;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;818;-473.0283,113.4158;Inherit;False;Property;_NoiseDistortion_Scale;NoiseDistortion_Scale;24;0;Create;True;0;0;0;False;0;False;1,1;1.5,1.5;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;819;-832,816;Inherit;False;windSpeed;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;911;-3088,1552;Inherit;False;2218.535;1016.511;Rotate;13;943;938;931;924;920;916;913;919;915;914;917;912;948;Rotate;1,1,1,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;821;422.9717,241.4158;Inherit;False;819;windSpeed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;822;38.97168,113.4158;Inherit;False;Property;_NoiseDistortion_Speed;NoiseDistortion_Speed;25;0;Create;True;0;0;0;False;0;False;-0.3,-0.3;0,-0.02;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;823;-217.0283,-14.58423;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;824;422.9717,-14.58423;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;913;-3008,1952;Inherit;True;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;825;1062.972,113.4158;Inherit;False;Constant;_Float0;Float 0;8;0;Create;True;0;0;0;False;0;False;0.1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;826;678.9717,-14.58423;Inherit;True;Property;_NoiseDistortion_Texture;NoiseDistortion_Texture;22;0;Create;True;0;0;0;False;1;Header(Distortion Texture);False;-1;None;33d4a85877826c14d83910d65644473a;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;916;-2688,2080;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;914;-2992,2352;Float;False;Property;_TimeSpeedRotation;Time Speed Rotation;9;0;Create;True;0;0;0;False;0;False;1;0.2;0;5;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;806;-500.5765,2724.445;Inherit;False;2997.113;1074.221;Noises;25;876;863;860;857;856;852;850;848;847;846;845;844;843;842;841;840;838;836;835;834;831;830;837;833;832;;0,0,0,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;827;1318.972,113.4158;Inherit;False;Property;_DistortionAmount;Distortion Amount;23;0;Create;True;0;0;0;False;0;False;1;0.86;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;828;1062.972,-14.58423;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;915;-2672,2368;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;3;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;920;-2480,2096;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;829;1318.972,-14.58423;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;830;-404.5765,3348.445;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;831;-404.5765,3476.445;Inherit;False;Property;_Noise_02_Scale;Noise_02_Scale;17;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;807;-521.0283,961.4158;Inherit;False;980;550;Distortion Mask;8;875;859;855;854;851;849;905;904;;0,0,0,1;0;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;833;-464,2960;Inherit;False;Property;_Noise_01_Scale;Noise_01_Scale;14;0;Create;True;0;0;0;False;0;False;0.8,0.8;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;832;-480,2784;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleTimeNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;919;-2496,2352;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;924;-2288,1952;Float;False;Property;_InvertRotation;Invert Rotation;7;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT4;0,0,0,0;False;0;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;4;FLOAT4;0,0,0,0;False;5;FLOAT4;0,0,0,0;False;6;FLOAT4;0,0,0,0;False;7;FLOAT4;0,0,0,0;False;8;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;839;1574.972,-14.58423;Inherit;False;Distortion;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;834;-148.5762,3348.445;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;835;235.4241,3604.445;Inherit;False;819;windSpeed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;836;-20.57591,3476.445;Inherit;False;Property;_Noise_02_Speed;Noise_02_Speed;18;0;Create;True;0;0;0;False;0;False;-0.2,0.4;-0.02,-0.2;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;838;-20.57591,2964.445;Inherit;False;Property;_Noise_01_Speed;Noise_01_Speed;15;0;Create;True;0;0;0;False;0;False;0.5,0.5;0.05,-0.2;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;840;235.4241,3092.445;Inherit;False;819;windSpeed;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;837;-160,2816;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;904;-496,1120;Inherit;False;Property;_DistortionMaskScale;Distortion Mask Scale;21;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;849;-496,992;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;931;-1776,1968;Inherit;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;938;-1968,2064;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;912;-1952,1632;Float;True;Property;_RotatingTexture;Rotating Texture;6;1;[Header];Create;True;1;Header(Rotate Texture);0;0;False;0;False;None;1f4af4eb67399c24b8192824db9d9d28;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.PannerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;841;235.4241,3348.445;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;842;491.4243,3604.445;Inherit;False;839;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;843;235.4241,2836.445;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;844;491.4243,3092.445;Inherit;False;839;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;875;-473.0283,1265.416;Inherit;False;839;Distortion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;851;-473.0283,1393.416;Inherit;False;Property;_DistortionMaskIntensity;Distortion Mask Intensity;20;0;Create;True;0;0;0;False;0;False;1;-0.93;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;905;-266.6944,1004.524;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;808;-521.0283,2115.245;Inherit;False;2391;470;Flame Mask;12;874;871;867;866;865;862;861;853;858;864;908;909;;0,0,0,1;0;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;917;-1552,1744;Inherit;False;1;0;SAMPLER2D;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.RotatorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;943;-1600,1888;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;845;491.4243,3348.445;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;846;491.4243,2836.445;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;968;-480,1648;Inherit;False;1095.691;297.1073;Base Alpha Setting;4;967;966;965;964;Base Alpha Setting;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;855;38.97168,1265.416;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;853;-448,2208;Inherit;False;Constant;_Mask_Scale;Mask_Scale;2;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;858;-432,2400;Inherit;False;Constant;_Mask_Offset;Mask_Offset;1;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;854;-89.02832,1009.416;Inherit;True;Property;_DistortionMaskTexture;Distortion Mask Texture;19;0;Create;True;0;0;0;False;1;Header(Distortion Mask Texture);False;-1;None;012414b869a3f3d42a6e42e4a211d688;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;948;-1376,1856;Inherit;True;Property;_T_Rotate_Alpha_Sharp;T_Rotate_Alpha_Sharp;0;0;Create;True;0;0;0;False;0;False;-1;ea4e7e49c16b5e8429bf4eae36b10a70;ea4e7e49c16b5e8429bf4eae36b10a70;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;848;747.4243,2836.445;Inherit;True;Property;_Noise_01_Texture;Noise_01_Texture;13;1;[Header];Create;True;1;Header(Noise Texture 1);0;0;False;0;False;-1;None;3128cd3ace6d4e64498af5e1c0954e06;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;847;747.4243,3348.445;Inherit;True;Property;_Noise_02_Texture;Noise_02_Texture;16;0;Create;True;0;0;0;False;1;Header(Noise Texture 2);False;-1;None;da4bab26c47415a459d1d741b51b03d6;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;859;294.9717,1009.416;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;861;-112,2240;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0.28,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PowerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;964;-416,1776;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;967;-208,1856;Float;False;Property;_RotationMaskPower;Rotation Mask Power;8;0;Create;True;0;0;0;False;0;False;1.5;2.37;1;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;850;1131.424,2836.445;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;852;1387.425,2964.445;Inherit;False;Property;_Noises_Power;Noises_Power;12;0;Create;True;0;0;0;False;0;False;1;1.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;862;384,2368;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;907;461.0694,2091.676;Inherit;True;Property;_TextureMask;Texture Mask;0;0;Create;True;0;0;0;False;0;False;None;2136e1bf44c86784f81d2e769f53074f;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.PowerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;966;96,1776;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;856;1643.425,2964.445;Inherit;False;Property;_Noises_Multiply;Noises_Multiply;11;0;Create;True;0;0;0;False;0;False;5;4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;857;1387.425,2836.445;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;865;680.9174,2264.422;Inherit;True;Property;_Mask_Texture;Mask_Texture;5;0;Create;True;0;0;0;False;0;False;-1;None;48dc7bc18268b924c8348d314decc5da;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;0,0,0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ClampOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;965;464,1776;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;860;1643.425,2836.445;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;909;848,2480;Inherit;False;Property;_Opacity;Opacity;1;0;Create;True;0;0;0;False;0;False;1;1.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;910;912,1936;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;863;1899.425,2836.445;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;864;1056,2272;Inherit;False;Property;_Mask_Power;Mask_Power;4;0;Create;True;0;0;0;False;0;False;1;1.3;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;908;1072,2432;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;867;1446.972,2291.245;Inherit;False;Property;_Mask_Multiply;Mask_Multiply;5;0;Create;True;0;0;0;False;0;False;1;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;876;2155.424,2836.445;Inherit;False;noises;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;866;1264,2176;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;871;1446.972,2163.245;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;870;2123.598,2280.736;Inherit;False;876;noises;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;868;2071.032,2398.073;Inherit;False;Property;_NoisesOpacityBoost;Noises Opacity Boost;10;0;Create;True;0;0;0;False;1;Header(Noise Setting);False;1;2.13;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;874;1702.972,2163.245;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;872;2351.572,2312.544;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;888;2553.194,2125.707;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;898;2153.397,1865.86;Inherit;False;Property;_StrokeColor;Stroke Color;27;0;Create;True;0;0;0;False;0;False;1,0,0,0;1,0,0.06274509,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;899;2756.836,1920.964;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;897;2767.405,1721.232;Inherit;False;Property;_FillColor;Fill;26;0;Create;True;0;0;0;False;0;False;0,0,0,0;1,0.206422,0,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;895;3227.593,2067.395;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;896;3317.265,1859.566;Inherit;False;Property;_EmissiveIntensity;Emissive Intensity;3;0;Create;True;0;0;0;False;0;False;1;1.91;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;900;3558.306,2052.553;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;901;3844.952,2490.131;Inherit;False;Property;_Src;Src;28;1;[Enum];Create;True;0;0;1;UnityEngine.Rendering.BlendMode;True;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;902;3852.639,2584.618;Inherit;False;Property;_Dst;Dst;29;1;[Enum];Create;True;0;0;1;UnityEngine.Rendering.BlendMode;True;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;906;3836.04,2082.142;Float;False;True;-1;2;AmplifyShaderEditor.MaterialInspector;0;3;Custom/Frame UI Shader Detailed Rotation;5056123faa0c79b47ab6ad7e8bf059a4;True;Default;0;0;Default;2;True;True;4;1;True;_Src;1;True;_Dst;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;True;True;True;True;True;0;True;_ColorMask;False;False;False;False;False;False;False;True;True;0;True;_Stencil;255;True;_StencilReadMask;255;True;_StencilWriteMask;0;True;_StencilComp;0;True;_StencilOp;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;0;True;unity_GUIZTestMode;False;True;5;Queue=Transparent=Queue=0;IgnoreProjector=True;RenderType=Transparent=RenderType;PreviewType=Plane;CanUseSpriteAtlas=True;False;False;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;3;False;0;;0;0;Standard;0;0;1;True;False;;False;0
WireConnection;817;0;815;0
WireConnection;817;1;816;0
WireConnection;819;0;817;0
WireConnection;823;0;820;0
WireConnection;823;1;818;0
WireConnection;824;0;823;0
WireConnection;824;2;822;0
WireConnection;824;1;821;0
WireConnection;826;1;824;0
WireConnection;916;0;913;1
WireConnection;828;0;826;1
WireConnection;828;1;825;0
WireConnection;915;0;914;0
WireConnection;920;0;916;0
WireConnection;920;1;913;2
WireConnection;829;0;828;0
WireConnection;829;1;827;0
WireConnection;919;0;915;0
WireConnection;924;1;913;0
WireConnection;924;0;920;0
WireConnection;839;0;829;0
WireConnection;834;0;830;0
WireConnection;834;1;831;0
WireConnection;837;0;832;0
WireConnection;837;1;833;0
WireConnection;931;0;924;0
WireConnection;938;0;919;0
WireConnection;841;0;834;0
WireConnection;841;2;836;0
WireConnection;841;1;835;0
WireConnection;843;0;837;0
WireConnection;843;2;838;0
WireConnection;843;1;840;0
WireConnection;905;0;849;0
WireConnection;905;1;904;0
WireConnection;917;0;912;0
WireConnection;943;0;931;0
WireConnection;943;2;938;0
WireConnection;845;0;841;0
WireConnection;845;1;842;0
WireConnection;846;0;843;0
WireConnection;846;1;844;0
WireConnection;855;0;875;0
WireConnection;855;1;851;0
WireConnection;854;1;905;0
WireConnection;948;0;917;0
WireConnection;948;1;943;0
WireConnection;848;1;846;0
WireConnection;847;1;845;0
WireConnection;859;0;854;1
WireConnection;859;1;855;0
WireConnection;861;0;853;0
WireConnection;861;1;858;0
WireConnection;964;0;948;1
WireConnection;850;0;848;1
WireConnection;850;1;847;1
WireConnection;862;0;859;0
WireConnection;862;1;861;0
WireConnection;966;0;964;0
WireConnection;966;1;967;0
WireConnection;857;0;850;0
WireConnection;857;1;852;0
WireConnection;865;0;907;0
WireConnection;865;1;862;0
WireConnection;965;0;966;0
WireConnection;860;0;857;0
WireConnection;860;1;856;0
WireConnection;910;0;865;1
WireConnection;910;1;965;0
WireConnection;863;0;860;0
WireConnection;908;0;910;0
WireConnection;908;1;909;0
WireConnection;876;0;863;0
WireConnection;866;0;908;0
WireConnection;866;1;864;0
WireConnection;871;0;866;0
WireConnection;871;1;867;0
WireConnection;874;0;871;0
WireConnection;872;0;870;0
WireConnection;872;1;868;0
WireConnection;888;0;872;0
WireConnection;888;1;874;0
WireConnection;899;0;898;0
WireConnection;899;2;888;0
WireConnection;895;0;899;0
WireConnection;895;1;897;0
WireConnection;895;2;888;0
WireConnection;900;0;895;0
WireConnection;900;1;896;0
WireConnection;906;0;900;0
ASEEND*/
//CHKSM=9019DF19643F2E713E74A38517D82F687770D51D
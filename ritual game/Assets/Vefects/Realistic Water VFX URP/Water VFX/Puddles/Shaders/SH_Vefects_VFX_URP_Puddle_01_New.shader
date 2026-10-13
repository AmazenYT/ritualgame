// Made with Amplify Shader Editor v1.9.9.12
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_Vefects_VFX_URP_Puddle_01_New"
{
	Properties
	{
		[Toggle( _USEWORLDSPACE_ON )] _UseWorldSpace( "Use World Space", Float ) = 1
		_Color01( "Color 01", Color ) = ( 1, 1, 1 )
		_Specular( "Specular", Float ) = 0.001
		_Smoothness( "Smoothness", Float ) = 0
		_DepthFadeOpacity( "Depth Fade Opacity", Float ) = 1
		[Space(33)][Header(UV Noise)][Space(13)] _UVNoise( "UV Noise", 2D ) = "white" {}
		_UVNoiseIntensity( "UV Noise Intensity", Float ) = 0.1
		_UVNoiseScale( "UV Noise Scale", Vector ) = ( 1, 1, 0, 0 )
		[Space(33)][Header(World Noise)][Space(13)] _WorldNoise( "World Noise", 2D ) = "white" {}
		_WorldUVNoiseIntensity( "World UV Noise Intensity", Float ) = 0.1
		_TriplanarTile( "Triplanar Tile", Vector ) = ( 1, 1, 1, 0 )
		_TriplanarTileOffsetCustom( "Triplanar Tile Offset Custom", Vector ) = ( 0, 0, 0, 0 )
		_WaterNormalStrength( "Water Normal Strength", Float ) = 1
		[Space(33)][Header(Refraction Fresnel)][Space(13)] _RefractionFresnelBias( "Refraction Fresnel Bias", Float ) = 0
		_RefractionFresnelScale( "Refraction Fresnel Scale", Float ) = 1
		_RefractionFresnelPower( "Refraction Fresnel Power", Float ) = 5
		_RefractionMin( "Refraction Min", Float ) = 0
		_RefractionMax( "Refraction Max", Float ) = 1
		[Space(33)][Header(Camera Depth Fade)][Space(13)] _CameraDepthFadeLength( "Camera Depth Fade Length", Float ) = 1
		_CameraDepthFadeOffset( "Camera Depth Fade Offset", Float ) = 0
		[Space(33)][Header(Wave Mask)][Space(13)] _WaveMask( "Wave Mask", 2D ) = "white" {}
		_WavesIntensity( "Waves Intensity", Float ) = 8
		_WavesTiling( "Waves Tiling", Float ) = 3
		_WavesSpeed( "Waves Speed", Float ) = 2
		_ErodeIn( "Erode In", Float ) = 0
		_ErodeOut( "Erode Out", Float ) = -0.5
		[Space(33)][Header(AR)][Space(13)] _Cull( "Cull", Float ) = 0
		_Src( "Src", Float ) = 5
		_Dst( "Dst", Float ) = 10
		_ZWrite( "ZWrite", Float ) = 0
		_ZTest( "ZTest", Float ) = 2


		//_TransmissionShadow( "Transmission Shadow", Range( 0, 1 ) ) = 0.5
		//_TransStrength( "Trans Strength", Range( 0, 50 ) ) = 1
		//_TransNormal( "Trans Normal Distortion", Range( 0, 1 ) ) = 0.5
		//_TransScattering( "Trans Scattering", Range( 1, 50 ) ) = 2
		//_TransDirect( "Trans Direct", Range( 0, 1 ) ) = 0.9
		//_TransAmbient( "Trans Ambient", Range( 0, 1 ) ) = 0.1
		//_TransShadow( "Trans Shadow", Range( 0, 1 ) ) = 0.5

		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25

		//_InstancedTerrainNormals("Instanced Terrain Normals", Float) = 1.0

		[ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlights("Specular Highlights", Float) = 1.0
		[ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
		[ToggleOff] _ScreenSpaceReflections("Screen Space Reflections", Float) = 1.0
		[ToggleOff] _ScreenSpaceReflectionsContributeTransparent("Screen Space Reflections Contribute Transparent", Float) = 1.0
		//[HideInInspector][ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0

		[HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}

		//[HideInInspector][ToggleUI] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 1
		//[HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1

		//[HideInInspector] _AlphaClip("__clip", Float) = 0.0
	}

	SubShader
	{
		PackageRequirements
		{
			"com.unity.render-pipelines.universal": "[17.0,18.0]"
		}

		

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "UniversalMaterialType"="Lit" }

	LOD 0

		Cull [_Cull]
		ZWrite [_ZWrite]
		ZTest [_ZTest]
		Offset 0 , 0
		AlphaToMask Off

		

		HLSLINCLUDE
		#pragma target 4.5
		#pragma prefer_hlslcc gles
		// ensure rendering platforms toggle list is visible

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

		#define ASE_ADJUST_CLIP_POSITION( x ) x

		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}

		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlane (float3 pos, float4 plane)
		{
			float d = dot (float4(pos,1.0f), plane);
			return d;
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS
		ENDHLSL

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }

			Blend [_Src] [_Dst], One OneMinusSrcAlpha
			ZWrite [_ZWrite]
			ZTest [_ZTest]
			Offset 0 , 0
			ColorMask RGBA

			

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#define _NORMAL_DROPOFF_TS 1
			#pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define _SPECULAR_SETUP 1
			#define _SURFACE_TYPE_TRANSPARENT 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003
			#define REQUIRE_OPAQUE_TEXTURE 1
			#define REQUIRE_DEPTH_TEXTURE 1


			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
			#if ( UNITY_VERSION >= 60010000 )
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
			#endif

			#if defined(UNITY_PLATFORM_META_QUEST) && ( UNITY_VERSION >= 60050000 )
            #pragma multi_compile _ META_QUEST_LIGHTUNROLL
            #endif

			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#if ( UNITY_VERSION >= 60030000 )
			#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
			#endif
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile _ _LIGHT_LAYERS
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
			#if ( UNITY_VERSION >= 60010000 )
			#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
			#else
			#pragma multi_compile _ _FORWARD_PLUS
			#endif

            #if defined(UNITY_PLATFORM_META_QUEST) && ( UNITY_VERSION >= 60050000 )
            #pragma multi_compile _ META_QUEST_ORTHO_PROJ
            #pragma multi_compile _ META_QUEST_NO_SPOTLIGHTS_LIGHT_LOOP
            #endif

			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#if ( UNITY_VERSION >= 60010000 )
			#pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
			#endif
			#if ( UNITY_VERSION >= 60030000 )
			#pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
			#endif
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile _ USE_LEGACY_LIGHTMAPS

			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SHADERPASS SHADERPASS_FORWARD

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#if ( UNITY_VERSION >= 60010000 )
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
			#else
			#pragma multi_compile_fog
			#endif
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined( UNITY_INSTANCING_ENABLED ) && defined( ASE_INSTANCED_TERRAIN ) && ( defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL) || defined(_INSTANCEDTERRAINNORMALS_PIXEL) )
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_TEXTURE_COORDINATES1
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES1
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_SCREEN_POSITION_NORMALIZED
			#define ASE_NEEDS_FRAG_WORLD_VIEW_DIR
			#define ASE_NEEDS_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_TANGENT
			#define ASE_NEEDS_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_BITANGENT
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_VERT_POSITION
			#pragma shader_feature_local _USEWORLDSPACE_ON


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			#if ( UNITY_VERSION < 60010000 )
				#define USE_CLUSTER_LIGHT_LOOP USE_FORWARD_PLUS
				#define CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					float4 texcoord1 : TEXCOORD1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					float4 texcoord2 : TEXCOORD2;
				#endif
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				half3 normalWS : TEXCOORD1;
				float4 tangentWS : TEXCOORD2; // holds terrainUV ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
				float4 lightmapUVOrVertexSH : TEXCOORD3;
				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					half4 fogFactorAndVertexLight : TEXCOORD4;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON)
					float2 dynamicLightmapUV : TEXCOORD5;
				#endif
				#if defined(USE_APV_PROBE_OCCLUSION)
					float4 probeOcclusion : TEXCOORD6;
				#endif
				float4 ase_texcoord7 : TEXCOORD7;
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_color : COLOR;
				float4 ase_texcoord9 : TEXCOORD9;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _Color01;
			float3 _TriplanarTileOffsetCustom;
			float3 _TriplanarTile;
			float2 _UVNoiseScale;
			float _Cull;
			float _DepthFadeOpacity;
			float _RefractionFresnelPower;
			float _RefractionFresnelScale;
			float _RefractionFresnelBias;
			float _RefractionMax;
			float _RefractionMin;
			float _Smoothness;
			float _Specular;
			float _WaterNormalStrength;
			float _ErodeIn;
			float _CameraDepthFadeLength;
			float _WavesIntensity;
			float _WorldUVNoiseIntensity;
			float _WavesSpeed;
			float _UVNoiseIntensity;
			float _WavesTiling;
			float _ZWrite;
			float _Dst;
			float _Src;
			float _ZTest;
			float _ErodeOut;
			float _CameraDepthFadeOffset;
			float _AlphaClip;
			float _Cutoff;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _WaveMask;
			sampler2D _UVNoise;
			sampler2D _WorldNoise;


			
			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 customSurfaceDepth170 = input.positionOS.xyz;
				float customEye170 = -TransformWorldToView(TransformObjectToWorld(customSurfaceDepth170)).z;
				output.ase_texcoord9.x = customEye170;
				
				output.ase_texcoord7 = input.texcoord;
				output.ase_texcoord8 = input.texcoord1;
				output.ase_color = input.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord9.yzw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif
				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				#ifdef ASE_CUSTOM_MOTION_VECTOR
					// Declared so the Motion Vector output port surfaces on the master node; only consumed by the motion vector passes.
					float3 aseCustomMotionVector = float3(0, 0, 0);
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				OUTPUT_LIGHTMAP_UV(input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy);
				#if defined(DYNAMICLIGHTMAP_ON)
					output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif
				OUTPUT_SH4(vertexInput.positionWS, normalInput.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.lightmapUVOrVertexSH.xyz, output.probeOcclusion);

				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					output.fogFactorAndVertexLight = 0;
					#if defined(ASE_FOG) && !defined(_FOG_FRAGMENT)
						output.fogFactorAndVertexLight.x = ComputeFogFactor(vertexInput.positionCS.z);
					#endif
					#ifdef _ADDITIONAL_LIGHTS_VERTEX
						half3 vertexLight = VertexLighting( vertexInput.positionWS, normalInput.normalWS );
						output.fogFactorAndVertexLight.yzw = vertexLight;
					#endif
				#endif

				output.positionCS = ASE_ADJUST_CLIP_POSITION( vertexInput.positionCS );
				output.positionWS = vertexInput.positionWS;
				output.normalWS = normalInput.normalWS;
				output.tangentWS = float4( normalInput.tangentWS, ( input.tangentOS.w > 0.0 ? 1.0 : -1.0 ) * GetOddNegativeScale() );

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					output.tangentWS.zw = input.texcoord.xy;
					output.tangentWS.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					float4 texcoord1 : TEXCOORD1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					float4 texcoord2 : TEXCOORD2;
				#endif
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					output.texcoord1 = input.texcoord1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					output.texcoord2 = input.texcoord2;
				#endif
				output.ase_color = input.ase_color;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				#endif
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag ( PackedVaryings input
						#if defined( ASE_WRITE_DEPTH )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						#if ( UNITY_VERSION >= 60020000 )
						, out uint outRenderingLayers : SV_Target1
						#else
						, out float4 outRenderingLayers : SV_Target1
						#endif
						#endif
						, uint ase_vface : SV_IsFrontFace ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				#if defined( _SURFACE_TYPE_TRANSPARENT )
					const bool isTransparent = true;
				#else
					const bool isTransparent = false;
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					float4 shadowCoord = TransformWorldToShadowCoord( input.positionWS );
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				// @diogo: mikktspace compliant
				float renormFactor = 1.0 / max( FLT_MIN, length( input.normalWS ) );

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float3 ViewDirWS = GetWorldSpaceNormalizeViewDir( PositionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );
				float3 TangentWS = input.tangentWS.xyz * renormFactor;
				float3 BitangentWS = cross( input.normalWS, input.tangentWS.xyz ) * input.tangentWS.w * renormFactor;
				float3 NormalWS = input.normalWS * renormFactor;

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float2 sampleCoords = (input.tangentWS.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					NormalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					TangentWS = -cross(GetObjectToWorldMatrix()._13_23_33, NormalWS);
					BitangentWS = cross(NormalWS, -TangentWS);
				#endif

				float3 temp_cast_0 = (0.0).xxx;
				
				float2 texCoord437 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult433 = (float2(0.005 , 0.0));
				float wavesTiling444 = _WavesTiling;
				float2 temp_cast_1 = (( wavesTiling444 / 2.0 )).xx;
				float2 temp_output_2_0_g94 = ( ( ( texCoord437 + appendResult433 ) * wavesTiling444 ) - temp_cast_1 );
				float2 temp_output_15_0_g94 = ( temp_output_2_0_g94 * temp_output_2_0_g94 );
				float2 appendResult20_g94 = (float2(frac( ( atan2( (temp_output_2_0_g94).x , (temp_output_2_0_g94).y ) / 6.283 ) ) , sqrt( ( (temp_output_15_0_g94).x + (temp_output_15_0_g94).y ) )));
				float2 temp_output_468_0 = appendResult20_g94;
				float saferPower469 = abs( (temp_output_468_0).y );
				float2 texCoord423 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float randomOffset532 = input.ase_texcoord8.x;
				float4 tex2DNode419 = tex2D( _UVNoise, ( ( texCoord423 * _UVNoiseScale ) + randomOffset532 ) );
				float2 appendResult429 = (float2(( ( tex2DNode419.r + -0.2 ) * 2.0 ) , ( ( tex2DNode419.g + -0.2 ) * 2.0 )));
				float2 UVNoise421 = ( appendResult429 * _UVNoiseIntensity );
				float2 appendResult472 = (float2((temp_output_468_0).x , ( pow( saferPower469 , 2.0 ) + UVNoise421 ).x));
				float wavesSpeed482 = _WavesSpeed;
				float2 appendResult475 = (float2(0.0 , frac( ( _TimeParameters.x * wavesSpeed482 ) )));
				float3 objToWorld518 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				#ifdef _USEWORLDSPACE_ON
				float3 staticSwitch525 = PositionWS;
				#else
				float3 staticSwitch525 = ( PositionWS - objToWorld518 );
				#endif
				float3 temp_output_520_0 = ( staticSwitch525 + _TriplanarTileOffsetCustom );
				float4 tex2DNode365 = tex2D( _WorldNoise, ( float3( (temp_output_520_0).xz ,  0.0 ) * _TriplanarTile ).xy );
				float2 appendResult527 = (float2(( ( tex2DNode365.r + -0.2 ) * 2.0 ) , ( ( tex2DNode365.g + -0.2 ) * 2.0 )));
				float2 worldUVNoise364 = ( appendResult527 * _WorldUVNoiseIntensity );
				float2 texCoord439 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_5 = (( wavesTiling444 / 2.0 )).xx;
				float2 temp_output_2_0_g95 = ( ( texCoord439 * wavesTiling444 ) - temp_cast_5 );
				float2 temp_output_15_0_g95 = ( temp_output_2_0_g95 * temp_output_2_0_g95 );
				float2 appendResult20_g95 = (float2(frac( ( atan2( (temp_output_2_0_g95).x , (temp_output_2_0_g95).y ) / 6.283 ) ) , sqrt( ( (temp_output_15_0_g95).x + (temp_output_15_0_g95).y ) )));
				float2 temp_output_498_0 = appendResult20_g95;
				float saferPower486 = abs( (temp_output_498_0).y );
				float2 appendResult489 = (float2((temp_output_498_0).x , ( pow( saferPower486 , 2.0 ) + UVNoise421 ).x));
				float2 appendResult493 = (float2(0.0 , frac( ( _TimeParameters.x * wavesSpeed482 ) )));
				float4 tex2DNode403 = tex2D( _WaveMask, ( ( appendResult489 - appendResult493 ) + worldUVNoise364 ) );
				float3 appendResult410 = (float3(1.0 , 0.0 , ( ( tex2D( _WaveMask, ( ( appendResult472 - appendResult475 ) + worldUVNoise364 ) ).g - tex2DNode403.g ) * _WavesIntensity )));
				float2 appendResult434 = (float2(0.0 , 0.005));
				float2 texCoord436 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_7 = (( wavesTiling444 / 2.0 )).xx;
				float2 temp_output_2_0_g96 = ( ( ( appendResult434 + texCoord436 ) * wavesTiling444 ) - temp_cast_7 );
				float2 temp_output_15_0_g96 = ( temp_output_2_0_g96 * temp_output_2_0_g96 );
				float2 appendResult20_g96 = (float2(frac( ( atan2( (temp_output_2_0_g96).x , (temp_output_2_0_g96).y ) / 6.283 ) ) , sqrt( ( (temp_output_15_0_g96).x + (temp_output_15_0_g96).y ) )));
				float2 temp_output_513_0 = appendResult20_g96;
				float saferPower501 = abs( (temp_output_513_0).y );
				float2 appendResult504 = (float2((temp_output_513_0).x , ( pow( saferPower501 , 2.0 ) + UVNoise421 ).x));
				float2 appendResult508 = (float2(0.0 , frac( ( _TimeParameters.x * wavesSpeed482 ) )));
				float3 appendResult411 = (float3(0.0 , 1.0 , ( ( tex2DNode403.g - tex2D( _WaveMask, ( ( appendResult504 - appendResult508 ) + worldUVNoise364 ) ).g ) * _WavesIntensity )));
				float2 texCoord352 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g86 = clamp( ( ( length(  (float2( -1,-1 ) + ( ( texCoord352 + worldUVNoise364 ) - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.666 ) * 4.2 ) , 0.0 , 1.0 );
				float2 texCoord356 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g87 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord356 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.5 ) * 2.0 ) , 0.0 , 1.0 );
				float erodeIn534 = input.ase_texcoord7.z;
				float temp_output_539_0 = ( erodeIn534 + _ErodeIn );
				float2 texCoord377 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g88 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord377 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult372 = smoothstep( temp_output_539_0 , ( temp_output_539_0 + 0.5 ) , ( 1.0 - saturate( clampResult9_g88 ) ));
				float erodeOut535 = input.ase_texcoord7.w;
				float temp_output_537_0 = ( erodeOut535 + _ErodeOut );
				float2 texCoord385 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g90 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord385 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult375 = smoothstep( temp_output_537_0 , ( temp_output_537_0 + 0.5 ) , saturate( clampResult9_g90 ));
				float temp_output_392_0 = saturate( ( saturate( ( saturate( ( ( 1.0 - saturate( clampResult9_g86 ) ) * ( 1.0 - saturate( clampResult9_g87 ) ) ) ) * smoothstepResult372 ) ) * smoothstepResult375 ) );
				float3 lerpResult347 = lerp( float3( 0, 0, 1 ) , cross( appendResult410 , appendResult411 ) , temp_output_392_0);
				float3 normanReedus398 = lerpResult347;
				float3 appendResult138 = (float3(_WaterNormalStrength , _WaterNormalStrength , 1.0));
				float3 WaterNormal140 = ( normanReedus398 * appendResult138 );
				
				float spec84 = _Specular;
				float3 temp_cast_9 = (spec84).xxx;
				
				float smoothness90 = _Smoothness;
				
				float3 tanToWorld0 = float3( TangentWS.x, BitangentWS.x, NormalWS.x );
				float3 tanToWorld1 = float3( TangentWS.y, BitangentWS.y, NormalWS.y );
				float3 tanToWorld2 = float3( TangentWS.z, BitangentWS.z, NormalWS.z );
				float3 tanNormal1_g92 = float4( 0,0,1,0 ).rgb;
				float temp_output_4_0_g92 = _RefractionFresnelPower;
				float lerpResult3_g92 = lerp( ( -1.0 * temp_output_4_0_g92 ) , temp_output_4_0_g92 , ase_vface);
				float fresnelNdotV1_g92 = dot( float3( dot( tanToWorld0, tanNormal1_g92 ), dot( tanToWorld1, tanNormal1_g92 ), dot( tanToWorld2, tanNormal1_g92 ) ), ViewDirWS );
				float fresnelNode1_g92 = ( _RefractionFresnelBias + _RefractionFresnelScale * pow( 1.0 - fresnelNdotV1_g92, lerpResult3_g92 ) );
				float temp_output_198_0 = fresnelNode1_g92;
				float lerpResult205 = lerp( _RefractionMin , _RefractionMax , saturate( temp_output_198_0 ));
				float4 fetchOpaqueVal23 = float4( SHADERGRAPH_SAMPLE_SCENE_COLOR( ( (ScreenPosNorm).xy + ( ( ( (WaterNormal140).xy + -0.5 ) * 2.0 ) * lerpResult205 ) ).xy ), 1.0 );
				float4 temp_output_39_0 = ( ( float4( _Color01 , 0.0 ) * input.ase_color ) * fetchOpaqueVal23 );
				
				float screenDepth36 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPosNorm.xy ),_ZBufferParams);
				float distanceDepth36 = saturate( ( screenDepth36 - LinearEyeDepth( ScreenPosNorm.z,_ZBufferParams ) ) / ( _DepthFadeOpacity ) );
				float DF59 = distanceDepth36;
				float customEye170 = input.ase_texcoord9.x;
				float cameraDepthFade170 = (( customEye170 -_ProjectionParams.y - _CameraDepthFadeOffset ) / _CameraDepthFadeLength);
				float camDF183 = saturate( cameraDepthFade170 );
				float gangnamStyle397 = temp_output_392_0;
				

				float3 BaseColor = temp_cast_0;
				float3 Normal = WaterNormal140;
				float3 Specular = temp_cast_9;
				float Metallic = 0;
				float Smoothness = smoothness90;
				float Occlusion = 1;
				float3 Emission = temp_output_39_0.rgb;
				float Alpha = saturate( ( input.ase_color.a * saturate( ( saturate( ( saturate( DF59 ) * camDF183 ) ) * gangnamStyle397 ) ) ) );
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
					float AlphaClipThresholdShadow = 0.5;
				#endif
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				float3 Transmission = 1;
				float3 Translucency = 1;

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = input.positionCS.z;
				#endif

				#ifdef _CLEARCOAT
					float CoatMask = 0;
					float CoatSmoothness = 0;
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_CHANGES_WORLD_POS)
					ShadowCoord = TransformWorldToShadowCoord( PositionWS );
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = PositionWS;
				inputData.positionCS = input.positionCS;
				inputData.normalizedScreenSpaceUV = ScreenPosNorm.xy;
				inputData.viewDirectionWS = ViewDirWS;
				inputData.shadowCoord = ShadowCoord;

				#ifdef _NORMALMAP
						#if _NORMAL_DROPOFF_TS
							inputData.normalWS = TransformTangentToWorld(Normal, half3x3(TangentWS, BitangentWS, NormalWS));
						#elif _NORMAL_DROPOFF_OS
							inputData.normalWS = TransformObjectToWorldNormal(Normal);
						#elif _NORMAL_DROPOFF_WS
							inputData.normalWS = Normal;
						#endif
					inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				#else
					inputData.normalWS = NormalWS;
				#endif

				#ifdef ASE_FOG
					inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactorAndVertexLight.x);
				#endif
				#ifdef _ADDITIONAL_LIGHTS_VERTEX
					inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
				#endif

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float3 SH = SampleSH(inputData.normalWS.xyz);
				#else
					float3 SH = input.lightmapUVOrVertexSH.xyz;
				#endif

				#if defined(_SCREEN_SPACE_IRRADIANCE) && ( UNITY_VERSION >= 60030000 )
					#if ( UNITY_VERSION >= 60060000 )
						inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy, inputData.normalWS));
					#else
						inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
					#endif
				#elif defined(DYNAMICLIGHTMAP_ON)
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
					inputData.bakedGI = SAMPLE_GI( SH, GetAbsolutePositionWS(inputData.positionWS),
						inputData.normalWS,
						inputData.viewDirectionWS,
						input.positionCS.xy,
						input.probeOcclusion,
						inputData.shadowMask );
				#else
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#endif

				#ifdef ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#endif

				#if defined(DEBUG_DISPLAY)
					#if defined(DYNAMICLIGHTMAP_ON)
						inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
					#endif
					#if defined(LIGHTMAP_ON)
						inputData.staticLightmapUV = input.lightmapUVOrVertexSH.xy;
					#else
						inputData.vertexSH = SH;
					#endif
					#if defined(USE_APV_PROBE_OCCLUSION)
						inputData.probeOcclusion = input.probeOcclusion;
					#endif
				#endif

				SurfaceData surfaceData;
				surfaceData.albedo              = BaseColor;
				surfaceData.metallic            = saturate(Metallic);
				surfaceData.specular            = Specular;
				surfaceData.smoothness          = saturate(Smoothness),
				surfaceData.occlusion           = Occlusion,
				surfaceData.emission            = Emission,
				surfaceData.alpha               = saturate(Alpha);
				surfaceData.normalTS            = Normal;
				surfaceData.clearCoatMask       = 0;
				surfaceData.clearCoatSmoothness = 1;

				#ifdef _CLEARCOAT
					surfaceData.clearCoatMask       = saturate(CoatMask);
					surfaceData.clearCoatSmoothness = saturate(CoatSmoothness);
				#endif

				#if defined(_DBUFFER)
					ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
				#endif

				#ifdef ASE_LIGHTING_SIMPLE
					half4 color = UniversalFragmentBlinnPhong( inputData, surfaceData);
				#else
					half4 color = UniversalFragmentPBR( inputData, surfaceData);
				#endif

				#ifdef ASE_TRANSMISSION
				{
					float shadow = _TransmissionShadow;

					#define SUM_LIGHT_TRANSMISSION(Light)\
						float3 atten = Light.color * Light.distanceAttenuation;\
						atten = lerp( atten, atten * Light.shadowAttenuation, shadow );\
						half3 transmission = max( 0, -dot( inputData.normalWS, Light.direction ) ) * atten * Transmission;\
						color.rgb += BaseColor * transmission;

					SUM_LIGHT_TRANSMISSION( GetMainLight( inputData.shadowCoord ) );

					#if defined(_ADDITIONAL_LIGHTS)
						uint meshRenderingLayers = GetMeshRenderingLayer();
						uint pixelLightCount = GetAdditionalLightsCount();
						#if USE_CLUSTER_LIGHT_LOOP
							[loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
							{
								CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

								Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
								#ifdef _LIGHT_LAYERS
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								#endif
								{
									SUM_LIGHT_TRANSMISSION( light );
								}
							}
						#endif
						LIGHT_LOOP_BEGIN( pixelLightCount )
							Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
							#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
							#endif
							{
								SUM_LIGHT_TRANSMISSION( light );
							}
						LIGHT_LOOP_END
					#endif
				}
				#endif

				#ifdef ASE_TRANSLUCENCY
				{
					float shadow = _TransShadow;
					float normal = _TransNormal;
					float scattering = _TransScattering;
					float direct = _TransDirect;
					float ambient = _TransAmbient;
					float strength = _TransStrength;

					#define SUM_LIGHT_TRANSLUCENCY(Light)\
						float3 atten = Light.color * Light.distanceAttenuation;\
						atten = lerp( atten, atten * Light.shadowAttenuation, shadow );\
						half3 lightDir = Light.direction + inputData.normalWS * normal;\
						half VdotL = pow( saturate( dot( inputData.viewDirectionWS, -lightDir ) ), scattering );\
						half3 translucency = atten * ( VdotL * direct + inputData.bakedGI * ambient ) * Translucency;\
						color.rgb += BaseColor * translucency * strength;

					SUM_LIGHT_TRANSLUCENCY( GetMainLight( inputData.shadowCoord ) );

					#if defined(_ADDITIONAL_LIGHTS)
						uint meshRenderingLayers = GetMeshRenderingLayer();
						uint pixelLightCount = GetAdditionalLightsCount();
						#if USE_CLUSTER_LIGHT_LOOP
							[loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
							{
								CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

								Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
								#ifdef _LIGHT_LAYERS
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								#endif
								{
									SUM_LIGHT_TRANSLUCENCY( light );
								}
							}
						#endif
						LIGHT_LOOP_BEGIN( pixelLightCount )
							Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
							#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
							#endif
							{
								SUM_LIGHT_TRANSLUCENCY( light );
							}
						LIGHT_LOOP_END
					#endif
				}
				#endif

				#ifdef ASE_REFRACTION
					float4 projScreenPos = ScreenPos / ScreenPos.w;
					float3 refractionOffset = ( RefractionIndex - 1.0 ) * mul( UNITY_MATRIX_V, float4( NormalWS,0 ) ).xyz * ( 1.0 - dot( NormalWS, ViewDirWS ) );
					projScreenPos.xy += refractionOffset.xy;
					float3 refraction = SHADERGRAPH_SAMPLE_SCENE_COLOR( projScreenPos.xy ) * RefractionColor;
					color.rgb = lerp( refraction, color.rgb, color.a );
					color.a = 1;
				#endif

				#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY
					color.rgb *= color.a;
				#endif

				#ifdef ASE_FOG
					#ifdef TERRAIN_SPLAT_ADDPASS
						color.rgb = MixFogColor(color.rgb, half3(0,0,0), inputData.fogCoord);
					#else
						color.rgb = MixFog(color.rgb, inputData.fogCoord);
					#endif
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					#if ( UNITY_VERSION >= 60020000 )
					outRenderingLayers = EncodeMeshRenderingLayer();
					#else
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4( EncodeMeshRenderingLayer( renderingLayers ), 0, 0, 0 );
					#endif
				#endif

				#if defined( ASE_OPAQUE_KEEP_ALPHA )
					return half4( color.rgb, color.a );
				#else
					return half4( color.rgb, OutputAlpha( color.a, isTransparent ) );
				#endif
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask R
			AlphaToMask Off

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define _SPECULAR_SETUP 1
			#define _SURFACE_TYPE_TRANSPARENT 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003
			#define REQUIRE_DEPTH_TEXTURE 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_FRAG_SCREEN_POSITION_NORMALIZED
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature_local _USEWORLDSPACE_ON


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 ase_color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _Color01;
			float3 _TriplanarTileOffsetCustom;
			float3 _TriplanarTile;
			float2 _UVNoiseScale;
			float _Cull;
			float _DepthFadeOpacity;
			float _RefractionFresnelPower;
			float _RefractionFresnelScale;
			float _RefractionFresnelBias;
			float _RefractionMax;
			float _RefractionMin;
			float _Smoothness;
			float _Specular;
			float _WaterNormalStrength;
			float _ErodeIn;
			float _CameraDepthFadeLength;
			float _WavesIntensity;
			float _WorldUVNoiseIntensity;
			float _WavesSpeed;
			float _UVNoiseIntensity;
			float _WavesTiling;
			float _ZWrite;
			float _Dst;
			float _Src;
			float _ZTest;
			float _ErodeOut;
			float _CameraDepthFadeOffset;
			float _AlphaClip;
			float _Cutoff;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _WorldNoise;


			
			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 customSurfaceDepth170 = input.positionOS.xyz;
				float customEye170 = -TransformWorldToView(TransformObjectToWorld(customSurfaceDepth170)).z;
				output.ase_texcoord1.x = customEye170;
				
				output.ase_color = input.ase_color;
				output.ase_texcoord2 = input.ase_texcoord;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.yzw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = ASE_ADJUST_CLIP_POSITION( vertexInput.positionCS );
				output.positionWS = vertexInput.positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.ase_color = input.ase_color;
				output.ase_texcoord = input.ase_texcoord;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(	PackedVaryings input
						#if defined( ASE_WRITE_DEPTH )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( input.positionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );

				float screenDepth36 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPosNorm.xy ),_ZBufferParams);
				float distanceDepth36 = saturate( ( screenDepth36 - LinearEyeDepth( ScreenPosNorm.z,_ZBufferParams ) ) / ( _DepthFadeOpacity ) );
				float DF59 = distanceDepth36;
				float customEye170 = input.ase_texcoord1.x;
				float cameraDepthFade170 = (( customEye170 -_ProjectionParams.y - _CameraDepthFadeOffset ) / _CameraDepthFadeLength);
				float camDF183 = saturate( cameraDepthFade170 );
				float2 texCoord352 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float3 objToWorld518 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				#ifdef _USEWORLDSPACE_ON
				float3 staticSwitch525 = PositionWS;
				#else
				float3 staticSwitch525 = ( PositionWS - objToWorld518 );
				#endif
				float3 temp_output_520_0 = ( staticSwitch525 + _TriplanarTileOffsetCustom );
				float4 tex2DNode365 = tex2D( _WorldNoise, ( float3( (temp_output_520_0).xz ,  0.0 ) * _TriplanarTile ).xy );
				float2 appendResult527 = (float2(( ( tex2DNode365.r + -0.2 ) * 2.0 ) , ( ( tex2DNode365.g + -0.2 ) * 2.0 )));
				float2 worldUVNoise364 = ( appendResult527 * _WorldUVNoiseIntensity );
				float clampResult9_g86 = clamp( ( ( length(  (float2( -1,-1 ) + ( ( texCoord352 + worldUVNoise364 ) - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.666 ) * 4.2 ) , 0.0 , 1.0 );
				float2 texCoord356 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g87 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord356 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.5 ) * 2.0 ) , 0.0 , 1.0 );
				float erodeIn534 = input.ase_texcoord2.z;
				float temp_output_539_0 = ( erodeIn534 + _ErodeIn );
				float2 texCoord377 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g88 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord377 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult372 = smoothstep( temp_output_539_0 , ( temp_output_539_0 + 0.5 ) , ( 1.0 - saturate( clampResult9_g88 ) ));
				float erodeOut535 = input.ase_texcoord2.w;
				float temp_output_537_0 = ( erodeOut535 + _ErodeOut );
				float2 texCoord385 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g90 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord385 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult375 = smoothstep( temp_output_537_0 , ( temp_output_537_0 + 0.5 ) , saturate( clampResult9_g90 ));
				float temp_output_392_0 = saturate( ( saturate( ( saturate( ( ( 1.0 - saturate( clampResult9_g86 ) ) * ( 1.0 - saturate( clampResult9_g87 ) ) ) ) * smoothstepResult372 ) ) * smoothstepResult375 ) );
				float gangnamStyle397 = temp_output_392_0;
				

				float Alpha = saturate( ( input.ase_color.a * saturate( ( saturate( ( saturate( DF59 ) * camDF183 ) ) * gangnamStyle397 ) ) ) );
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = input.positionCS.z;
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Meta"
			Tags { "LightMode"="Meta" }

			Cull Off

			HLSLPROGRAM
			#define ASE_GEOMETRY
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define _SPECULAR_SETUP 1
			#define _SURFACE_TYPE_TRANSPARENT 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003
			#define REQUIRE_OPAQUE_TEXTURE 1
			#define REQUIRE_DEPTH_TEXTURE 1

			#pragma shader_feature EDITOR_VISUALIZATION

			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SHADERPASS SHADERPASS_META

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_TEXTURE_COORDINATES1
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES1
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_VERT_TANGENT
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_VERT_POSITION
			#pragma shader_feature_local _USEWORLDSPACE_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				#ifdef EDITOR_VISUALIZATION
					float4 VizUV : TEXCOORD1;
					float4 LightCoord : TEXCOORD2;
				#endif
				float4 ase_color : COLOR;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				float4 ase_texcoord7 : TEXCOORD7;
				float4 ase_texcoord8 : TEXCOORD8;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _Color01;
			float3 _TriplanarTileOffsetCustom;
			float3 _TriplanarTile;
			float2 _UVNoiseScale;
			float _Cull;
			float _DepthFadeOpacity;
			float _RefractionFresnelPower;
			float _RefractionFresnelScale;
			float _RefractionFresnelBias;
			float _RefractionMax;
			float _RefractionMin;
			float _Smoothness;
			float _Specular;
			float _WaterNormalStrength;
			float _ErodeIn;
			float _CameraDepthFadeLength;
			float _WavesIntensity;
			float _WorldUVNoiseIntensity;
			float _WavesSpeed;
			float _UVNoiseIntensity;
			float _WavesTiling;
			float _ZWrite;
			float _Dst;
			float _Src;
			float _ZTest;
			float _ErodeOut;
			float _CameraDepthFadeOffset;
			float _AlphaClip;
			float _Cutoff;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _WaveMask;
			sampler2D _UVNoise;
			sampler2D _WorldNoise;


			
			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float4 ase_positionCS = TransformObjectToHClip( ( input.positionOS ).xyz );
				float4 screenPos = ComputeScreenPos( ase_positionCS );
				output.ase_texcoord3 = screenPos;
				float3 ase_tangentWS = TransformObjectToWorldDir( input.tangentOS.xyz );
				output.ase_texcoord6.xyz = ase_tangentWS;
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				output.ase_texcoord7.xyz = ase_normalWS;
				float ase_tangentSign = input.tangentOS.w * ( unity_WorldTransformParams.w >= 0.0 ? 1.0 : -1.0 );
				float3 ase_bitangentWS = cross( ase_normalWS, ase_tangentWS ) * ase_tangentSign;
				output.ase_texcoord8.xyz = ase_bitangentWS;
				
				float3 customSurfaceDepth170 = input.positionOS.xyz;
				float customEye170 = -TransformWorldToView(TransformObjectToWorld(customSurfaceDepth170)).z;
				output.ase_texcoord6.w = customEye170;
				
				output.ase_color = input.ase_color;
				output.ase_texcoord4 = input.texcoord;
				output.ase_texcoord5 = input.texcoord1;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord7.w = 0;
				output.ase_texcoord8.w = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				#ifdef EDITOR_VISUALIZATION
					float2 VizUV = 0;
					float4 LightCoord = 0;
					UnityEditorVizData(input.positionOS.xyz, input.texcoord.xy, input.texcoord1.xy, input.texcoord2.xy, VizUV, LightCoord);
					output.VizUV = float4(VizUV, 0, 0);
					output.LightCoord = LightCoord;
				#endif

				output.positionCS = MetaVertexPosition( input.positionOS, input.texcoord1.xy, input.texcoord1.xy, unity_LightmapST, unity_DynamicLightmapST );
				output.positionWS = TransformObjectToWorld( input.positionOS.xyz );
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				output.texcoord1 = input.texcoord1;
				output.texcoord2 = input.texcoord2;
				output.ase_color = input.ase_color;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(PackedVaryings input , uint ase_vface : SV_IsFrontFace ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( input.positionWS );
				float4 ShadowCoord = shadowCoord;

				float3 temp_cast_0 = (0.0).xxx;
				
				float4 screenPos = input.ase_texcoord3;
				float4 ase_positionSSNorm = screenPos / screenPos.w;
				ase_positionSSNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_positionSSNorm.z : ase_positionSSNorm.z * 0.5 + 0.5;
				float2 texCoord437 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult433 = (float2(0.005 , 0.0));
				float wavesTiling444 = _WavesTiling;
				float2 temp_cast_2 = (( wavesTiling444 / 2.0 )).xx;
				float2 temp_output_2_0_g94 = ( ( ( texCoord437 + appendResult433 ) * wavesTiling444 ) - temp_cast_2 );
				float2 temp_output_15_0_g94 = ( temp_output_2_0_g94 * temp_output_2_0_g94 );
				float2 appendResult20_g94 = (float2(frac( ( atan2( (temp_output_2_0_g94).x , (temp_output_2_0_g94).y ) / 6.283 ) ) , sqrt( ( (temp_output_15_0_g94).x + (temp_output_15_0_g94).y ) )));
				float2 temp_output_468_0 = appendResult20_g94;
				float saferPower469 = abs( (temp_output_468_0).y );
				float2 texCoord423 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float randomOffset532 = input.ase_texcoord5.x;
				float4 tex2DNode419 = tex2D( _UVNoise, ( ( texCoord423 * _UVNoiseScale ) + randomOffset532 ) );
				float2 appendResult429 = (float2(( ( tex2DNode419.r + -0.2 ) * 2.0 ) , ( ( tex2DNode419.g + -0.2 ) * 2.0 )));
				float2 UVNoise421 = ( appendResult429 * _UVNoiseIntensity );
				float2 appendResult472 = (float2((temp_output_468_0).x , ( pow( saferPower469 , 2.0 ) + UVNoise421 ).x));
				float wavesSpeed482 = _WavesSpeed;
				float2 appendResult475 = (float2(0.0 , frac( ( _TimeParameters.x * wavesSpeed482 ) )));
				float3 objToWorld518 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				#ifdef _USEWORLDSPACE_ON
				float3 staticSwitch525 = PositionWS;
				#else
				float3 staticSwitch525 = ( PositionWS - objToWorld518 );
				#endif
				float3 temp_output_520_0 = ( staticSwitch525 + _TriplanarTileOffsetCustom );
				float4 tex2DNode365 = tex2D( _WorldNoise, ( float3( (temp_output_520_0).xz ,  0.0 ) * _TriplanarTile ).xy );
				float2 appendResult527 = (float2(( ( tex2DNode365.r + -0.2 ) * 2.0 ) , ( ( tex2DNode365.g + -0.2 ) * 2.0 )));
				float2 worldUVNoise364 = ( appendResult527 * _WorldUVNoiseIntensity );
				float2 texCoord439 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_6 = (( wavesTiling444 / 2.0 )).xx;
				float2 temp_output_2_0_g95 = ( ( texCoord439 * wavesTiling444 ) - temp_cast_6 );
				float2 temp_output_15_0_g95 = ( temp_output_2_0_g95 * temp_output_2_0_g95 );
				float2 appendResult20_g95 = (float2(frac( ( atan2( (temp_output_2_0_g95).x , (temp_output_2_0_g95).y ) / 6.283 ) ) , sqrt( ( (temp_output_15_0_g95).x + (temp_output_15_0_g95).y ) )));
				float2 temp_output_498_0 = appendResult20_g95;
				float saferPower486 = abs( (temp_output_498_0).y );
				float2 appendResult489 = (float2((temp_output_498_0).x , ( pow( saferPower486 , 2.0 ) + UVNoise421 ).x));
				float2 appendResult493 = (float2(0.0 , frac( ( _TimeParameters.x * wavesSpeed482 ) )));
				float4 tex2DNode403 = tex2D( _WaveMask, ( ( appendResult489 - appendResult493 ) + worldUVNoise364 ) );
				float3 appendResult410 = (float3(1.0 , 0.0 , ( ( tex2D( _WaveMask, ( ( appendResult472 - appendResult475 ) + worldUVNoise364 ) ).g - tex2DNode403.g ) * _WavesIntensity )));
				float2 appendResult434 = (float2(0.0 , 0.005));
				float2 texCoord436 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_8 = (( wavesTiling444 / 2.0 )).xx;
				float2 temp_output_2_0_g96 = ( ( ( appendResult434 + texCoord436 ) * wavesTiling444 ) - temp_cast_8 );
				float2 temp_output_15_0_g96 = ( temp_output_2_0_g96 * temp_output_2_0_g96 );
				float2 appendResult20_g96 = (float2(frac( ( atan2( (temp_output_2_0_g96).x , (temp_output_2_0_g96).y ) / 6.283 ) ) , sqrt( ( (temp_output_15_0_g96).x + (temp_output_15_0_g96).y ) )));
				float2 temp_output_513_0 = appendResult20_g96;
				float saferPower501 = abs( (temp_output_513_0).y );
				float2 appendResult504 = (float2((temp_output_513_0).x , ( pow( saferPower501 , 2.0 ) + UVNoise421 ).x));
				float2 appendResult508 = (float2(0.0 , frac( ( _TimeParameters.x * wavesSpeed482 ) )));
				float3 appendResult411 = (float3(0.0 , 1.0 , ( ( tex2DNode403.g - tex2D( _WaveMask, ( ( appendResult504 - appendResult508 ) + worldUVNoise364 ) ).g ) * _WavesIntensity )));
				float2 texCoord352 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g86 = clamp( ( ( length(  (float2( -1,-1 ) + ( ( texCoord352 + worldUVNoise364 ) - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.666 ) * 4.2 ) , 0.0 , 1.0 );
				float2 texCoord356 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g87 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord356 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.5 ) * 2.0 ) , 0.0 , 1.0 );
				float erodeIn534 = input.ase_texcoord4.z;
				float temp_output_539_0 = ( erodeIn534 + _ErodeIn );
				float2 texCoord377 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g88 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord377 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult372 = smoothstep( temp_output_539_0 , ( temp_output_539_0 + 0.5 ) , ( 1.0 - saturate( clampResult9_g88 ) ));
				float erodeOut535 = input.ase_texcoord4.w;
				float temp_output_537_0 = ( erodeOut535 + _ErodeOut );
				float2 texCoord385 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g90 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord385 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult375 = smoothstep( temp_output_537_0 , ( temp_output_537_0 + 0.5 ) , saturate( clampResult9_g90 ));
				float temp_output_392_0 = saturate( ( saturate( ( saturate( ( ( 1.0 - saturate( clampResult9_g86 ) ) * ( 1.0 - saturate( clampResult9_g87 ) ) ) ) * smoothstepResult372 ) ) * smoothstepResult375 ) );
				float3 lerpResult347 = lerp( float3( 0, 0, 1 ) , cross( appendResult410 , appendResult411 ) , temp_output_392_0);
				float3 normanReedus398 = lerpResult347;
				float3 appendResult138 = (float3(_WaterNormalStrength , _WaterNormalStrength , 1.0));
				float3 WaterNormal140 = ( normanReedus398 * appendResult138 );
				float3 ase_viewVectorWS = ( ( unity_OrthoParams.w == 0 ) ? _WorldSpaceCameraPos - PositionWS : UNITY_MATRIX_V[ 2 ].xyz );
				float3 ase_viewDirWS = normalize( ase_viewVectorWS );
				float3 ase_tangentWS = input.ase_texcoord6.xyz;
				float3 ase_normalWS = input.ase_texcoord7.xyz;
				float3 ase_bitangentWS = input.ase_texcoord8.xyz;
				float3 tanToWorld0 = float3( ase_tangentWS.x, ase_bitangentWS.x, ase_normalWS.x );
				float3 tanToWorld1 = float3( ase_tangentWS.y, ase_bitangentWS.y, ase_normalWS.y );
				float3 tanToWorld2 = float3( ase_tangentWS.z, ase_bitangentWS.z, ase_normalWS.z );
				float3 tanNormal1_g92 = float4( 0,0,1,0 ).rgb;
				float temp_output_4_0_g92 = _RefractionFresnelPower;
				float lerpResult3_g92 = lerp( ( -1.0 * temp_output_4_0_g92 ) , temp_output_4_0_g92 , ase_vface);
				float fresnelNdotV1_g92 = dot( float3( dot( tanToWorld0, tanNormal1_g92 ), dot( tanToWorld1, tanNormal1_g92 ), dot( tanToWorld2, tanNormal1_g92 ) ), ase_viewDirWS );
				float fresnelNode1_g92 = ( _RefractionFresnelBias + _RefractionFresnelScale * pow( 1.0 - fresnelNdotV1_g92, lerpResult3_g92 ) );
				float temp_output_198_0 = fresnelNode1_g92;
				float lerpResult205 = lerp( _RefractionMin , _RefractionMax , saturate( temp_output_198_0 ));
				float4 fetchOpaqueVal23 = float4( SHADERGRAPH_SAMPLE_SCENE_COLOR( ( (ase_positionSSNorm).xy + ( ( ( (WaterNormal140).xy + -0.5 ) * 2.0 ) * lerpResult205 ) ).xy ), 1.0 );
				float4 temp_output_39_0 = ( ( float4( _Color01 , 0.0 ) * input.ase_color ) * fetchOpaqueVal23 );
				
				float screenDepth36 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_positionSSNorm.xy ),_ZBufferParams);
				float distanceDepth36 = saturate( ( screenDepth36 - LinearEyeDepth( ase_positionSSNorm.z,_ZBufferParams ) ) / ( _DepthFadeOpacity ) );
				float DF59 = distanceDepth36;
				float customEye170 = input.ase_texcoord6.w;
				float cameraDepthFade170 = (( customEye170 -_ProjectionParams.y - _CameraDepthFadeOffset ) / _CameraDepthFadeLength);
				float camDF183 = saturate( cameraDepthFade170 );
				float gangnamStyle397 = temp_output_392_0;
				

				float3 BaseColor = temp_cast_0;
				float3 Emission = temp_output_39_0.rgb;
				float Alpha = saturate( ( input.ase_color.a * saturate( ( saturate( ( saturate( DF59 ) * camDF183 ) ) * gangnamStyle397 ) ) ) );
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				MetaInput metaInput = (MetaInput)0;
				metaInput.Albedo = BaseColor;
				metaInput.Emission = Emission;
				#ifdef EDITOR_VISUALIZATION
					metaInput.VizUV = input.VizUV.xy;
					metaInput.LightCoord = input.LightCoord;
				#endif

				return UnityMetaFragment(metaInput);
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Universal2D"
			Tags { "LightMode"="Universal2D" }

			Blend [_Src] [_Dst], One OneMinusSrcAlpha
			ZWrite [_ZWrite]
			ZTest [_ZTest]
			Offset 0 , 0
			ColorMask RGBA

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define _SPECULAR_SETUP 1
			#define _SURFACE_TYPE_TRANSPARENT 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003
			#define REQUIRE_DEPTH_TEXTURE 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SHADERPASS SHADERPASS_2D

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature_local _USEWORLDSPACE_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 ase_color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _Color01;
			float3 _TriplanarTileOffsetCustom;
			float3 _TriplanarTile;
			float2 _UVNoiseScale;
			float _Cull;
			float _DepthFadeOpacity;
			float _RefractionFresnelPower;
			float _RefractionFresnelScale;
			float _RefractionFresnelBias;
			float _RefractionMax;
			float _RefractionMin;
			float _Smoothness;
			float _Specular;
			float _WaterNormalStrength;
			float _ErodeIn;
			float _CameraDepthFadeLength;
			float _WavesIntensity;
			float _WorldUVNoiseIntensity;
			float _WavesSpeed;
			float _UVNoiseIntensity;
			float _WavesTiling;
			float _ZWrite;
			float _Dst;
			float _Src;
			float _ZTest;
			float _ErodeOut;
			float _CameraDepthFadeOffset;
			float _AlphaClip;
			float _Cutoff;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _WorldNoise;


			
			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_TRANSFER_INSTANCE_ID( input, output );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				float4 ase_positionCS = TransformObjectToHClip( ( input.positionOS ).xyz );
				float4 screenPos = ComputeScreenPos( ase_positionCS );
				output.ase_texcoord1 = screenPos;
				float3 customSurfaceDepth170 = input.positionOS.xyz;
				float customEye170 = -TransformWorldToView(TransformObjectToWorld(customSurfaceDepth170)).z;
				output.ase_texcoord2.x = customEye170;
				
				output.ase_color = input.ase_color;
				output.ase_texcoord3 = input.ase_texcoord;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord2.yzw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.ase_color = input.ase_color;
				output.ase_texcoord = input.ase_texcoord;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(PackedVaryings input  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( input.positionWS );
				float4 ShadowCoord = shadowCoord;

				float3 temp_cast_0 = (0.0).xxx;
				
				float4 screenPos = input.ase_texcoord1;
				float4 ase_positionSSNorm = screenPos / screenPos.w;
				ase_positionSSNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_positionSSNorm.z : ase_positionSSNorm.z * 0.5 + 0.5;
				float screenDepth36 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_positionSSNorm.xy ),_ZBufferParams);
				float distanceDepth36 = saturate( ( screenDepth36 - LinearEyeDepth( ase_positionSSNorm.z,_ZBufferParams ) ) / ( _DepthFadeOpacity ) );
				float DF59 = distanceDepth36;
				float customEye170 = input.ase_texcoord2.x;
				float cameraDepthFade170 = (( customEye170 -_ProjectionParams.y - _CameraDepthFadeOffset ) / _CameraDepthFadeLength);
				float camDF183 = saturate( cameraDepthFade170 );
				float2 texCoord352 = input.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float3 objToWorld518 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				#ifdef _USEWORLDSPACE_ON
				float3 staticSwitch525 = PositionWS;
				#else
				float3 staticSwitch525 = ( PositionWS - objToWorld518 );
				#endif
				float3 temp_output_520_0 = ( staticSwitch525 + _TriplanarTileOffsetCustom );
				float4 tex2DNode365 = tex2D( _WorldNoise, ( float3( (temp_output_520_0).xz ,  0.0 ) * _TriplanarTile ).xy );
				float2 appendResult527 = (float2(( ( tex2DNode365.r + -0.2 ) * 2.0 ) , ( ( tex2DNode365.g + -0.2 ) * 2.0 )));
				float2 worldUVNoise364 = ( appendResult527 * _WorldUVNoiseIntensity );
				float clampResult9_g86 = clamp( ( ( length(  (float2( -1,-1 ) + ( ( texCoord352 + worldUVNoise364 ) - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.666 ) * 4.2 ) , 0.0 , 1.0 );
				float2 texCoord356 = input.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g87 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord356 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.5 ) * 2.0 ) , 0.0 , 1.0 );
				float erodeIn534 = input.ase_texcoord3.z;
				float temp_output_539_0 = ( erodeIn534 + _ErodeIn );
				float2 texCoord377 = input.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g88 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord377 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult372 = smoothstep( temp_output_539_0 , ( temp_output_539_0 + 0.5 ) , ( 1.0 - saturate( clampResult9_g88 ) ));
				float erodeOut535 = input.ase_texcoord3.w;
				float temp_output_537_0 = ( erodeOut535 + _ErodeOut );
				float2 texCoord385 = input.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g90 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord385 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult375 = smoothstep( temp_output_537_0 , ( temp_output_537_0 + 0.5 ) , saturate( clampResult9_g90 ));
				float temp_output_392_0 = saturate( ( saturate( ( saturate( ( ( 1.0 - saturate( clampResult9_g86 ) ) * ( 1.0 - saturate( clampResult9_g87 ) ) ) ) * smoothstepResult372 ) ) * smoothstepResult375 ) );
				float gangnamStyle397 = temp_output_392_0;
				

				float3 BaseColor = temp_cast_0;
				float Alpha = saturate( ( input.ase_color.a * saturate( ( saturate( ( saturate( DF59 ) * camDF183 ) ) * gangnamStyle397 ) ) ) );
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
				#endif

				half4 color = half4(BaseColor, Alpha );

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				return color;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthNormals"
			Tags { "LightMode"="DepthNormals" }

			ZWrite On
			Blend One Zero
			ZTest LEqual
			ZWrite On

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define _SPECULAR_SETUP 1
			#define _SURFACE_TYPE_TRANSPARENT 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003
			#define REQUIRE_DEPTH_TEXTURE 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
			//#define SHADERPASS SHADERPASS_DEPTHNORMALS

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined( UNITY_INSTANCING_ENABLED ) && defined( ASE_INSTANCED_TERRAIN ) && ( defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL) || defined(_INSTANCEDTERRAINNORMALS_PIXEL) )
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_TEXTURE_COORDINATES1
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES1
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_SCREEN_POSITION_NORMALIZED
			#define ASE_NEEDS_VERT_POSITION
			#pragma shader_feature_local _USEWORLDSPACE_ON


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				half4 texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				half3 normalWS : TEXCOORD1;
				float4 tangentWS : TEXCOORD2; // holds terrainUV ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_color : COLOR;
				float4 ase_texcoord5 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _Color01;
			float3 _TriplanarTileOffsetCustom;
			float3 _TriplanarTile;
			float2 _UVNoiseScale;
			float _Cull;
			float _DepthFadeOpacity;
			float _RefractionFresnelPower;
			float _RefractionFresnelScale;
			float _RefractionFresnelBias;
			float _RefractionMax;
			float _RefractionMin;
			float _Smoothness;
			float _Specular;
			float _WaterNormalStrength;
			float _ErodeIn;
			float _CameraDepthFadeLength;
			float _WavesIntensity;
			float _WorldUVNoiseIntensity;
			float _WavesSpeed;
			float _UVNoiseIntensity;
			float _WavesTiling;
			float _ZWrite;
			float _Dst;
			float _Src;
			float _ZTest;
			float _ErodeOut;
			float _CameraDepthFadeOffset;
			float _AlphaClip;
			float _Cutoff;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _WaveMask;
			sampler2D _UVNoise;
			sampler2D _WorldNoise;


			
			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 customSurfaceDepth170 = input.positionOS.xyz;
				float customEye170 = -TransformWorldToView(TransformObjectToWorld(customSurfaceDepth170)).z;
				output.ase_texcoord5.x = customEye170;
				
				output.ase_texcoord3 = input.texcoord;
				output.ase_texcoord4 = input.ase_texcoord1;
				output.ase_color = input.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord5.yzw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				output.positionCS = ASE_ADJUST_CLIP_POSITION( vertexInput.positionCS );
				output.positionWS = vertexInput.positionWS;
				output.normalWS = normalInput.normalWS;
				output.tangentWS = float4( normalInput.tangentWS, ( input.tangentOS.w > 0.0 ? 1.0 : -1.0 ) * GetOddNegativeScale() );

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					output.tangentWS.zw = input.texcoord.xy;
					output.tangentWS.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				output.texcoord = input.texcoord;
				output.ase_texcoord1 = input.ase_texcoord1;
				output.ase_color = input.ase_color;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			void frag(	PackedVaryings input
						, out half4 outNormalWS : SV_Target0
						#if defined( ASE_WRITE_DEPTH )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						#if ( UNITY_VERSION >= 60020000 )
						, out uint outRenderingLayers : SV_Target1
						#else
						, out float4 outRenderingLayers : SV_Target1
						#endif
						#endif
						 )
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				// @diogo: mikktspace compliant
				float renormFactor = 1.0 / max( FLT_MIN, length( input.normalWS ) );

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( input.positionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );
				float3 TangentWS = input.tangentWS.xyz * renormFactor;
				float3 BitangentWS = cross( input.normalWS, input.tangentWS.xyz ) * input.tangentWS.w * renormFactor;
				float3 NormalWS = input.normalWS * renormFactor;

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float2 sampleCoords = (input.tangentWS.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					NormalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					TangentWS = -cross(GetObjectToWorldMatrix()._13_23_33, NormalWS);
					BitangentWS = cross(NormalWS, -TangentWS);
				#endif

				float2 texCoord437 = input.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult433 = (float2(0.005 , 0.0));
				float wavesTiling444 = _WavesTiling;
				float2 temp_cast_0 = (( wavesTiling444 / 2.0 )).xx;
				float2 temp_output_2_0_g94 = ( ( ( texCoord437 + appendResult433 ) * wavesTiling444 ) - temp_cast_0 );
				float2 temp_output_15_0_g94 = ( temp_output_2_0_g94 * temp_output_2_0_g94 );
				float2 appendResult20_g94 = (float2(frac( ( atan2( (temp_output_2_0_g94).x , (temp_output_2_0_g94).y ) / 6.283 ) ) , sqrt( ( (temp_output_15_0_g94).x + (temp_output_15_0_g94).y ) )));
				float2 temp_output_468_0 = appendResult20_g94;
				float saferPower469 = abs( (temp_output_468_0).y );
				float2 texCoord423 = input.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float randomOffset532 = input.ase_texcoord4.x;
				float4 tex2DNode419 = tex2D( _UVNoise, ( ( texCoord423 * _UVNoiseScale ) + randomOffset532 ) );
				float2 appendResult429 = (float2(( ( tex2DNode419.r + -0.2 ) * 2.0 ) , ( ( tex2DNode419.g + -0.2 ) * 2.0 )));
				float2 UVNoise421 = ( appendResult429 * _UVNoiseIntensity );
				float2 appendResult472 = (float2((temp_output_468_0).x , ( pow( saferPower469 , 2.0 ) + UVNoise421 ).x));
				float wavesSpeed482 = _WavesSpeed;
				float2 appendResult475 = (float2(0.0 , frac( ( _TimeParameters.x * wavesSpeed482 ) )));
				float3 objToWorld518 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				#ifdef _USEWORLDSPACE_ON
				float3 staticSwitch525 = PositionWS;
				#else
				float3 staticSwitch525 = ( PositionWS - objToWorld518 );
				#endif
				float3 temp_output_520_0 = ( staticSwitch525 + _TriplanarTileOffsetCustom );
				float4 tex2DNode365 = tex2D( _WorldNoise, ( float3( (temp_output_520_0).xz ,  0.0 ) * _TriplanarTile ).xy );
				float2 appendResult527 = (float2(( ( tex2DNode365.r + -0.2 ) * 2.0 ) , ( ( tex2DNode365.g + -0.2 ) * 2.0 )));
				float2 worldUVNoise364 = ( appendResult527 * _WorldUVNoiseIntensity );
				float2 texCoord439 = input.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_4 = (( wavesTiling444 / 2.0 )).xx;
				float2 temp_output_2_0_g95 = ( ( texCoord439 * wavesTiling444 ) - temp_cast_4 );
				float2 temp_output_15_0_g95 = ( temp_output_2_0_g95 * temp_output_2_0_g95 );
				float2 appendResult20_g95 = (float2(frac( ( atan2( (temp_output_2_0_g95).x , (temp_output_2_0_g95).y ) / 6.283 ) ) , sqrt( ( (temp_output_15_0_g95).x + (temp_output_15_0_g95).y ) )));
				float2 temp_output_498_0 = appendResult20_g95;
				float saferPower486 = abs( (temp_output_498_0).y );
				float2 appendResult489 = (float2((temp_output_498_0).x , ( pow( saferPower486 , 2.0 ) + UVNoise421 ).x));
				float2 appendResult493 = (float2(0.0 , frac( ( _TimeParameters.x * wavesSpeed482 ) )));
				float4 tex2DNode403 = tex2D( _WaveMask, ( ( appendResult489 - appendResult493 ) + worldUVNoise364 ) );
				float3 appendResult410 = (float3(1.0 , 0.0 , ( ( tex2D( _WaveMask, ( ( appendResult472 - appendResult475 ) + worldUVNoise364 ) ).g - tex2DNode403.g ) * _WavesIntensity )));
				float2 appendResult434 = (float2(0.0 , 0.005));
				float2 texCoord436 = input.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_6 = (( wavesTiling444 / 2.0 )).xx;
				float2 temp_output_2_0_g96 = ( ( ( appendResult434 + texCoord436 ) * wavesTiling444 ) - temp_cast_6 );
				float2 temp_output_15_0_g96 = ( temp_output_2_0_g96 * temp_output_2_0_g96 );
				float2 appendResult20_g96 = (float2(frac( ( atan2( (temp_output_2_0_g96).x , (temp_output_2_0_g96).y ) / 6.283 ) ) , sqrt( ( (temp_output_15_0_g96).x + (temp_output_15_0_g96).y ) )));
				float2 temp_output_513_0 = appendResult20_g96;
				float saferPower501 = abs( (temp_output_513_0).y );
				float2 appendResult504 = (float2((temp_output_513_0).x , ( pow( saferPower501 , 2.0 ) + UVNoise421 ).x));
				float2 appendResult508 = (float2(0.0 , frac( ( _TimeParameters.x * wavesSpeed482 ) )));
				float3 appendResult411 = (float3(0.0 , 1.0 , ( ( tex2DNode403.g - tex2D( _WaveMask, ( ( appendResult504 - appendResult508 ) + worldUVNoise364 ) ).g ) * _WavesIntensity )));
				float2 texCoord352 = input.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g86 = clamp( ( ( length(  (float2( -1,-1 ) + ( ( texCoord352 + worldUVNoise364 ) - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.666 ) * 4.2 ) , 0.0 , 1.0 );
				float2 texCoord356 = input.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g87 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord356 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.5 ) * 2.0 ) , 0.0 , 1.0 );
				float erodeIn534 = input.ase_texcoord3.z;
				float temp_output_539_0 = ( erodeIn534 + _ErodeIn );
				float2 texCoord377 = input.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g88 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord377 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult372 = smoothstep( temp_output_539_0 , ( temp_output_539_0 + 0.5 ) , ( 1.0 - saturate( clampResult9_g88 ) ));
				float erodeOut535 = input.ase_texcoord3.w;
				float temp_output_537_0 = ( erodeOut535 + _ErodeOut );
				float2 texCoord385 = input.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g90 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord385 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult375 = smoothstep( temp_output_537_0 , ( temp_output_537_0 + 0.5 ) , saturate( clampResult9_g90 ));
				float temp_output_392_0 = saturate( ( saturate( ( saturate( ( ( 1.0 - saturate( clampResult9_g86 ) ) * ( 1.0 - saturate( clampResult9_g87 ) ) ) ) * smoothstepResult372 ) ) * smoothstepResult375 ) );
				float3 lerpResult347 = lerp( float3( 0, 0, 1 ) , cross( appendResult410 , appendResult411 ) , temp_output_392_0);
				float3 normanReedus398 = lerpResult347;
				float3 appendResult138 = (float3(_WaterNormalStrength , _WaterNormalStrength , 1.0));
				float3 WaterNormal140 = ( normanReedus398 * appendResult138 );
				
				float screenDepth36 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPosNorm.xy ),_ZBufferParams);
				float distanceDepth36 = saturate( ( screenDepth36 - LinearEyeDepth( ScreenPosNorm.z,_ZBufferParams ) ) / ( _DepthFadeOpacity ) );
				float DF59 = distanceDepth36;
				float customEye170 = input.ase_texcoord5.x;
				float cameraDepthFade170 = (( customEye170 -_ProjectionParams.y - _CameraDepthFadeOffset ) / _CameraDepthFadeLength);
				float camDF183 = saturate( cameraDepthFade170 );
				float gangnamStyle397 = temp_output_392_0;
				

				float3 Normal = WaterNormal140;
				float Alpha = saturate( ( input.ase_color.a * saturate( ( saturate( ( saturate( DF59 ) * camDF183 ) ) * gangnamStyle397 ) ) ) );
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = input.positionCS.z;
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				#if defined(_GBUFFER_NORMALS_OCT)
					float2 octNormalWS = PackNormalOctQuadEncode(NormalWS);
					float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
					half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
					outNormalWS = half4(packedNormalWS, 0.0);
				#else
					#if defined(_NORMALMAP)
						#if _NORMAL_DROPOFF_TS
							float3 normalWS = TransformTangentToWorld(Normal, half3x3(TangentWS, BitangentWS, NormalWS));
						#elif _NORMAL_DROPOFF_OS
							float3 normalWS = TransformObjectToWorldNormal(Normal);
						#elif _NORMAL_DROPOFF_WS
							float3 normalWS = Normal;
						#endif
					#else
						float3 normalWS = NormalWS;
					#endif
					outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					#if ( UNITY_VERSION >= 60020000 )
					outRenderingLayers = EncodeMeshRenderingLayer();
					#else
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4( EncodeMeshRenderingLayer( renderingLayers ), 0, 0, 0 );
					#endif
				#endif
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "GBuffer"
			Tags { "LightMode"="UniversalGBuffer" }

			Blend [_Src] [_Dst], One OneMinusSrcAlpha
			ZWrite [_ZWrite]
			ZTest [_ZTest]
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#define _NORMAL_DROPOFF_TS 1
			#pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define _SPECULAR_SETUP 1
			#define _SURFACE_TYPE_TRANSPARENT 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003
			#define REQUIRE_OPAQUE_TEXTURE 1
			#define REQUIRE_DEPTH_TEXTURE 1


			// Deferred Rendering Path does not support the OpenGL-based graphics API:
			// Desktop OpenGL, OpenGL ES 3.0, WebGL 2.0.
			#pragma exclude_renderers glcore gles3 

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#if ( UNITY_VERSION >= 60000058 )
			#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
			#endif
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#if ( UNITY_VERSION >= 60030000 )
			#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
			#endif
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
			#pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
			#if ( UNITY_VERSION >= 60010000 )
			#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
			#endif

			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
			#pragma multi_compile _ LIGHTMAP_ON
			#if ( UNITY_VERSION >= 60010000 )
			#pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
			#endif
			#if ( UNITY_VERSION >= 60030000 )
			#pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
			#endif
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON

			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SHADERPASS SHADERPASS_GBUFFER

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if ( UNITY_VERSION >= 60030016 && UNITY_VERSION < 60040000 ) || ( UNITY_VERSION >= 60040010 )
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutputFormat.hlsl"
			#endif

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined( UNITY_INSTANCING_ENABLED ) && defined( ASE_INSTANCED_TERRAIN ) && ( defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL) || defined(_INSTANCEDTERRAINNORMALS_PIXEL) )
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_TEXTURE_COORDINATES1
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES1
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_SCREEN_POSITION_NORMALIZED
			#define ASE_NEEDS_FRAG_WORLD_VIEW_DIR
			#define ASE_NEEDS_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_TANGENT
			#define ASE_NEEDS_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_BITANGENT
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_VERT_POSITION
			#pragma shader_feature_local _USEWORLDSPACE_ON


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					float4 texcoord1 : TEXCOORD1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					float4 texcoord2 : TEXCOORD2;
				#endif
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				half3 normalWS : TEXCOORD1;
				float4 tangentWS : TEXCOORD2; // holds terrainUV ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
				float4 lightmapUVOrVertexSH : TEXCOORD3;
				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					half4 fogFactorAndVertexLight : TEXCOORD4;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON)
					float2 dynamicLightmapUV : TEXCOORD5;
				#endif
				#if defined(USE_APV_PROBE_OCCLUSION)
					float4 probeOcclusion : TEXCOORD6;
				#endif
				float4 ase_texcoord7 : TEXCOORD7;
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_color : COLOR;
				float4 ase_texcoord9 : TEXCOORD9;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _Color01;
			float3 _TriplanarTileOffsetCustom;
			float3 _TriplanarTile;
			float2 _UVNoiseScale;
			float _Cull;
			float _DepthFadeOpacity;
			float _RefractionFresnelPower;
			float _RefractionFresnelScale;
			float _RefractionFresnelBias;
			float _RefractionMax;
			float _RefractionMin;
			float _Smoothness;
			float _Specular;
			float _WaterNormalStrength;
			float _ErodeIn;
			float _CameraDepthFadeLength;
			float _WavesIntensity;
			float _WorldUVNoiseIntensity;
			float _WavesSpeed;
			float _UVNoiseIntensity;
			float _WavesTiling;
			float _ZWrite;
			float _Dst;
			float _Src;
			float _ZTest;
			float _ErodeOut;
			float _CameraDepthFadeOffset;
			float _AlphaClip;
			float _Cutoff;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _WaveMask;
			sampler2D _UVNoise;
			sampler2D _WorldNoise;


			#if ( UNITY_VERSION >= 60010000 )
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
			#else
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
			#endif

			
			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 customSurfaceDepth170 = input.positionOS.xyz;
				float customEye170 = -TransformWorldToView(TransformObjectToWorld(customSurfaceDepth170)).z;
				output.ase_texcoord9.x = customEye170;
				
				output.ase_texcoord7 = input.texcoord;
				output.ase_texcoord8 = input.texcoord1;
				output.ase_color = input.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord9.yzw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				#ifdef ASE_CUSTOM_MOTION_VECTOR
					// Declared so the Motion Vector output port surfaces on the master node; only consumed by the motion vector passes.
					float3 aseCustomMotionVector = float3(0, 0, 0);
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				OUTPUT_LIGHTMAP_UV(input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy);
				#if defined(DYNAMICLIGHTMAP_ON)
					output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif
				OUTPUT_SH4(vertexInput.positionWS, normalInput.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.lightmapUVOrVertexSH.xyz, output.probeOcclusion);

				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					output.fogFactorAndVertexLight = 0;
					#if defined(ASE_FOG) && !defined(_FOG_FRAGMENT)
						// @diogo: no fog applied in GBuffer
					#endif
					#ifdef _ADDITIONAL_LIGHTS_VERTEX
						half3 vertexLight = VertexLighting( vertexInput.positionWS, normalInput.normalWS );
						output.fogFactorAndVertexLight.yzw = vertexLight;
					#endif
				#endif

				output.positionCS = ASE_ADJUST_CLIP_POSITION( vertexInput.positionCS );
				output.positionWS = vertexInput.positionWS;
				output.normalWS = normalInput.normalWS;
				output.tangentWS = float4( normalInput.tangentWS, ( input.tangentOS.w > 0.0 ? 1.0 : -1.0 ) * GetOddNegativeScale() );

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					output.tangentWS.zw = input.texcoord.xy;
					output.tangentWS.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					float4 texcoord1 : TEXCOORD1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					float4 texcoord2 : TEXCOORD2;
				#endif
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					output.texcoord1 = input.texcoord1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					output.texcoord2 = input.texcoord2;
				#endif
				output.ase_color = input.ase_color;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				#endif
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

		#if ( UNITY_VERSION >= 60010000 )
			GBufferFragOutput frag ( PackedVaryings input
		#else
			FragmentOutput frag ( PackedVaryings input
		#endif
								#if defined( ASE_WRITE_DEPTH )
								,out float outputDepth : ASE_SV_DEPTH
								#endif
								, uint ase_vface : SV_IsFrontFace )
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					float4 shadowCoord = TransformWorldToShadowCoord( input.positionWS );
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				// @diogo: mikktspace compliant
				float renormFactor = 1.0 / max( FLT_MIN, length( input.normalWS ) );

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float3 ViewDirWS = GetWorldSpaceNormalizeViewDir( PositionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );
				float3 TangentWS = input.tangentWS.xyz * renormFactor;
				float3 BitangentWS = cross( input.normalWS, input.tangentWS.xyz ) * input.tangentWS.w * renormFactor;
				float3 NormalWS = input.normalWS * renormFactor;

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float2 sampleCoords = (input.tangentWS.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					NormalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					TangentWS = -cross(GetObjectToWorldMatrix()._13_23_33, NormalWS);
					BitangentWS = cross(NormalWS, -TangentWS);
				#endif

				float3 temp_cast_0 = (0.0).xxx;
				
				float2 texCoord437 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult433 = (float2(0.005 , 0.0));
				float wavesTiling444 = _WavesTiling;
				float2 temp_cast_1 = (( wavesTiling444 / 2.0 )).xx;
				float2 temp_output_2_0_g94 = ( ( ( texCoord437 + appendResult433 ) * wavesTiling444 ) - temp_cast_1 );
				float2 temp_output_15_0_g94 = ( temp_output_2_0_g94 * temp_output_2_0_g94 );
				float2 appendResult20_g94 = (float2(frac( ( atan2( (temp_output_2_0_g94).x , (temp_output_2_0_g94).y ) / 6.283 ) ) , sqrt( ( (temp_output_15_0_g94).x + (temp_output_15_0_g94).y ) )));
				float2 temp_output_468_0 = appendResult20_g94;
				float saferPower469 = abs( (temp_output_468_0).y );
				float2 texCoord423 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float randomOffset532 = input.ase_texcoord8.x;
				float4 tex2DNode419 = tex2D( _UVNoise, ( ( texCoord423 * _UVNoiseScale ) + randomOffset532 ) );
				float2 appendResult429 = (float2(( ( tex2DNode419.r + -0.2 ) * 2.0 ) , ( ( tex2DNode419.g + -0.2 ) * 2.0 )));
				float2 UVNoise421 = ( appendResult429 * _UVNoiseIntensity );
				float2 appendResult472 = (float2((temp_output_468_0).x , ( pow( saferPower469 , 2.0 ) + UVNoise421 ).x));
				float wavesSpeed482 = _WavesSpeed;
				float2 appendResult475 = (float2(0.0 , frac( ( _TimeParameters.x * wavesSpeed482 ) )));
				float3 objToWorld518 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				#ifdef _USEWORLDSPACE_ON
				float3 staticSwitch525 = PositionWS;
				#else
				float3 staticSwitch525 = ( PositionWS - objToWorld518 );
				#endif
				float3 temp_output_520_0 = ( staticSwitch525 + _TriplanarTileOffsetCustom );
				float4 tex2DNode365 = tex2D( _WorldNoise, ( float3( (temp_output_520_0).xz ,  0.0 ) * _TriplanarTile ).xy );
				float2 appendResult527 = (float2(( ( tex2DNode365.r + -0.2 ) * 2.0 ) , ( ( tex2DNode365.g + -0.2 ) * 2.0 )));
				float2 worldUVNoise364 = ( appendResult527 * _WorldUVNoiseIntensity );
				float2 texCoord439 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_5 = (( wavesTiling444 / 2.0 )).xx;
				float2 temp_output_2_0_g95 = ( ( texCoord439 * wavesTiling444 ) - temp_cast_5 );
				float2 temp_output_15_0_g95 = ( temp_output_2_0_g95 * temp_output_2_0_g95 );
				float2 appendResult20_g95 = (float2(frac( ( atan2( (temp_output_2_0_g95).x , (temp_output_2_0_g95).y ) / 6.283 ) ) , sqrt( ( (temp_output_15_0_g95).x + (temp_output_15_0_g95).y ) )));
				float2 temp_output_498_0 = appendResult20_g95;
				float saferPower486 = abs( (temp_output_498_0).y );
				float2 appendResult489 = (float2((temp_output_498_0).x , ( pow( saferPower486 , 2.0 ) + UVNoise421 ).x));
				float2 appendResult493 = (float2(0.0 , frac( ( _TimeParameters.x * wavesSpeed482 ) )));
				float4 tex2DNode403 = tex2D( _WaveMask, ( ( appendResult489 - appendResult493 ) + worldUVNoise364 ) );
				float3 appendResult410 = (float3(1.0 , 0.0 , ( ( tex2D( _WaveMask, ( ( appendResult472 - appendResult475 ) + worldUVNoise364 ) ).g - tex2DNode403.g ) * _WavesIntensity )));
				float2 appendResult434 = (float2(0.0 , 0.005));
				float2 texCoord436 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_7 = (( wavesTiling444 / 2.0 )).xx;
				float2 temp_output_2_0_g96 = ( ( ( appendResult434 + texCoord436 ) * wavesTiling444 ) - temp_cast_7 );
				float2 temp_output_15_0_g96 = ( temp_output_2_0_g96 * temp_output_2_0_g96 );
				float2 appendResult20_g96 = (float2(frac( ( atan2( (temp_output_2_0_g96).x , (temp_output_2_0_g96).y ) / 6.283 ) ) , sqrt( ( (temp_output_15_0_g96).x + (temp_output_15_0_g96).y ) )));
				float2 temp_output_513_0 = appendResult20_g96;
				float saferPower501 = abs( (temp_output_513_0).y );
				float2 appendResult504 = (float2((temp_output_513_0).x , ( pow( saferPower501 , 2.0 ) + UVNoise421 ).x));
				float2 appendResult508 = (float2(0.0 , frac( ( _TimeParameters.x * wavesSpeed482 ) )));
				float3 appendResult411 = (float3(0.0 , 1.0 , ( ( tex2DNode403.g - tex2D( _WaveMask, ( ( appendResult504 - appendResult508 ) + worldUVNoise364 ) ).g ) * _WavesIntensity )));
				float2 texCoord352 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g86 = clamp( ( ( length(  (float2( -1,-1 ) + ( ( texCoord352 + worldUVNoise364 ) - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.666 ) * 4.2 ) , 0.0 , 1.0 );
				float2 texCoord356 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g87 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord356 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.5 ) * 2.0 ) , 0.0 , 1.0 );
				float erodeIn534 = input.ase_texcoord7.z;
				float temp_output_539_0 = ( erodeIn534 + _ErodeIn );
				float2 texCoord377 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g88 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord377 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult372 = smoothstep( temp_output_539_0 , ( temp_output_539_0 + 0.5 ) , ( 1.0 - saturate( clampResult9_g88 ) ));
				float erodeOut535 = input.ase_texcoord7.w;
				float temp_output_537_0 = ( erodeOut535 + _ErodeOut );
				float2 texCoord385 = input.ase_texcoord7.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g90 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord385 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult375 = smoothstep( temp_output_537_0 , ( temp_output_537_0 + 0.5 ) , saturate( clampResult9_g90 ));
				float temp_output_392_0 = saturate( ( saturate( ( saturate( ( ( 1.0 - saturate( clampResult9_g86 ) ) * ( 1.0 - saturate( clampResult9_g87 ) ) ) ) * smoothstepResult372 ) ) * smoothstepResult375 ) );
				float3 lerpResult347 = lerp( float3( 0, 0, 1 ) , cross( appendResult410 , appendResult411 ) , temp_output_392_0);
				float3 normanReedus398 = lerpResult347;
				float3 appendResult138 = (float3(_WaterNormalStrength , _WaterNormalStrength , 1.0));
				float3 WaterNormal140 = ( normanReedus398 * appendResult138 );
				
				float spec84 = _Specular;
				float3 temp_cast_9 = (spec84).xxx;
				
				float smoothness90 = _Smoothness;
				
				float3 tanToWorld0 = float3( TangentWS.x, BitangentWS.x, NormalWS.x );
				float3 tanToWorld1 = float3( TangentWS.y, BitangentWS.y, NormalWS.y );
				float3 tanToWorld2 = float3( TangentWS.z, BitangentWS.z, NormalWS.z );
				float3 tanNormal1_g92 = float4( 0,0,1,0 ).rgb;
				float temp_output_4_0_g92 = _RefractionFresnelPower;
				float lerpResult3_g92 = lerp( ( -1.0 * temp_output_4_0_g92 ) , temp_output_4_0_g92 , ase_vface);
				float fresnelNdotV1_g92 = dot( float3( dot( tanToWorld0, tanNormal1_g92 ), dot( tanToWorld1, tanNormal1_g92 ), dot( tanToWorld2, tanNormal1_g92 ) ), ViewDirWS );
				float fresnelNode1_g92 = ( _RefractionFresnelBias + _RefractionFresnelScale * pow( 1.0 - fresnelNdotV1_g92, lerpResult3_g92 ) );
				float temp_output_198_0 = fresnelNode1_g92;
				float lerpResult205 = lerp( _RefractionMin , _RefractionMax , saturate( temp_output_198_0 ));
				float4 fetchOpaqueVal23 = float4( SHADERGRAPH_SAMPLE_SCENE_COLOR( ( (ScreenPosNorm).xy + ( ( ( (WaterNormal140).xy + -0.5 ) * 2.0 ) * lerpResult205 ) ).xy ), 1.0 );
				float4 temp_output_39_0 = ( ( float4( _Color01 , 0.0 ) * input.ase_color ) * fetchOpaqueVal23 );
				
				float screenDepth36 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPosNorm.xy ),_ZBufferParams);
				float distanceDepth36 = saturate( ( screenDepth36 - LinearEyeDepth( ScreenPosNorm.z,_ZBufferParams ) ) / ( _DepthFadeOpacity ) );
				float DF59 = distanceDepth36;
				float customEye170 = input.ase_texcoord9.x;
				float cameraDepthFade170 = (( customEye170 -_ProjectionParams.y - _CameraDepthFadeOffset ) / _CameraDepthFadeLength);
				float camDF183 = saturate( cameraDepthFade170 );
				float gangnamStyle397 = temp_output_392_0;
				

				float3 BaseColor = temp_cast_0;
				float3 Normal = WaterNormal140;
				float3 Specular = temp_cast_9;
				float Metallic = 0;
				float Smoothness = smoothness90;
				float Occlusion = 1;
				float3 Emission = temp_output_39_0.rgb;
				float Alpha = saturate( ( input.ase_color.a * saturate( ( saturate( ( saturate( DF59 ) * camDF183 ) ) * gangnamStyle397 ) ) ) );
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
					float AlphaClipThresholdShadow = 0.5;
				#endif
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				float3 Transmission = 1;
				float3 Translucency = 1;

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = input.positionCS.z;
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_CHANGES_WORLD_POS)
					ShadowCoord = TransformWorldToShadowCoord( PositionWS );
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = PositionWS;
				inputData.positionCS = input.positionCS;
				inputData.normalizedScreenSpaceUV = ScreenPosNorm.xy;
				inputData.shadowCoord = ShadowCoord;

				#ifdef _NORMALMAP
					#if _NORMAL_DROPOFF_TS
						inputData.normalWS = TransformTangentToWorld(Normal, half3x3( TangentWS, BitangentWS, NormalWS ));
					#elif _NORMAL_DROPOFF_OS
						inputData.normalWS = TransformObjectToWorldNormal(Normal);
					#elif _NORMAL_DROPOFF_WS
						inputData.normalWS = Normal;
					#endif
				#else
					inputData.normalWS = NormalWS;
				#endif

				inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				inputData.viewDirectionWS = SafeNormalize( ViewDirWS );

				#ifdef ASE_FOG
					// @diogo: no fog applied in GBuffer
				#endif
				#ifdef _ADDITIONAL_LIGHTS_VERTEX
					inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
				#endif

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float3 SH = SampleSH(inputData.normalWS.xyz);
				#else
					float3 SH = input.lightmapUVOrVertexSH.xyz;
				#endif

				#if defined(_SCREEN_SPACE_IRRADIANCE) && ( UNITY_VERSION >= 60030000 )
					#if ( UNITY_VERSION >= 60060000 )
						inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy, inputData.normalWS));
					#else
						inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
					#endif
				#elif defined(DYNAMICLIGHTMAP_ON)
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
					inputData.bakedGI = SAMPLE_GI(SH,
						GetAbsolutePositionWS(inputData.positionWS),
						inputData.normalWS,
						inputData.viewDirectionWS,
						input.positionCS.xy,
						input.probeOcclusion,
						inputData.shadowMask);
				#else
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#endif

				#ifdef ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#endif

				#if defined(DEBUG_DISPLAY)
					#if defined(DYNAMICLIGHTMAP_ON)
						inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
						#endif
					#if defined(LIGHTMAP_ON)
						inputData.staticLightmapUV = input.lightmapUVOrVertexSH.xy;
					#else
						inputData.vertexSH = SH;
					#endif
					#if defined(USE_APV_PROBE_OCCLUSION)
						inputData.probeOcclusion = input.probeOcclusion;
					#endif
				#endif

				#ifdef _DBUFFER
					ApplyDecal(input.positionCS,
						BaseColor,
						Specular,
						inputData.normalWS,
						Metallic,
						Occlusion,
						Smoothness);
				#endif

				BRDFData brdfData;
				InitializeBRDFData(BaseColor, Metallic, Specular, Smoothness, Alpha, brdfData);

				Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
				half4 color;
				MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);

			#if ( UNITY_VERSION >= 60010000 )
				color.rgb = GlobalIllumination(brdfData, (BRDFData)0, 0,
                              inputData.bakedGI, Occlusion, inputData.positionWS,
                              inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
			#else
				color.rgb = GlobalIllumination(brdfData, inputData.bakedGI, Occlusion, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
			#endif

				color.a = Alpha;

				#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY
					color.rgb *= color.a;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

			#if ( UNITY_VERSION >= 60010000 )
				return PackGBuffersBRDFData(brdfData, inputData, Smoothness, Emission + color.rgb, Occlusion);
			#else
				return BRDFDataToGbuffer(brdfData, inputData, Smoothness, Emission + color.rgb, Occlusion);
			#endif
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "SceneSelectionPass"
			Tags { "LightMode"="SceneSelectionPass" }

			Cull Off
			AlphaToMask Off

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define _SPECULAR_SETUP 1
			#define _SURFACE_TYPE_TRANSPARENT 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003
			#define REQUIRE_DEPTH_TEXTURE 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SCENESELECTIONPASS 1

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_FRAG_SCREEN_POSITION_NORMALIZED
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature_local _USEWORLDSPACE_ON


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 ase_color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _Color01;
			float3 _TriplanarTileOffsetCustom;
			float3 _TriplanarTile;
			float2 _UVNoiseScale;
			float _Cull;
			float _DepthFadeOpacity;
			float _RefractionFresnelPower;
			float _RefractionFresnelScale;
			float _RefractionFresnelBias;
			float _RefractionMax;
			float _RefractionMin;
			float _Smoothness;
			float _Specular;
			float _WaterNormalStrength;
			float _ErodeIn;
			float _CameraDepthFadeLength;
			float _WavesIntensity;
			float _WorldUVNoiseIntensity;
			float _WavesSpeed;
			float _UVNoiseIntensity;
			float _WavesTiling;
			float _ZWrite;
			float _Dst;
			float _Src;
			float _ZTest;
			float _ErodeOut;
			float _CameraDepthFadeOffset;
			float _AlphaClip;
			float _Cutoff;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _WorldNoise;


			
			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			PackedVaryings VertexFunction(Attributes input  )
			{
				PackedVaryings output;
				ZERO_INITIALIZE(PackedVaryings, output);

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 customSurfaceDepth170 = input.positionOS.xyz;
				float customEye170 = -TransformWorldToView(TransformObjectToWorld(customSurfaceDepth170)).z;
				output.ase_texcoord1.x = customEye170;
				
				output.ase_color = input.ase_color;
				output.ase_texcoord2 = input.ase_texcoord;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.yzw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = ASE_ADJUST_CLIP_POSITION( vertexInput.positionCS );
				output.positionWS = vertexInput.positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.ase_color = input.ase_color;
				output.ase_texcoord = input.ase_texcoord;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag( PackedVaryings input
				#if defined( ASE_WRITE_DEPTH )
				,out float outputDepth : ASE_SV_DEPTH
				#endif
				 ) : SV_Target
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;

				float screenDepth36 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPosNorm.xy ),_ZBufferParams);
				float distanceDepth36 = saturate( ( screenDepth36 - LinearEyeDepth( ScreenPosNorm.z,_ZBufferParams ) ) / ( _DepthFadeOpacity ) );
				float DF59 = distanceDepth36;
				float customEye170 = input.ase_texcoord1.x;
				float cameraDepthFade170 = (( customEye170 -_ProjectionParams.y - _CameraDepthFadeOffset ) / _CameraDepthFadeLength);
				float camDF183 = saturate( cameraDepthFade170 );
				float2 texCoord352 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float3 objToWorld518 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				#ifdef _USEWORLDSPACE_ON
				float3 staticSwitch525 = PositionWS;
				#else
				float3 staticSwitch525 = ( PositionWS - objToWorld518 );
				#endif
				float3 temp_output_520_0 = ( staticSwitch525 + _TriplanarTileOffsetCustom );
				float4 tex2DNode365 = tex2D( _WorldNoise, ( float3( (temp_output_520_0).xz ,  0.0 ) * _TriplanarTile ).xy );
				float2 appendResult527 = (float2(( ( tex2DNode365.r + -0.2 ) * 2.0 ) , ( ( tex2DNode365.g + -0.2 ) * 2.0 )));
				float2 worldUVNoise364 = ( appendResult527 * _WorldUVNoiseIntensity );
				float clampResult9_g86 = clamp( ( ( length(  (float2( -1,-1 ) + ( ( texCoord352 + worldUVNoise364 ) - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.666 ) * 4.2 ) , 0.0 , 1.0 );
				float2 texCoord356 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g87 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord356 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.5 ) * 2.0 ) , 0.0 , 1.0 );
				float erodeIn534 = input.ase_texcoord2.z;
				float temp_output_539_0 = ( erodeIn534 + _ErodeIn );
				float2 texCoord377 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g88 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord377 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult372 = smoothstep( temp_output_539_0 , ( temp_output_539_0 + 0.5 ) , ( 1.0 - saturate( clampResult9_g88 ) ));
				float erodeOut535 = input.ase_texcoord2.w;
				float temp_output_537_0 = ( erodeOut535 + _ErodeOut );
				float2 texCoord385 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g90 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord385 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult375 = smoothstep( temp_output_537_0 , ( temp_output_537_0 + 0.5 ) , saturate( clampResult9_g90 ));
				float temp_output_392_0 = saturate( ( saturate( ( saturate( ( ( 1.0 - saturate( clampResult9_g86 ) ) * ( 1.0 - saturate( clampResult9_g87 ) ) ) ) * smoothstepResult372 ) ) * smoothstepResult375 ) );
				float gangnamStyle397 = temp_output_392_0;
				

				surfaceDescription.Alpha = saturate( ( input.ase_color.a * saturate( ( saturate( ( saturate( DF59 ) * camDF183 ) ) * gangnamStyle397 ) ) ) );
				#if defined( _ALPHATEST_ON )
					surfaceDescription.AlphaClipThreshold = _Cutoff;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(surfaceDescription.Alpha - surfaceDescription.AlphaClipThreshold);
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				return half4( _ObjectId, _PassValue, 1.0, 1.0 );
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "ScenePickingPass"
			Tags { "LightMode"="Picking" }

			AlphaToMask Off

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define _SPECULAR_SETUP 1
			#define _SURFACE_TYPE_TRANSPARENT 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003
			#define REQUIRE_DEPTH_TEXTURE 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

		    #define SCENEPICKINGPASS 1

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_FRAG_SCREEN_POSITION_NORMALIZED
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature_local _USEWORLDSPACE_ON


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 ase_color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _Color01;
			float3 _TriplanarTileOffsetCustom;
			float3 _TriplanarTile;
			float2 _UVNoiseScale;
			float _Cull;
			float _DepthFadeOpacity;
			float _RefractionFresnelPower;
			float _RefractionFresnelScale;
			float _RefractionFresnelBias;
			float _RefractionMax;
			float _RefractionMin;
			float _Smoothness;
			float _Specular;
			float _WaterNormalStrength;
			float _ErodeIn;
			float _CameraDepthFadeLength;
			float _WavesIntensity;
			float _WorldUVNoiseIntensity;
			float _WavesSpeed;
			float _UVNoiseIntensity;
			float _WavesTiling;
			float _ZWrite;
			float _Dst;
			float _Src;
			float _ZTest;
			float _ErodeOut;
			float _CameraDepthFadeOffset;
			float _AlphaClip;
			float _Cutoff;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _WorldNoise;


			
			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output;
				ZERO_INITIALIZE(PackedVaryings, output);

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 customSurfaceDepth170 = input.positionOS.xyz;
				float customEye170 = -TransformWorldToView(TransformObjectToWorld(customSurfaceDepth170)).z;
				output.ase_texcoord1.x = customEye170;
				
				output.ase_color = input.ase_color;
				output.ase_texcoord2 = input.ase_texcoord;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord1.yzw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = ASE_ADJUST_CLIP_POSITION( vertexInput.positionCS );
				output.positionWS = vertexInput.positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.ase_color = input.ase_color;
				output.ase_texcoord = input.ase_texcoord;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag( PackedVaryings input
				#if defined( ASE_WRITE_DEPTH )
				,out float outputDepth : ASE_SV_DEPTH
				#endif
				 ) : SV_Target
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;

				float screenDepth36 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPosNorm.xy ),_ZBufferParams);
				float distanceDepth36 = saturate( ( screenDepth36 - LinearEyeDepth( ScreenPosNorm.z,_ZBufferParams ) ) / ( _DepthFadeOpacity ) );
				float DF59 = distanceDepth36;
				float customEye170 = input.ase_texcoord1.x;
				float cameraDepthFade170 = (( customEye170 -_ProjectionParams.y - _CameraDepthFadeOffset ) / _CameraDepthFadeLength);
				float camDF183 = saturate( cameraDepthFade170 );
				float2 texCoord352 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float3 objToWorld518 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				#ifdef _USEWORLDSPACE_ON
				float3 staticSwitch525 = PositionWS;
				#else
				float3 staticSwitch525 = ( PositionWS - objToWorld518 );
				#endif
				float3 temp_output_520_0 = ( staticSwitch525 + _TriplanarTileOffsetCustom );
				float4 tex2DNode365 = tex2D( _WorldNoise, ( float3( (temp_output_520_0).xz ,  0.0 ) * _TriplanarTile ).xy );
				float2 appendResult527 = (float2(( ( tex2DNode365.r + -0.2 ) * 2.0 ) , ( ( tex2DNode365.g + -0.2 ) * 2.0 )));
				float2 worldUVNoise364 = ( appendResult527 * _WorldUVNoiseIntensity );
				float clampResult9_g86 = clamp( ( ( length(  (float2( -1,-1 ) + ( ( texCoord352 + worldUVNoise364 ) - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.666 ) * 4.2 ) , 0.0 , 1.0 );
				float2 texCoord356 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g87 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord356 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.5 ) * 2.0 ) , 0.0 , 1.0 );
				float erodeIn534 = input.ase_texcoord2.z;
				float temp_output_539_0 = ( erodeIn534 + _ErodeIn );
				float2 texCoord377 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g88 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord377 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult372 = smoothstep( temp_output_539_0 , ( temp_output_539_0 + 0.5 ) , ( 1.0 - saturate( clampResult9_g88 ) ));
				float erodeOut535 = input.ase_texcoord2.w;
				float temp_output_537_0 = ( erodeOut535 + _ErodeOut );
				float2 texCoord385 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g90 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord385 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult375 = smoothstep( temp_output_537_0 , ( temp_output_537_0 + 0.5 ) , saturate( clampResult9_g90 ));
				float temp_output_392_0 = saturate( ( saturate( ( saturate( ( ( 1.0 - saturate( clampResult9_g86 ) ) * ( 1.0 - saturate( clampResult9_g87 ) ) ) ) * smoothstepResult372 ) ) * smoothstepResult375 ) );
				float gangnamStyle397 = temp_output_392_0;
				

				surfaceDescription.Alpha = saturate( ( input.ase_color.a * saturate( ( saturate( ( saturate( DF59 ) * camDF183 ) ) * gangnamStyle397 ) ) ) );
				#if defined( _ALPHATEST_ON )
					surfaceDescription.AlphaClipThreshold = _Cutoff;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(surfaceDescription.Alpha - surfaceDescription.AlphaClipThreshold);
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				return unity_SelectionID;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "MotionVectors"
			Tags { "LightMode"="MotionVectors" }

			ColorMask RG

			HLSLPROGRAM

			#define ASE_GEOMETRY
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_TIME_BASED_MOTION_VECTORS
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define _SPECULAR_SETUP 1
			#define _SURFACE_TYPE_TRANSPARENT 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19912
			#define ASE_SRP_VERSION 170003
			#define REQUIRE_DEPTH_TEXTURE 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

            #define SHADERPASS SHADERPASS_MOTION_VECTORS

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
			#endif

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

			#define ASE_NEEDS_FRAG_SCREEN_POSITION_NORMALIZED
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature_local _USEWORLDSPACE_ON


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			#if ( UNITY_VERSION < 60010000 )
				#define APPLICATION_SPACE_WARP_MOTION APLICATION_SPACE_WARP_MOTION
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 positionOld : TEXCOORD4;
				#if _ADD_PRECOMPUTED_VELOCITY
					float3 alembicMotionVector : TEXCOORD5;
				#endif
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 positionCSNoJitter : TEXCOORD0;
				float4 previousPositionCSNoJitter : TEXCOORD1;
				float3 positionWS : TEXCOORD2;
				float4 ase_color : COLOR;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float3 _Color01;
			float3 _TriplanarTileOffsetCustom;
			float3 _TriplanarTile;
			float2 _UVNoiseScale;
			float _Cull;
			float _DepthFadeOpacity;
			float _RefractionFresnelPower;
			float _RefractionFresnelScale;
			float _RefractionFresnelBias;
			float _RefractionMax;
			float _RefractionMin;
			float _Smoothness;
			float _Specular;
			float _WaterNormalStrength;
			float _ErodeIn;
			float _CameraDepthFadeLength;
			float _WavesIntensity;
			float _WorldUVNoiseIntensity;
			float _WavesSpeed;
			float _UVNoiseIntensity;
			float _WavesTiling;
			float _ZWrite;
			float _Dst;
			float _Src;
			float _ZTest;
			float _ErodeOut;
			float _CameraDepthFadeOffset;
			float _AlphaClip;
			float _Cutoff;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _WorldNoise;


			
			// Applies the graph's vertex stage at a given time so the motion vector pass can
			// evaluate the current frame and re-evaluate the previous frame (procedural / time-based animation).
			Attributes ASEApplyVertexModification( Attributes input, float3 timeParameters, inout PackedVaryings output, out float3 customMotionVector  )
			{
				float3 currentTimeParameters = _TimeParameters.xyz;
				_TimeParameters.xyz = timeParameters;

				float3 customSurfaceDepth170 = input.positionOS.xyz;
				float customEye170 = -TransformWorldToView(TransformObjectToWorld(customSurfaceDepth170)).z;
				output.ase_texcoord3.x = customEye170;
				
				output.ase_color = input.ase_color;
				output.ase_texcoord4 = input.ase_texcoord;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord3.yzw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				customMotionVector = float3(0, 0, 0);

				_TimeParameters.xyz = currentTimeParameters;
				return input;
			}

			PackedVaryings VertexFunction( Attributes input )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				Attributes defaultInput = input;
				float3 currentMotionVector;
				input = ASEApplyVertexModification( input, _TimeParameters.xyz, output, currentMotionVector );

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				#if defined(APPLICATION_SPACE_WARP_MOTION)
					float4 positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, input.positionOS));
					float4 positionCS = positionCSNoJitter;
				#else
					float4 positionCS = vertexInput.positionCS;
					float4 positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, input.positionOS));
				#endif

				// Custom output and automatic time-based motion are mutually exclusive.
				#if defined(ASE_CUSTOM_MOTION_VECTOR)
					float3 prevPositionOS = ( unity_MotionVectorsParams.x == 1 ) ? input.positionOld : input.positionOS.xyz;
					prevPositionOS -= currentMotionVector;
				#else
					float3 prevPositionOS = ( unity_MotionVectorsParams.x == 1 ) ? input.positionOld : defaultInput.positionOS.xyz;
					#ifdef ASE_TIME_BASED_MOTION_VECTORS
						Attributes prevInput = defaultInput;
						prevInput.positionOS.xyz = prevPositionOS;
						PackedVaryings prevOutput = (PackedVaryings)0;
						float3 prevMotionVector;
						prevInput = ASEApplyVertexModification( prevInput, _LastTimeParameters.xyz, prevOutput, prevMotionVector );
						prevPositionOS = prevInput.positionOS.xyz;
					#endif
				#endif
				#if _ADD_PRECOMPUTED_VELOCITY
					prevPositionOS -= input.alembicMotionVector;
				#endif
				float4 previousPositionCSNoJitter = mul( _PrevViewProjMatrix, mul( UNITY_PREV_MATRIX_M, float4( prevPositionOS, 1 ) ) );

				output.positionCS = ASE_ADJUST_CLIP_POSITION( positionCS );
				output.positionCSNoJitter = ASE_ADJUST_CLIP_POSITION( positionCSNoJitter );
				output.previousPositionCSNoJitter = ASE_ADJUST_CLIP_POSITION( previousPositionCSNoJitter );
				output.positionWS = vertexInput.positionWS;

				return output;
			}

			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}

			half4 frag(	PackedVaryings input
				#if defined( ASE_WRITE_DEPTH )
				,out float outputDepth : ASE_SV_DEPTH
				#endif
				 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;

				float screenDepth36 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPosNorm.xy ),_ZBufferParams);
				float distanceDepth36 = saturate( ( screenDepth36 - LinearEyeDepth( ScreenPosNorm.z,_ZBufferParams ) ) / ( _DepthFadeOpacity ) );
				float DF59 = distanceDepth36;
				float customEye170 = input.ase_texcoord3.x;
				float cameraDepthFade170 = (( customEye170 -_ProjectionParams.y - _CameraDepthFadeOffset ) / _CameraDepthFadeLength);
				float camDF183 = saturate( cameraDepthFade170 );
				float2 texCoord352 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float3 objToWorld518 = mul( GetObjectToWorldMatrix(), float4( float3( 0,0,0 ), 1 ) ).xyz;
				#ifdef _USEWORLDSPACE_ON
				float3 staticSwitch525 = PositionWS;
				#else
				float3 staticSwitch525 = ( PositionWS - objToWorld518 );
				#endif
				float3 temp_output_520_0 = ( staticSwitch525 + _TriplanarTileOffsetCustom );
				float4 tex2DNode365 = tex2D( _WorldNoise, ( float3( (temp_output_520_0).xz ,  0.0 ) * _TriplanarTile ).xy );
				float2 appendResult527 = (float2(( ( tex2DNode365.r + -0.2 ) * 2.0 ) , ( ( tex2DNode365.g + -0.2 ) * 2.0 )));
				float2 worldUVNoise364 = ( appendResult527 * _WorldUVNoiseIntensity );
				float clampResult9_g86 = clamp( ( ( length(  (float2( -1,-1 ) + ( ( texCoord352 + worldUVNoise364 ) - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.666 ) * 4.2 ) , 0.0 , 1.0 );
				float2 texCoord356 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g87 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord356 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.5 ) * 2.0 ) , 0.0 , 1.0 );
				float erodeIn534 = input.ase_texcoord4.z;
				float temp_output_539_0 = ( erodeIn534 + _ErodeIn );
				float2 texCoord377 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g88 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord377 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult372 = smoothstep( temp_output_539_0 , ( temp_output_539_0 + 0.5 ) , ( 1.0 - saturate( clampResult9_g88 ) ));
				float erodeOut535 = input.ase_texcoord4.w;
				float temp_output_537_0 = ( erodeOut535 + _ErodeOut );
				float2 texCoord385 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult9_g90 = clamp( ( ( length(  (float2( -1,-1 ) + ( texCoord385 - float2( 0,0 ) ) * ( float2( 1,1 ) - float2( -1,-1 ) ) / ( float2( 1,1 ) - float2( 0,0 ) ) ) ) + -0.2 ) * 1.0 ) , 0.0 , 1.0 );
				float smoothstepResult375 = smoothstep( temp_output_537_0 , ( temp_output_537_0 + 0.5 ) , saturate( clampResult9_g90 ));
				float temp_output_392_0 = saturate( ( saturate( ( saturate( ( ( 1.0 - saturate( clampResult9_g86 ) ) * ( 1.0 - saturate( clampResult9_g87 ) ) ) ) * smoothstepResult372 ) ) * smoothstepResult375 ) );
				float gangnamStyle397 = temp_output_392_0;
				

				float Alpha = saturate( ( input.ase_color.a * saturate( ( saturate( ( saturate( DF59 ) * camDF183 ) ) * gangnamStyle397 ) ) ) );
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#if defined(ASE_CHANGES_WORLD_POS)
					float3 positionOS = mul( GetWorldToObjectMatrix(),  float4( PositionWS, 1.0 ) ).xyz;
					float3 previousPositionWS = mul( GetPrevObjectToWorldMatrix(),  float4( positionOS, 1.0 ) ).xyz;
					input.positionCSNoJitter = mul( _NonJitteredViewProjMatrix, float4( PositionWS, 1.0 ) );
					input.previousPositionCSNoJitter = mul( _PrevViewProjMatrix, float4( previousPositionWS, 1.0 ) );
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				#if defined(APPLICATION_SPACE_WARP_MOTION)
					return float4( CalcAswNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 1 );
				#else
					return float4( CalcNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 0, 0 );
				#endif
			}
			ENDHLSL
		}

	
	}
	

	

	CustomEditor "UnityEditor.ShaderGraphLitGUI"
	FallBack "Hidden/Shader Graph/FallbackError"
	
	Fallback Off
}
/*ASEBEGIN
Version=19912
{"type":"AmplifyShaderEditor.WorldPosInputsNode, AmplifyShaderEditor","id":517,"pos":[-10624,-4096],"params":["Inherit","False","0","4","FLOAT3","0","FLOAT","1","FLOAT","2","FLOAT","3"]}
{"type":"AmplifyShaderEditor.TransformPositionNode, AmplifyShaderEditor","id":518,"pos":[-10624,-3840],"params":["Inherit","False","Object","World","False","Fast","True","1","0","FLOAT3","0,0,0","False","5","FLOAT3","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor","id":519,"pos":[-10368,-3968],"params":["Inherit","False","2","0","FLOAT3","0,0,0","False","1","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor","id":525,"pos":[-10368,-4096],"params":["Inherit","False","Property","_UseWorldSpace","Use World Space","0","0","Create","True","0","0","0","False","0","False","","0","1","1","True","","Toggle","2","Key0","Key1","Create","True","True","All","9","1","FLOAT3","0,0,0","False","0","FLOAT3","0,0,0","False","2","FLOAT3","0,0,0","False","3","FLOAT3","0,0,0","False","4","FLOAT3","0,0,0","False","5","FLOAT3","0,0,0","False","6","FLOAT3","0,0,0","False","7","FLOAT3","0,0,0","False","8","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor","id":515,"pos":[-9856,-3584],"params":["Inherit","False","Property","_TriplanarTileOffsetCustom","Triplanar Tile Offset Custom","13","0","Create","True","0","0","0","False","0","False","Object","-1","","0,0,0","0,0,0","0","4","FLOAT3","0","FLOAT","1","FLOAT","2","FLOAT","3"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":520,"pos":[-9856,-4096],"params":["Inherit","False","2","2","0","FLOAT3","0,0,0","False","1","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor","id":413,"pos":[-8754,-4146],"params":["Inherit","False","1820.773","539.3306","World UV Noise","9","364","367","366","368","365","363","526","527","528","World UV Noise","0,0,0,1","0","0"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":524,"pos":[-9344,-3840],"params":["Inherit","False","True","False","True","False","1","0","FLOAT3","0,0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor","id":516,"pos":[-9088,-3712],"params":["Inherit","False","Property","_TriplanarTile","Triplanar Tile","12","0","Create","True","0","0","0","False","0","False","Object","-1","","1,1,1","1,1,1","0","4","FLOAT3","0","FLOAT","1","FLOAT","2","FLOAT","3"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":521,"pos":[-9088,-3840],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.TexturePropertyNode, AmplifyShaderEditor","id":363,"pos":[-8704,-4096],"params":["Inherit","True","Property","_WorldNoise","World Noise","10","0","Create","True","0","0","0","False","3","Space(33)","Header(World Noise)","Space(13)","False","","d96e50eec907e1c428a285fb1124a713","d96e50eec907e1c428a285fb1124a713","False","white","Auto","Texture2D","False","-1","0","2","SAMPLER2D","0","SAMPLERSTATE","1"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":365,"pos":[-8704,-3840],"params":["Inherit","True","Property","_TextureSample0","Texture Sample 0","29","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","white","Auto","False","Instance","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":526,"pos":[-8304,-3840],"params":["Inherit","True","ConstantBiasScale","-1","","84","63208df05c83e8e49a48ffbdce2e43a0","0","3","3","FLOAT","0","False","1","FLOAT","-0.2","False","2","FLOAT","2","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":528,"pos":[-8304,-4096],"params":["Inherit","True","ConstantBiasScale","-1","","85","63208df05c83e8e49a48ffbdce2e43a0","0","3","3","FLOAT","0","False","1","FLOAT","-0.2","False","2","FLOAT","2","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":368,"pos":[-7424,-3712],"params":["Inherit","False","Property","_WorldUVNoiseIntensity","World UV Noise Intensity","11","0","Create","True","0","0","0","False","0","False","Object","-1","","0.1","0.1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":527,"pos":[-7920,-3968],"params":["Inherit","True","FLOAT2","4","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":367,"pos":[-7424,-3840],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":364,"pos":[-7168,-3840],"params":["Inherit","False","worldUVNoise","-1","True","1","0","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":352,"pos":[-6912,-4096],"params":["Inherit","False","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":369,"pos":[-6400,-4224],"params":["Inherit","False","364","worldUVNoise","1","0","OBJECT","","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":353,"pos":[-6272,-3840],"params":["Inherit","False","Constant","_Float0","Float 0","28","0","Create","True","0","0","0","False","0","False","Object","-1","","0.666","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":362,"pos":[-6400,-4096],"params":["Inherit","True","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":354,"pos":[-6272,-3712],"params":["Inherit","False","Constant","_Float1","Float 1","28","0","Create","True","0","0","0","False","0","False","Object","-1","","4.2","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":360,"pos":[-6272,-3072],"params":["Inherit","False","Constant","_Float4","Float 1","28","0","Create","True","0","0","0","False","0","False","Object","-1","","2","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":356,"pos":[-6272,-3456],"params":["Inherit","False","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":359,"pos":[-6272,-3200],"params":["Inherit","False","Constant","_Float3","Float 0","28","0","Create","True","0","0","0","False","0","False","Object","-1","","0.5","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TexCoordVertexDataNode, AmplifyShaderEditor","id":529,"pos":[-7552,-2176],"params":["Inherit","False","0","4","0","5","FLOAT4","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":345,"pos":[-5888,-4096],"params":["Inherit","False","RadialGradient","-1","","86","ec972f7745a8353409da2eb8d000a2e3","0","3","1","FLOAT2","0,0","False","6","FLOAT","0","False","7","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":361,"pos":[-5888,-3456],"params":["Inherit","False","RadialGradient","-1","","87","ec972f7745a8353409da2eb8d000a2e3","0","3","1","FLOAT2","0,0","False","6","FLOAT","0","False","7","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":376,"pos":[-6272,-2304],"params":["Inherit","False","Constant","_Float5","Float 1","28","0","Create","True","0","0","0","False","0","False","Object","-1","","1","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":377,"pos":[-6272,-2688],"params":["Inherit","False","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":380,"pos":[-6272,-2432],"params":["Inherit","False","Constant","_Float6","Float 0","28","0","Create","True","0","0","0","False","0","False","Object","-1","","0.2","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":534,"pos":[-7296,-2048],"params":["Inherit","False","erodeIn","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":349,"pos":[-5504,-4096],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":357,"pos":[-5504,-3456],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":381,"pos":[-5888,-2688],"params":["Inherit","False","RadialGradient","-1","","88","ec972f7745a8353409da2eb8d000a2e3","0","3","1","FLOAT2","0,0","False","6","FLOAT","0","False","7","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":373,"pos":[-6272,-2816],"params":["Inherit","False","Property","_ErodeIn","Erode In","29","0","Create","True","0","0","0","False","0","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":538,"pos":[-6272,-2944],"params":["Inherit","False","534","erodeIn","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor","id":355,"pos":[-5248,-4096],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor","id":358,"pos":[-5248,-3456],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":378,"pos":[-5504,-2688],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor","id":188,"pos":[-3250,1230],"params":["Inherit","False","676","290.95","Depth Fade","3","38","36","59","Depth Fade","0,0,0,1","0","0"]}
{"type":"AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor","id":190,"pos":[-3248,2640],"params":["Inherit","False","804","674.95","Camera Depth Fade","6","166","167","168","170","172","183","Camera Depth Fade","0,0,0,1","0","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":535,"pos":[-7296,-1920],"params":["Inherit","False","erodeOut","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":539,"pos":[-6016,-2944],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":370,"pos":[-4992,-3840],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":382,"pos":[-6016,-2784],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0.5","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor","id":379,"pos":[-5248,-2688],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":385,"pos":[-6272,-1920],"params":["Inherit","False","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":38,"pos":[-3200,1408],"params":["Inherit","False","Property","_DepthFadeOpacity","Depth Fade Opacity","6","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":374,"pos":[-6272,-2048],"params":["Inherit","False","Property","_ErodeOut","Erode Out","30","0","Create","True","0","0","0","False","0","False","Object","-1","","-0.5","-0.5","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":536,"pos":[-6272,-2176],"params":["Inherit","False","535","erodeOut","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":166,"pos":[-3200,3200],"params":["Inherit","False","Property","_CameraDepthFadeOffset","Camera Depth Fade Offset","24","0","Create","True","0","0","0","False","0","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":167,"pos":[-3200,3072],"params":["Inherit","False","Property","_CameraDepthFadeLength","Camera Depth Fade Length","23","0","Create","True","0","0","0","False","3","Space(33)","Header(Camera Depth Fade)","Space(13)","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.PosVertexDataNode, AmplifyShaderEditor","id":168,"pos":[-3200,2816],"params":["Inherit","False","0","0","5","FLOAT3","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":389,"pos":[-5888,-1920],"params":["Inherit","False","RadialGradient","-1","","90","ec972f7745a8353409da2eb8d000a2e3","0","3","1","FLOAT2","0,0","False","6","FLOAT","0","False","7","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SmoothstepOpNode, AmplifyShaderEditor","id":372,"pos":[-4992,-2816],"params":["Inherit","True","3","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","1","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":371,"pos":[-4736,-3840],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.DepthFade, AmplifyShaderEditor","id":36,"pos":[-3200,1280],"params":["Inherit","False","True","True","False","2","1","FLOAT3","0,0,0","False","0","FLOAT","1","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":537,"pos":[-6016,-2176],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.CameraDepthFade, AmplifyShaderEditor","id":170,"pos":[-3200,2688],"params":["Inherit","False","3","2","FLOAT3","0,0,0","False","0","FLOAT","1","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":383,"pos":[-6016,-2016],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0.5","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":386,"pos":[-5504,-1920],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":390,"pos":[-4608,-3200],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":172,"pos":[-2944,2688],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SmoothstepOpNode, AmplifyShaderEditor","id":375,"pos":[-4992,-2048],"params":["Inherit","True","3","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","1","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":391,"pos":[-4352,-3200],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":59,"pos":[-2816,1280],"params":["Inherit","False","DF","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":180,"pos":[-2048,768],"params":["Inherit","False","59","DF","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":183,"pos":[-2688,2688],"params":["Inherit","False","camDF","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":393,"pos":[-4352,-2432],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":185,"pos":[-1280,640],"params":["Inherit","False","183","camDF","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":171,"pos":[-1536,768],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":392,"pos":[-4096,-2432],"params":["Inherit","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":173,"pos":[-1280,768],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":397,"pos":[-3712,-2432],"params":["Inherit","False","gangnamStyle","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":174,"pos":[-1024,768],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":396,"pos":[-768,1024],"params":["Inherit","False","397","gangnamStyle","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":394,"pos":[-768,896],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor","id":29,"pos":[-1280,384],"params":["Inherit","False","0","5","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":395,"pos":[-640,896],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor","id":189,"pos":[-3250,1742],"params":["Inherit","False","1060","674.95","Double Sided Fresnel","7","175","176","177","178","179","184","181","Double Sided Fresnel","0,0,0,1","0","0"]}
{"type":"AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor","id":326,"pos":[-2480,1232],"params":["Inherit","False","676","290.95","Depth Fade Color","3","323","325","324","Depth Fade Color","0,0,0,1","0","0"]}
{"type":"AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor","id":243,"pos":[-3250,4174],"params":["Inherit","False","932","162.9502","Smoothness","2","90","86","Smoothness","0,0,0,1","0","0"]}
{"type":"AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor","id":242,"pos":[-3250,3534],"params":["Inherit","False","932","418.95","Specular","2","84","80","Specular","0,0,0,1","0","0"]}
{"type":"AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor","id":15,"pos":[590,-50],"params":["Inherit","False","1252","162.95","Ge Lush was here! <3","5","10","14","11","12","13","Ge Lush was here! <3","0,0,0,1","0","0"]}
{"type":"AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor","id":415,"pos":[-8752,-3376],"params":["Inherit","False","1841","824.95","UV Noise","8","420","421","419","418","417","429","430","431","UV Noise","0,0,0,1","0","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":37,"pos":[-896,512],"params":["Inherit","False","2","2","0","FLOAT","1","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":175,"pos":[-3200,2304],"params":["Inherit","False","Property","_FresnelPower","Fresnel Power","17","0","Create","True","0","0","0","False","0","False","Object","-1","","5","5","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":176,"pos":[-3200,2176],"params":["Inherit","False","Property","_FresnelScale","Fresnel Scale","16","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":177,"pos":[-3200,2048],"params":["Inherit","False","Property","_FresnelBias","Fresnel Bias","15","0","Create","True","0","0","0","False","3","Space(33)","Header(Fresnel)","Space(13)","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":178,"pos":[-3200,1792],"params":["Inherit","False","DoubleSidedFresnel","-1","","91","64ed426cf297c5b48b5b91bdfa24d4b5","0","4","10","COLOR","0,0,1,0","False","7","FLOAT","0","False","6","FLOAT","1","False","4","FLOAT","5","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":184,"pos":[-2688,1792],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor","id":179,"pos":[-2944,1792],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":10,"pos":[640,0],"params":["Inherit","False","Property","_Cull","Cull","31","0","Create","True","0","0","0","True","3","Space(33)","Header(AR)","Space(13)","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":14,"pos":[1664,0],"params":["Inherit","False","Property","_ZTest","ZTest","35","0","Create","True","0","0","0","True","0","False","Object","-1","","2","2","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":11,"pos":[896,0],"params":["Inherit","False","Property","_Src","Src","32","0","Create","True","0","0","0","True","0","False","Object","-1","","5","5","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":12,"pos":[1152,0],"params":["Inherit","False","Property","_Dst","Dst","33","0","Create","True","0","0","0","True","0","False","Object","-1","","10","10","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":13,"pos":[1408,0],"params":["Inherit","False","Property","_ZWrite","ZWrite","34","0","Create","True","0","0","0","True","0","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":20,"pos":[-1536,0],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.ScreenPosInputsNode, AmplifyShaderEditor","id":21,"pos":[-2048,-384],"params":["Float","False","0","False","0","5","FLOAT4","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":22,"pos":[-1792,-384],"params":["Inherit","False","True","True","False","False","1","0","FLOAT4","0,0,0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.ScreenColorNode, AmplifyShaderEditor","id":23,"pos":[-1280,0],"params":["Inherit","False","Global","_GrabScreen0","Grab Screen 0","6","0","Create","True","0","0","0","False","0","False","","Object","-1","False","False","False","False","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","5","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":39,"pos":[-640,-384],"params":["Inherit","False","2","2","0","COLOR","0,0,0,0","False","1","COLOR","0,0,0,0","False","1","COLOR","0"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":18,"pos":[-2816,0],"params":["Inherit","False","True","True","False","True","1","0","FLOAT3","0,0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":141,"pos":[-3200,0],"params":["Inherit","False","140","WaterNormal","1","0","OBJECT","","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":19,"pos":[-2048,0],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":198,"pos":[-3200,256],"params":["Inherit","False","DoubleSidedFresnel","-1","","92","64ed426cf297c5b48b5b91bdfa24d4b5","0","4","10","COLOR","0,0,1,0","False","7","FLOAT","0","False","6","FLOAT","1","False","4","FLOAT","5","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":200,"pos":[-2688,256],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":196,"pos":[-3200,640],"params":["Inherit","False","Property","_RefractionFresnelScale","Refraction Fresnel Scale","19","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":195,"pos":[-3200,768],"params":["Inherit","False","Property","_RefractionFresnelPower","Refraction Fresnel Power","20","0","Create","True","0","0","0","False","0","False","Object","-1","","5","5","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":197,"pos":[-3200,512],"params":["Inherit","False","Property","_RefractionFresnelBias","Refraction Fresnel Bias","18","0","Create","True","0","0","0","False","3","Space(33)","Header(Refraction Fresnel)","Space(13)","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":204,"pos":[-2688,640],"params":["Inherit","False","Property","_RefractionMax","Refraction Max","22","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":203,"pos":[-2688,512],"params":["Inherit","False","Property","_RefractionMin","Refraction Min","21","0","Create","True","0","0","0","False","0","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.LerpOp, AmplifyShaderEditor","id":205,"pos":[-2432,256],"params":["Inherit","False","3","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":84,"pos":[-2560,3584],"params":["Inherit","False","spec","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":90,"pos":[-2560,4224],"params":["Inherit","False","smoothness","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":86,"pos":[-3200,4224],"params":["Inherit","False","Property","_Smoothness","Smoothness","4","0","Create","True","0","0","0","False","0","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.DepthFade, AmplifyShaderEditor","id":324,"pos":[-2432,1280],"params":["Inherit","False","True","True","False","2","1","FLOAT3","0,0,0","False","0","FLOAT","1","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":325,"pos":[-2432,1408],"params":["Inherit","False","Property","_DepthFadeColor","Depth Fade Color","5","0","Create","True","0","0","0","False","0","False","Object","-1","","3","3","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":331,"pos":[-128,-384],"params":["Inherit","False","2","2","0","COLOR","0,0,0,0","False","1","FLOAT","0","False","1","COLOR","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":332,"pos":[-128,-256],"params":["Inherit","False","Property","_Emission","Emission","2","0","Create","True","0","0","0","False","0","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":17,"pos":[-2560,0],"params":["Inherit","False","ConstantBiasScale","-1","","93","63208df05c83e8e49a48ffbdce2e43a0","0","3","3","FLOAT2","0,0","False","1","FLOAT","-0.5","False","2","FLOAT","2","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor","id":199,"pos":[-2944,320],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":80,"pos":[-3200,3584],"params":["Inherit","False","Property","_Specular","Specular","3","0","Create","True","0","0","0","False","0","False","Object","-1","","0.001","0.001","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.LerpOp, AmplifyShaderEditor","id":347,"pos":[-4096,-1280],"params":["Inherit","True","3","0","FLOAT3","0,0,0","False","1","FLOAT3","0,0,0","False","2","FLOAT","0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor","id":348,"pos":[-4480,-1536],"params":["Inherit","False","Constant","_Vector0","Vector 0","28","0","Create","True","0","0","0","False","0","False","Object","-1","","0,0,1","0,0,0","0","4","FLOAT3","0","FLOAT","1","FLOAT","2","FLOAT","3"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":398,"pos":[-3712,-1280],"params":["Inherit","False","normanReedus","-1","True","1","0","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.TexturePropertyNode, AmplifyShaderEditor","id":420,"pos":[-8704,-3328],"params":["Inherit","True","Property","_UVNoise","UV Noise","7","0","Create","True","0","0","0","False","3","Space(33)","Header(UV Noise)","Space(13)","False","","d96e50eec907e1c428a285fb1124a713","d96e50eec907e1c428a285fb1124a713","False","white","Auto","Texture2D","False","-1","0","2","SAMPLER2D","0","SAMPLERSTATE","1"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":419,"pos":[-8704,-3072],"params":["Inherit","True","Property","_TextureSample4","Texture Sample 0","29","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","white","Auto","False","Instance","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":418,"pos":[-7680,-3072],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":417,"pos":[-7680,-2944],"params":["Inherit","False","Property","_UVNoiseIntensity","UV Noise Intensity","8","0","Create","True","0","0","0","False","0","False","Object","-1","","0.1","0.1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":421,"pos":[-7296,-3072],"params":["Inherit","True","UVNoise","-1","True","1","0","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":400,"pos":[-7424,-1152],"params":["Inherit","True","Property","_TextureSample1","Texture Sample 0","29","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","white","Auto","False","Instance","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":403,"pos":[-7424,-768],"params":["Inherit","True","Property","_TextureSample2","Texture Sample 0","29","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","white","Auto","False","Instance","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":404,"pos":[-7424,-384],"params":["Inherit","True","Property","_TextureSample3","Texture Sample 0","29","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","white","Auto","False","Instance","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor","id":405,"pos":[-6912,-384],"params":["Inherit","False","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":406,"pos":[-6656,-384],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":407,"pos":[-6656,-1024],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":409,"pos":[-6912,-768],"params":["Inherit","False","Property","_WavesIntensity","Waves Intensity","26","0","Create","True","0","0","0","False","0","False","Object","-1","","8","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":410,"pos":[-6272,-1024],"params":["Inherit","True","FLOAT3","4","0","FLOAT","1","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":411,"pos":[-6272,-384],"params":["Inherit","True","FLOAT3","4","0","FLOAT","0","False","1","FLOAT","1","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.CrossProductOpNode, AmplifyShaderEditor","id":346,"pos":[-5760,-640],"params":["Inherit","True","2","0","FLOAT3","0,0,0","False","1","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor","id":402,"pos":[-6912,-1024],"params":["Inherit","False","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":437,"pos":[-11008,-1152],"params":["Inherit","False","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":438,"pos":[-10368,-1152],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":446,"pos":[-10112,-1152],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor","id":447,"pos":[-9856,-1152],"params":["Inherit","False","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleDivideOpNode, AmplifyShaderEditor","id":448,"pos":[-10112,-1024],"params":["Inherit","False","2","0","FLOAT","0","False","1","FLOAT","2","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":449,"pos":[-10368,-1024],"params":["Inherit","False","444","wavesTiling","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":432,"pos":[-11392,-256],"params":["Inherit","False","Constant","_Float7","Float 7","32","0","Create","True","0","0","0","False","0","False","Object","-1","","0.005","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":439,"pos":[-10624,-256],"params":["Inherit","False","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":440,"pos":[-10368,-256],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor","id":441,"pos":[-10112,-256],"params":["Inherit","False","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":445,"pos":[-10624,-128],"params":["Inherit","False","444","wavesTiling","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":436,"pos":[-11008,640],"params":["Inherit","False","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":454,"pos":[-9088,-1152],"params":["Inherit","False","True","False","False","False","1","0","FLOAT2","0,0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":455,"pos":[-9088,-1024],"params":["Inherit","False","False","True","False","False","1","0","FLOAT2","0,0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.PowerNode, AmplifyShaderEditor","id":469,"pos":[-8832,-1024],"params":["Inherit","False","True","2","0","FLOAT","0","False","1","FLOAT","2","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":471,"pos":[-8832,-896],"params":["Inherit","False","421","UVNoise","1","0","OBJECT","","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":470,"pos":[-8576,-1024],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":472,"pos":[-8448,-1152],"params":["Inherit","False","FLOAT2","4","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor","id":473,"pos":[-8192,-1152],"params":["Inherit","False","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":414,"pos":[-7936,-1024],"params":["Inherit","False","364","worldUVNoise","1","0","OBJECT","","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":474,"pos":[-7936,-1152],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":475,"pos":[-8320,-896],"params":["Inherit","False","FLOAT2","4","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleTimeNode, AmplifyShaderEditor","id":478,"pos":[-8832,-768],"params":["Inherit","False","1","0","FLOAT","1","False","5","FLOAT","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.FractNode, AmplifyShaderEditor","id":480,"pos":[-8416,-768],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":479,"pos":[-8576,-768],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":483,"pos":[-8832,-640],"params":["Inherit","False","482","wavesSpeed","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleDivideOpNode, AmplifyShaderEditor","id":442,"pos":[-10368,-128],"params":["Inherit","False","2","0","FLOAT","0","False","1","FLOAT","2","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":434,"pos":[-11008,512],"params":["Inherit","False","FLOAT2","4","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":433,"pos":[-11008,-1024],"params":["Inherit","False","FLOAT2","4","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":435,"pos":[-10368,512],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":450,"pos":[-10112,512],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor","id":451,"pos":[-9856,512],"params":["Inherit","False","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleDivideOpNode, AmplifyShaderEditor","id":452,"pos":[-10112,640],"params":["Inherit","False","2","0","FLOAT","0","False","1","FLOAT","2","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":453,"pos":[-10368,640],"params":["Inherit","False","444","wavesTiling","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":468,"pos":[-9600,-1152],"params":["Inherit","False","SH_F_Vefects_VFX_Vector_To_Radial_Value","-1","","94","56cc3109e9c9cfb4eb8311df7a8658f8","0","1","2","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":484,"pos":[-9088,-256],"params":["Inherit","False","True","False","False","False","1","0","FLOAT2","0,0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":485,"pos":[-9088,-128],"params":["Inherit","False","False","True","False","False","1","0","FLOAT2","0,0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.PowerNode, AmplifyShaderEditor","id":486,"pos":[-8832,-128],"params":["Inherit","False","True","2","0","FLOAT","0","False","1","FLOAT","2","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":487,"pos":[-8832,0],"params":["Inherit","False","421","UVNoise","1","0","OBJECT","","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":488,"pos":[-8576,-128],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":489,"pos":[-8448,-256],"params":["Inherit","False","FLOAT2","4","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor","id":490,"pos":[-8192,-256],"params":["Inherit","False","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":491,"pos":[-7936,-128],"params":["Inherit","False","364","worldUVNoise","1","0","OBJECT","","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":492,"pos":[-7936,-256],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":493,"pos":[-8320,0],"params":["Inherit","False","FLOAT2","4","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleTimeNode, AmplifyShaderEditor","id":494,"pos":[-8832,128],"params":["Inherit","False","1","0","FLOAT","1","False","5","FLOAT","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.FractNode, AmplifyShaderEditor","id":495,"pos":[-8416,128],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":496,"pos":[-8576,128],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":497,"pos":[-8832,256],"params":["Inherit","False","482","wavesSpeed","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":498,"pos":[-9600,-256],"params":["Inherit","False","SH_F_Vefects_VFX_Vector_To_Radial_Value","-1","","95","56cc3109e9c9cfb4eb8311df7a8658f8","0","1","2","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":499,"pos":[-9088,512],"params":["Inherit","False","True","False","False","False","1","0","FLOAT2","0,0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":500,"pos":[-9088,640],"params":["Inherit","False","False","True","False","False","1","0","FLOAT2","0,0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.PowerNode, AmplifyShaderEditor","id":501,"pos":[-8832,640],"params":["Inherit","False","True","2","0","FLOAT","0","False","1","FLOAT","2","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":502,"pos":[-8832,768],"params":["Inherit","False","421","UVNoise","1","0","OBJECT","","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":503,"pos":[-8576,640],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":504,"pos":[-8448,512],"params":["Inherit","False","FLOAT2","4","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor","id":505,"pos":[-8192,512],"params":["Inherit","False","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":506,"pos":[-7936,640],"params":["Inherit","False","364","worldUVNoise","1","0","OBJECT","","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":507,"pos":[-7936,512],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":508,"pos":[-8320,768],"params":["Inherit","False","FLOAT2","4","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.SimpleTimeNode, AmplifyShaderEditor","id":509,"pos":[-8832,896],"params":["Inherit","False","1","0","FLOAT","1","False","5","FLOAT","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.FractNode, AmplifyShaderEditor","id":510,"pos":[-8416,896],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":511,"pos":[-8576,896],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":512,"pos":[-8832,1024],"params":["Inherit","False","482","wavesSpeed","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":513,"pos":[-9600,512],"params":["Inherit","False","SH_F_Vefects_VFX_Vector_To_Radial_Value","-1","","96","56cc3109e9c9cfb4eb8311df7a8658f8","0","1","2","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":444,"pos":[-11776,-256],"params":["Inherit","False","wavesTiling","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":481,"pos":[-12032,-128],"params":["Inherit","False","Property","_WavesSpeed","Waves Speed","28","0","Create","True","0","0","0","False","0","False","Object","-1","","2","2","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":482,"pos":[-11776,-128],"params":["Inherit","False","wavesSpeed","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":443,"pos":[-12032,-256],"params":["Inherit","False","Property","_WavesTiling","Waves Tiling","27","0","Create","True","0","0","0","False","0","False","Object","-1","","3","3","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":138,"pos":[-3968,-256],"params":["Inherit","False","FLOAT3","4","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":139,"pos":[-4352,-144],"params":["Inherit","False","Constant","_Float2","Float 2","29","0","Create","True","0","0","0","False","0","False","Object","-1","","1","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":136,"pos":[-3968,-512],"params":["Inherit","False","2","2","0","FLOAT3","0,0,0","False","1","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":137,"pos":[-4352,-256],"params":["Inherit","False","Property","_WaterNormalStrength","Water Normal Strength","14","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":399,"pos":[-4224,-512],"params":["Inherit","False","398","normanReedus","1","0","OBJECT","","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":140,"pos":[-3712,-512],"params":["Inherit","False","WaterNormal","-1","True","1","0","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.TexturePropertyNode, AmplifyShaderEditor","id":401,"pos":[-7424,-1408],"params":["Inherit","True","Property","_WaveMask","Wave Mask","25","0","Create","True","0","0","0","False","3","Space(33)","Header(Wave Mask)","Space(13)","False","","3c64f3705b3056d418217e7e84d4fb62","3c64f3705b3056d418217e7e84d4fb62","False","white","Auto","Texture2D","False","-1","0","2","SAMPLER2D","0","SAMPLERSTATE","1"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":522,"pos":[-9344,-4096],"params":["Inherit","False","True","True","False","False","1","0","FLOAT3","0,0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":523,"pos":[-9344,-3968],"params":["Inherit","False","False","True","True","False","1","0","FLOAT3","0,0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":431,"pos":[-8320,-2944],"params":["Inherit","True","ConstantBiasScale","-1","","97","63208df05c83e8e49a48ffbdce2e43a0","0","3","3","FLOAT","0","False","1","FLOAT","-0.2","False","2","FLOAT","2","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor","id":429,"pos":[-7936,-3072],"params":["Inherit","True","FLOAT2","4","0","FLOAT","0","False","1","FLOAT","0","False","2","FLOAT","0","False","3","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":430,"pos":[-8320,-3200],"params":["Inherit","True","ConstantBiasScale","-1","","98","63208df05c83e8e49a48ffbdce2e43a0","0","3","3","FLOAT","0","False","1","FLOAT","-0.2","False","2","FLOAT","2","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor","id":366,"pos":[-7680,-3840],"params":["Inherit","True","True","True","False","True","1","0","FLOAT3","0,0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":423,"pos":[-9856,-3072],"params":["Inherit","False","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":424,"pos":[-9472,-3072],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT2","0,0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":426,"pos":[-9472,-2944],"params":["Inherit","False","Property","_UVNoiseScale","UV Noise Scale","9","0","Create","True","0","0","0","False","0","False","Object","-1","","1,1","1,1","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor","id":531,"pos":[-9088,-3072],"params":["Inherit","False","2","2","0","FLOAT2","0,0","False","1","FLOAT","0","False","1","FLOAT2","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":532,"pos":[-7296,-2176],"params":["Inherit","False","randomOffset","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":533,"pos":[-9088,-2944],"params":["Inherit","False","532","randomOffset","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TexCoordVertexDataNode, AmplifyShaderEditor","id":540,"pos":[-7552,-1920],"params":["Inherit","False","1","4","0","5","FLOAT4","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":323,"pos":[-2048,1280],"params":["Inherit","False","DFColor","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":181,"pos":[-2432,1792],"params":["Inherit","False","DSFres","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":187,"pos":[-1792,528],"params":["Inherit","False","181","DSFres","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":169,"pos":[-1792,656],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":333,"pos":[-1792,-1664],"params":["Inherit","False","2","2","0","FLOAT3","0,0,0","False","1","COLOR","0,0,0,0","False","1","COLOR","0"]}
{"type":"AmplifyShaderEditor.ColorNode, AmplifyShaderEditor","id":26,"pos":[-2048,-1664],"params":["Inherit","False","Property","_Color01","Color 01","1","0","Create","True","0","0","0","False","0","False","Object","-1","","1,1,1,0","1,1,1,0","True","False","0","6","FLOAT3","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor","id":334,"pos":[-2048,-1408],"params":["Inherit","False","0","5","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":186,"pos":[-640,512],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":91,"pos":[-640,384],"params":["Inherit","False","90","smoothness","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":85,"pos":[-640,256],"params":["Inherit","False","84","spec","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":79,"pos":[-640,128],"params":["Inherit","False","140","WaterNormal","1","0","OBJECT","","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":543,"pos":[-282.6329,-149.2643],"params":["Inherit","False","Constant","_Float0","Float 0","36","0","Create","True","0","0","0","False","0","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":61,"pos":[0,0],"params":["Float","False","False","-1","2","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","94348b07e5e8bab40bd6c8a1e3df54cd","True","ExtraPrePass","0","0","ExtraPrePass","6","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","True","1","1","False","","0","False","","0","1","False","","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","0","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":62,"pos":[0,0],"params":["Float","False","True","-1","2","UnityEditor.ShaderGraphLitGUI","0","15","Vefects/SH_Vefects_VFX_URP_Puddle_01_New","94348b07e5e8bab40bd6c8a1e3df54cd","True","Forward","0","1","Forward","22","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","True","True","0","True","_Cull","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","True","True","2","True","_ZWrite","True","3","True","_ZTest","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Transparent=RenderType","Queue=Transparent=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","True","True","1","5","True","_Src","10","True","_Dst","1","1","False","","10","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","True","True","1","True","_ZWrite","True","3","True","_ZTest","True","True","0","False","","0","False","","False","True","1","LightMode=UniversalForward","False","False","0","","0","0","Standard","52","Category","0","0","  Instanced Terrain Normals","1","0","Lighting Model","0","0","Workflow","0","638760135066821991","Surface","1","638760136525985827","  Keep Alpha","0","0","  Refraction Model","0","0","  Blend","0","0","Two Sided","1","0","Alpha Clipping","0","638760136595984769","  Use Shadow Threshold","0","0","Fragment Normal Space","0","0","Forward Only","0","0","Transmission","0","0","  Transmission Shadow","0.5,False,","0","Translucency","0","0","  Translucency Strength","1,False,","0","  Normal Distortion","0.5,False,","0","  Scattering","2,False,","0","  Direct","0.9,False,","0","  Ambient","0.1,False,","0","  Shadow","0.5,False,","0","Cast Shadows","0","638760151794615394","Receive Shadows","1","0","Specular Highlights","2","0","Environment Reflections","2","0","Receive SSAO","1","0","Motion Vectors","1","0","  Additional Motion Vectors","1","0","  Alembic Motion Vectors","0","0","  XR Motion Vectors","0","0","GPU Instancing","1","0","LOD CrossFade","1","0","Built-in Fog","1","0","_FinalColorxAlpha","0","0","Meta Pass","1","0","Override Baked GI","0","0","Extra Pre Pass","0","0","Tessellation","0","0","  Phong","0","0","  Strength","0.5,False,","0","  Type","0","0","  Tess","16,False,","0","  Min","10,False,","0","  Max","25,False,","0","  Edge Length","16,False,","0","  Max Displacement","25,False,","0","Write Depth","0","0","  Conservative","0","0","Vertex Position","1","0","Debug Display","0","0","Clear Coat","0","0","0","12","False","True","False","True","True","True","True","True","True","True","True","False","False","","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":63,"pos":[0,0],"params":["Float","False","False","-1","2","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","94348b07e5e8bab40bd6c8a1e3df54cd","True","ShadowCaster","0","2","ShadowCaster","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","False","False","True","False","False","False","False","0","False","","False","False","False","False","False","False","False","False","False","True","1","False","","True","3","False","","False","False","True","1","LightMode=ShadowCaster","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":64,"pos":[0,0],"params":["Float","False","False","-1","2","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","94348b07e5e8bab40bd6c8a1e3df54cd","True","DepthOnly","0","3","DepthOnly","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","False","False","True","True","False","False","False","0","False","","False","False","False","False","False","False","False","False","False","True","1","False","","False","False","False","True","1","LightMode=DepthOnly","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":65,"pos":[0,0],"params":["Float","False","False","-1","2","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","94348b07e5e8bab40bd6c8a1e3df54cd","True","Meta","0","4","Meta","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","2","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","LightMode=Meta","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":66,"pos":[0,0],"params":["Float","False","False","-1","2","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","94348b07e5e8bab40bd6c8a1e3df54cd","True","Universal2D","0","5","Universal2D","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","True","1","5","True","_Src","10","True","_Dst","1","1","False","","10","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","False","False","True","1","True","","True","3","True","","True","True","0","False","","0","False","","False","True","1","LightMode=Universal2D","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":67,"pos":[0,0],"params":["Float","False","False","-1","2","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","94348b07e5e8bab40bd6c8a1e3df54cd","True","DepthNormals","0","6","DepthNormals","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","True","1","1","False","","0","False","","0","1","False","","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","False","","True","3","False","","False","False","True","1","LightMode=DepthNormals","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":68,"pos":[0,0],"params":["Float","False","False","-1","2","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","94348b07e5e8bab40bd6c8a1e3df54cd","True","GBuffer","0","7","GBuffer","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","True","1","5","True","_Src","10","True","_Dst","1","1","False","","10","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","True","","True","3","True","","True","True","0","False","","0","False","","False","True","1","LightMode=UniversalGBuffer","False","True","12","d3d11","gles","metal","vulkan","xboxone","xboxseries","playstation","ps4","ps5","switch","switch2","webgpu","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":69,"pos":[0,0],"params":["Float","False","False","-1","2","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","94348b07e5e8bab40bd6c8a1e3df54cd","True","SceneSelectionPass","0","8","SceneSelectionPass","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","2","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","LightMode=SceneSelectionPass","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":70,"pos":[0,0],"params":["Float","False","False","-1","2","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","94348b07e5e8bab40bd6c8a1e3df54cd","True","ScenePickingPass","0","9","ScenePickingPass","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","LightMode=Picking","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":541,"pos":[0,100],"params":["Float","False","False","-1","2","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","94348b07e5e8bab40bd6c8a1e3df54cd","True","MotionVectors","0","10","MotionVectors","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","True","True","False","False","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","LightMode=MotionVectors","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":542,"pos":[0,110],"params":["Float","False","False","-1","3","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","94348b07e5e8bab40bd6c8a1e3df54cd","True","XRMotionVectors","0","11","XRMotionVectors","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","True","1","False","","255","False","","1","False","","7","False","","3","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","False","False","False","False","True","1","LightMode=XRMotionVectors","False","False","0","","0","0","Standard","0","False","0"]}
{"wire":[519,0,517,0]}
{"wire":[519,1,518,0]}
{"wire":[525,1,519,0]}
{"wire":[525,0,517,0]}
{"wire":[520,0,525,0]}
{"wire":[520,1,515,0]}
{"wire":[524,0,520,0]}
{"wire":[521,0,524,0]}
{"wire":[521,1,516,0]}
{"wire":[365,0,363,0]}
{"wire":[365,1,521,0]}
{"wire":[526,3,365,2]}
{"wire":[528,3,365,1]}
{"wire":[527,0,528,0]}
{"wire":[527,1,526,0]}
{"wire":[367,0,527,0]}
{"wire":[367,1,368,0]}
{"wire":[364,0,367,0]}
{"wire":[362,0,352,0]}
{"wire":[362,1,369,0]}
{"wire":[345,1,362,0]}
{"wire":[345,6,353,0]}
{"wire":[345,7,354,0]}
{"wire":[361,1,356,0]}
{"wire":[361,6,359,0]}
{"wire":[361,7,360,0]}
{"wire":[534,0,529,3]}
{"wire":[349,0,345,0]}
{"wire":[357,0,361,0]}
{"wire":[381,1,377,0]}
{"wire":[381,6,380,0]}
{"wire":[381,7,376,0]}
{"wire":[355,0,349,0]}
{"wire":[358,0,357,0]}
{"wire":[378,0,381,0]}
{"wire":[535,0,529,4]}
{"wire":[539,0,538,0]}
{"wire":[539,1,373,0]}
{"wire":[370,0,355,0]}
{"wire":[370,1,358,0]}
{"wire":[382,0,539,0]}
{"wire":[379,0,378,0]}
{"wire":[389,1,385,0]}
{"wire":[389,6,380,0]}
{"wire":[389,7,376,0]}
{"wire":[372,0,379,0]}
{"wire":[372,1,539,0]}
{"wire":[372,2,382,0]}
{"wire":[371,0,370,0]}
{"wire":[36,0,38,0]}
{"wire":[537,0,536,0]}
{"wire":[537,1,374,0]}
{"wire":[170,2,168,0]}
{"wire":[170,0,167,0]}
{"wire":[170,1,166,0]}
{"wire":[383,0,537,0]}
{"wire":[386,0,389,0]}
{"wire":[390,0,371,0]}
{"wire":[390,1,372,0]}
{"wire":[172,0,170,0]}
{"wire":[375,0,386,0]}
{"wire":[375,1,537,0]}
{"wire":[375,2,383,0]}
{"wire":[391,0,390,0]}
{"wire":[59,0,36,0]}
{"wire":[183,0,172,0]}
{"wire":[393,0,391,0]}
{"wire":[393,1,375,0]}
{"wire":[171,0,180,0]}
{"wire":[392,0,393,0]}
{"wire":[173,0,171,0]}
{"wire":[173,1,185,0]}
{"wire":[397,0,392,0]}
{"wire":[174,0,173,0]}
{"wire":[394,0,174,0]}
{"wire":[394,1,396,0]}
{"wire":[395,0,394,0]}
{"wire":[37,0,29,4]}
{"wire":[37,1,395,0]}
{"wire":[178,7,177,0]}
{"wire":[178,6,176,0]}
{"wire":[178,4,175,0]}
{"wire":[184,0,178,0]}
{"wire":[179,0,178,0]}
{"wire":[20,0,22,0]}
{"wire":[20,1,19,0]}
{"wire":[22,0,21,0]}
{"wire":[23,0,20,0]}
{"wire":[39,0,333,0]}
{"wire":[39,1,23,0]}
{"wire":[18,0,141,0]}
{"wire":[19,0,17,0]}
{"wire":[19,1,205,0]}
{"wire":[198,7,197,0]}
{"wire":[198,6,196,0]}
{"wire":[198,4,195,0]}
{"wire":[200,0,198,0]}
{"wire":[205,0,203,0]}
{"wire":[205,1,204,0]}
{"wire":[205,2,200,0]}
{"wire":[84,0,80,0]}
{"wire":[90,0,86,0]}
{"wire":[324,0,325,0]}
{"wire":[331,0,39,0]}
{"wire":[331,1,332,0]}
{"wire":[17,3,18,0]}
{"wire":[199,0,198,0]}
{"wire":[347,0,348,0]}
{"wire":[347,1,346,0]}
{"wire":[347,2,392,0]}
{"wire":[398,0,347,0]}
{"wire":[419,0,420,0]}
{"wire":[419,1,531,0]}
{"wire":[418,0,429,0]}
{"wire":[418,1,417,0]}
{"wire":[421,0,418,0]}
{"wire":[400,0,401,0]}
{"wire":[400,1,474,0]}
{"wire":[403,0,401,0]}
{"wire":[403,1,492,0]}
{"wire":[404,0,401,0]}
{"wire":[404,1,507,0]}
{"wire":[405,0,403,2]}
{"wire":[405,1,404,2]}
{"wire":[406,0,405,0]}
{"wire":[406,1,409,0]}
{"wire":[407,0,402,0]}
{"wire":[407,1,409,0]}
{"wire":[410,2,407,0]}
{"wire":[411,2,406,0]}
{"wire":[346,0,410,0]}
{"wire":[346,1,411,0]}
{"wire":[402,0,400,2]}
{"wire":[402,1,403,2]}
{"wire":[438,0,437,0]}
{"wire":[438,1,433,0]}
{"wire":[446,0,438,0]}
{"wire":[446,1,449,0]}
{"wire":[447,0,446,0]}
{"wire":[447,1,448,0]}
{"wire":[448,0,449,0]}
{"wire":[440,0,439,0]}
{"wire":[440,1,445,0]}
{"wire":[441,0,440,0]}
{"wire":[441,1,442,0]}
{"wire":[454,0,468,0]}
{"wire":[455,0,468,0]}
{"wire":[469,0,455,0]}
{"wire":[470,0,469,0]}
{"wire":[470,1,471,0]}
{"wire":[472,0,454,0]}
{"wire":[472,1,470,0]}
{"wire":[473,0,472,0]}
{"wire":[473,1,475,0]}
{"wire":[474,0,473,0]}
{"wire":[474,1,414,0]}
{"wire":[475,1,480,0]}
{"wire":[480,0,479,0]}
{"wire":[479,0,478,0]}
{"wire":[479,1,483,0]}
{"wire":[442,0,445,0]}
{"wire":[434,1,432,0]}
{"wire":[433,0,432,0]}
{"wire":[435,0,434,0]}
{"wire":[435,1,436,0]}
{"wire":[450,0,435,0]}
{"wire":[450,1,453,0]}
{"wire":[451,0,450,0]}
{"wire":[451,1,452,0]}
{"wire":[452,0,453,0]}
{"wire":[468,2,447,0]}
{"wire":[484,0,498,0]}
{"wire":[485,0,498,0]}
{"wire":[486,0,485,0]}
{"wire":[488,0,486,0]}
{"wire":[488,1,487,0]}
{"wire":[489,0,484,0]}
{"wire":[489,1,488,0]}
{"wire":[490,0,489,0]}
{"wire":[490,1,493,0]}
{"wire":[492,0,490,0]}
{"wire":[492,1,491,0]}
{"wire":[493,1,495,0]}
{"wire":[495,0,496,0]}
{"wire":[496,0,494,0]}
{"wire":[496,1,497,0]}
{"wire":[498,2,441,0]}
{"wire":[499,0,513,0]}
{"wire":[500,0,513,0]}
{"wire":[501,0,500,0]}
{"wire":[503,0,501,0]}
{"wire":[503,1,502,0]}
{"wire":[504,0,499,0]}
{"wire":[504,1,503,0]}
{"wire":[505,0,504,0]}
{"wire":[505,1,508,0]}
{"wire":[507,0,505,0]}
{"wire":[507,1,506,0]}
{"wire":[508,1,510,0]}
{"wire":[510,0,511,0]}
{"wire":[511,0,509,0]}
{"wire":[511,1,512,0]}
{"wire":[513,2,451,0]}
{"wire":[444,0,443,0]}
{"wire":[482,0,481,0]}
{"wire":[138,0,137,0]}
{"wire":[138,1,137,0]}
{"wire":[138,2,139,0]}
{"wire":[136,0,399,0]}
{"wire":[136,1,138,0]}
{"wire":[140,0,136,0]}
{"wire":[522,0,520,0]}
{"wire":[523,0,520,0]}
{"wire":[431,3,419,2]}
{"wire":[429,0,430,0]}
{"wire":[429,1,431,0]}
{"wire":[430,3,419,1]}
{"wire":[366,0,365,5]}
{"wire":[424,0,423,0]}
{"wire":[424,1,426,0]}
{"wire":[531,0,424,0]}
{"wire":[531,1,533,0]}
{"wire":[532,0,540,1]}
{"wire":[323,0,324,0]}
{"wire":[181,0,184,0]}
{"wire":[169,0,180,0]}
{"wire":[169,1,187,0]}
{"wire":[333,0,26,0]}
{"wire":[333,1,334,0]}
{"wire":[186,0,37,0]}
{"wire":[62,0,543,0]}
{"wire":[62,1,79,0]}
{"wire":[62,9,85,0]}
{"wire":[62,4,91,0]}
{"wire":[62,2,39,0]}
{"wire":[62,6,186,0]}
ASEEND*/
//CHKSM=D76F96CBF7DDAE3FC973DDDAF3C0EC55F8F32739
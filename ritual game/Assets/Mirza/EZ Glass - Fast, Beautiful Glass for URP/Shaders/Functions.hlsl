
#include "../../../Mirza/_VFXToolkit/_Demo/Shaders/_HLSL/Fresnel.hlsl"
#include "../../../Mirza/_VFXToolkit/_Demo/Shaders/_HLSL/Functions.hlsl"

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Refraction.hlsl"

// ...

float3 NormalStrength(float3 In, float Strength)
{
    return float3(In.rg * Strength, lerp(1.0, In.b, saturate(Strength)));
}

// ...

#define GLASS_SAMPLER_LINEAR_CLAMP sampler_LinearClamp
#define GLASS_SAMPLER_LINEAR_REPEAT sampler_LinearRepeat

// ...

float2 WorldToScreenSpace(float3 positionWS)
{
    // Convert world space exit position to homogeneous clip space.    
    
    float4 positionCS = TransformWorldToHClip(positionWS);
    
    // Compute screen space pixel position from clip space coordinates.
    
    float4 positionPS = ComputeScreenPos(positionCS);
    
    // Divide by 'w' component to get normalized device coordinates.
    // -- (aka, "NDC", also aka -> normalized screen space UVs).
    
    return positionPS.xy / positionPS.w;
}

// EZ Glass.

// Between being subject to Shader Graph variant overflow due to 'too many' keywords, 
// and 'live' bool spam which makes the shader a bit less efficient, I chose the latter.

// At least it's pretty, and it's just "ready to go" for the newest of newbies.
// I didn't profile where the if-statements provide a better tradeoff.

// I imagine that a single if-statement is faster/better than a single texture sample.
// -- (but I haven't tested that...)

void EZGlass_float(

    Texture2D cameraTexture, 

    // ...

    float3 positionWS, float2 uvTS, float2 uvSS,
    float3 normalWS, float3 tangentWS, float3 bitangentWS,

    // ...

    float4 baseColour,

    bool enableBaseTexture,
    Texture2D baseTexture,

    float baseTextureBlend,
    float alphaFromBaseTexture,

    // ...

    float4 tintColour,

    bool enableTintTexture,
    Texture2D tintTexture,

    // ...

    float metallicIn,
    float smoothnessIn,

    // ...

    bool enableSmoothnessMaskTexture,

    Texture2D smoothnessMaskTexture,
    float smoothnessMaskTextureBlend,

    // ...

    bool enableNormalTexture,

    Texture2D normalTexture,
    float normalTextureStrength,

    // ...

    bool enableEmission,

    float4 emissionColour,
    Texture2D emissionTexture,

    bool emissionTextureAlpha,

    // ...

    bool enableExtraReflection,

    float extraReflection,
    float extraReflectionPower,

    // ...

    bool enableRefraction, 

    float ior, float refraction, float refractionThickness,
    bool enableNormalTextureRefraction, float normalTextureRefraction,

    // ...

    bool enableDispersion,

    float baseDispersion, float refractionDispersion,

    // ...

    bool enableBlur,
    bool blurNoise, 

    int blurQuality, 
    float blurRadius, 

    float blurAlpha,
    float blurAlphaMaskFromSmoothnessMaskTexture,

    // ...

    bool enableFresnel,

    float4 fresnelColour, float fresnelPower,
    float fresnelRemapMin, float fresnelRemapMax,

    // ...

    bool enableFresnelShading,

    float fresnelShading, float fresnelShadingPower,
    float fresnelShadingRemapMin, float fresnelShadingRemapMax,

    // ...

    bool enableFresnelAlpha, 

    bool enableFresnelAlphaCameraTextureBlend,
    bool enableFresnelAlphaCameraTextureBlendPBR,

    float fresnelAlpha, float fresnelAlphaPower,
    float fresnelAlphaRemapMin, float fresnelAlphaRemapMax,

    // ...

    out float3 base, out float3 normalTS,
    out float metallicOut, out float smoothnessOut,
    out float3 emission, out float alpha

)
{
    // -- Setup.
    
    float3 cameraPositionWS = GetCameraPositionWS();
    
    // 'viewDirection' == direction from this object's surface vertex/fragment to camera.
    
    float3 viewVectorWS = cameraPositionWS - positionWS;
    float distanceToCameraWS = length(viewVectorWS);
    
    float3 viewDirectionWS = viewVectorWS / distanceToCameraWS;
        
    // Aspect ratio.
    
    float aspectRatio = _ScreenParams.y / _ScreenParams.x;
    
    //float2 screenResolution = GetScaledScreenParams().xy;
    //float aspectRatio = screenResolution.y / screenResolution.x;
    
    // -- Smoothness mask (immediately modifies 'smoothnessIn' with the mask).
    
    float smoothnessMaskTextureSample = 0.0;
    
    if (enableSmoothnessMaskTexture)
    {
        float2 smoothnessMaskTextureTiling = _Smoothness_Mask_Texture_ST.xy;
        float2 smoothnessMaskTextureOffset = _Smoothness_Mask_Texture_ST.zw;
        
        smoothnessMaskTextureSample = SAMPLE_TEXTURE2D(smoothnessMaskTexture, GLASS_SAMPLER_LINEAR_REPEAT, (uvTS * smoothnessMaskTextureTiling) + smoothnessMaskTextureOffset).r;
        smoothnessIn *= lerp(1.0, smoothnessMaskTextureSample, smoothnessMaskTextureBlend);
    }
    
    // -- Normal texture.
    
    float3 textureNormalTS;
    float2 normalTextureRefractionVector = 0.0;
    
    if (enableNormalTexture)
    {
        float2 normalTextureTiling = _Normal_Texture_ST.xy;
        float2 normalTextureOffset = _Normal_Texture_ST.zw;
    
        float4 normalTextureSample = SAMPLE_TEXTURE2D(normalTexture, GLASS_SAMPLER_LINEAR_REPEAT, (uvTS * normalTextureTiling) + normalTextureOffset);
    
        textureNormalTS = UnpackNormal(normalTextureSample);
        
        normalTextureRefractionVector = textureNormalTS * normalTextureRefraction;
    }
    
    // -- Refraction and blurring.
    
    emission = 0.0;
    
//#define _ENABLE_CAMERA_TEXTURE
    
#ifdef _ENABLE_CAMERA_TEXTURE

    bool cameraTextureSampleRequired =
        enableRefraction || enableNormalTextureRefraction || enableDispersion || enableBlur;
    
    cameraTextureSampleRequired = true; // Don't auto-disable.
    
    if (cameraTextureSampleRequired)
    {
        float2 uvSS_refracted = uvSS;
        
        float3 refractionRayWS;
        float3 reflectionRayWS;
        
        float refractionRayVS_length = 0.0;
                
        // ...
    
        // https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.core/ShaderLibrary/Refraction.hlsl
        
        /*        
        
            struct RefractionModelResult
            {
                real  dist;       // length of the transmission during refraction through the shape
                float3 positionWS; // out ray position
                real3 rayWS;      // out ray direction
            };
        
        */
                
        if (enableRefraction)
        {
            float iorSource = 1.0f;
            float iorMedium = ior;
    
            // Internally, Unity's refraction models/functions do refract(-viewDirectionWS, ...), so I pass it *not*-negated.
            
            RefractionModelResult refractionModelResult;
            
#if defined(_REFRACTION_MODEL_SPHERE)
            refractionModelResult = RefractionModelSphere(viewDirectionWS, positionWS, normalWS, iorMedium, refractionThickness);
#elif defined(_REFRACTION_MODEL_BOX)
            refractionModelResult = RefractionModelBox(viewDirectionWS, positionWS, normalWS, iorMedium, refractionThickness);
#else            
            float eta = iorSource / iorMedium;
                        
            refractionModelResult.rayWS = refract(-viewDirectionWS, normalWS, eta);
            refractionModelResult.dist = refractionThickness;
#endif

            refractionRayWS = refractionModelResult.rayWS * refractionModelResult.dist;
    
            // Transform the final exiting ray into view space.
            
            float2 refractionRayVS = TransformWorldToViewDir(refractionRayWS).xy;
        
            refractionRayVS.x = refractionRayVS.x * aspectRatio;
            refractionRayVS = refractionRayVS * refraction;
                        
            refractionRayVS_length = length(refractionRayVS);
        
            uvSS_refracted = uvSS + refractionRayVS;
        }
        
        if (enableNormalTexture && enableNormalTextureRefraction)
        {
            uvSS_refracted += normalTextureRefractionVector;
        }
        
        float3 cameraTextureSample;
        
        if (!enableBlur)
        {
            cameraTextureSample = SAMPLE_TEXTURE2D(cameraTexture, GLASS_SAMPLER_LINEAR_CLAMP, uvSS_refracted).rgb;
        }
        else
        {
            float4 blurredCameraTextureSample;
            
            BoxBlur_float(cameraTexture, uvSS_refracted, float2(aspectRatio, 1.0), GLASS_SAMPLER_LINEAR_CLAMP, blurQuality, blurRadius, true, true, blurNoise, blurredCameraTextureSample);
            
            if (blurAlpha < 1.0 || blurAlphaMaskFromSmoothnessMaskTexture > 0.0)
            {
                float blurAlphaMask = blurAlpha;
                
                // Sample *inverted* smoothness mask:
                // -> Less smooth == more rough -> more scattering -> more blur.
                    
                blurAlphaMask *= lerp(1.0, 1.0 - smoothnessMaskTextureSample, blurAlphaMaskFromSmoothnessMaskTexture);
                
                cameraTextureSample = SAMPLE_TEXTURE2D(cameraTexture, GLASS_SAMPLER_LINEAR_CLAMP, uvSS_refracted).rgb;
                blurredCameraTextureSample.rgb = lerp(cameraTextureSample, blurredCameraTextureSample.rgb, blurAlphaMask);
            }
            
            cameraTextureSample = blurredCameraTextureSample.rgb;
        }
                
        if (enableTintTexture)
        {
            float2 tintTextureTiling = _Tint_Texture_ST.xy;
            float2 tintTextureOffset = _Tint_Texture_ST.zw;
            
            tintColour *= SAMPLE_TEXTURE2D(tintTexture, GLASS_SAMPLER_LINEAR_REPEAT, (uvTS * tintTextureTiling) + tintTextureOffset);
        }
        
        emission = lerp(cameraTextureSample, cameraTextureSample * tintColour.rgb, tintColour.a);
        
        if (enableDispersion)
        {
            float3 cameraTextureSample_ddx = ddx(cameraTextureSample);
                        
            float dispersion = baseDispersion + (refractionDispersion * refractionRayVS_length);
            dispersion *= _ScreenParams.x; // Scale by screen resolution.
            
            float3 dispersionRGB = dispersion * float3(-1.0, 0.0, 1.0);
            
            emission += dispersionRGB * cameraTextureSample_ddx;
        }
        
        //baseColour.rgb = 0.0;
        //baseColour.a = 1.0;
    }

#endif
    
    // ...
    
    if (enableNormalTexture)
    {
        textureNormalTS = NormalStrength(textureNormalTS, normalTextureStrength);
        normalTS = textureNormalTS;
    }
    else
    {
        normalTS = float3(0.0, 0.0, 1.0);
    }
    
    // ...
    
    if (enableBaseTexture)
    {
        float2 baseTextureTiling = _Base_Texture_ST.xy;
        float2 baseTextureOffset = _Base_Texture_ST.zw;
        
        // Added refraction vector because I figured it looks cool/nice.
        
        float2 baseTextureSampleUV = (uvTS * baseTextureTiling) + baseTextureOffset;
        
        if (enableNormalTextureRefraction)
        {
            baseTextureSampleUV += normalTextureRefractionVector;
        }
    
        float4 baseTextureSample = SAMPLE_TEXTURE2D(baseTexture, GLASS_SAMPLER_LINEAR_REPEAT, baseTextureSampleUV);
        
        baseTextureSample.a = lerp(0.0, baseTextureSample.a, baseTextureBlend);
        
        baseColour.rgb = lerp(baseColour.rgb, baseColour.rgb * baseTextureSample.rgb, baseTextureSample.a);
        emission = lerp(emission, 0.0, baseTextureSample.a);
    
        baseColour.a = lerp(baseColour.a, baseColour.a * baseTextureSample.a, alphaFromBaseTexture);
    }
    
    // -- Reflection.
    
    if (enableExtraReflection)
    {
        float3 reflectionRayWS = reflect(-viewDirectionWS, normalWS);
        
        float reflectionFresnel = Fresnel(viewDirectionWS, normalWS);
        reflectionFresnel = pow(reflectionFresnel, extraReflectionPower);
        
        float reflectionRoughness = 1.0 - smoothnessIn;
        float reflectionOcclusion = reflectionFresnel;
    
        float3 reflectionSample = GlossyEnvironmentReflection(reflectionRayWS, positionWS, reflectionRoughness, reflectionOcclusion, uvSS);
            
        baseColour.rgb += reflectionSample * extraReflection;
    }
    
    // ...
    
    float fresnel;
    
    bool fresnelRequired =
        enableFresnel || enableFresnelAlpha || enableFresnelShading;
    
    if (fresnelRequired)
    {
        fresnel = Fresnel(viewDirectionWS, normalWS);
    }
    
    float fresnelOneMinus = 1.0 - fresnel;
    
    // ...
    
    if (enableFresnel)
    {
        float fresnelLightMask = pow(fresnelOneMinus, fresnelPower);
        fresnelLightMask = smoothstep(fresnelRemapMin, fresnelRemapMax, fresnelLightMask);
        
        baseColour.rgb += (fresnelLightMask * fresnelColour.a) * fresnelColour.rgb;
    }
    
    // -- Emission.
    
    float4 emissionTextureSample = 1.0;
    
    if (enableEmission)
    {
        float2 emissionTextureTiling = _Emission_Texture_ST.xy;
        float2 emissionTextureOffset = _Emission_Texture_ST.zw;
        
        emissionTextureSample = SAMPLE_TEXTURE2D(emissionTexture, GLASS_SAMPLER_LINEAR_REPEAT, (uvTS * emissionTextureTiling) + emissionTextureOffset);
        
        //emission = lerp(emission, emissionColour.rgb * emissionTextureSample.rgb, emissionColour.a * emissionTextureSample.a);
        
        if (!emissionTextureAlpha)
        {
            emission += (emissionColour.rgb * emissionTextureSample.rgb) * (emissionColour.a * emissionTextureSample.a);
        }
        else
        {
            emission = lerp(emission, emissionColour.rgb * emissionTextureSample.rgb, emissionColour.a * emissionTextureSample.a);
        }        
    }
    
    // ...
    
    if (enableFresnelShading)
    {
        float fresnelShadingMask = pow(fresnelOneMinus, fresnelShadingPower);
        fresnelShadingMask = smoothstep(fresnelShadingRemapMin, fresnelShadingRemapMax, fresnelShadingMask);
        
        emission *= lerp(1.0, 1.0 - fresnelShadingMask, fresnelShading);
    }
    
    // ...
    
    if (enableFresnelAlpha)
    {
        float fresnelAlphaMask = pow(fresnel, fresnelAlphaPower);
        fresnelAlphaMask = smoothstep(fresnelAlphaRemapMin, fresnelAlphaRemapMax, fresnelAlphaMask);
        
        fresnelAlphaMask = lerp(1.0, fresnelAlphaMask, fresnelAlpha);
        
        if (enableFresnelAlphaCameraTextureBlend)
        {
            float3 rawCameraTextureSample = SAMPLE_TEXTURE2D(cameraTexture, GLASS_SAMPLER_LINEAR_CLAMP, uvSS).rgb;
            emission = lerp(rawCameraTextureSample, emission, fresnelAlphaMask);
        
            if (enableFresnelAlphaCameraTextureBlendPBR)
            {
                metallicIn *= fresnelAlphaMask;
                smoothnessIn *= fresnelAlphaMask;
            }            
        }
        else
        {
            baseColour.a *= fresnelAlphaMask;
        }   
    }
    
    // ...
    
    base = baseColour.rgb;
    alpha = baseColour.a;
    
    //bool isFrontFace = dot(normalWS, viewDirectionWS) > 0.0;
    
    //if (isFrontFace)
    //{
    //    alpha = 1.0;
    //    discard;
    //}
    
    // ...
    
    metallicOut = metallicIn;
    smoothnessOut = smoothnessIn;
}

#ifdef TEST_DEBUG_NONSENSE

// Self-sufficient, stochastic process (arbitrary, 'random').

DerivedReality StochasticProcess(/* cannot have inputs, it's self-sufficient */)
{
    return Realize(????);
}

// Self-sufficient, intentional (choice, selection).

DerivedReality Command_BeAndItIs(Thing name)
{
    return Realize(name);
}

// There can only be one self-sufficient will/intent.
// Two 'self-sufficient' wills are distinguished (not the same).

// Each is subject to the existence of the other (shared, objective context).
// So that, neither is the ultimate determinator of reality.

// How these wills resolve is by some arbitrary context objective to both, rather than by self-sufficient intent/will.

#endif
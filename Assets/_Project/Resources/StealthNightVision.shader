Shader "Hidden/Tumbler/StealthNightVision"
{
    Properties { _Strength("Strength", Range(0,1)) = 1 }
    HLSLINCLUDE
    #pragma vertex Vert
    #pragma target 4.5
    #pragma only_renderers d3d11 vulkan metal
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"
    TEXTURE2D_X(_SensorSource);
    float _Strength;
    float4 NightVision(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        uint2 pixel = uint2(input.positionCS.xy);
        float2 uv = input.positionCS.xy * _ScreenSize.zw;
        float3 source = SAMPLE_TEXTURE2D_X_LOD(_SensorSource, s_linear_clamp_sampler, uv * _RTHandleScale.xy, 0).rgb;
        float depth = LoadCameraDepth(pixel);
        float luminance = dot(max(source, 0), float3(.2126,.7152,.0722));
        float sensor = .035;
        if (depth != UNITY_RAW_FAR_CLIP_VALUE)
        {
            PositionInputs pos = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
            NormalData surface; DecodeFromNormalBuffer(pixel, surface);
            float3 view = GetWorldSpaceNormalizeViewDir(pos.positionWS);
            // Active sensor response reveals unlit surfaces without adding any world-space lights.
            float facing = saturate(abs(dot(surface.normalWS, view)));
            float relief = saturate(dot(surface.normalWS, normalize(float3(.35,.8,.4))) * .5 + .5);
            float range = exp2(-pos.linearDepth / 70.0);
            sensor = (.10 + .25 * facing + .24 * relief) * range + .035;
            // Preserve visible markings and material detail, with highlight compression in daylight.
            sensor += .25 * luminance / (.25 + luminance);
            float edge = saturate(length(fwidth(surface.normalWS)) * 1.4);
            sensor += edge * .12 * range;
        }
        float noise = frac(sin(dot(floor(input.positionCS.xy), float2(12.9898,78.233)) + floor(_Time.y*24)) * 43758.5453) - .5;
        float vignette = 1 - .38 * smoothstep(.2,.8,length((uv-.5)*float2(1.2,1)));
        float scanline = .985 + .015 * sin(input.positionCS.y * 3.14159);
        sensor = pow(saturate((sensor + noise * .025) * vignette * scanline), 1.4);
        float3 green = sensor * float3(.16,1,.10);
        return float4(lerp(source, green, _Strength), 1);
    }
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Night Vision"
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma fragment NightVision
            ENDHLSL
        }
    }
    Fallback Off
}

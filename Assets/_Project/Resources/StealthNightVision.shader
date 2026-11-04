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
    float _Strength, _SonarTime;
    float3 _SonarOrigin;

    float SurfaceDots(float2 plane)
    {
        // World-locked samples: perspective follows real surfaces, never the screen.
        float2 grid = plane * 3.0;
        float2 footprint = max(fwidth(grid), .001);
        float2 cell = abs(frac(grid + .5) - .5);
        float dotShape = 1 - smoothstep(.07, .07 + max(footprint.x, footprint.y), length(cell));
        return dotShape * (1 - smoothstep(.25, .65, max(footprint.x, footprint.y)));
    }
    float4 NightVision(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        uint2 pixel = uint2(input.positionCS.xy);
        float2 uv = input.positionCS.xy * _ScreenSize.zw;
        float3 source = SAMPLE_TEXTURE2D_X_LOD(_SensorSource, s_linear_clamp_sampler, uv * _RTHandleScale.xy, 0).rgb;
        float depth = LoadCameraDepth(pixel);
        float3 sonar = float3(.002, .006, .018);
        if (depth != UNITY_RAW_FAR_CLIP_VALUE)
        {
            PositionInputs pos = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
            NormalData surface; DecodeFromNormalBuffer(pixel, surface);
            float3 normal = surface.normalWS;
            float3 world = GetAbsolutePositionWS(pos.positionWS);
            float distanceToSensor = length(world - _SonarOrigin);
            float range = exp2(-distanceToSensor / 115.0);
            float3 view = GetWorldSpaceNormalizeViewDir(pos.positionWS);
            float facing = saturate(abs(dot(normal, view)));
            float relief = saturate(dot(normal, normalize(float3(.35, .8, .4))) * .5 + .5);

            // Depth discontinuities delineate silhouettes, normal differences reveal corners
            // even on completely unlit walls. Clamp neighbor reads at the viewport border.
            uint2 limit = uint2(_ScreenSize.xy) - 1;
            uint2 px = min(pixel + uint2(1,0), limit);
            uint2 py = min(pixel + uint2(0,1), limit);
            PositionInputs neighborX = GetPositionInput(float2(px)+.5, _ScreenSize.zw, LoadCameraDepth(px), UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
            PositionInputs neighborY = GetPositionInput(float2(py)+.5, _ScreenSize.zw, LoadCameraDepth(py), UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
            // Distance from the local tangent plane avoids false glowing bands on a
            // flat road seen at grazing angles (raw depth gradients cannot do that).
            float separation = max(abs(dot(normal, neighborX.positionWS-pos.positionWS)),
                abs(dot(normal, neighborY.positionWS-pos.positionWS)));
            float depthEdge = smoothstep(.025, .12, separation / max(1, pos.linearDepth * .03));
            NormalData nx, ny; DecodeFromNormalBuffer(px, nx); DecodeFromNormalBuffer(py, ny);
            float normalEdge = saturate(max(length(normal-nx.normalWS), length(normal-ny.normalWS)) * 1.6);
            float edge = max(depthEdge, normalEdge);
            float rim = pow(1-facing, 3) * .18;

            // Repeating expanding acoustic wave with a short echo decay. A steady base
            // keeps roads and obstacles readable between pulses while driving.
            float behind = frac((_SonarTime * 60.0 - distanceToSensor) / 180.0) * 180.0;
            float pulse = exp2(-behind * .65);
            float echo = exp2(-behind * .055);
            float3 dominant = abs(normal);
            float2 plane = dominant.y >= max(dominant.x, dominant.z) ? world.xz :
                (dominant.x > dominant.z ? world.zy : world.xy);
            float dots = SurfaceDots(plane);
            float contourCoord = world.y * 2.0;
            float contourWidth = max(fwidth(contourCoord), .001);
            float contour = (1-smoothstep(.025, .025+contourWidth, abs(frac(contourCoord+.5)-.5))) *
                (1-smoothstep(.2,.6,contourWidth)) * (1-abs(normal.y));
            float detail = dot(max(source,0), float3(.2126,.7152,.0722));
            detail = detail / (.3 + detail);
            float fill = (.035 + .065*relief + .035*facing + .035*detail) * range;
            sonar += float3(.035, .25, .62) * fill;
            sonar += float3(.08, .48, .95) * (rim + dots*(.12+.22*echo) + contour*.07) * range;
            sonar += float3(.28, .72, 1.0) * edge * (.38+.38*echo) * range;
            sonar += float3(.17, .48, .82) * (pulse*.3 + echo*.055) * range;
        }
        float vignette = 1 - .24 * smoothstep(.25,.8,length((uv-.5)*float2(1.2,1)));
        sonar = saturate(sonar * vignette);
        return float4(lerp(source, sonar, _Strength), 1);
    }
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Blue Surface Sonar"
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma fragment NightVision
            ENDHLSL
        }
    }
    Fallback Off
}

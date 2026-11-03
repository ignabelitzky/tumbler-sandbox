Shader "Tumbler/Tire Smoke HDRP"
{
 SubShader
 {
  Tags { "RenderPipeline"="HDRenderPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Name "ForwardOnly"
   Tags { "LightMode"="ForwardOnly" }
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
   #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
   struct Attributes { float3 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
   struct Varyings { float4 positionCS:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
   Varyings Vert(Attributes v)
   {
    Varyings o;
    o.positionCS=TransformWorldToHClip(TransformObjectToWorld(v.positionOS));
    o.color=v.color;o.uv=v.uv;return o;
   }
   float4 Frag(Varyings i):SV_Target
   {
    float2 p=(i.uv-.5)*2;
    float radius=dot(p,p);
    float cloud=saturate(1-radius);
    cloud=cloud*cloud;
    float detail=.82+.18*sin(i.uv.x*21+sin(i.uv.y*17))*sin(i.uv.y*23);
    // Neutral display-referred smoke, independent of the HDRP physical exposure.
    return float4(i.color.rgb,i.color.a*cloud*detail);
   }
   ENDHLSL
  }
 }
}

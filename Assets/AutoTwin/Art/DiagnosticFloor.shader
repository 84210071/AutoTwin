Shader "AutoTwin/Diagnostic Floor"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Output { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Output Vert(float4 positionOS : POSITION)
            {
                Output output;
                output.positionWS = TransformObjectToWorld(positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half Grid(float2 position, float spacing)
            {
                float2 cell = abs(frac(position / spacing - .5) - .5) * spacing;
                float2 width = max(fwidth(position), .001);
                return 1.0 - saturate(min(cell.x / width.x, cell.y / width.y));
            }

            half4 Frag(Output input) : SV_Target
            {
                float2 position = input.positionWS.xz;
                float distanceSquared = dot(position, position);
                float fade = exp(-distanceSquared * .049);
                float2 spillPosition = position * float2(.58, .33);
                float spill = exp(-dot(spillPosition, spillPosition));
                float2 corePosition = (position - float2(-.15, .32)) * float2(1.28, .71);
                float core = exp(-dot(corePosition, corePosition));
                half grid = (.62 * Grid(position, .64) + .15 * Grid(position, 1.28)) * fade;

                // 不铺设舞台圆环。网格由暗到明，柔和光斑给透视车辆提供落地感。
                half shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS));
                half3 color = half3(.006, .009, .016) + half3(.008, .015, .039) * spill;
                color += (half3(.014, .030, .076) * core + half3(.012, .025, .046) * grid) * lerp(.62, 1, shadow);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}

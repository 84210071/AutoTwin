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

            half Stroke(float value, float center, float width)
            {
                return 1 - smoothstep(width, width + max(fwidth(value), .001), abs(value - center));
            }

            half4 Frag(Output input) : SV_Target
            {
                float2 position = input.positionWS.xz;
                float radius = length(position);
                float fade = exp(-radius * radius / 53.0);
                float inside = 1 - smoothstep(2.78, 2.84, radius);
                float angle = atan2(position.y, position.x);
                float ticks = step(.83, frac(angle * 7.64));
                float rings = Stroke(radius, 2.85, .010) + .45 * Stroke(radius, 2.98, .007) * ticks;
                float grid = .47 * Grid(position, 1.0) + .21 * Grid(position, .25) * inside;

                // 外围网格衰减到视口背景，保留地面接触阴影。
                half shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS));
                half3 color = half3(.003, .007, .014) + half3(.006, .014, .026) * fade;
                color += half3(.018, .044, .072) * (grid * fade + inside * .2);
                color += half3(.018, .129, .201) * rings;
                color *= lerp(.52, 1, shadow);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}

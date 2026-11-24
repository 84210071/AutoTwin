Shader "AutoTwin/Vehicle X-Ray"
{
    Properties
    {
        _BaseColor ("Optical tint", Color) = (.32, .65, 1, 1)
        _Opacity ("Transmission density", Range(0, 1)) = .30
        _Signal ("Selected surface", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Opacity;
                half _Signal;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing = saturate(dot(normal, view));
                half curvature = pow(1 - facing, 1.5);
                half rim = pow(1 - facing, 4.2);
                half topLight = .64 + .36 * saturate(dot(normal, normalize(half3(-.31, .87, .38))));
                half density = (.095 * topLight + .21 * curvature + .63 * rim) * _Opacity;
                density += _Signal * (.16 + .37 * rim);
                half3 tint = lerp(_BaseColor.rgb, saturate(_BaseColor.rgb * half3(1.55, 1.22, 1.0)), rim);
                // 加色混合对透明覆盖件的提交顺序不敏感，动力件通过深度遮挡背面车身。
                return half4(tint * density, 0);
            }
            ENDHLSL
        }
    }
}

// Spark shader for particles: a soft round point of light with a hot center, fading to nothing at its edge,
// so no particle ever shows as a square. When the particle system stretches a particle along its motion,
// the same falloff becomes a thin glowing streak, like a spark flying off a fire.
// Light adds up (additive blending). The particle's color (and its fade over its life) comes in as vertex color.
// It lives in a Resources folder so that it's included in builds, even though only code uses it.

Shader "SpaceCowboy/Spark"
{
    Properties
    {
        _Intensity ("Brightness", Float) = 1.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Spark"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 0 at the center of the particle, 1 at its edge (and beyond, in the corners).
                float away = length(input.uv * 2.0 - 1.0);
                float glow = pow(saturate(1.0 - away), 2.2);
                float core = pow(saturate(1.0 - away / 0.35), 2.0);

                half3 hot = lerp(input.color.rgb, half3(1, 1, 1), 0.3);
                half3 light = input.color.rgb * glow * 1.4 + hot * core * 0.9;
                return half4(light * input.color.a * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}

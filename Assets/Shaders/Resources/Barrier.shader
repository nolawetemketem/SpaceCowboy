// Barrier shader: a see-through force field. A faint tint with a glowing grid of lines, brighter where
// you see the wall at a glancing angle, with a slow shimmer moving across it. Seen from inside or out.
// The grid follows the mesh's UVs (around and up a sphere), _GridLines lines each way.
// It lives in a Resources folder so that it's included in builds, even though only code uses it.

Shader "SpaceCowboy/Barrier"
{
    Properties
    {
        _BarrierColor ("Color", Color) = (0.34, 0.9, 1, 1)
        _Tint ("Tint (how see-through)", Range(0, 1)) = 0.05
        _GridLines ("Grid Lines", Float) = 64
        _LineWidth ("Line Width", Range(0.001, 0.2)) = 0.03
        _LineStrength ("Line Strength", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Barrier"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BarrierColor;
                float _Tint;
                float _GridLines;
                float _LineWidth;
                float _LineStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.world);
                output.normal = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Grid lines: near a whole number of cells, either way. The upright lines are half as many,
                // so the cells come out roughly square on a sphere.
                float2 cells = input.uv * float2(_GridLines, _GridLines * 0.5);
                float2 toLine = abs(frac(cells) - 0.5);
                float grid = saturate(1.0 - (0.5 - max(toLine.x, toLine.y)) / _LineWidth);

                float3 view = GetWorldSpaceNormalizeViewDir(input.world);
                float glancing = pow(1.0 - abs(dot(normalize(input.normal), view)), 3.0);
                float shimmer = 0.75 + 0.25 * sin(_Time.y * 1.3 + input.uv.y * 40.0 + input.uv.x * 20.0);

                float alpha = _Tint + grid * _LineStrength * shimmer + glancing * 0.35;
                return half4(_BarrierColor.rgb, saturate(alpha) * _BarrierColor.a);
            }
            ENDHLSL
        }
    }
}

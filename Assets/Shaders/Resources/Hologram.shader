// Hologram shader: see-through glowing light with moving scanlines.
// _Reveal is how much of the surface exists: 1 = all of it, 0 = none. In between, it breaks up into small blocks.
// _SweepAmount 0 = blocks vanish in random order (wreckage disintegrating).
// _SweepAmount 1 = blocks appear from the bottom up (a ship being projected into the world).
// It lives in a Resources folder so that it's included in builds, even though only code uses it.

Shader "SpaceCowboy/Hologram"
{
    Properties
    {
        _HoloColor ("Color", Color) = (0.34, 0.9, 1, 1)
        _Reveal ("Reveal", Range(0, 1)) = 1
        _SweepAmount ("Sweep Amount", Range(0, 1)) = 0
        _SweepBottom ("Sweep Bottom (world Y)", Float) = 0
        _SweepHeight ("Sweep Height", Float) = 1
        _BlockSize ("Blocks Per Unit", Float) = 2.2
        _ScanDensity ("Scanlines Per Unit", Float) = 1.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HoloColor;
                float _Reveal;
                float _SweepAmount;
                float _SweepBottom;
                float _SweepHeight;
                float _BlockSize;
                float _ScanDensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            // A random number from 0 to 1 for each block of space.
            float Random01(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1) * 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;

                // Each block has its turn to appear; a block shows once _Reveal has passed its turn.
                float grain = Random01(floor(p * _BlockSize));
                float height = saturate((p.y - _SweepBottom) / max(_SweepHeight, 0.001));
                float turn = lerp(grain, height * 0.85 + grain * 0.15, _SweepAmount);
                float cut = _Reveal * 1.15 - 0.15;
                clip(cut - turn);

                // Blocks that have only just appeared (or are about to go) burn white.
                float edge = saturate(1.0 - (cut - turn) / 0.12);

                float scan = frac(p.y * _ScanDensity + _Time.y * 1.4) < 0.45 ? 1.0 : 0.3;
                float3 view = GetWorldSpaceNormalizeViewDir(p);
                float rim = pow(1.0 - abs(dot(normalize(input.normalWS), view)), 2.5);
                float flicker = 0.88 + 0.12 * sin(_Time.y * 47.0 + p.y * 3.0);

                half3 color = lerp(_HoloColor.rgb + rim * 0.35, half3(1, 1, 1), edge * 0.85);
                half alpha = saturate((0.18 + 0.55 * scan + 0.5 * rim) * flicker + edge) * _HoloColor.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

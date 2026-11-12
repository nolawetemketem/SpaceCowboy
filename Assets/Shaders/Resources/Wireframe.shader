// Wireframe shader: glowing LED lines along a ship's edges, with a dot of light at each corner.
// Each vertex of the wireframe mesh carries, in uv.x, the moment (0 to 1) the trace reaches it.
// Raise _Progress from 0 to 1 and the lines grow along the ship from corner to corner,
// with a white-hot tip where they're still growing. _Fade dims the whole thing (1 = full, 0 = gone).
// It lives in a Resources folder so that it's included in builds, even though only code uses it.

Shader "SpaceCowboy/Wireframe"
{
    Properties
    {
        _WireColor ("Color", Color) = (0.37, 0.9, 0.02, 1)
        _Progress ("Progress", Range(0, 1)) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _TipLength ("Tip Length", Float) = 0.06
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Wireframe"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One // light adds up, like real LEDs
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WireColor;
                float _Progress;
                float _Fade;
                float _TipLength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float arrival : TEXCOORD0; float height : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.arrival = input.uv.x;
                output.height = world.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Not reached yet: nothing here.
                float since = _Progress - input.arrival;
                clip(since);

                // Just reached: the growing tip burns white, then settles to the LED color.
                float tip = saturate(1.0 - since / _TipLength);
                float flicker = 0.85 + 0.15 * sin(_Time.y * 53.0 + input.height * 9.0);
                half3 color = lerp(_WireColor.rgb, half3(1, 1, 1), tip * 0.85) * (1.0 + tip * 1.5);
                return half4(color * flicker * _Fade, 1);
            }
            ENDHLSL
        }
    }
}

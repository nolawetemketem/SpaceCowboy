// Thruster shader: a jet flame on a cone mesh (made in code, nozzle at uv.y = 0, tip at uv.y = 1).
// White-hot at the nozzle, the flame color through the middle, fading to nothing at the tip. Faces turned
// edge-on to the camera fade out, so the cone reads as a soft glowing plume rather than a solid shape.
// Bands of flicker race down it. Light adds up (additive blending), like the sparks and lasers.
// It lives in a Resources folder so that it's included in builds, even though only code uses it.

Shader "SpaceCowboy/Thruster"
{
    Properties
    {
        _Color ("Flame Color", Color) = (1, 0.54, 0.12, 1)
        _Intensity ("Brightness", Float) = 1.5
        _FlickerSpeed ("Flicker Speed", Float) = 38
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Thruster"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _FlickerSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewWS : TEXCOORD2; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float along = input.uv.y; // 0 at the nozzle, 1 at the tip

                // 1 where the cone faces the camera, 0 on its outline: a soft, round-looking plume.
                float facing = abs(dot(normalize(input.normalWS), normalize(input.viewWS)));
                float body = pow(facing, 1.4);

                // Flicker: bands racing down the flame, a little twisted around it.
                float bands = sin(along * 22.0 - _Time.y * _FlickerSpeed + input.uv.x * 6.2832 * 2.0) * 0.5 + 0.5;
                float fade = pow(saturate(1.0 - along), 1.7) * (0.75 + 0.25 * bands);

                // The white-hot core near the nozzle.
                float core = pow(facing, 5.0) * pow(saturate(1.0 - along * 1.6), 3.0);

                half3 color = _Color.rgb * body * fade + lerp(_Color.rgb, half3(1, 1, 1), 0.7) * core;
                return half4(color * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}

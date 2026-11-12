// Laser shader: a bolt of light shaped like a capsule, a line with rounded ends.
// A thin hot core runs down the middle of a soft, saturated glow that fades all the way to nothing,
// so there are no hard edges or flat ends. The bolt is brightest at its head and trails off toward the tail.
// It's drawn on a flat card that always faces the camera, and the glow is worked out from each pixel's
// distance to the bolt's center line, so it looks round from every angle, even flying straight away from you.
// Light adds up (additive blending), like real light.
//
// The mesh is four corners: uv.x is the side (-1 or 1), uv.y is the end (0 = tail, 1 = head).
// The bolt runs along the object's +Z from z = -1 (tail) to 0 (head); the object's Z scale sets its length.
// _Width is the full width of the glow, in world units.
// It lives in a Resources folder so that it's included in builds, even though only code uses it.

Shader "SpaceCowboy/Laser"
{
    Properties
    {
        _LaserColor ("Color", Color) = (1, 0.54, 0.12, 1)
        _Width ("Glow Width", Float) = 2.6
        _Intensity ("Brightness", Float) = 2
        _Saturation ("Saturation", Range(1, 3)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Laser"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LaserColor;
                float _Width;
                float _Intensity;
                float _Saturation;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 world : TEXCOORD0;
                nointerpolation float3 tail : TEXCOORD1;
                nointerpolation float3 head : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 tail = TransformObjectToWorld(float3(0, 0, -1));
                float3 head = TransformObjectToWorld(float3(0, 0, 0));
                float radius = _Width * 0.5;

                // The card's corner: start at this end of the bolt...
                float3 end = lerp(tail, head, input.uv.y);
                float3 toCamera = normalize(GetCameraPositionWS() - end);

                // ...find the bolt's direction as seen from the camera (flattened onto the screen)...
                float3 along = head - tail;
                float3 flat = along - dot(along, toCamera) * toCamera;
                if (dot(flat, flat) < 1e-6) flat = cross(toCamera, abs(toCamera.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0));
                flat = normalize(flat);
                float3 side = normalize(cross(flat, toCamera));

                // ...then push the corner out sideways and past the end, so the round caps fit inside the card.
                float3 world = end + side * (input.uv.x * radius) + flat * ((input.uv.y * 2 - 1) * radius);

                output.positionCS = TransformWorldToHClip(world);
                output.world = world;
                output.tail = tail;
                output.head = head;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // How far this pixel is from the bolt's center line, and how far along the bolt it is.
                float3 bolt = input.head - input.tail;
                float t = saturate(dot(input.world - input.tail, bolt) / max(dot(bolt, bolt), 1e-6));
                float3 nearest = input.tail + bolt * t;
                float across = length(input.world - nearest) / (_Width * 0.5); // 0 on the line, 1 at the glow's edge

                // Soft glow that reaches exactly zero at the edge, and a thin hot core.
                float glow = pow(saturate(1.0 - across), 2.4);
                float core = pow(saturate(1.0 - across / 0.3), 2.0);

                // Brightest at the head, fading to nothing at the tail.
                float trail = smoothstep(0.0, 0.85, t);

                // Saturated color: raising it to a power deepens the hue (orange gets richer, less yellow).
                half3 deep = pow(max(_LaserColor.rgb, 0.0001), _Saturation);
                half3 hot = lerp(_LaserColor.rgb, half3(1, 1, 1), 0.45);
                half3 color = deep * glow * 1.3 + hot * core;
                return half4(color * trail * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}

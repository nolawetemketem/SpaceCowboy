// Sky:
//  - A three-color gradient: horizon, then mid, then top. Use it as a Skybox material (Window > Rendering > Lighting > Environment > Skybox Material).
//  - Defaults are the daytime colors; night is top #2c2236, mid #594863, horizon #6b4a5e.
//  - The sun (or moon, at night): wherever the scene's main light comes from, a bright disc with a soft glow
//    around it, in the light's color. It's brighter than white, so the bloom spreads it into glare.
//    The game's Atmosphere sets how big the glow is (_SunGlow) and how big the disc is (_SunSize).

Shader "SpaceCowboy/GradientSky"
{
    Properties
    {
        _Top ("Top", Color) = (0.027, 0.220, 0.553, 1)     // #0F3474
        _Mid ("Mid", Color) = (0.290, 0.486, 0.690, 1)     // #567EA7
        _Horizon ("Horizon", Color) = (0.824, 0.749, 0.627, 1) // #F5F3F1, dusty desert haze
        _SunSize ("Sun Size (degrees across)", Float) = 3
        _SunGlow ("Sun Glow", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Top;
                half4 _Mid;
                half4 _Horizon;
                float _SunSize;
                float _SunGlow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.direction = v.positionOS.xyz; // the skybox is centred on the camera, so this is the view direction
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // -1 straight down .. 1 straight up. Normalized per pixel so the bands stay smooth.
                float3 view = normalize(i.direction);
                float h = max(view.y, 0.0);
                half3 col = lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0.0, 0.13, h));
                col = lerp(col, _Top.rgb, smoothstep(0.13, 0.3, h));

                // The sun: a disc, a tight bright halo, and a wide haze around it (light scattered by the air).
                float toward = dot(view, normalize(_MainLightPosition.xyz)); // 1 looking straight at it
                float radius = cos(radians(_SunSize * 0.5));
                float disc = smoothstep(radius - 0.00015, radius + 0.00015, toward);
                float halo = pow(saturate(toward), 600.0) * 1.5 + pow(saturate(toward), 40.0) * 0.45 + pow(saturate(toward), 6.0) * 0.18;
                half3 light = _MainLightColor.rgb * 0.5; // the toon shader's "plain color" sun has intensity 2
                col += light * halo * _SunGlow;
                col = lerp(col, light * 4.0, disc * saturate(_SunGlow)); // brighter than white: the bloom turns it into glare
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}

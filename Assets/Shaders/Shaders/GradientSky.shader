// Sky:
//  - A three-color gradient: horizon, then mid, then top. Use it as a Skybox material (Window > Rendering > Lighting > Environment > Skybox Material).
//  - Defaults are the daytime colors; night is top #2c2236, mid #594863, horizon #6b4a5e.

Shader "SpaceCowboy/GradientSky"
{
    Properties
    {
        _Top ("Top", Color) = (0.027, 0.220, 0.553, 1)     // #07388d
        _Mid ("Mid", Color) = (0.290, 0.486, 0.690, 1)     // #4a7cb0
        _Horizon ("Horizon", Color) = (0.824, 0.749, 0.627, 1) // #d2bfa0, dusty desert haze
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
                float h = max(normalize(i.direction).y, 0.0);
                half3 col = lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0.0, 0.18, h));
                col = lerp(col, _Top.rgb, smoothstep(0.18, 0.6, h));
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}

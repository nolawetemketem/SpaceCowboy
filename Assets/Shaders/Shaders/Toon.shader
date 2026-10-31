// Toon shader: ported from Space Cowboy Web (src/render/templates/customToon/).
// Three flat light bands, and shadows that lean toward a color (Bebop purple/blue) instead of grey.
//
// What changed from the web version:
//   - It uses the scene's main Directional Light, so lightDirection/lightColor are gone.
//   - Cast shadows and fog work (the web version left both out).
//   - It has DepthOnly / DepthNormals passes, so the ink outline pass can see it.

Shader "SpaceCowboy/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.945, 0.902, 0.812, 1) // craftBody cream
        _ShadowColor ("Shadow Color", Color) = (0.39, 0.46, 0.55, 1)            // the shadow band: color × this (measured from the web game)
        _NightShadowColor ("Night Shadow Color", Color) = (0.37, 0.33, 0.42, 1) // the same at night (purple), when the game switches to night
        _LightStrength ("Light Strength", Float) = 1
        _BandLit ("Lit Band (N·L above)", Range(-1, 1)) = 0.5
        _BandMid ("Mid Band (N·L above)", Range(-1, 1)) = 0
        [Toggle(_VERTEX_COLORS)] _UseVertexColors ("Use Vertex Colors (desert ground)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Every pass shares the same material values (keeps the SRP Batcher happy).
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowColor;
            half4 _NightShadowColor;
            half _LightStrength;
            half _BandLit;
            half _BandMid;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Set for every toon material at once by the game's TimeOfDay: 0 = day, 1 = night.
        float _ToonNight;
        ENDHLSL

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma shader_feature_local _VERTEX_COLORS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR; // per-vertex color (the desert's sand, worn field and rock)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldNormal : TEXCOORD0;   // surface direction, in world space, for N·L
                float3 worldPosition : TEXCOORD1; // for shadows, and for noise that "sticks" to the world
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                half3 color : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                #if defined(_VERTEX_COLORS)
                    o.color = v.color.rgb;
                #else
                    o.color = 1;
                #endif
                o.worldPosition = TransformObjectToWorld(v.positionOS.xyz);
                o.worldNormal = TransformObjectToWorldNormal(v.normalOS); // handles non-uniform scale too
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.positionCS = TransformWorldToHClip(o.worldPosition);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 1. Which way does this pixel face? Interpolation shrinks normals, so re-normalize.
                float3 n = normalize(i.worldNormal);

                // 2. Lambert: 1 = head-on, 0 = edge-on, <0 = facing away.
                //    Unity's light direction points TOWARD the light, so no minus sign (the web one had -lightDirection).
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.worldPosition));
                float nDotL = dot(n, sun.direction);

                // Cast shadows drop the pixel into the shadow band, so they match turned-away faces exactly.
                nDotL = min(nDotL, lerp(-1.0, 1.0, sun.shadowAttenuation));

                // 5. YOUR TURN — hand-drawn band edges (Messenger's look). Nudge nDotL here, before step 3:
                //    float noise = frac(sin(dot(i.worldPosition.xz, float2(12.9898, 78.233))) * 43758.5453);
                //    nDotL += (noise - 0.5) * 0.08;
                //    Try a smoother value noise at a lower frequency (i.worldPosition.xz * 0.5),
                //    and add _Time.y to the noise input so the edges "boil".

                // 3. Bands: snap the smooth value to 3 flat steps, like the web game's toon ramp:
                //    facing away = shadow (0), up to _BandLit = mid (0.75), beyond = fully lit (1).
                float band = 0.75 * step(_BandMid, nDotL) + 0.25 * step(_BandLit, nDotL);

                // 4. Colored shadow: the shadow band is the color times _ShadowColor (a cool, dark tint),
                //    the lit band is the color itself in sunlight. Measured from the web game: lit sand
                //    shows its exact color, shadowed sand about (0.39, 0.46, 0.55) of it.
                //    A sun of intensity 2 (white) gives the plain color; brighter or warmer lights tint it.
                half3 albedo = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * i.color;
                half3 sunlight = sun.color * (0.5 * _LightStrength);
                half3 shadowTint = lerp(_ShadowColor.rgb, _NightShadowColor.rgb, _ToonNight);
                half3 color = albedo * lerp(shadowTint, max(sunlight, shadowTint), band);

                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // Lets this object cast shadows.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection; // set by URP while drawing shadow maps
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif

                Varyings o;
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        // Writes depth, for the depth texture the ink pass reads.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        // Writes surface directions, for the normals texture the ink pass reads (its creases).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 normalWS = NormalizeNormalPerPixel(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    return half4(PackFloat2To888(saturate(octNormalWS * 0.5 + 0.5)), 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}

// Ink pass
//  - Draws the outlines after the scene is rendered, in screen space. For each screen pixel it
//  - compares the depth and surface direction with its neighbours; where either jumps, that's an
//    edge: ink it. Lines are the same pixel width at any distance.
//
// Use it with URP's Full Screen Pass Renderer Feature (on the URP Renderer asset):
//   Injection Point: Before Rendering Post Processing
//   Requirements:    Depth + Normal
// That feature hands this shader the scene colors (_BlitTexture); URP fills in the
// depth and normals textures, so there's no separate normals render like the web version had.

Shader "SpaceCowboy/InkOutline"
{
    Properties
    {
        _Thickness ("Thickness (px, about half the line width)", Float) = 1.0
        _InkStrength ("Ink Strength (1 = solid)", Range(0, 1)) = 0.85
        _InkColor ("Ink Color", Color) = (0.141, 0.086, 0.118, 1) // a warm near-black, #24161e
        _DepthThreshold ("Depth Threshold (silhouettes)", Float) = 0.12
        _NormalThreshold ("Normal Threshold (creases)", Float) = 0.35
        _ColorThreshold ("Color Threshold", Range(0, 1)) = 0.12
        _FadeNear ("Fade Start (distance)", Float) = 60
        _FadeFar ("Fade End (distance)", Float) = 400
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "InkOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _Thickness;
            float _InkStrength;
            float4 _InkColor;
            float _DepthThreshold;
            float _NormalThreshold;
            float _ColorThreshold;
            float _FadeNear;
            float _FadeFar;

            // Distance from the camera. The depth buffer stores it non-linearly, so convert.
            float ViewDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            // World-space here (the web version was view-space); the crease test works the same in either.
            float3 ViewNormal(float2 uv)
            {
                return SampleSceneNormals(uv);
            }

            float Brightness(float2 uv)
            {
                return dot(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb, float3(0.2126, 0.7152, 0.0722));
            }

            // How much the color changes across this pixel, relative to how bright it is (0 = none).
            float ColorChange(float2 uv, float2 ox, float2 oy)
            {
                float l = Brightness(uv - ox), r = Brightness(uv + ox);
                float d = Brightness(uv - oy), u = Brightness(uv + oy);
                float change = max(abs(l - r), abs(u - d));
                return change / max(max(max(l, r), max(u, d)), 0.02);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 ox = float2(_Thickness * _ScreenSize.z, 0.0); // _ScreenSize.zw = 1 / pixel size
                float2 oy = float2(0.0, _Thickness * _ScreenSize.w);

                // Silhouettes, from depth. 1/depth changes evenly across any flat surface on screen,
                // so its "bend" (left + right - 2 × centre) is ~0 on flat ground, even at grazing angles,
                // and only spikes where one surface ends and another starts behind it.
                float c = ViewDepth(uv);
                float ic = 1.0 / c;
                float bendX = abs(1.0 / ViewDepth(uv - ox) + 1.0 / ViewDepth(uv + ox) - 2.0 * ic);
                float bendY = abs(1.0 / ViewDepth(uv - oy) + 1.0 / ViewDepth(uv + oy) - 2.0 * ic);
                float depthEdge = (bendX + bendY) / ic;

                // Creases, from normals: 1 - dot is 0 for faces pointing the same way and grows as they turn.
                float3 n = ViewNormal(uv);
                float turn = max(
                    max(1.0 - dot(n, ViewNormal(uv - ox)), 1.0 - dot(n, ViewNormal(uv + ox))),
                    max(1.0 - dot(n, ViewNormal(uv - oy)), 1.0 - dot(n, ViewNormal(uv + oy))));

                // Only ink what you can see: where both sides are the same color, skip creases.
                // Silhouettes follow the same rule unless the depth jump is big, which always gets a line.
                float visible = smoothstep(_ColorThreshold, _ColorThreshold * 2.0, ColorChange(uv, ox * 2.0, oy * 2.0));
                float bigJump = smoothstep(_DepthThreshold * 3.0, _DepthThreshold * 6.0, depthEdge);

                float edge = max(
                    smoothstep(_DepthThreshold, _DepthThreshold * 1.5, depthEdge) * max(visible, bigJump),
                    smoothstep(_NormalThreshold, _NormalThreshold * 1.5, turn) * visible);

                // brush weight. Real ink lines swell and thin. Ideas:
                //   a) make a smooth noise value from the screen position (uv * _ScreenSize.xy / 40.0),
                //   b) scale `ox` and `oy` by (0.6 + noise) before sampling, so the line width varies,
                //   c) or scale `edge` by noise so some lines fade like a dry brush.

                edge *= 1.0 - smoothstep(_FadeNear, _FadeFar, c);
                return half4(lerp(scene.rgb, _InkColor.rgb, edge * _InkStrength), scene.a);
            }
            ENDHLSL
        }
    }
}

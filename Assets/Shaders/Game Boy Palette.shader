// Game Boy filter: maps every game pixel to one of 4 shades (darkest -> lightest) by its perceived brightness.
// Runs on the game-resolution image, so each game pixel gets exactly one shade.
Shader "Hidden/Game Boy Palette"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "GameBoyPalette"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Set with Material.SetColor, so they arrive in the project's (linear) colour space
            float4 _Shade0;
            float4 _Shade1;
            float4 _Shade2;
            float4 _Shade3;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 colour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord).rgb;

                // Brightness as the eye sees it (sRGB), so the 4 bands split where they look evenly spaced
                float3 display = LinearToSRGB(saturate(colour));
                float luma = dot(display, float3(0.299, 0.587, 0.114));

                float level = min(floor(luma * 4.0), 3.0);
                float3 shade = level < 0.5 ? _Shade0.rgb : level < 1.5 ? _Shade1.rgb : level < 2.5 ? _Shade2.rgb : _Shade3.rgb;
                return half4(shade, 1.0);
            }
            ENDHLSL
        }
    }
}

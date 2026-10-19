// TV filter: composite-video softness. Runs on the game-resolution image (256x240 in gameplay) and lets each
// pixel bleed a little into its left/right neighbours, like the horizontal blur of a composite signal.
Shader "Hidden/CRT Composite"
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
            Name "CRTComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Blur;            // weight of each neighbour (0 = off, 0.33 = full box blur)
            float4 _SourceTexelSize; // (1/width, 1/height, width, height)

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 dx = float2(_SourceTexelSize.x, 0.0);

                half3 centre = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;
                half3 left = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv - dx).rgb;
                half3 right = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + dx).rgb;

                return half4(centre * (1.0 - 2.0 * _Blur) + (left + right) * _Blur, 1.0);
            }
            ENDHLSL
        }
    }
}

// TV filter: CRT scanlines (+ faint aperture grille) drawn over the game view on an overlay canvas.
// The RawImage uvRect is (0, 0, gamePixelsX, gamePixelsY), so one UV unit is exactly one game pixel.
// "2x multiply" blending (dst * src * 2): output 0.5 leaves the screen untouched, below darkens, above brightens.
Shader "UI/CRT Scanlines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Zoom ("Screen Pixels Per Game Pixel", Float) = 4
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.35
        _MaskStrength ("Aperture Grille Strength", Range(0, 0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend DstColor SrcColor

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 cell : TEXCOORD0;
            };

            float _Zoom;
            float _ScanlineStrength;
            float _MaskStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.cell = v.texcoord;
                return o;
            }

            // Antiderivative of sin^2(pi * y): lets us average the beam profile over the screen pixel exactly,
            // so scanlines stay even at non-integer scales (Full screen) instead of beating.
            float BeamIntegral(float y)
            {
                return y * 0.5 - sin(6.2831853 * y) / 12.5663706;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float zoom = max(_Zoom, 0.001);

                // Beam brightness across the game pixel row: 0 at the row edges, 1 in the middle (mean 0.5)
                float footprint = max(1.0 / zoom, 1e-4);
                float y0 = i.cell.y - footprint * 0.5;
                float y1 = i.cell.y + footprint * 0.5;
                float beam = (BeamIntegral(y1) - BeamIntegral(y0)) / footprint;

                // Scanlines need at least ~3 screen pixels per row to read as lines rather than noise
                float strength = _ScanlineStrength * smoothstep(1.5, 3.0, zoom);
                // Mean of the multiplier is 1, so overall brightness stays the same as with no filter
                float3 mult = (1.0 - strength + strength * beam * 2.0).xxx;

                // Aperture grille: R/G/B columns one screen pixel wide, only once pixels are big (4x+).
                // Per-channel average stays 1 so colours don't shift.
                float k = _MaskStrength * smoothstep(3.5, 5.0, zoom);
                float column = fmod(floor(i.vertex.x), 3.0);
                float3 phosphor = float3(column == 0.0, column == 1.0, column == 2.0);
                mult *= 1.0 + k * (3.0 * phosphor - 1.0);

                // RawImage color alpha fades the whole effect
                mult = lerp(1.0, mult, i.color.a);

                return fixed4(saturate(mult * 0.5), 1.0);
            }
            ENDCG
        }
    }
}

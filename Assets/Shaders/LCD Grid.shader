// Monitor filter: LCD pixel grid drawn over the game view on an overlay canvas.
// The RawImage uvRect is (0, 0, gamePixelsX, gamePixelsY), so one UV unit is exactly one game pixel.
// "2x multiply" blending (dst * src * 2): output 0.5 leaves the screen untouched, below darkens, above brightens.
Shader "UI/LCD Grid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Zoom ("Screen Pixels Per Game Pixel", Float) = 4
        _GridStrength ("Grid Strength", Range(0, 1)) = 0.3
        _GapWidth ("Gap Width (fraction of a game pixel)", Range(0, 0.5)) = 0.12
        _SubpixelStrength ("Subpixel RGB Strength", Range(0, 0.5)) = 0.15
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
            float _GridStrength;
            float _GapWidth;
            float _SubpixelStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.cell = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float zoom = max(_Zoom, 0.001);
                float2 p = frac(i.cell);

                // Gap on the right and bottom of each game pixel, at least one screen pixel wide.
                // Edges are anti-aliased over one screen pixel so non-integer scales (Pillar) stay even.
                float gap = min(max(_GapWidth, 1.0 / zoom), 0.5);
                float aa = 0.5 / zoom;
                float lineX = smoothstep(1.0 - gap - aa, 1.0 - gap + aa, p.x);
                float lineY = 1.0 - smoothstep(gap - aa, gap + aa, p.y);
                float grid = max(lineX, lineY);

                // At 1-2x a grid is just noise, so fade it in from 2x to 3x
                float strength = _GridStrength * smoothstep(1.5, 3.0, zoom);

                // Brighten the lit part of the cell so the average brightness stays the same
                float gapArea = 1.0 - (1.0 - gap) * (1.0 - gap);
                float boost = min(gapArea * strength / max(1.0 - gapArea, 0.001), 0.5);
                float3 mult = lerp(1.0 + boost, 1.0 - strength, grid).xxx;

                // Faint vertical R/G/B stripes inside each cell once a game pixel is big enough (5x+).
                // Triangular weights keep the per-channel average at 1 so colors don't shift.
                float k = _SubpixelStrength * smoothstep(4.5, 6.0, zoom);
                float x = saturate(p.x / (1.0 - gap));
                float3 w = saturate(1.0 - abs(x - float3(1.0 / 6.0, 0.5, 5.0 / 6.0)) * 3.0);
                mult *= 1.0 + k * (3.0 * w - 1.0);

                // RawImage color alpha fades the whole effect
                mult = lerp(1.0, mult, i.color.a);

                return fixed4(saturate(mult * 0.5), 1.0);
            }
            ENDCG
        }
    }
}

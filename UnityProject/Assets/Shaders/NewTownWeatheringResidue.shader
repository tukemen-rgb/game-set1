Shader "NewTown/QualityBlockWeatheringResidue"
{
    Properties
    {
        _Tint ("Dry Deposit Tint", Color) = (0.25,0.25,0.23,1)
        _Opacity ("Deposit Optical Density", Range(0,0.25)) = 0.10
        _Roughness ("Perceptual Roughness", Range(0.80,1.0)) = 0.94
        _Metallic ("Metallic (must remain zero)", Float) = 0
        _DielectricF0 ("Dielectric F0 (Standard dielectric = 0.04)", Range(0.02,0.06)) = 0.04
        _NormalScale ("Normal Scale (no invented relief)", Float) = 0
        _Wetness ("Wetness (dry benchmark)", Float) = 0
        [NoScaleOffset] _MaskTex ("UV0 Mask Carrier", 2D) = "white" {}
        _ProfileMode ("Mask Profile: 0 Ribbon, 1 Grade, 2 Ellipse", Float) = 0
        _EdgeFeatherU ("Cross-edge Feather", Range(0.001,0.49)) = 0.22
        _StartFeatherV ("Source-end Feather", Range(0,0.49)) = 0.08
        _EndFeatherV ("Tail/top Feather", Range(0.001,0.75)) = 0.28
        _EllipseCore ("Ellipse Core Radius", Range(0,0.95)) = 0.56
        _MicroBreakup ("Sub-pixel-safe Optical Breakup", Range(0,0.15)) = 0.035
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 250
        Cull Back
        ZWrite Off

        CGPROGRAM
        // Standard gives the residue the same live direct-light, shadow, GI and reflection-probe
        // environment as its host facade. The deposit is deliberately kept as an optically thin,
        // high-roughness dielectric layer; renderer shadow casting is disabled by construction QA.
        #pragma target 3.0
        #pragma surface surf Standard alpha:fade fullforwardshadows

        sampler2D _MaskTex;
        fixed4 _Tint;
        half _Opacity;
        half _Roughness;
        half _ProfileMode;
        half _EdgeFeatherU;
        half _StartFeatherV;
        half _EndFeatherV;
        half _EllipseCore;
        half _MicroBreakup;

        struct Input
        {
            float2 uv_MaskTex;
            float3 worldPos;
        };

        half RibbonMask(float2 uv)
        {
            half u = saturate(uv.x);
            half v = saturate(uv.y);
            half crossEdge = smoothstep(0.0h, _EdgeFeatherU, u) *
                             smoothstep(0.0h, _EdgeFeatherU, 1.0h - u);
            half sourceFade = _StartFeatherV <= 0.0001h ? 1.0h : smoothstep(0.0h, _StartFeatherV, v);
            half tailFade = 1.0h - smoothstep(1.0h - _EndFeatherV, 1.0h, v);
            return crossEdge * sourceFade * tailFade;
        }

        half GradeMask(float2 uv)
        {
            half u = saturate(uv.x);
            half v = saturate(uv.y);
            // Geometry owns causal splash-height variation. Optical density is strongest at grade
            // and feathers toward the irregular upper edge; no light-facing stripe is encoded here.
            half sideFade = smoothstep(0.0h, _EdgeFeatherU, u) *
                            smoothstep(0.0h, _EdgeFeatherU, 1.0h - u);
            half topFade = 1.0h - smoothstep(1.0h - _EndFeatherV, 1.0h, v);
            return sideFade * topFade;
        }

        half EllipseMask(float2 uv)
        {
            half radial = length((uv - 0.5h) * 2.0h);
            return 1.0h - smoothstep(_EllipseCore, 1.0h, radial);
        }

        half StableBreakup(float3 worldPos)
        {
            // Low-amplitude world-space deposit-density modulation avoids a perfectly uniform decal
            // while staying intentionally below texture-like high frequencies that would shimmer at 4K.
            // It contains no sun direction, contact shadow, AO or highlight information.
            half low = sin(worldPos.x * 5.17h + worldPos.y * 3.11h + worldPos.z * 4.03h);
            half mid = sin(worldPos.x * 11.73h - worldPos.y * 7.31h + worldPos.z * 9.19h);
            return saturate(1.0h + _MicroBreakup * (0.68h * low + 0.32h * mid));
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            half mask;
            if (_ProfileMode < 0.5h)
                mask = RibbonMask(IN.uv_MaskTex);
            else if (_ProfileMode < 1.5h)
                mask = GradeMask(IN.uv_MaskTex);
            else
                mask = EllipseMask(IN.uv_MaskTex);

            mask *= StableBreakup(IN.worldPos);
            half alpha = saturate(_Opacity * mask);
            clip(alpha - 0.0005h);

            o.Albedo = _Tint.rgb;
            o.Metallic = 0.0h;
            o.Smoothness = saturate(1.0h - _Roughness);
            o.Occlusion = 1.0h;
            o.Emission = half3(0.0h, 0.0h, 0.0h);
            o.Alpha = alpha;
        }
        ENDCG
    }

    Fallback Off
}

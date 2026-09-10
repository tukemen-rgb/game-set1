Shader "NewTown/QualityBlockWeatheringResidue"
{
    Properties
    {
        _Tint ("Dry Deposit Tint", Color) = (0.25,0.25,0.23,1)
        _Opacity ("Deposit Optical Density", Range(0,0.25)) = 0.10
        _Roughness ("Perceptual Roughness", Range(0.80,1.0)) = 0.94
        _DielectricF0 ("Dielectric F0", Range(0.02,0.06)) = 0.04
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Tint;
            half _Opacity;
            half _Roughness;
            half _DielectricF0;
            half _ProfileMode;
            half _EdgeFeatherU;
            half _StartFeatherV;
            half _EndFeatherV;
            half _EllipseCore;
            half _MicroBreakup;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                half3 worldNormal : TEXCOORD2;
                SHADOW_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                TRANSFER_SHADOW(o);
                return o;
            }

            half FresnelSchlick(half f0, half cosTheta)
            {
                half m = 1.0h - saturate(cosTheta);
                half m2 = m * m;
                return f0 + (1.0h - f0) * m2 * m2 * m;
            }

            half GgxDirectSpecular(half noL, half noV, half noH, half voH, half roughness, half f0)
            {
                if (noL <= 0.0h || noV <= 0.0h)
                    return 0.0h;

                half alpha = max(0.04h, roughness * roughness);
                half alpha2 = alpha * alpha;
                half dDenom = noH * noH * (alpha2 - 1.0h) + 1.0h;
                half D = alpha2 / max(0.001h, 3.14159265h * dDenom * dDenom);
                half k = roughness + 1.0h;
                k = (k * k) * 0.125h;
                half Gv = noV / max(0.001h, noV * (1.0h - k) + k);
                half Gl = noL / max(0.001h, noL * (1.0h - k) + k);
                half F = FresnelSchlick(f0, voH);
                return (D * Gv * Gl * F) / max(0.001h, 4.0h * noL * noV) * noL;
            }

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
                // Geometry already carries the causal splash-height variation. Optical density is
                // strongest at grade and feathers toward the irregular upper edge; no vertical strip
                // or light-facing highlight is painted into the mask.
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
                // Low-amplitude world-space modulation breaks perfectly uniform opacity without
                // introducing high-frequency noise that would shimmer at native 4K. It is optical
                // deposit density only; it never encodes sun direction, contact shadow or a highlight.
                half low = sin(worldPos.x * 5.17h + worldPos.y * 3.11h + worldPos.z * 4.03h);
                half mid = sin(worldPos.x * 11.73h - worldPos.y * 7.31h + worldPos.z * 9.19h);
                return saturate(1.0h + _MicroBreakup * (0.68h * low + 0.32h * mid));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half mask;
                if (_ProfileMode < 0.5h)
                    mask = RibbonMask(i.uv);
                else if (_ProfileMode < 1.5h)
                    mask = GradeMask(i.uv);
                else
                    mask = EllipseMask(i.uv);

                mask *= StableBreakup(i.worldPos);
                half alpha = saturate(_Opacity * mask);
                clip(alpha - 0.0005h);

                half3 n = normalize(i.worldNormal);
                half3 l = normalize(UnityWorldSpaceLightDir(i.worldPos));
                half3 v = normalize(UnityWorldSpaceViewDir(i.worldPos));
                half3 h = normalize(l + v);
                half noL = saturate(dot(n, l));
                half noV = saturate(dot(n, v));
                half noH = saturate(dot(n, h));
                half voH = saturate(dot(v, h));
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);

                half f0 = clamp(_DielectricF0, 0.02h, 0.06h);
                half roughness = clamp(_Roughness, 0.80h, 1.0h);
                half fresnel = FresnelSchlick(f0, voH);
                half3 ambient = max(0.0h, ShadeSH9(half4(n, 1.0h)));
                half3 directDiffuse = _LightColor0.rgb * noL * attenuation * (1.0h - fresnel);
                half specular = GgxDirectSpecular(noL, noV, noH, voH, roughness, f0) * attenuation;

                // The residue is a dry, optically thin dielectric deposit. Directional response is
                // computed from the live light/view vectors; no highlight or shadow is encoded in tint.
                half3 litDeposit = _Tint.rgb * (ambient + directDiffuse) + _LightColor0.rgb * specular;
                return fixed4(litDeposit, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}

Shader "Hidden/NewTown/FilmicTonemap"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _ExposureMultiplier ("Exposure Multiplier", Float) = 1.0
        _Contrast ("Display Contrast", Float) = 1.03
        _Saturation ("Display Saturation", Float) = 0.97
        _ShadowSoftening ("Shadow Softening", Float) = 0.025
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _ExposureMultiplier;
            float _Contrast;
            float _Saturation;
            float _ShadowSoftening;

            float3 AcesApprox(float3 x)
            {
                // Stable real-time fitted shoulder/toe transform. It is intentionally a display
                // transform only: no bloom, painted highlight, local contrast or sharpening can
                // conceal geometry/material defects from the 4K gate.
                const float a = 2.51;
                const float b = 0.03;
                const float c = 2.43;
                const float d = 0.59;
                const float e = 0.14;
                return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
            }

            float4 frag(v2f_img i) : SV_Target
            {
                // Keep full floating-point range through exposure and shoulder compression. The
                // output is clamped only after tonemapping so scene-linear highlights are not lost
                // before the filmic rolloff can operate.
                float3 sceneLinear = max(tex2D(_MainTex, i.uv).rgb, 0.0);
                float3 color = AcesApprox(sceneLinear * _ExposureMultiplier);

                // Keep grading deliberately restrained. Luminance-preserving saturation avoids
                // the oversaturated blue-sky/green-foliage look common in game-like summer scenes.
                float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
                color = lerp(luma.xxx, color, _Saturation);
                color = (color - 0.18) * _Contrast + 0.18;

                // Gently open very dark recesses without lifting true black. This is a display toe,
                // not synthetic fill light: 0 remains 0 and the effect vanishes toward white.
                float3 shadowWeight = saturate(1.0 - color * 4.0);
                color += _ShadowSoftening * color * shadowWeight;

                return float4(saturate(color), 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}

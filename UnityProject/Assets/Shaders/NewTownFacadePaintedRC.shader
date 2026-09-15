Shader "NewTown/FacadePaintedRC"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Base Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Primary Normal", 2D) = "bump" {}
        _BumpScale ("Primary Normal Scale", Range(0,2)) = 0.82
        _MetallicGlossMap ("Metallic Smoothness", 2D) = "black" {}
        _Metallic ("Metallic Fallback", Range(0,1)) = 0
        _Glossiness ("Smoothness Fallback", Range(0,1)) = 0.14
        _GlossMapScale ("Smoothness Map Scale", Range(0,1)) = 1
        [Normal] _DetailNormalMap ("Detail Normal", 2D) = "bump" {}
        _DetailNormalMapScale ("Detail Normal Strength", Range(0,2)) = 0.42
        _MacroVariationMap ("Neutral Macro Variation", 2D) = "gray" {}
        _EmissionColor ("Emission Guard", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        Cull Back
        ZWrite On

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard fullforwardshadows addshadow
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _MetallicGlossMap;
        sampler2D _DetailNormalMap;
        sampler2D _MacroVariationMap;
        fixed4 _Color;
        half _BumpScale;
        half _DetailNormalMapScale;
        half _Metallic;
        half _Glossiness;
        half _GlossMapScale;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_DetailNormalMap;
            float2 uv_MacroVariationMap;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 baseSample = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half3 macroVariation = tex2D(_MacroVariationMap, IN.uv_MacroVariationMap).rgb * unity_ColorSpaceDouble.rgb;
            half4 metallicSmoothness = tex2D(_MetallicGlossMap, IN.uv_MainTex);

            // Both normal maps are real lighting inputs, never baked highlights. Crucially, Surface Shader
            // supplies independent texture transforms here: the primary field remains 2.4 m, the detail
            // normal is 0.22 m, and the neutral macro-variation field is 7.9 m.
            half3 primaryNormal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            half3 detailNormal = UnpackScaleNormal(tex2D(_DetailNormalMap, IN.uv_DetailNormalMap), _DetailNormalMapScale);

            o.Albedo = saturate(baseSample.rgb * macroVariation);
            o.Normal = BlendNormals(primaryNormal, detailNormal);
            o.Metallic = saturate(max(_Metallic, metallicSmoothness.r));
            o.Smoothness = saturate(metallicSmoothness.a * _GlossMapScale);
            o.Occlusion = 1.0h;
            o.Emission = 0.0h;
            o.Alpha = baseSample.a;
        }
        ENDCG
    }

    FallBack "Standard"
}

Shader "NewTown/FoliageTransmission"
{
    Properties
    {
        _Color ("Base Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _MetallicGlossMap ("Smoothness Mask", 2D) = "black" {}
        _TransmissionColor ("Transmission Tint", Color) = (0.48,0.72,0.24,1)
        _TransmissionStrength ("Transmission Strength", Range(0,1)) = 0.34
        _Wrap ("Diffuse Wrap", Range(0,1)) = 0.28
        _SmoothnessScale ("Smoothness Scale", Range(0,1)) = 0.24
        _DielectricF0 ("Leaf Dielectric F0", Range(0.02,0.06)) = 0.03
        _NormalScale ("Leaf Normal Scale", Range(0,2)) = 0.72
        _ExposureBias ("Canopy Exposure Bias", Range(-0.2,0.2)) = 0
        _LeafVariation ("Leaf Value Variation", Range(-0.2,0.2)) = 0
        _LeafEdgeInset ("Leaf Margin Inset", Range(0,0.12)) = 0.055
        _LeafSerration ("Leaf Margin Serration", Range(0,0.05)) = 0.020
        _LeafEdgeAAScale ("Leaf Edge Derivative AA", Range(0.5,3)) = 1.35
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    half _LeafEdgeInset;
    half _LeafSerration;
    half _LeafEdgeAAScale;
    half _LeafVariation;

    // Canonical leaf UVs are independent from the tiled PBR texture UVs. The generated spray mesh
    // maps tip/right/base/left to (0.5,1)/(1,0.5)/(0.5,0)/(0,0.5). We keep that manufacturing-like
    // lamina domain for the macro margin and use the tiled textures only for cellular/vein microdetail.
    // This avoids painting a highlight or silhouette into albedo and keeps the same physical edge in
    // the forward and shadow-caster passes.
    half LeafEdgeCoverage(float2 rawUv, half variation)
    {
        half2 p = rawUv * 2.0h - 1.0h;
        half ay = saturate(abs(p.y));
        half diamondHalfWidth = saturate(1.0h - ay);

        // The source quad is a straight-edged diamond. A mild shoulder inset plus small deterministic
        // margin undulation breaks the ruler-straight card silhouette without inventing damage, holes
        // or torn leaves. Cluster variation changes phase but not physical width limits.
        half shoulder = lerp(0.90h, 1.0h, saturate(1.0h - ay * 1.55h));
        half phase = variation * 23.0h;
        half serration = sin((p.y * 0.5h + 0.5h) * 75.3982237h + phase) *
                          _LeafSerration * diamondHalfWidth;
        half centerSkew = sin(p.y * 3.14159265h + phase * 0.23h) * 0.018h;
        half halfWidth = max(0.0h,
            diamondHalfWidth * shoulder - _LeafEdgeInset * diamondHalfWidth + serration);
        half signedDistance = halfWidth - abs(p.x - centerSkew);

        // Screen-space derivative antialiasing feeds alpha-to-coverage on MSAA targets while clip()
        // still gives deterministic binary coverage on non-MSAA and shadow-map targets.
        half aa = max(fwidth(signedDistance) * _LeafEdgeAAScale, 0.0001h);
        return saturate(signedDistance / aa + 0.5h);
    }
    ENDCG

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        LOD 300
        Cull Off

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            ZWrite On
            AlphaToMask On

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BumpMap;
            sampler2D _MetallicGlossMap;
            fixed4 _Color;
            fixed4 _TransmissionColor;
            half _TransmissionStrength;
            half _Wrap;
            half _SmoothnessScale;
            half _DielectricF0;
            half _NormalScale;
            half _ExposureBias;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                half3 worldTangent : TEXCOORD2;
                half3 worldBitangent : TEXCOORD3;
                half3 worldNormal : TEXCOORD4;
                float2 rawLeafUv : TEXCOORD5;
                SHADOW_COORDS(6)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.rawLeafUv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                o.worldBitangent = cross(o.worldNormal, o.worldTangent) * tangentSign;
                TRANSFER_SHADOW(o);
                return o;
            }

            half3 ScaleTangentNormal(half3 n, half scale)
            {
                n.xy *= scale;
                n.z = sqrt(saturate(1.0h - dot(n.xy, n.xy)));
                return normalize(n);
            }

            half FresnelSchlick(half f0, half cosTheta)
            {
                half m = 1.0h - saturate(cosTheta);
                half m2 = m * m;
                half m5 = m2 * m2 * m;
                return f0 + (1.0h - f0) * m5;
            }

            half GgxDirectSpecular(half noL, half noV, half noH, half voH, half perceptualRoughness, half f0)
            {
                if (noL <= 0.0h || noV <= 0.0h)
                    return 0.0h;

                // Isotropic GGX with Schlick-Smith visibility. The lobe is deliberately broad for
                // dry living leaves: the source smoothness mask may only reduce perceptual roughness
                // into the 0.76-0.95 range, avoiding chrome/plastic sparkle on 4K foliage.
                half alpha = max(0.04h, perceptualRoughness * perceptualRoughness);
                half alpha2 = alpha * alpha;
                half dDenom = noH * noH * (alpha2 - 1.0h) + 1.0h;
                half D = alpha2 / max(0.001h, 3.14159265h * dDenom * dDenom);

                half k = perceptualRoughness + 1.0h;
                k = (k * k) * 0.125h;
                half Gv = noV / max(0.001h, noV * (1.0h - k) + k);
                half Gl = noL / max(0.001h, noL * (1.0h - k) + k);
                half G = Gv * Gl;
                half F = FresnelSchlick(f0, voH);

                half brdf = (D * G * F) / max(0.001h, 4.0h * noL * noV);
                return brdf * noL;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                half edgeCoverage = LeafEdgeCoverage(i.rawLeafUv, _LeafVariation);
                clip(edgeCoverage - 0.01h);

                fixed4 tex = tex2D(_MainTex, i.uv) * _Color;
                half3 tangentNormal = ScaleTangentNormal(UnpackNormal(tex2D(_BumpMap, i.uv)), _NormalScale);
                half3 normalWS = normalize(
                    i.worldTangent * tangentNormal.x +
                    i.worldBitangent * tangentNormal.y +
                    i.worldNormal * tangentNormal.z);

                // Cull Off is intentional for thin lamina geometry. Flip the shading normal on the
                // back face so the same physical leaf surface has a coherent view-facing normal and
                // the transmission test responds to the sun being behind that viewed face.
                half faceSign = facing >= 0.0h ? 1.0h : -1.0h;
                normalWS *= faceSign;

                half3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
                half3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                half rawNdotL = dot(normalWS, lightDir);
                half noL = saturate(rawNdotL);
                half noV = saturate(dot(normalWS, viewDir));
                half wrappedDiffuse = saturate((rawNdotL + _Wrap) / (1.0h + _Wrap));
                half backLighting = saturate(-rawNdotL);

                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);

                half3 ambient = ShadeSH9(half4(normalWS, 1.0h));
                half exposureMultiplier = saturate(1.0h + _ExposureBias + _LeafVariation);
                half3 albedo = tex.rgb * exposureMultiplier;

                half smoothness = saturate(tex2D(_MetallicGlossMap, i.uv).a * _SmoothnessScale);
                half perceptualRoughness = clamp(1.0h - smoothness, 0.45h, 0.95h);
                half3 halfDir = normalize(lightDir + viewDir);
                half noH = saturate(dot(normalWS, halfDir));
                half voH = saturate(dot(viewDir, halfDir));

                // Living leaf cell-wall/cuticle optics are dielectric. F0 is constrained near 0.03
                // instead of using a generic game-art specular scalar, and Schlick Fresnel makes the
                // grazing response angular rather than painted into albedo.
                half f0 = clamp(_DielectricF0, 0.02h, 0.06h);
                half directFresnel = FresnelSchlick(f0, voH);
                half viewFresnel = FresnelSchlick(f0, noV);
                half diffuseEnergy = 1.0h - directFresnel;
                half transmissionEnergy = 1.0h - viewFresnel;

                half3 direct = albedo * _LightColor0.rgb * wrappedDiffuse * attenuation * diffuseEnergy;

                // Thin leaves transmit a modest, green-biased portion of sunlight when the sun is
                // behind the visible face. Transmission obeys main-light attenuation and shares the
                // dielectric energy budget, so deep occlusion does not self-illuminate and the lobe
                // cannot exceed the available non-reflected energy.
                half3 transmission = albedo * _TransmissionColor.rgb * _LightColor0.rgb *
                                     backLighting * _TransmissionStrength * attenuation * transmissionEnergy;

                half specularTerm = GgxDirectSpecular(noL, noV, noH, voH, perceptualRoughness, f0);
                half3 specular = _LightColor0.rgb * specularTerm * attenuation;

                return fixed4(albedo * ambient + direct + transmission + specular, edgeCoverage);
            }
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster

            struct v2fShadow
            {
                V2F_SHADOW_CASTER;
                float2 rawLeafUv : TEXCOORD1;
            };

            v2fShadow vertShadow(appdata_base v)
            {
                v2fShadow o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.rawLeafUv = v.texcoord.xy;
                return o;
            }

            float4 fragShadow(v2fShadow i) : SV_Target
            {
                // Use exactly the same analytic margin as the visible pass. A stricter binary
                // threshold keeps the shadow map temporally stable while preventing diamond-card
                // silhouettes from reappearing only in dappled shadows.
                clip(LeafEdgeCoverage(i.rawLeafUv, _LeafVariation) - 0.5h);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}

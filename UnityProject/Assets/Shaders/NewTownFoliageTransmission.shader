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
        _ExposureBias ("Canopy Exposure Bias", Range(-0.2,0.2)) = 0
        _LeafVariation ("Leaf Value Variation", Range(-0.2,0.2)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        LOD 300
        Cull Off

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BumpMap;
            sampler2D _MetallicGlossMap;
            fixed4 _Color;
            fixed4 _TransmissionColor;
            half _TransmissionStrength;
            half _Wrap;
            half _SmoothnessScale;
            half _ExposureBias;
            half _LeafVariation;

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
                SHADOW_COORDS(5)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                o.worldBitangent = cross(o.worldNormal, o.worldTangent) * tangentSign;
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * _Color;
                half3 tangentNormal = UnpackNormal(tex2D(_BumpMap, i.uv));
                half3 normalWS = normalize(
                    i.worldTangent * tangentNormal.x +
                    i.worldBitangent * tangentNormal.y +
                    i.worldNormal * tangentNormal.z);

                half3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
                half3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                half rawNdotL = dot(normalWS, lightDir);
                half wrappedDiffuse = saturate((rawNdotL + _Wrap) / (1.0h + _Wrap));
                half backLighting = saturate(-rawNdotL);

                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);

                half3 ambient = ShadeSH9(half4(normalWS, 1.0h));
                half exposureMultiplier = saturate(1.0h + _ExposureBias + _LeafVariation);
                half3 albedo = tex.rgb * exposureMultiplier;
                half3 direct = albedo * _LightColor0.rgb * wrappedDiffuse * attenuation;

                // Thin leaves transmit a modest, green-biased portion of sunlight when the sun is
                // behind the visible face. Transmission still obeys the main light attenuation so
                // fully occluded leaves do not self-illuminate in deep canopy shade.
                half3 transmission = albedo * _TransmissionColor.rgb * _LightColor0.rgb *
                                     backLighting * _TransmissionStrength * attenuation;

                half smoothness = tex2D(_MetallicGlossMap, i.uv).a * _SmoothnessScale;
                half3 halfDir = normalize(lightDir + viewDir);
                half specPower = lerp(8.0h, 72.0h, smoothness);
                half specularTerm = pow(saturate(dot(normalWS, halfDir)), specPower) * smoothness * 0.16h;
                half3 specular = _LightColor0.rgb * specularTerm * attenuation;

                return fixed4(albedo * ambient + direct + transmission + specular, 1.0h);
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
            #include "UnityCG.cginc"

            struct v2fShadow
            {
                V2F_SHADOW_CASTER;
            };

            v2fShadow vertShadow(appdata_base v)
            {
                v2fShadow o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 fragShadow(v2fShadow i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}

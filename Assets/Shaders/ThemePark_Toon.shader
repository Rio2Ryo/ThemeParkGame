// ============================================================
// ThemeParkGame - Toon/Cel Shader
// テーマパーク向けトゥーンシェーダー
// グラデーション照明 + リムライト + 環境色対応
// WebGL互換
// ============================================================

Shader "ThemeParkGame/Toon"
{
    Properties
    {
        _MainTex ("Albedo Texture", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Color", Color) = (0.6, 0.55, 0.7, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.45
        _ShadowSoftness ("Shadow Softness", Range(0.001, 0.3)) = 0.05
        _RimColor ("Rim Color", Color) = (1, 0.95, 0.85, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.0
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.1
        _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _Glossiness ("Glossiness", Range(1, 256)) = 32
        _SpecThreshold ("Spec Threshold", Range(0, 1)) = 0.5
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _TileScale ("Tile Scale", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            sampler2D _BumpMap;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _ShadowColor;
            half _ShadowThreshold;
            half _ShadowSoftness;
            fixed4 _RimColor;
            half _RimPower;
            half _RimThreshold;
            half _Glossiness;
            half _SpecThreshold;
            fixed4 _EmissionColor;
            half _Metallic;
            float _TileScale;

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
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 tSpace0 : TEXCOORD3;
                float3 tSpace1 : TEXCOORD4;
                float3 tSpace2 : TEXCOORD5;
                UNITY_FOG_COORDS(6)
                SHADOW_COORDS(7)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * _TileScale;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                float3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                float tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                float3 worldBinormal = cross(o.worldNormal, worldTangent) * tangentSign;
                o.tSpace0 = float3(worldTangent.x, worldBinormal.x, o.worldNormal.x);
                o.tSpace1 = float3(worldTangent.y, worldBinormal.y, o.worldNormal.y);
                o.tSpace2 = float3(worldTangent.z, worldBinormal.z, o.worldNormal.z);

                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Normal map
                half3 tnormal = UnpackNormal(tex2D(_BumpMap, i.uv));
                half3 worldNormal;
                worldNormal.x = dot(i.tSpace0, tnormal);
                worldNormal.y = dot(i.tSpace1, tnormal);
                worldNormal.z = dot(i.tSpace2, tnormal);
                worldNormal = normalize(worldNormal);

                // Albedo
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;

                // Lighting
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float NdotL = dot(worldNormal, lightDir);

                // Toon shadow with smooth step
                float shadow = smoothstep(_ShadowThreshold - _ShadowSoftness,
                                         _ShadowThreshold + _ShadowSoftness, NdotL);

                // Shadow attenuation
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
                shadow *= atten;

                // Toon color blend
                fixed3 litColor = albedo.rgb * _LightColor0.rgb;
                fixed3 shadowColor = albedo.rgb * _ShadowColor.rgb;
                fixed3 diffuse = lerp(shadowColor, litColor, shadow);

                // Ambient
                fixed3 ambient = ShadeSH9(float4(worldNormal, 1.0)) * albedo.rgb * 0.5;

                // Specular (toon)
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = dot(worldNormal, halfDir);
                float spec = pow(max(0, NdotH), _Glossiness);
                float toonSpec = smoothstep(_SpecThreshold - 0.01, _SpecThreshold + 0.01, spec);
                fixed3 specular = toonSpec * _LightColor0.rgb * lerp(fixed3(1,1,1), albedo.rgb, _Metallic) * shadow;

                // Rim light
                float NdotV = 1.0 - max(0, dot(worldNormal, viewDir));
                float rim = pow(NdotV, _RimPower);
                rim = smoothstep(_RimThreshold - 0.01, _RimThreshold + 0.01, rim);
                fixed3 rimColor = rim * _RimColor.rgb * shadow;

                // Combine
                fixed3 finalColor = diffuse + ambient + specular + rimColor + _EmissionColor.rgb;

                fixed4 col = fixed4(finalColor, albedo.a);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        // Shadow caster pass
        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f { V2F_SHADOW_CASTER; };

            v2f vert(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}

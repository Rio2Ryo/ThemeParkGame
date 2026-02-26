// ============================================================
// ThemeParkGame - Foliage/Tree Shader
// テーマパーク向け草木シェーダー
// 風揺れアニメーション + 裏面透過 + サブサーフェス近似
// WebGL互換
// ============================================================

Shader "ThemeParkGame/Foliage"
{
    Properties
    {
        _Color ("Leaf Color", Color) = (0.25, 0.65, 0.2, 1)
        _ColorVariation ("Color Variation", Color) = (0.35, 0.75, 0.15, 1)
        _ShadowColor ("Shadow Color", Color) = (0.15, 0.35, 0.12, 1)
        _TranslucencyColor ("Translucency Color", Color) = (0.4, 0.7, 0.1, 1)
        _TranslucencyPower ("Translucency", Range(0, 2)) = 0.5
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.0
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.08
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 150
        Cull Off

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

            fixed4 _Color;
            fixed4 _ColorVariation;
            fixed4 _ShadowColor;
            fixed4 _TranslucencyColor;
            half _TranslucencyPower;
            half _WindSpeed;
            half _WindStrength;
            half _ShadowThreshold;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float colorVar : TEXCOORD3;
                UNITY_FOG_COORDS(4)
                SHADOW_COORDS(5)
            };

            v2f vert(appdata v)
            {
                v2f o;

                // Wind animation based on world position and vertex height
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float windPhase = worldPos.x * 0.5 + worldPos.z * 0.3 + _Time.y * _WindSpeed;
                float windOffset = sin(windPhase) * _WindStrength * v.vertex.y;
                float windOffset2 = cos(windPhase * 1.3 + 0.7) * _WindStrength * 0.5 * v.vertex.y;
                v.vertex.x += windOffset;
                v.vertex.z += windOffset2;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv;

                // Color variation per vertex
                o.colorVar = frac(sin(dot(worldPos.xz, float2(12.9898, 78.233))) * 43758.5453);

                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // Face normal toward camera if back-facing
                float facing = dot(normal, viewDir);
                normal *= sign(facing);

                // Color variation
                fixed4 leafColor = lerp(_Color, _ColorVariation, i.colorVar);

                // Toon-style diffuse
                float NdotL = dot(normal, lightDir);
                float shadow = smoothstep(_ShadowThreshold - 0.05, _ShadowThreshold + 0.05, NdotL);

                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
                shadow *= atten;

                fixed3 litColor = leafColor.rgb * _LightColor0.rgb;
                fixed3 shadowColor = leafColor.rgb * _ShadowColor.rgb;
                fixed3 diffuse = lerp(shadowColor, litColor, shadow);

                // Subsurface scattering approximation (translucency)
                float backLight = max(0, dot(-normal, lightDir));
                fixed3 translucency = _TranslucencyColor.rgb * backLight * _TranslucencyPower;

                // Ambient
                fixed3 ambient = ShadeSH9(float4(normal, 1.0)) * leafColor.rgb * 0.4;

                fixed3 finalColor = diffuse + ambient + translucency;

                fixed4 col = fixed4(finalColor, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            half _WindSpeed;
            half _WindStrength;

            struct v2f { V2F_SHADOW_CASTER; };

            v2f vert(appdata_base v)
            {
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float windPhase = worldPos.x * 0.5 + worldPos.z * 0.3 + _Time.y * _WindSpeed;
                v.vertex.x += sin(windPhase) * _WindStrength * v.vertex.y;
                v.vertex.z += cos(windPhase * 1.3 + 0.7) * _WindStrength * 0.5 * v.vertex.y;

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

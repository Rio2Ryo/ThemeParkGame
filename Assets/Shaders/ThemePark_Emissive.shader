// ============================================================
// ThemeParkGame - Emissive Light Shader
// テーマパーク向けエミッシブ装飾ライト
// 発光色パルスアニメーション + HDRブルーム対応
// WebGL互換
// ============================================================

Shader "ThemeParkGame/Emissive"
{
    Properties
    {
        _Color ("Base Color", Color) = (1, 0.9, 0.3, 1)
        _EmissionColor ("Emission Color", Color) = (1, 0.9, 0.3, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 2.0
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.5
        _PulseMin ("Pulse Min", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _EmissionColor;
            half _EmissionIntensity;
            half _PulseSpeed;
            half _PulseMin;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);

                // Base lighting
                float NdotL = max(0, dot(normal, normalize(_WorldSpaceLightPos0.xyz)));
                fixed3 lit = _Color.rgb * (NdotL * 0.3 + 0.7);

                // Pulsing emission
                float pulse = lerp(_PulseMin, 1.0, (sin(_Time.y * _PulseSpeed) * 0.5 + 0.5));
                fixed3 emission = _EmissionColor.rgb * _EmissionIntensity * pulse;

                fixed4 col = fixed4(lit + emission, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Self-Illumin/Diffuse"
}

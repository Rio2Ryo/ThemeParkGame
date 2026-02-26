// ============================================================
// ThemeParkGame - Stylized Water Shader
// テーマパーク向け噴水・水面シェーダー
// 波紋アニメーション + フレネル反射 + 色グラデーション
// WebGL互換
// ============================================================

Shader "ThemeParkGame/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.3, 0.75, 0.9, 0.7)
        _DeepColor ("Deep Color", Color) = (0.05, 0.25, 0.55, 0.85)
        _FoamColor ("Foam Color", Color) = (0.9, 0.95, 1.0, 0.9)
        _WaveSpeed ("Wave Speed", Range(0.1, 5)) = 1.0
        _WaveScale ("Wave Scale", Range(0.5, 20)) = 4.0
        _WaveHeight ("Wave Height", Range(0, 0.5)) = 0.08
        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 2.0
        _Glossiness ("Glossiness", Range(0, 1)) = 0.9
        _Opacity ("Opacity", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _ShallowColor;
            fixed4 _DeepColor;
            fixed4 _FoamColor;
            half _WaveSpeed;
            half _WaveScale;
            half _WaveHeight;
            half _FresnelPower;
            half _Glossiness;
            half _Opacity;

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
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            // Simple 2D noise
            float hash(float2 p)
            {
                float h = dot(p, float2(127.1, 311.7));
                return frac(sin(h) * 43758.5453);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            v2f vert(appdata v)
            {
                v2f o;

                // Wave displacement
                float2 waveUV = v.uv * _WaveScale;
                float time = _Time.y * _WaveSpeed;
                float wave1 = sin(waveUV.x * 2.0 + time) * 0.5 + 0.5;
                float wave2 = sin(waveUV.y * 1.7 + time * 0.8) * 0.5 + 0.5;
                float wave3 = noise2D(waveUV + float2(time * 0.3, time * 0.2));
                float displacement = (wave1 * 0.3 + wave2 * 0.3 + wave3 * 0.4) * _WaveHeight;

                v.vertex.y += displacement;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 normal = normalize(i.worldNormal);

                // Animated normal perturbation
                float time = _Time.y * _WaveSpeed;
                float2 waveUV = i.uv * _WaveScale;
                float nx = noise2D(waveUV + float2(time * 0.5, 0)) * 2.0 - 1.0;
                float nz = noise2D(waveUV + float2(0, time * 0.4)) * 2.0 - 1.0;
                normal = normalize(normal + float3(nx, 0, nz) * 0.3);

                // Fresnel
                float NdotV = max(0, dot(normal, viewDir));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // Color blend (shallow vs deep based on view angle)
                fixed4 waterColor = lerp(_ShallowColor, _DeepColor, fresnel);

                // Foam pattern
                float foam = noise2D(waveUV * 2.0 + float2(time * 0.15, time * 0.1));
                foam = smoothstep(0.65, 0.75, foam);
                waterColor = lerp(waterColor, _FoamColor, foam * 0.5);

                // Lighting
                float NdotL = max(0, dot(normal, lightDir));
                fixed3 diffuse = waterColor.rgb * (NdotL * 0.6 + 0.4) * _LightColor0.rgb;

                // Specular highlight
                float3 halfDir = normalize(lightDir + viewDir);
                float spec = pow(max(0, dot(normal, halfDir)), _Glossiness * 256.0);
                fixed3 specular = spec * _LightColor0.rgb * 0.8;

                // Ambient
                fixed3 ambient = ShadeSH9(float4(normal, 1.0)) * waterColor.rgb * 0.3;

                fixed3 finalColor = diffuse + ambient + specular;
                float alpha = waterColor.a * _Opacity;

                // Add fresnel reflection boost
                alpha = saturate(alpha + fresnel * 0.3);

                fixed4 col = fixed4(finalColor, alpha);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}

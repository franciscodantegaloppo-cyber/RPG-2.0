Shader "RPG/Fantasy Panorama Blend"
{
    Properties
    {
        _MainTex ("Current Panorama", 2D) = "white" {}
        _BlendTex ("Next Panorama", 2D) = "white" {}
        _Blend ("Transition", Range(0,1)) = 0
        _Rotation ("Cloud Drift", Range(0,360)) = 0
        _Exposure ("Exposure", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BlendTex;
            float _Blend;
            float _Rotation;
            float _Exposure;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; };
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 direction = normalize(i.direction);
                float angle = atan2(direction.z, direction.x) + radians(_Rotation);
                float2 uv = float2(angle * 0.15915494309 + 0.5, asin(direction.y) * 0.31830988618 + 0.5);
                fixed4 panorama = lerp(tex2D(_MainTex, uv), tex2D(_BlendTex, uv), _Blend);
                return panorama * _Exposure;
            }
            ENDHLSL
        }
    }
}

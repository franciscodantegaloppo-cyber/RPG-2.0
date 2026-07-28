Shader "UI/SoftEdgeKingGoblinPortrait"
{
    Properties
    {
        [PerRendererData] _MainTex ("Portrait", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EdgeSoftness ("Edge Softness", Range(0.02, 0.4)) = 0.18
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _EdgeSoftness;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv) * input.color;
                float2 border = min(input.uv, 1.0 - input.uv);
                float rectangularFade = smoothstep(0.0, _EdgeSoftness,
                    min(border.x, border.y));
                float2 centered = (input.uv - 0.5) * float2(1.45, 1.0);
                float vignette = 1.0 - smoothstep(0.68, 1.02, length(centered));
                color.a *= rectangularFade * vignette;
                return color;
            }
            ENDCG
        }
    }
}

Shader "Custom/SelectionIndicator"
{
    Properties
    {
        _Color       ("Color",        Color)             = (0.2, 0.75, 1.0, 1.0)
        _BorderWidth ("Border Width", Range(0.005, 0.1)) = 0.04
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" }
        ZTest Always
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            fixed4 _Color;
            float  _BorderWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                float  d = abs(c.x) + abs(c.y); // L1 → diamond shape

                float inside     = step(d, 0.5);
                float innerEdge  = 0.5 - _BorderWidth;
                float borderMask = inside * step(innerEdge, d);

                clip(borderMask - 0.001);
                return fixed4(_Color.rgb, _Color.a * borderMask);
            }
            ENDCG
        }
    }
}

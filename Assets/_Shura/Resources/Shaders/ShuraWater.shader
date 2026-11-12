// A lake's living surface, drawn over the painted water: slow light bands, glints that
// come and go, and a soft brighter rim at the shore. _MainTex is the lake's shape (alpha).
Shader "Shura/Water"
{
    Properties
    {
        _MainTex ("Lake mask", 2D) = "white" {}
        _Tint ("Light", Color) = (0.75, 0.92, 1, 1)
        _Strength ("Strength", Float) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Tint;
            float _Strength;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; fixed4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.color = v.color;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            fixed4 frag(v2f i) : SV_Target
            {
                float mask = tex2D(_MainTex, i.uv).a;
                if (mask < 0.01) discard;
                float t = _Time.y;
                float2 w = i.world;
                // Long soft bands of light drifting across, crossed by a second set.
                float b1 = sin(w.x * 2.1 + w.y * 0.9 + t * 0.9) * 0.5 + 0.5;
                float b2 = sin(w.x * -1.3 + w.y * 2.6 - t * 0.7) * 0.5 + 0.5;
                float bands = pow(b1 * b2, 3.0);
                // Glints: little cells that flash now and then, pixel-sized.
                float2 cell = floor(w * 8.0);
                float h = hash(cell);
                float glint = step(0.985, h) * pow(saturate(sin(t * (1.5 + h * 3.0) + h * 40.0)), 12.0);
                // The rim: where the mask fades, near the shore.
                float edge = 1.0 - saturate((mask - 0.55) * 4.0);
                float a = (bands * 0.35 + glint * 0.9 + edge * 0.25) * _Strength * mask * i.color.a;
                return fixed4(_Tint.rgb, saturate(a));
            }
            ENDCG
        }
    }
}

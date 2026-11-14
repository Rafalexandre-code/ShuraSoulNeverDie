// A lake's living surface, drawn over the painted water. À noite de lua:
// - o reflexo escuro das copas da margem de cima, pendurado na água e ondulando;
// - o caminho da lua: um rastro de luz partido em traços que tremem;
// - faixas de luz lentas, estrelas e vaga-lumes refletidos que piscam;
// - uma borda mais clara junto à margem.
// _MainTex is the lake's shape (alpha).
Shader "Shura/Water"
{
    Properties
    {
        _MainTex ("Lake mask", 2D) = "white" {}
        _Tint ("Light", Color) = (0.75, 0.92, 1, 1)
        _Strength ("Strength", Float) = 0.5
        _Canopy ("Reflexo das copas", Color) = (0.01, 0.05, 0.08, 1)
        _Center ("Centro do lago (mundo)", Vector) = (0, 0, 0, 0)
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
            fixed4 _Canopy;
            float4 _Center;

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
                // Pixel a pixel (16 por unidade), como o resto do jogo.
                float2 w = floor(i.world * 16.0) / 16.0;
                float wob = sin(w.y * 9.0 + t * 1.6) * 0.06;

                // O reflexo das copas: pinheiros de cabeça para baixo pendurados na margem de cima.
                float fromTop = 1.0 - i.uv.y;
                float col = floor((w.x + wob) * 1.6);
                float inCol = frac((w.x + wob) * 1.6);
                float treeH = 0.22 + 0.22 * hash(float2(col, 3.0));
                float taper = 1.0 - abs(inCol - 0.5) * 2.0;
                float canopy = step(fromTop, treeH * (0.35 + 0.65 * taper)) * smoothstep(0.0, 0.08, fromTop + 0.02);
                float canopyA = canopy * 0.55;

                // O caminho da lua: uma coluna de traços claros que tremem.
                float lane = (w.x - _Center.x - 0.35) + sin(w.y * 3.0 + t * 0.8) * 0.12;
                float laneW = exp(-lane * lane / 0.09);
                float dash = step(0.45, frac(w.y * 5.0 + sin(w.x * 23.0 + t * 2.4) * 0.35 - t * 0.35));
                float moon = laneW * dash * (0.75 + 0.25 * sin(t * 3.0 + w.y * 13.0));

                // Faixas lentas de luz cruzadas.
                float b1 = sin(w.x * 2.1 + w.y * 0.9 + t * 0.9) * 0.5 + 0.5;
                float b2 = sin(w.x * -1.3 + w.y * 2.6 - t * 0.7) * 0.5 + 0.5;
                float bands = pow(b1 * b2, 3.0);
                // Estrelas e vaga-lumes refletidos que piscam.
                float2 cell = floor(i.world * 8.0);
                float h = hash(cell);
                float glint = step(0.982, h) * pow(saturate(sin(t * (1.5 + h * 3.0) + h * 40.0)), 10.0);
                // A borda clara junto da margem.
                float edge = 1.0 - saturate((mask - 0.55) * 4.0);

                float lightA = saturate((bands * 0.22 + moon * 0.75 + glint * 0.95 + edge * 0.3) * _Strength * (1.0 - canopy * 0.7));
                float darkA = canopyA * (1.0 - lightA);
                float a = (lightA + darkA) * mask * i.color.a;
                float3 rgb = (_Tint.rgb * lightA + _Canopy.rgb * darkA) / max(1e-4, lightA + darkA);
                return fixed4(rgb, saturate(a));
            }
            ENDCG
        }
    }
}

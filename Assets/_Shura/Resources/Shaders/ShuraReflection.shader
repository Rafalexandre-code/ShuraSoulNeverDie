// A figure mirrored in still water: the sprite, waving sideways a little more the farther it
// is from the feet, cooler and darker, fading as it goes down.
Shader "Shura/Reflection"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Wave ("Wave (texels)", Float) = 1.2
        _Fade ("Fade", Float) = 1
        _FeetY ("Feet (world y)", Float) = 0
        _Height ("Height (world)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Wave;
            float _Fade;
            float _FeetY;
            float _Height;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; fixed4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // How far below the feet (in the figure's own height): the reflection hangs downward.
                float depth = saturate((_FeetY - i.world.y) / max(0.05, _Height));
                float t = _Time.y;
                // _Wave is in texels, so it reads the same on any sprite sheet.
                float wave = sin(i.world.y * 14.0 + t * 4.0) * _Wave * _MainTex_TexelSize.x * (0.4 + depth * 1.6);
                fixed4 c = tex2D(_MainTex, i.uv + float2(wave, 0));
                c.rgb = lerp(c.rgb, float3(0.35, 0.55, 0.75), 0.35) * 0.8;
                c.a *= i.color.a * saturate(1.15 - depth * 0.9) * _Fade;
                // Thin dark lines of moving water cut through it.
                float line_ = step(0.82, frac(i.world.y * 9.0 - t * 0.6));
                c.a *= 1.0 - line_ * 0.45;
                return c;
            }
            ENDCG
        }
    }
}

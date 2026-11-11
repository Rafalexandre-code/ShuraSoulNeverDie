// Shura's image pass: a soft bloom chain, then a painterly grade (exposure, contrast,
// split toning, saturation and vibrance), a whisper of chromatic aberration at the edges,
// a vignette and a fine film grain. Built-in render pipeline (OnRenderImage).
Shader "Hidden/Shura/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Bloom ("Bloom", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _Bloom;

    float _Threshold;
    float _Knee;
    float _BloomIntensity;
    float4 _BloomTint;
    float _Exposure;
    float _Contrast;
    float _Saturation;
    float _Vibrance;
    float4 _ShadowTint;
    float4 _HighlightTint;
    float _Vignette;
    float _Aberration;
    float _Grain;
    float _GrainSeed;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 Box4(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
                + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25h;
    }

    // Only what is bright blooms; the soft knee keeps the edge of the threshold gentle.
    half3 Prefilter(half3 c)
    {
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Threshold + _Knee, 0.0h, 2.0h * _Knee);
        soft = soft * soft / (4.0h * _Knee + 1e-4h);
        half contrib = max(soft, br - _Threshold) / max(br, 1e-4h);
        return c * contrib;
    }

    half4 FragPrefilter(v2f i) : SV_Target { return half4(Prefilter(Box4(i.uv, 1.0)), 1.0h); }
    half4 FragDown(v2f i) : SV_Target { return half4(Box4(i.uv, 1.0), 1.0h); }
    half4 FragUp(v2f i) : SV_Target { return half4(Box4(i.uv, 0.5), 1.0h); }

    half Luma(half3 c) { return dot(c, half3(0.2126h, 0.7152h, 0.0722h)); }

    half4 FragComposite(v2f i) : SV_Target
    {
        float2 uv = i.uv;
        float2 dc = uv - 0.5;
        float r2 = dot(dc, dc);

        // Chromatic aberration: nothing at the centre, a hair at the corners.
        float ab = _Aberration * r2;
        half3 col;
        col.r = tex2D(_MainTex, uv - dc * ab).r;
        col.g = tex2D(_MainTex, uv).g;
        col.b = tex2D(_MainTex, uv + dc * ab).b;

        // Bloom, tinted like light through mist.
        col += tex2D(_Bloom, uv).rgb * _BloomTint.rgb * _BloomIntensity;

        col *= _Exposure;
        col = (col - 0.5h) * _Contrast + 0.5h;

        // Split toning: cool in the shadows, warm in the lights.
        half l = saturate(Luma(col));
        col += (_ShadowTint.rgb - 0.5h) * _ShadowTint.a * (1.0h - l);
        col += (_HighlightTint.rgb - 0.5h) * _HighlightTint.a * l;

        // Saturation, then vibrance (dull colours gain the most).
        l = Luma(col);
        col = lerp(l.xxx, col, _Saturation);
        half mx = max(col.r, max(col.g, col.b));
        half mn = min(col.r, min(col.g, col.b));
        col = lerp(l.xxx, col, 1.0h + _Vibrance * (1.0h - saturate(mx - mn)));

        // Vignette.
        col *= 1.0h - _Vignette * saturate(r2 * 2.2h);

        // Film grain.
        float n = frac(sin(dot(uv * _ScreenParams.xy + _GrainSeed, float2(12.9898, 78.233))) * 43758.5453);
        col += (n - 0.5) * _Grain;

        return half4(saturate(col), 1.0h);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragPrefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragDown
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragUp
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragComposite
            ENDCG
        }
    }
    Fallback Off
}

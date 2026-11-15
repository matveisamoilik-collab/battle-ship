// Level 7 whirlpool surface (URP, unlit, transparent). Spiral foam bands rotate around the UV
// centre, twisting harder and spinning faster toward the middle; the funnel darkens toward the
// centre and fades out at the rim. _Alpha is driven per-instance by Whirlpool.cs (appear / fade).
Shader "ShipButtlr/Whirlpool"
{
    Properties
    {
        _ColorDeep    ("Deep Colour",    Color) = (0.02, 0.07, 0.16, 1)
        _ColorShallow ("Shallow Colour", Color) = (0.12, 0.42, 0.66, 1)
        _FoamColor    ("Foam Colour",    Color) = (0.90, 0.97, 1.00, 1)
        _Speed        ("Spin Speed",     Float) = 1.2
        _Twist        ("Twist",          Float) = 7
        _Arms         ("Spiral Arms",    Float) = 5
        _Alpha        ("Alpha",          Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorDeep;
                half4 _ColorShallow;
                half4 _FoamColor;
                float _Speed;
                float _Twist;
                float _Arms;
                float _Alpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = saturate(length(p));
                float a = atan2(p.y, p.x);
                float t = _Time.y * _Speed;
                float inner = 1.0 - r;
                // Spiral shape (twist grows toward the centre) rotating rigidly at a constant rate.
                // A radius-dependent angular speed would wind the arms tighter forever (rings).
                float swirl = a + _Twist * inner * inner + t * 2.0;
                float bands = sin(swirl * _Arms) * 0.5 + 0.5;
                // finer streaks that flow inward (scroll in log radius)
                float fine  = sin(swirl * _Arms * 2.0 - log(max(r, 0.02)) * 6.0 + t * 4.0) * 0.5 + 0.5;
                float foam  = smoothstep(0.72, 0.95, bands) * smoothstep(0.05, 0.25, r);
                foam = max(foam, smoothstep(0.85, 1.0, fine) * 0.35 * r);
                half3 col = lerp(_ColorDeep.rgb, _ColorShallow.rgb, smoothstep(0.0, 0.9, r));
                col = lerp(col, _FoamColor.rgb, foam * 0.8);
                float alpha = saturate((1.0 - r) * 5.0) * lerp(0.9, 0.75, r) * _Alpha;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

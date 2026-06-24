Shader "Hidden/MagnetRush/StabInk"
{
    Properties
    {
        _InkLow ("白に振る濃さの下限", Range(0, 1)) = 0.05
        _InkHigh ("黒に振る濃さの上限", Range(0, 1)) = 0.4
        _Strength ("演出の強さ", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "StabInk"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_StabInkMask);
            SAMPLER(sampler_StabInkMask);

            CBUFFER_START(UnityPerMaterial)
                float _InkLow;
                float _InkHigh;
                float _Strength;
            CBUFFER_END

            half4 frag(Varyings input) : SV_Target
            {
                half4 src = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
                // マスク＝効果だけを黒地に描いたもの。濃さ＝アルファと明るさの大きい方（加算系の雷も拾える）。
                half4 m = SAMPLE_TEXTURE2D(_StabInkMask, sampler_StabInkMask, input.texcoord);
                half density = saturate(max(m.a, dot(m.rgb, half3(0.3333, 0.3333, 0.3333))));
                // 薄い所＝白、濃い所＝黒（後ろの景色は使わないので透けない）
                half ink = smoothstep(_InkLow, _InkHigh, density);
                half3 stylized = lerp(half3(1, 1, 1), half3(0, 0, 0), ink);
                // _Strength で通常画面→演出画面へ補間（Timeline クリップ両端でフェード可能）
                half3 col = lerp(src.rgb, stylized, saturate(_Strength));
                return half4(col, src.a);
            }
            ENDHLSL
        }
    }
}

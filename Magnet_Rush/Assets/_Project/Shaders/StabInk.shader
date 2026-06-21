Shader "Hidden/MagnetRush/StabInk"
{
    Properties
    {
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
                float _Strength;
            CBUFFER_END

            half4 frag(Varyings input) : SV_Target
            {
                half4 src = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
                // マスクは「白い背景の上にスタブ効果だけを合成したもの」。後ろの景色は含まないので透けない。
                half3 stylized = SAMPLE_TEXTURE2D(_StabInkMask, sampler_StabInkMask, input.texcoord).rgb;
                // _Strength で通常画面→演出画面へ補間（Timeline クリップ両端でフェード可能）
                half3 col = lerp(src.rgb, stylized, saturate(_Strength));
                return half4(col, src.a);
            }
            ENDHLSL
        }
    }
}

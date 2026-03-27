Shader "MagnetRush/MagnetOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.2, 0.4, 1.0, 1.0)
        _OutlineWidth ("Outline Width", Range(0.001, 0.1)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                // オブジェクト中心からの方向で膨張（ハードエッジでも隙間が出ない）
                float3 dir = normalize(input.positionOS.xyz);
                float3 expandedPos = input.positionOS.xyz + dir * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(expandedPos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}

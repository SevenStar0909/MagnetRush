Shader "MagnetRush/Boss_PerBoneOutlineHull"
{
    Properties
    {
        _NColor ("N Pole Color", Color) = (1.0, 0.25, 0.25, 1.0)
        _SColor ("S Pole Color", Color) = (0.25, 0.5, 1.0, 1.0)
        _OutlineWidth ("Outline Width (object space)", Range(0.001, 0.5)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
        }

        Pass
        {
            Name "BoneOutline"
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // SkinnedMeshRenderer の bones[] 長に対応する固定上限。
            // 余裕を持って 64 まで。ボス本体は 33 ボーン。
            #define MAX_BONES 64

            CBUFFER_START(UnityPerMaterial)
                float4 _NColor;
                float4 _SColor;
                float _OutlineWidth;
            CBUFFER_END

            // MaterialPropertyBlock 経由でランタイム更新する。
            // ボーン index i が N極なら _IsNPerBone[i]=1、S極なら _IsSPerBone[i]=1、None は両方 0。
            float _IsNPerBone[MAX_BONES];
            float _IsSPerBone[MAX_BONES];

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                uint4  boneIdx    : BLENDINDICES;
                float4 boneWt     : BLENDWEIGHTS;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Inverted Hull: スキン済みの法線方向にオブジェクト空間で押し出す。
                // ボスの sharedMesh は SkinnedMeshRenderer が事前スキニング済みで送ってくる。
                float3 expanded = IN.positionOS + normalize(IN.normalOS) * _OutlineWidth;
                OUT.positionCS = TransformObjectToHClip(expanded);

                // dominant bone takes all: weight 最大の bone のみで色を決定する。
                // weight=0 のスロット(影響数<4 の vertex で空きスロットの boneIdx=0 が残っている)を
                // 拾わないように epsilon で初期化し、`>` 厳格比較で同点回避する。
                float bestW = 1e-5;
                uint dominantIdx = 0;
                bool anyValid = false;
                [unroll]
                for (int i = 0; i < 4; ++i)
                {
                    float wi = IN.boneWt[i];
                    if (wi > bestW)
                    {
                        bestW = wi;
                        dominantIdx = IN.boneIdx[i];
                        anyValid = true;
                    }
                }

                float isN = anyValid ? _IsNPerBone[dominantIdx] : 0.0;
                float isS = anyValid ? _IsSPerBone[dominantIdx] : 0.0;
                float total = isN + isS;

                if (total < 0.001)
                {
                    // 非磁化 bone が dominant の vertex は描画しない
                    OUT.color = float4(0, 0, 0, 0);
                }
                else
                {
                    float3 mixed = (_NColor.rgb * isN + _SColor.rgb * isS) / max(total, 0.001);
                    OUT.color = float4(mixed, 1.0);
                }
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                clip(IN.color.a - 0.5);
                return half4(IN.color.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

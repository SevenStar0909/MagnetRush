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

                // 各骨の N/S 強度を BLENDWEIGHT で重み付け blend する
                float4 nv = float4(
                    _IsNPerBone[IN.boneIdx.x],
                    _IsNPerBone[IN.boneIdx.y],
                    _IsNPerBone[IN.boneIdx.z],
                    _IsNPerBone[IN.boneIdx.w]);
                float4 sv = float4(
                    _IsSPerBone[IN.boneIdx.x],
                    _IsSPerBone[IN.boneIdx.y],
                    _IsSPerBone[IN.boneIdx.z],
                    _IsSPerBone[IN.boneIdx.w]);
                float nStr = dot(nv, IN.boneWt);
                float sStr = dot(sv, IN.boneWt);

                float total = nStr + sStr;
                if (total < 0.001)
                {
                    // 非磁化部分は描画しない
                    OUT.color = float4(0, 0, 0, 0);
                }
                else
                {
                    float3 mixed = (_NColor.rgb * nStr + _SColor.rgb * sStr) / max(total, 0.001);
                    OUT.color = float4(mixed, saturate(total));
                }
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 非磁化部分は完全カリング（深度バッファも書き込まない）
                clip(IN.color.a - 0.01);
                return half4(IN.color.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

Shader "Hidden/MagnetRush/EdgeDetectionOutline"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _OutlineColorS ("S Pole Color", Color) = (0.2, 0.4, 1.0, 1.0)
        _OutlineColorN ("N Pole Color", Color) = (1.0, 0.2, 0.2, 1.0)
        _OutlineThickness ("Outline Thickness", Float) = 1.0
        _DepthThreshold ("Depth Threshold", Float) = 0.5
        _NormalThreshold ("Normal Threshold", Float) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "EdgeDetection"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_ViewSpaceNormals);
            SAMPLER(sampler_ViewSpaceNormals);

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColorS;
                float4 _OutlineColorN;
                float _OutlineThickness;
                float _DepthThreshold;
                float _NormalThreshold;
            CBUFFER_END

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                float2 texelSize = _BlitTexture_TexelSize.xy * _OutlineThickness;

                // Roberts Cross: 4つの対角サンプル
                float2 uvBL = uv + float2(-texelSize.x, -texelSize.y);
                float2 uvTR = uv + float2( texelSize.x,  texelSize.y);
                float2 uvBR = uv + float2( texelSize.x, -texelSize.y);
                float2 uvTL = uv + float2(-texelSize.x,  texelSize.y);

                // 法線エッジ検出
                float3 n0 = SAMPLE_TEXTURE2D(_ViewSpaceNormals, sampler_ViewSpaceNormals, uvBL).rgb;
                float3 n1 = SAMPLE_TEXTURE2D(_ViewSpaceNormals, sampler_ViewSpaceNormals, uvTR).rgb;
                float3 n2 = SAMPLE_TEXTURE2D(_ViewSpaceNormals, sampler_ViewSpaceNormals, uvBR).rgb;
                float3 n3 = SAMPLE_TEXTURE2D(_ViewSpaceNormals, sampler_ViewSpaceNormals, uvTL).rgb;

                float3 normalDiff0 = n1 - n0;
                float3 normalDiff1 = n3 - n2;
                float edgeNormal = sqrt(dot(normalDiff0, normalDiff0) + dot(normalDiff1, normalDiff1));

                // 深度エッジ検出
                float d0 = SampleSceneDepth(uvBL);
                float d1 = SampleSceneDepth(uvTR);
                float d2 = SampleSceneDepth(uvBR);
                float d3 = SampleSceneDepth(uvTL);

                float depthDiff0 = d1 - d0;
                float depthDiff1 = d3 - d2;
                float edgeDepth = sqrt(depthDiff0 * depthDiff0 + depthDiff1 * depthDiff1) * 100.0;

                // ペアごとの磁化マスク（BL-TR, BR-TL の各ペアで少なくとも1つが磁化領域）
                float mask0 = step(0.01, length(n0));
                float mask1 = step(0.01, length(n1));
                float mask2 = step(0.01, length(n2));
                float mask3 = step(0.01, length(n3));
                float pairMask0 = max(mask0, mask1);
                float pairMask1 = max(mask2, mask3);

                // 深度エッジ: ペア内に磁化ピクセルがある場合のみ有効
                float maskedDepthEdge = sqrt(
                    depthDiff0 * depthDiff0 * pairMask0 +
                    depthDiff1 * depthDiff1 * pairMask1
                ) * 100.0;

                // 法線エッジ: 非磁化ピクセルは法線=(0,0,0)なので自然にマスクされる
                float edge = max(
                    maskedDepthEdge > _DepthThreshold ? 1.0 : 0.0,
                    edgeNormal > _NormalThreshold ? 1.0 : 0.0
                );

                // 極性色（アルファに極性IDを格納：0.0=N極, 1.0=S極）
                float4 centerNormal = SAMPLE_TEXTURE2D(_ViewSpaceNormals, sampler_ViewSpaceNormals, uv);
                float poleId = max(max(SAMPLE_TEXTURE2D(_ViewSpaceNormals, sampler_ViewSpaceNormals, uvBL).a,
                                       SAMPLE_TEXTURE2D(_ViewSpaceNormals, sampler_ViewSpaceNormals, uvTR).a),
                                   max(SAMPLE_TEXTURE2D(_ViewSpaceNormals, sampler_ViewSpaceNormals, uvBR).a,
                                       SAMPLE_TEXTURE2D(_ViewSpaceNormals, sampler_ViewSpaceNormals, uvTL).a));
                // centerNormalのアルファも考慮
                poleId = max(poleId, centerNormal.a);
                float4 outlineColor = lerp(_OutlineColorN, _OutlineColorS, poleId);

                float finalEdge = edge;

                return lerp(sceneColor, outlineColor, finalEdge);
            }
            ENDHLSL
        }
    }
}

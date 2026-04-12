using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// インポートされたマテリアルがBuilt-in Standardシェーダーの場合、
/// 自動的にURP Litに変換するAssetPostprocessor。
/// </summary>
public class MaterialAutoConverter : AssetPostprocessor
{
    static readonly HashSet<string> k_TargetShaderNames = new HashSet<string>
    {
        "Standard",
        "Standard (Specular setup)"
    };

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        // URPプロジェクトでない場合はスキップ
        if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset)
            return;

        var upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset));
        if (upgraders == null || upgraders.Count == 0) return;

        bool converted = false;

        foreach (string path in importedAssets)
        {
            if (!path.EndsWith(".mat")) continue;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;
            if (!k_TargetShaderNames.Contains(mat.shader.name)) continue;

            string message = string.Empty;
            if (MaterialUpgrader.Upgrade(mat, upgraders, MaterialUpgrader.UpgradeFlags.None, ref message))
            {
                EditorUtility.SetDirty(mat);
                Debug.Log($"[MaterialAutoConverter] URP Litに変換: {path}");
                converted = true;
            }
            else
            {
                Debug.LogWarning($"[MaterialAutoConverter] 変換失敗: {path} - {message}");
            }
        }

        if (converted)
            AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// プロジェクト内の全Built-in Standardマテリアルを一括変換する。
    /// </summary>
    [MenuItem("Tools/マテリアル自動変換 (Built-in → URP)")]
    static void ConvertAllInProject()
    {
        var upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset));
        if (upgraders == null || upgraders.Count == 0)
        {
            Debug.LogWarning("[MaterialAutoConverter] URP用Upgraderが見つかりません");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material");
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;
            if (!k_TargetShaderNames.Contains(mat.shader.name)) continue;

            string message = string.Empty;
            if (MaterialUpgrader.Upgrade(mat, upgraders, MaterialUpgrader.UpgradeFlags.None, ref message))
            {
                EditorUtility.SetDirty(mat);
                Debug.Log($"[MaterialAutoConverter] URP Litに変換: {path}");
                count++;
            }
            else
            {
                Debug.LogWarning($"[MaterialAutoConverter] 変換失敗: {path} - {message}");
            }
        }

        if (count > 0)
            AssetDatabase.SaveAssets();

        Debug.Log($"[MaterialAutoConverter] 完了: {count}個のマテリアルを変換しました");
    }
}

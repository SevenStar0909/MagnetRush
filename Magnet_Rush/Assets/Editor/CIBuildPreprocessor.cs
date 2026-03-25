// CI環境でビルド前にURP設定を自動アップグレードする
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class CIBuildPreprocessor : IPreprocessBuildWithReport
{
    // URPPreprocessBuildより先に実行（URPはint.MinValue + 100）
    public int callbackOrder => int.MinValue;

    public void OnPreprocessBuild(BuildReport report)
    {
        // URP GlobalSettings を強制再シリアライズしてバージョンを最新化
        var guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineGlobalSettings");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.ForceReserializeAssets(new[] { path });
            Debug.Log($"[CIBuildPreprocessor] ForceReserialize: {path}");
        }

        // RP Assets も再シリアライズ
        guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.ForceReserializeAssets(new[] { path });
        }
    }
}
#endif

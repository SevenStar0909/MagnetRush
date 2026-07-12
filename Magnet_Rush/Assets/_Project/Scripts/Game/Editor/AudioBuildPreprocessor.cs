using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// ビルド時に CRI の音声ファイル（acf / acb / awb）を StreamingAssets 直下へ自動コピーする。
/// SoundManager はビルドでは StreamingAssets 直下の相対パスで読む設計のため、これが無いとビルドが無音になる。
/// ビルド成功後にコピーは削除する（Audio フォルダを単一の真実に保つ。ビルド失敗時の残骸は .gitignore で無視）。
/// </summary>
public sealed class AudioBuildPreprocessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string k_AudioRoot = "Assets/_Project/Asset/Audio";
    private const string k_AcfPath = k_AudioRoot + "/MagnetRush.acf";
    private const string k_StreamingAssetsRoot = "Assets/StreamingAssets";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        List<string> sources = CollectAudioFiles();

        if (!AssetDatabase.IsValidFolder(k_StreamingAssetsRoot))
            AssetDatabase.CreateFolder("Assets", "StreamingAssets");

        foreach (string source in sources)
        {
            string destination = $"{k_StreamingAssetsRoot}/{Path.GetFileName(source)}";
            File.Copy(source, destination, overwrite: true);
            Debug.Log($"[AudioBuildPreprocessor] コピー: {source} -> {destination}");
        }

        AssetDatabase.Refresh();
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (!AssetDatabase.IsValidFolder(k_StreamingAssetsRoot)) return;

        foreach (string source in CollectAudioFiles())
            AssetDatabase.DeleteAsset($"{k_StreamingAssetsRoot}/{Path.GetFileName(source)}");

        // 他用途のファイルが入っていない限りフォルダごと片付ける
        bool isEmpty = Directory.GetFileSystemEntries(k_StreamingAssetsRoot).Length == 0;
        if (isEmpty) AssetDatabase.DeleteAsset(k_StreamingAssetsRoot);

        AssetDatabase.Refresh();
    }

    /// <summary>
    /// コピー対象の音声ファイル一覧を返す。ACF またはキューシートが1枚も無い場合は
    /// 無音ビルドを防ぐためビルド自体を失敗させる。
    /// </summary>
    private static List<string> CollectAudioFiles()
    {
        if (!File.Exists(k_AcfPath))
            throw new BuildFailedException($"[AudioBuildPreprocessor] ACF が見つかりません: {k_AcfPath}（このままビルドすると無音になるため中断）");

        var sources = new List<string> { k_AcfPath };

        // SoundManager は StreamingAssets 直下のフラットな "<キューシート名>.acb" を読むため、
        // サブフォルダ構成は保たず全 acb / awb をフラットに集める
        foreach (string guid in AssetDatabase.FindAssets("", new[] { k_AudioRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".acb" && extension != ".awb") continue;
            sources.Add(path);
        }

        if (sources.Count == 1)
            throw new BuildFailedException($"[AudioBuildPreprocessor] acb が1枚も見つかりません: {k_AudioRoot}（このままビルドすると無音になるため中断）");

        return sources;
    }
}

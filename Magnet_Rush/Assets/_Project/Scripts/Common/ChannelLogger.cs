using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// チャンネルベースのログシステム。サブシステムごとにログをフィルタリングできる。
/// 使用例: ChannelLogger.Log("Magnet", "磁場を適用");
/// Editor + Development Buildのみ出力。リリースビルドではゼロコスト。
/// </summary>
public static class ChannelLogger
{
    static readonly Dictionary<string, bool> s_channelEnabled = new Dictionary<string, bool>();
    static bool s_globalEnabled = true;

    // チャンネルごとの色（Consoleで識別しやすくする）
    static readonly Dictionary<string, string> s_channelColors = new Dictionary<string, string>
    {
        { "Player", "#4CAF50" },
        { "Magnet", "#2196F3" },
        { "Bullet", "#FF9800" },
        { "Enemy",  "#F44336" },
        { "Entity", "#9C27B0" },
        { "UI",     "#00BCD4" },
        { "Game",   "#FFC107" }
    };

    /// <summary>ログ出力。リリースビルドでは呼び出し自体が除去される。</summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG")]
    public static void Log(string channel, string message)
    {
        if (!s_globalEnabled) return;
        if (s_channelEnabled.TryGetValue(channel, out bool enabled) && !enabled) return;

        string color = s_channelColors.TryGetValue(channel, out string c) ? c : "#FFFFFF";
        Debug.Log($"<color={color}>[{channel}]</color> {message}");
    }

    /// <summary>警告ログ。</summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG")]
    public static void LogWarning(string channel, string message)
    {
        if (!s_globalEnabled) return;
        if (s_channelEnabled.TryGetValue(channel, out bool enabled) && !enabled) return;

        string color = s_channelColors.TryGetValue(channel, out string c) ? c : "#FFFFFF";
        Debug.LogWarning($"<color={color}>[{channel}]</color> {message}");
    }

    /// <summary>エラーログ。チャンネルフィルタに関係なく常に出力。</summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG")]
    public static void LogError(string channel, string message)
    {
        string color = s_channelColors.TryGetValue(channel, out string c) ? c : "#FFFFFF";
        Debug.LogError($"<color={color}>[{channel}]</color> {message}");
    }

    /// <summary>チャンネルの有効/無効を切り替える。</summary>
    public static void SetChannelEnabled(string channel, bool enabled)
    {
        s_channelEnabled[channel] = enabled;
    }

    /// <summary>全ログの有効/無効を切り替える。</summary>
    public static void SetGlobalEnabled(bool enabled)
    {
        s_globalEnabled = enabled;
    }

    /// <summary>チャンネルの色を設定する。</summary>
    public static void SetChannelColor(string channel, string hexColor)
    {
        s_channelColors[channel] = hexColor;
    }

    /// <summary>登録されている全チャンネル名を返す。</summary>
    public static IEnumerable<string> GetChannels()
    {
        return s_channelColors.Keys;
    }

    /// <summary>チャンネルが有効かどうかを返す。</summary>
    public static bool IsChannelEnabled(string channel)
    {
        if (s_channelEnabled.TryGetValue(channel, out bool enabled))
            return enabled;
        return true;
    }
}

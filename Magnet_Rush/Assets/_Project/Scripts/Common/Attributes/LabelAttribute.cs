using UnityEngine;

/// <summary>
/// Inspector に表示するラベルを差し替える属性。フィールド名は命名規則（英語 m_camelCase / camelCase）を維持しつつ、
/// 表示だけ日本語化したい場合に使う。Tooltip 属性と併用可。
/// 例: [Label("最大エイム時間（秒）")] [Tooltip("...")] public float aimMaxDuration;
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class LabelAttribute : PropertyAttribute
{
    public string Text { get; }

    public LabelAttribute(string text)
    {
        Text = text;
    }
}

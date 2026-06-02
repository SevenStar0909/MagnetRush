using System;

/// <summary>
/// ScriptableObject 型の表示名を日本語化する属性。プランナー用調整シート（SOBrowserWindow）の型選択ドロップダウン等で参照する。
/// 命名規則（クラス名は PascalCase の英語）を維持しつつ、表示だけ日本語化したい場合に使う。
/// 例: [ClassLabelSO("プレイヤー設定")] public class PlayerSettings : ScriptableObject { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ClassLabelSOAttribute : Attribute
{
    public string Text { get; }

    public ClassLabelSOAttribute(string text)
    {
        Text = text;
    }
}

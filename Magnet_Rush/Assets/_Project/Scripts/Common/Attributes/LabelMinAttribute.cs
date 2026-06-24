using UnityEngine;

/// <summary>
/// 日本語ラベル + 最小値クランプ（Min）を両立する属性。Label と Min の合体版。
/// Unity の制約で 1 フィールドに PropertyDrawer は 1 つしか効かないため、Min と Label を併用したい場合はこちらを使う。
/// 例: [LabelMin("揺れの大きさ（m）", 0f)] public float amplitude;
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class LabelMinAttribute : PropertyAttribute
{
    public string Text { get; }
    public float Min { get; }

    public LabelMinAttribute(string text, float min)
    {
        Text = text;
        Min = min;
    }
}

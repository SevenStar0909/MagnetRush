using UnityEngine;

/// <summary>
/// 日本語ラベル + Range スライダーを両立する属性。Label と Range の合体版。
/// Unity の制約で 1 フィールドに PropertyDrawer は 1 つしか効かないため、Range と Label を併用したい場合はこちらを使う。
/// 例: [LabelRange("磁力場速度低下率", 0f, 1f)] public float magnetSpeedDamping;
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class LabelRangeAttribute : PropertyAttribute
{
    public string Text { get; }
    public float Min { get; }
    public float Max { get; }

    public LabelRangeAttribute(string text, float min, float max)
    {
        Text = text;
        Min = min;
        Max = max;
    }
}

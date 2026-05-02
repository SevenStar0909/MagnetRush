using UnityEditor;
using UnityEngine;

/// <summary>
/// LabelRangeAttribute 用 PropertyDrawer。日本語ラベル + Range スライダーを描画する。
/// float/int に対応。それ以外の型は警告を表示する。
/// </summary>
[CustomPropertyDrawer(typeof(LabelRangeAttribute))]
public sealed class LabelRangeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = (LabelRangeAttribute)attribute;
        label.text = attr.Text;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Float:
                EditorGUI.Slider(position, property, attr.Min, attr.Max, label);
                break;
            case SerializedPropertyType.Integer:
                EditorGUI.IntSlider(position, property, (int)attr.Min, (int)attr.Max, label);
                break;
            default:
                EditorGUI.LabelField(position, label.text, "LabelRange は float または int でのみ使用可");
                break;
        }
    }
}

using UnityEditor;
using UnityEngine;

/// <summary>
/// LabelMinAttribute 用 PropertyDrawer。日本語ラベル + 最小値クランプを描画する。
/// float/int に対応。それ以外の型は警告を表示する。
/// </summary>
[CustomPropertyDrawer(typeof(LabelMinAttribute))]
public sealed class LabelMinDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = (LabelMinAttribute)attribute;
        label.text = attr.Text;

        if (property.propertyType == SerializedPropertyType.Float)
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUI.FloatField(position, label, property.floatValue);
            if (EditorGUI.EndChangeCheck()) property.floatValue = Mathf.Max(attr.Min, value);
        }
        else if (property.propertyType == SerializedPropertyType.Integer)
        {
            EditorGUI.BeginChangeCheck();
            int value = EditorGUI.IntField(position, label, property.intValue);
            if (EditorGUI.EndChangeCheck()) property.intValue = Mathf.Max((int)attr.Min, value);
        }
        else
        {
            EditorGUI.LabelField(position, label.text, "LabelMin は float または int でのみ使用可");
        }
    }
}

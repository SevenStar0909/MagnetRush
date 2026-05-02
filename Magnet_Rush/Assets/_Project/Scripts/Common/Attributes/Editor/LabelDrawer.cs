using UnityEditor;
using UnityEngine;

/// <summary>
/// LabelAttribute 用の PropertyDrawer。フィールドの表示名を日本語ラベルに置き換える。
/// Tooltip 属性が付いている場合は label.tooltip が Unity 側で自動付与されているのでそのまま維持される。
/// 制約: [Range] のように独自 PropertyDrawer を持つ属性とはスタックできない（Unity の制限。1 フィールドにつき PropertyDrawer は 1 つ）
/// </summary>
[CustomPropertyDrawer(typeof(LabelAttribute))]
public sealed class LabelDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = (LabelAttribute)attribute;
        // text のみ差し替え。tooltip は Unity が Tooltip 属性から自動セットするためそのまま使う
        label.text = attr.Text;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}

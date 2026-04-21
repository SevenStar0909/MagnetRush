using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// ClassTypeName 属性のカスタム描画。指定型のサブクラスをドロップダウン表示する。
/// 保存値は AssemblyQualifiedName（asmdef 境界越えの Type.GetType に対応）。
/// </summary>
[CustomPropertyDrawer(typeof(ClassTypeName))]
public class ClassTypeNameDrawer : PropertyDrawer
{
    private ClassTypeName m_classTypeName;
    private List<string> m_names;
    private List<string> m_formatedNames;
    private bool m_initialized = false;

    private void Initialize()
    {
        m_classTypeName = (ClassTypeName)attribute;

        var classes = System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsSubclassOf(m_classTypeName.type) && !t.IsAbstract)
            .ToList();

        m_names = classes.Select(t => t.AssemblyQualifiedName).ToList();
        m_formatedNames = classes
            .Select(t => t.Name)
            .Select(n => Regex.Replace(n, "(\\B[A-Z])", " $1"))
            .ToList();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!m_initialized)
        {
            m_initialized = true;
            Initialize();
        }

        if (m_names.Count == 0)
        {
            EditorGUI.LabelField(position, label.text, "(no subclass found)");
            return;
        }

        if (string.IsNullOrEmpty(property.stringValue))
            property.stringValue = m_names[0];

        if (!m_names.Contains(property.stringValue))
        {
            EditorGUI.LabelField(position, label.text, $"(missing: {property.stringValue})");
            return;
        }

        var current = m_names.IndexOf(property.stringValue);
        position = EditorGUI.PrefixLabel(position, label);
        var selected = EditorGUI.Popup(position, current, m_formatedNames.ToArray());
        property.stringValue = m_names[selected];
    }
}

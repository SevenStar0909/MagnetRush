using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Player の Inspector を拡張し、同じ GameObject 上の Ability 派生コンポーネントを Player のセクション内に inline 表示する。
/// 個別の Ability コンポーネントは AbilityHiddenEditor で空表示にされ、編集動線が Player に集約される。
/// foldout 状態は型単位でセッション内に記憶される。
/// </summary>
[CustomEditor(typeof(Player))]
public class PlayerEditor : Editor
{
    private static readonly Dictionary<Type, bool> s_foldouts = new Dictionary<Type, bool>();

    public override void OnInspectorGUI()
    {
        // Player 自身のフィールドを通常通り描画
        DrawDefaultInspector();

        var player = (Player)target;
        var abilities = player.GetComponents<Ability>();
        if (abilities == null || abilities.Length == 0) return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField($"Abilities ({abilities.Length})", EditorStyles.boldLabel);

        foreach (var ability in abilities)
        {
            if (ability == null) continue;

            var type = ability.GetType();
            if (!s_foldouts.TryGetValue(type, out bool expanded)) expanded = true;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            expanded = EditorGUILayout.Foldout(expanded, type.Name, true, EditorStyles.foldoutHeader);
            s_foldouts[type] = expanded;

            if (expanded)
            {
                using var so = new SerializedObject(ability);
                so.Update();
                var prop = so.GetIterator();
                // m_Script を skip
                if (prop.NextVisible(true))
                {
                    while (prop.NextVisible(false))
                        EditorGUILayout.PropertyField(prop, true);
                }
                so.ApplyModifiedProperties();
            }
            EditorGUILayout.EndVertical();
        }
    }
}

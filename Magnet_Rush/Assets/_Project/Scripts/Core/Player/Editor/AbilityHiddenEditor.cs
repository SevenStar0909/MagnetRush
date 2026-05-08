using UnityEditor;
using UnityEngine;

/// <summary>
/// Ability 派生クラスの個別 Inspector を空表示に置き換える。
/// 編集動線は Player の Inspector（PlayerEditor 経由）に集約される。
/// editorForChildClasses: true により Ability のあらゆる派生（AimAbility / ShootingAbility / PoleAbility 等）に適用される。
/// </summary>
[CustomEditor(typeof(Ability), editorForChildClasses: true)]
public class AbilityHiddenEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "この Ability は Player コンポーネントから編集してください。",
            MessageType.None);
    }
}

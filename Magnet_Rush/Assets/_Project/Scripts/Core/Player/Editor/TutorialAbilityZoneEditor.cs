using UnityEditor;
using UnityEngine;

/// <summary>
/// TutorialAbilityZone の Inspector を日本語化する。
/// 項目ラベルを日本語にし、選択中のアクションが何をするかの説明文を下に表示する。
/// アビリティ欄は「1機能を解除 / 1機能をロック」のときだけ編集できる（全ロック/全解除では使われないため）。
/// </summary>
[CustomEditor(typeof(TutorialAbilityZone))]
[CanEditMultipleObjects]
public class TutorialAbilityZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var actionProp = serializedObject.FindProperty("m_action");
        var abilityProp = serializedObject.FindProperty("m_ability");
        var oneShotProp = serializedObject.FindProperty("m_oneShot");

        EditorGUILayout.PropertyField(actionProp, new GUIContent("アクション", "プレイヤーがこのゾーンに入ったときに何をするか"));

        var action = (TutorialAbilityZone.ZoneAction)actionProp.enumValueIndex;
        bool usesAbility = action == TutorialAbilityZone.ZoneAction.Unlock
            || action == TutorialAbilityZone.ZoneAction.Lock;

        using (new EditorGUI.DisabledScope(!usesAbility))
        {
            EditorGUILayout.PropertyField(abilityProp, new GUIContent("アビリティ（対象の機能）",
                "「1機能を解除 / 1機能をロック」のときに対象にする機能。全ロック/全解除では使わない"));
        }

        EditorGUILayout.PropertyField(oneShotProp, new GUIContent("一度だけ発動",
            "ON: 最初に触れた1回だけ発動して以降は反応しない（通常はこれ）。OFF: 出て入り直すたびに毎回発動"));

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(BuildDescription(action, abilityProp), MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    private static string BuildDescription(TutorialAbilityZone.ZoneAction action, SerializedProperty abilityProp)
    {
        string abilityName = abilityProp.enumDisplayNames[abilityProp.enumValueIndex];
        switch (action)
        {
            case TutorialAbilityZone.ZoneAction.LockAll:
                return "プレイヤーが触れたら、移動とカメラ以外の全機能を使えなくします。\nチュートリアルの入口に置く用です。";
            case TutorialAbilityZone.ZoneAction.Unlock:
                return $"プレイヤーが触れたら「{abilityName}」だけ使えるようになります。\n機能を教える場所の手前に置く用です。";
            case TutorialAbilityZone.ZoneAction.Lock:
                return $"プレイヤーが触れたら「{abilityName}」だけ使えなくします。\n一度解禁した機能をまた禁止したいとき用です。";
            case TutorialAbilityZone.ZoneAction.UnlockAll:
                return "プレイヤーが触れたら、全機能を使えるようにします。\nチュートリアルの終了地点に置く用です。";
            default:
                return "";
        }
    }
}

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SoundCueClip))]
[CanEditMultipleObjects]
public class SoundCueClipEditor : Editor
{
    private SerializedProperty m_cueName;
    private SerializedProperty m_cueSheet;
    private SerializedProperty m_volumeCurve;
    private SerializedProperty m_pitchCurve;
    private SerializedProperty m_playbackSpeed;
    private SerializedProperty m_speedCurve;
    private SerializedProperty m_fadeInDuration;
    private SerializedProperty m_fadeOutDuration;

    private void OnEnable()
    {
        m_cueName = serializedObject.FindProperty("cueName");
        m_cueSheet = serializedObject.FindProperty("cueSheet");
        m_volumeCurve = serializedObject.FindProperty("volumeCurve");
        m_pitchCurve = serializedObject.FindProperty("pitchCurve");
        m_playbackSpeed = serializedObject.FindProperty("playbackSpeed");
        m_speedCurve = serializedObject.FindProperty("speedCurve");
        m_fadeInDuration = serializedObject.FindProperty("fadeInDuration");
        m_fadeOutDuration = serializedObject.FindProperty("fadeOutDuration");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Sound Cue", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_cueName, new GUIContent("Cue Name", "鳴らす効果音のキュー名"));
        EditorGUILayout.PropertyField(m_cueSheet, new GUIContent("Cue Sheet", "キューシート名"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Timeline Curves", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            m_volumeCurve,
            new GUIContent("Volume Curve", "横軸 0=バー開始 / 1=バー終了、縦軸 1=通常音量"));
        EditorGUILayout.PropertyField(
            m_pitchCurve,
            new GUIContent("Pitch Curve", "横軸 0=バー開始 / 1=バー終了、縦軸 0=通常ピッチ"));
        EditorGUILayout.PropertyField(
            m_playbackSpeed,
            new GUIContent("Speed Multiplier", "SEの再生速度倍率。2で倍速、0.5で半速"));
        EditorGUILayout.PropertyField(
            m_speedCurve,
            new GUIContent("Speed Curve", "横軸 0=バー開始 / 1=バー終了、縦軸 1=通常速度"));
        EditorGUILayout.PropertyField(
            m_fadeInDuration,
            new GUIContent("Fade In", "バー開始時の自動フェードイン秒数"));
        EditorGUILayout.PropertyField(
            m_fadeOutDuration,
            new GUIContent("Fade Out", "バー終了時の自動フェードアウト秒数"));

        serializedObject.ApplyModifiedProperties();
    }
}

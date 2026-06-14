using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(EnemyBossArenaGate))]
public class EnemyBossArenaGateEditor : Editor
{
    private readonly BoxBoundsHandle m_boundsHandle = new();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.HelpBox(
            "Scene View の白いハンドルで、このインスタンスだけのボスアリーナ範囲を変更できます。実際の壁は BoxCollider の X/Z サイズに合わせた12角形で生成されます。",
            MessageType.Info);
    }

    private void OnSceneGUI()
    {
        EnemyBossArenaGate gate = (EnemyBossArenaGate)target;
        BoxCollider areaBox = GetAreaBox(gate);
        if (areaBox == null)
            return;

        Transform gateTransform = gate.transform;

        using (new Handles.DrawingScope(gateTransform.localToWorldMatrix))
        {
            m_boundsHandle.center = areaBox.center;
            m_boundsHandle.size = areaBox.size;

            EditorGUI.BeginChangeCheck();
            m_boundsHandle.DrawHandle();
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(areaBox, "Resize Boss Arena Gate");

            Vector3 size = m_boundsHandle.size;
            size.x = Mathf.Max(0.1f, Mathf.Abs(size.x));
            size.y = Mathf.Max(0.1f, Mathf.Abs(size.y));
            size.z = Mathf.Max(0.1f, Mathf.Abs(size.z));

            areaBox.center = m_boundsHandle.center;
            areaBox.size = size;

            EditorUtility.SetDirty(areaBox);
            PrefabUtility.RecordPrefabInstancePropertyModifications(areaBox);
        }
    }

    private BoxCollider GetAreaBox(EnemyBossArenaGate gate)
    {
        serializedObject.Update();
        SerializedProperty areaBoxProperty = serializedObject.FindProperty("m_areaBox");

        BoxCollider areaBox = areaBoxProperty != null
            ? areaBoxProperty.objectReferenceValue as BoxCollider
            : null;

        if (areaBox == null)
            areaBox = gate.GetComponent<BoxCollider>();

        return areaBox;
    }
}

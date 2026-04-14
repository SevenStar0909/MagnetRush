using UnityEngine;

/// <summary>
/// Sceneビューで全ColliderをCollider=緑/Trigger=青で色分け描画 + レイヤー名ラベル表示。
/// デバッグビルド/エディタでのみ動作。削除時はこのファイルを消すだけ。
/// </summary>
public class DebugCollisionGizmos : MonoBehaviour
{
#if UNITY_EDITOR
    private const string k_ShowLabelsPrefKey = "DebugCollisionGizmos.ShowLabels";
    private const string k_ShowGizmosPrefKey = "DebugCollisionGizmos.ShowGizmos";
    [SerializeField] private float m_labelOffsetY = 0.3f;
    [SerializeField] private float m_labelLineHeight = 0.3f;

    private readonly System.Collections.Generic.List<Vector3> m_drawnLabels = new();

    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterSceneGui()
    {
        UnityEditor.SceneView.duringSceneGui -= OnSceneGui;
        UnityEditor.SceneView.duringSceneGui += OnSceneGui;
    }

    private static void OnSceneGui(UnityEditor.SceneView sceneView)
    {
        UnityEditor.Handles.BeginGUI();
        const float width = 160f;
        const float height = 22f;
        const float marginRight = 30f;
        const float marginBottom = 60f;
        const float spacing = 4f;
        var viewSize = sceneView.position.size;

        bool labelsOn = UnityEditor.EditorPrefs.GetBool(k_ShowLabelsPrefKey, false);
        var labelsRect = new Rect(viewSize.x - width - marginRight, viewSize.y - height - marginBottom, width, height);
        if (GUI.Button(labelsRect, labelsOn ? "Layer Labels: ON" : "Layer Labels: OFF"))
        {
            UnityEditor.EditorPrefs.SetBool(k_ShowLabelsPrefKey, !labelsOn);
            UnityEditor.SceneView.RepaintAll();
        }

        bool gizmosOn = UnityEditor.EditorPrefs.GetBool(k_ShowGizmosPrefKey, false);
        var gizmosRect = new Rect(labelsRect.x, labelsRect.y - height - spacing, width, height);
        if (GUI.Button(gizmosRect, gizmosOn ? "Collider Gizmos: ON" : "Collider Gizmos: OFF"))
        {
            UnityEditor.EditorPrefs.SetBool(k_ShowGizmosPrefKey, !gizmosOn);
            UnityEditor.SceneView.RepaintAll();
        }

        UnityEditor.Handles.EndGUI();
    }

    void OnDrawGizmos()
    {
        bool showGizmos = UnityEditor.EditorPrefs.GetBool(k_ShowGizmosPrefKey, false);
        bool showLabels = UnityEditor.EditorPrefs.GetBool(k_ShowLabelsPrefKey, false);
        if (!showGizmos && !showLabels) return;

        var colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        m_drawnLabels.Clear();

        foreach (var col in colliders)
        {
            if (col == null) continue;

            if (showGizmos)
            {
                // Trigger=青、Collider=緑
                Gizmos.color = col.isTrigger
                    ? new Color(0.2f, 0.4f, 1f, 0.3f)
                    : new Color(0.2f, 1f, 0.3f, 0.3f);

                DrawColliderGizmo(col);
            }

            if (showLabels)
            {
                string layerName = LayerMask.LayerToName(col.gameObject.layer);
                if (!string.IsNullOrEmpty(layerName) && col.gameObject.layer >= 6)
                {
                    string label = col.isTrigger ? $"[T] {layerName}" : $"[C] {layerName}";
                    Vector3 labelPos = col.bounds.center + Vector3.up * (col.bounds.extents.y + m_labelOffsetY);

                    // 既に描画したラベルと重なっていたら1行分上にずらす（重なりが解消するまで繰り返す）
                    while (HasOverlap(labelPos))
                        labelPos += Vector3.up * m_labelLineHeight;

                    m_drawnLabels.Add(labelPos);

                    // Trigger=青、Collider=緑のアウトライン
                    Color outline = col.isTrigger
                        ? new Color(0.1f, 0.3f, 1f, 1f)
                        : new Color(0.1f, 0.7f, 0.2f, 1f);
                    DrawLabelWithOutline(labelPos, label, outline, Color.white);
                }
            }
        }
    }

    void DrawLabelWithOutline(Vector3 worldPos, string text, Color outline, Color fill)
    {
        var cam = UnityEditor.SceneView.currentDrawingSceneView != null
            ? UnityEditor.SceneView.currentDrawingSceneView.camera
            : Camera.current;
        if (cam == null) return;

        // 背面の点はスキップ
        Vector3 viewPos = cam.WorldToViewportPoint(worldPos);
        if (viewPos.z < 0f) return;

        var content = new GUIContent(text);
        var style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        Vector2 size = style.CalcSize(content);
        Vector2 screenPos = UnityEditor.HandleUtility.WorldToGUIPoint(worldPos);
        Rect rect = new Rect(screenPos.x - size.x * 0.5f, screenPos.y - size.y * 0.5f, size.x, size.y);

        UnityEditor.Handles.BeginGUI();

        // 8方向にオフセットしてアウトライン描画
        style.normal.textColor = outline;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                GUI.Label(new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height), content, style);
            }
        }

        // 中央に塗りを描画
        style.normal.textColor = fill;
        GUI.Label(rect, content, style);

        UnityEditor.Handles.EndGUI();
    }

    bool HasOverlap(Vector3 pos)
    {
        float threshold = m_labelLineHeight * 0.9f;
        for (int i = 0; i < m_drawnLabels.Count; i++)
        {
            if (Vector3.Distance(m_drawnLabels[i], pos) < threshold)
                return true;
        }
        return false;
    }

    void DrawColliderGizmo(Collider col)
    {
        if (col is BoxCollider box)
        {
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (col is SphereCollider sphere)
        {
            float scale = Mathf.Max(sphere.transform.lossyScale.x, sphere.transform.lossyScale.y, sphere.transform.lossyScale.z);
            Gizmos.DrawWireSphere(sphere.transform.TransformPoint(sphere.center), sphere.radius * scale);
        }
        else if (col is CapsuleCollider capsule)
        {
            // カプセルはワイヤースフィアで簡易表示
            float scale = Mathf.Max(capsule.transform.lossyScale.x, capsule.transform.lossyScale.z);
            Vector3 center = capsule.transform.TransformPoint(capsule.center);
            Gizmos.DrawWireSphere(center, capsule.radius * scale);
        }
    }
#endif
}

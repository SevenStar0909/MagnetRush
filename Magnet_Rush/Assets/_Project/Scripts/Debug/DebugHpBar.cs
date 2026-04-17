using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// デバッグ用HPバー。OnGUIで全Health持ちの頭上にHPバーを描画する。
/// Canvas不要、このスクリプト1つで完結。削除時はこのファイルを消すだけ。
/// </summary>
public class DebugHpBar : MonoBehaviour
{
    [SerializeField] private float m_extraHeight = 0.3f;
    [SerializeField] private float m_barWidth = 60f;
    [SerializeField] private float m_barHeight = 8f;
    [SerializeField] private float m_scanInterval = 2f;

    private struct Target
    {
        public Health health;
        public float topY;
    }

    private List<Target> m_targets = new List<Target>();
    private float m_nextScanTime;
    private Texture2D m_whiteTex;

    void Start()
    {
        if (!Debug.isDebugBuild && !Application.isEditor)
        {
            Destroy(this);
            return;
        }

        m_whiteTex = new Texture2D(1, 1);
        m_whiteTex.SetPixel(0, 0, Color.white);
        m_whiteTex.Apply();
    }

    void Update()
    {
        if (Time.time >= m_nextScanTime)
        {
            m_nextScanTime = Time.time + m_scanInterval;
            ScanTargets();
        }
    }

    void ScanTargets()
    {
        m_targets.Clear();

        // 全Health持ちを対象にする（プレイヤー + エネミー + タレット等）
        var allHealth = FindObjectsByType<Health>(FindObjectsSortMode.None);
        foreach (var health in allHealth)
        {
            if (health.IsDead) continue;

            float topY = GetColliderTopY(health.gameObject);
            m_targets.Add(new Target { health = health, topY = topY });
        }
    }

    float GetColliderTopY(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null)
            return col.bounds.max.y - go.transform.position.y;

        // コライダーがなければ子から探す
        col = go.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.max.y - go.transform.position.y;

        return 2f;
    }

    void OnGUI()
    {
        var cam = Camera.main;
        if (cam == null) { ChannelLogger.LogGuardReturn("Game", "メインカメラなし"); return; }

        foreach (var target in m_targets)
        {
            if (target.health == null || target.health.IsDead) continue;

            Vector3 worldPos = target.health.transform.position + Vector3.up * (target.topY + m_extraHeight);
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            if (screenPos.z <= 0f) continue;

            float x = screenPos.x - m_barWidth * 0.5f;
            float y = Screen.height - screenPos.y;

            DrawRect(new Rect(x - 1, y - 1, m_barWidth + 2, m_barHeight + 2), new Color(0f, 0f, 0f, 0.7f));

            float ratio = target.health.HealthRatio;
            Color barColor;
            if (ratio > 0.5f)
                barColor = Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f);
            else
                barColor = Color.Lerp(Color.red, Color.yellow, ratio * 2f);

            DrawRect(new Rect(x, y, m_barWidth * ratio, m_barHeight), barColor);
        }
    }

    void DrawRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, m_whiteTex);
        GUI.color = Color.white;
    }

    void OnDestroy()
    {
        if (m_whiteTex != null)
            Destroy(m_whiteTex);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// デバッグ用スタミナバー。OnGUIで全Stamina持ちの頭上にスタミナバーを描画する。
/// Canvas不要、このスクリプト1つで完結。
/// </summary>
public class DebugStaminaBar : MonoBehaviour
{
    [SerializeField] private float m_extraHeight = 0.45f;
    [SerializeField] private float m_barWidth = 60f;
    [SerializeField] private float m_barHeight = 6f;
    [SerializeField] private float m_scanInterval = 2f;

    [Tooltip("HPバー等と重なる場合に下へずらす（ピクセル）")]
    [SerializeField] private float m_screenYOffset = 10f;

    private struct Target
    {
        public Stamina stamina;
        public float topY;
    }

    private readonly List<Target> m_targets = new List<Target>();
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

        var allStamina = FindObjectsByType<Stamina>(FindObjectsSortMode.None);
        foreach (var stamina in allStamina)
        {
            if (stamina == null) continue;

            float topY = GetColliderTopY(stamina.gameObject);
            m_targets.Add(new Target { stamina = stamina, topY = topY });
        }
    }

    float GetColliderTopY(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null)
            return col.bounds.max.y - go.transform.position.y;

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
            if (target.stamina == null) continue;

            Vector3 worldPos = target.stamina.transform.position + Vector3.up * (target.topY + m_extraHeight);
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            if (screenPos.z <= 0f) continue;

            float x = screenPos.x - m_barWidth * 0.5f;
            float y = Screen.height - screenPos.y + m_screenYOffset;

            DrawRect(new Rect(x - 1, y - 1, m_barWidth + 2, m_barHeight + 2), new Color(0f, 0f, 0f, 0.7f));

            float ratio = target.stamina.StaminaRatio;
            Color barColor = target.stamina.IsBroken ? new Color(1f, 0.2f, 0.2f, 1f) : new Color(0.2f, 0.7f, 1f, 1f);

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
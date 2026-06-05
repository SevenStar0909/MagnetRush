using System.Collections;
using UnityEngine;

/// <summary>
/// ゲームクリア時の演出（UI移動とスローモーション）を制御するクラス
/// </summary>
public class GameClearUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform m_gameText;
    [SerializeField] private RectTransform m_clearText;

    [Header("Animation Settings")]
    [Tooltip("文字が画面外から飛んでくる時間（秒）")]
    [SerializeField] private float m_uiMoveDuration = 0.8f;
    [Tooltip("文字がくっついた後、スローを維持する余韻の時間（秒）")]
    [SerializeField] private float m_stayDuration = 1.2f;

    [Header("Position Settings")]
    [SerializeField] private Vector2 m_gameStartPos = new Vector2(-1200f, 0f);
    [SerializeField] private Vector2 m_clearStartPos = new Vector2(1200f, 0f);
    [SerializeField] private Vector2 m_gameEndPos = new Vector2(-160f, 0f);
    [SerializeField] private Vector2 m_clearEndPos = new Vector2(160f, 0f);

    private float m_originalFixedDeltaTime;
    private bool m_isPlaying = false;

    private void Start()
    {
        // 念のため開始時はUIを非アクティブにしておく
        if (m_gameText != null) m_gameText.gameObject.SetActive(false);
        if (m_clearText != null) m_clearText.gameObject.SetActive(false);
    }

    /// <summary>
    /// クリア演出を開始する（外部のGameManagerなどから呼ばれる）
    /// </summary>
    public void PlayPerformance(float slowScale, float originalFixedDeltaTime)
    {
        if (m_isPlaying) return; // 二重発火防止

        m_originalFixedDeltaTime = originalFixedDeltaTime;
        StartCoroutine(ClearSequence(slowScale));
    }

    private IEnumerator ClearSequence(float slowScale)
    {
        m_isPlaying = true;

        if (m_gameText == null || m_clearText == null)
        {
            Debug.LogError("[GameClearUI] UIの参照が登録されていません！");
            yield break;
        }

        // 初期位置にセットしてアクティブ化
        m_gameText.anchoredPosition = m_gameStartPos;
        m_clearText.anchoredPosition = m_clearStartPos;
        m_gameText.gameObject.SetActive(true);
        m_clearText.gameObject.SetActive(true);

        // --------------------------------------------------
        // フェーズ1：文字が左右から飛んでくる（世界はスロー）
        // --------------------------------------------------
        float elapsedRealtime = 0f;
        while (elapsedRealtime < m_uiMoveDuration)
        {
            // タイムスケールを毎フレーム強制ロック（他スクリプトの上書き対策）
            Time.timeScale = slowScale;
            Time.fixedDeltaTime = m_originalFixedDeltaTime * slowScale;

            float progress = elapsedRealtime / m_uiMoveDuration;
            float t = Mathf.SmoothStep(0f, 1f, progress); // イージング

            m_gameText.anchoredPosition = Vector2.Lerp(m_gameStartPos, m_gameEndPos, t);
            m_clearText.anchoredPosition = Vector2.Lerp(m_clearStartPos, m_clearEndPos, t);

            elapsedRealtime += Time.unscaledDeltaTime;
            yield return null;
        }

        m_gameText.anchoredPosition = m_gameEndPos;
        m_clearText.anchoredPosition = m_clearEndPos;

        // --------------------------------------------------
        // フェーズ2：くっついた後の余韻（まだ世界はスロー）
        // --------------------------------------------------
        elapsedRealtime = 0f;
        while (elapsedRealtime < m_stayDuration)
        {
            Time.timeScale = slowScale;
            Time.fixedDeltaTime = m_originalFixedDeltaTime * slowScale;

            elapsedRealtime += Time.unscaledDeltaTime;
            yield return null;
        }

        // --------------------------------------------------
        // フェーズ3：スロー解除
        // --------------------------------------------------
        Time.timeScale = 1f;
        Time.fixedDeltaTime = m_originalFixedDeltaTime;

        Debug.Log("[GameClearUI] クリア演出が完了し、スローが解除されました。");
        m_isPlaying = false;
    }
}
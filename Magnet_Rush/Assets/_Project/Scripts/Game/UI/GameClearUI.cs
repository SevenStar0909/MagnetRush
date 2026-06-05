using System.Collections;
using UnityEngine;

/// <summary>
/// ゲームクリア時の演出（UI移動とスローモーションのすべての数値を一括管理）を制御するクラス
/// </summary>
public class GameClearUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform m_gameText;
    [SerializeField] private RectTransform m_clearText;

    [Header("Animation Settings")]
    [Tooltip("文字が飛んでくる時間 ＝ スローモーションがかかる時間（秒）")]
    [SerializeField] private float m_uiMoveDuration = 0.8f;

    [Tooltip("スローモーションの倍率（0.15f なら通常の 15% の速度）")]
    [SerializeField] private float m_slowScale = 0.15f; // ★GameManagerからこちらへ引っ越し

    [Header("Position Settings")]
    [SerializeField] private Vector2 m_gameStartPos = new Vector2(-1200f, 0f);
    [SerializeField] private Vector2 m_clearStartPos = new Vector2(1200f, 0f);
    [SerializeField] private Vector2 m_gameEndPos = new Vector2(-160f, 0f);
    [SerializeField] private Vector2 m_clearEndPos = new Vector2(160f, 0f);

    private float m_originalFixedDeltaTime;
    private bool m_isPlaying = false;

    private void Start()
    {
        if (m_gameText != null) m_gameText.gameObject.SetActive(false);
        if (m_clearText != null) m_clearText.gameObject.SetActive(false);
    }

    /// <summary>
    /// クリア演出を開始する（引数から slowScale を削除！）
    /// </summary>
    public void PlayPerformance(float originalFixedDeltaTime)
    {
        if (m_isPlaying) return;

        m_originalFixedDeltaTime = originalFixedDeltaTime;
        StartCoroutine(ClearSequence());
    }

    private IEnumerator ClearSequence()
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
        // 【移動 ＝ スロー期間】
        // --------------------------------------------------
        float elapsedRealtime = 0f;
        while (elapsedRealtime < m_uiMoveDuration)
        {
            // クラス内の m_slowScale を使って毎フレーム強制ロック
            Time.timeScale = m_slowScale;
            Time.fixedDeltaTime = m_originalFixedDeltaTime * m_slowScale;

            float progress = elapsedRealtime / m_uiMoveDuration;
            float t = Mathf.SmoothStep(0f, 1f, progress);

            m_gameText.anchoredPosition = Vector2.Lerp(m_gameStartPos, m_gameEndPos, t);
            m_clearText.anchoredPosition = Vector2.Lerp(m_clearStartPos, m_clearEndPos, t);

            elapsedRealtime += Time.unscaledDeltaTime;
            yield return null;
        }

        m_gameText.anchoredPosition = m_gameEndPos;
        m_clearText.anchoredPosition = m_clearEndPos;

        // --------------------------------------------------
        // 【移動完了！】即座にスロー解除
        // --------------------------------------------------
        Time.timeScale = 1f;
        Time.fixedDeltaTime = m_originalFixedDeltaTime;

        Debug.Log("[GameClearUI] クリア演出完了。");
        m_isPlaying = false;
    }
}
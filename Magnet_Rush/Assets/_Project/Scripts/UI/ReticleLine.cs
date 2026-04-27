using UnityEngine;

/// <summary>
/// レティクルの1ライン。Awake時の anchoredPosition を rest として記録し、
/// Kick() で外側にスナップ → AnimationCurve でリターン。
/// 連射は累積し maxKickDistance で上限クランプ。
/// 依存: RectTransform。Configure() でパラメータ注入。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ReticleLine : MonoBehaviour
{
    private RectTransform m_rect;
    private Vector2 m_restPos;
    private Vector2 m_outward;

    private float m_kickDistance;
    private float m_maxKickDistance;
    private float m_returnDuration;
    private AnimationCurve m_returnCurve;

    private Vector2 m_kickPeakOffset = Vector2.zero;
    private float m_returnTimer = -1f;

    void Awake()
    {
        m_rect = GetComponent<RectTransform>();
        m_restPos = m_rect.anchoredPosition;
        if (m_restPos.sqrMagnitude > 0.0001f)
        {
            m_outward = m_restPos.normalized;
        }
        else
        {
            // restPos が原点だと外向きが定義できないため Vector2.up にフォールバック
            m_outward = Vector2.up;
            ChannelLogger.LogGuardReturn("UI", $"ReticleLine '{name}' restPos が原点。outward=Vector2.up にフォールバック");
        }
    }

    /// <summary>
    /// パラメータ注入。ReticleUI.Awake から呼ばれる想定。
    /// </summary>
    /// <param name="kickDistance">1発で増えるオフセット（px）</param>
    /// <param name="maxKickDistance">累積上限（px）</param>
    /// <param name="returnDuration">ピーク→rest 戻り時間（秒）</param>
    /// <param name="returnCurve">リターン補間カーブ。null なら線形扱い</param>
    public void Configure(float kickDistance, float maxKickDistance, float returnDuration, AnimationCurve returnCurve)
    {
        m_kickDistance = kickDistance;
        m_maxKickDistance = maxKickDistance;
        m_returnDuration = returnDuration;
        m_returnCurve = returnCurve;
    }

    /// <summary>
    /// 蓄積式キック発火。現在位置から kickDistance 加算し maxKickDistance でクランプ、即スナップ。
    /// </summary>
    public void Kick()
    {
        if (m_rect == null) return;
        Vector2 currentOffset = m_rect.anchoredPosition - m_restPos;
        // outward 方向の射影成分のみで累積（横ずれは無視）
        float currentMag = Mathf.Max(0f, Vector2.Dot(currentOffset, m_outward));
        float newMag = Mathf.Min(currentMag + m_kickDistance, m_maxKickDistance);
        m_kickPeakOffset = m_outward * newMag;
        m_rect.anchoredPosition = m_restPos + m_kickPeakOffset;
        m_returnTimer = 0f;
    }

    /// <summary>位置・タイマー・ピークオフセットを全てクリアして rest 状態に戻す。</summary>
    public void ResetToRest()
    {
        if (m_rect != null) m_rect.anchoredPosition = m_restPos;
        m_kickPeakOffset = Vector2.zero;
        m_returnTimer = -1f;
    }

    void OnDisable()
    {
        ResetToRest();
    }

    void Update()
    {
        if (m_returnTimer < 0f) return;
        if (m_returnDuration <= 0f) { ResetToRest(); return; }

        // ポーズ中も進行させたいので unscaledDeltaTime
        m_returnTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(m_returnTimer / m_returnDuration);
        float curveValue = m_returnCurve != null ? m_returnCurve.Evaluate(t) : t;
        Vector2 offset = Vector2.Lerp(m_kickPeakOffset, Vector2.zero, curveValue);
        m_rect.anchoredPosition = m_restPos + offset;

        if (t >= 1f) m_returnTimer = -1f;
    }
}

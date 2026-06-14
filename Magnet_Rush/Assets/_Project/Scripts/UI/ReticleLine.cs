using UnityEngine;

/// <summary>
/// レティクルの1ライン。Awake時の anchoredPosition を基本位置とし、
/// エイム状態による中心への移動と、Kick() による外側へのスナップを合成する。
/// 連射は累積し maxKickDistance で上限クランプ。
/// 依存: RectTransform。Configure() でパラメータ注入。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ReticleLine : MonoBehaviour
{
    private RectTransform m_rect;
    private Vector2 m_originalRestPos; // Awake時の初期位置（通常時の位置）
    private Vector2 m_aimTargetPos;    // エイム時の目標位置（中心に近い位置）
    private Vector2 m_outward;

    private float m_kickDistance;
    private float m_maxKickDistance;
    private float m_returnDuration;
    private AnimationCurve m_returnCurve;

    private Vector2 m_kickPeakOffset = Vector2.zero;
    private float m_returnTimer = -1f;

    // --- エイム追加分 ---
    private float m_currentAimAmount = 0f; // 0f(通常) ～ 1f(エイム)

    void Awake()
    {
        m_rect = GetComponent<RectTransform>();
        m_originalRestPos = m_rect.anchoredPosition;
        if (m_originalRestPos.sqrMagnitude > 0.0001f)
        {
            m_outward = m_originalRestPos.normalized;
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
    /// 引数に aimDistancePercent を追加。
    /// </summary>
    public void Configure(float kickDistance, float maxKickDistance, float returnDuration, AnimationCurve returnCurve, float aimDistancePercent)
    {
        m_kickDistance = kickDistance;
        m_maxKickDistance = maxKickDistance;
        m_returnDuration = returnDuration;
        m_returnCurve = returnCurve;

        // エイム時の目標位置を計算 (中心 0,0 に向かって指定された割合の距離にする)
        m_aimTargetPos = m_originalRestPos * aimDistancePercent;
    }

    /// <summary>
    /// 外部(ReticleUI)から毎フレームエイムの進行度(0～1)を受け取る
    /// </summary>
    public void SetAimAmount(float amount)
    {
        m_currentAimAmount = Mathf.Clamp01(amount);
    }

    /// <summary>
    /// 現在の「エイム具合を考慮したベース位置」を取得
    /// </summary>
    private Vector2 GetCurrentBasePos()
    {
        return Vector2.Lerp(m_originalRestPos, m_aimTargetPos, m_currentAimAmount);
    }

    /// <summary>
    /// 蓄積式キック発火。現在のベース位置から kickDistance 加算し maxKickDistance でクランプ、即スナップ。
    /// </summary>
    public void Kick()
    {
        if (m_rect == null) return;

        Vector2 currentBasePos = GetCurrentBasePos();

        // 「固定のm_restPos」ではなく、「現在のベース位置」からのオフセットを計算
        Vector2 currentOffset = m_rect.anchoredPosition - currentBasePos;

        // outward 方向の射影成分のみで累積（横ずれは無視）
        float currentMag = Mathf.Max(0f, Vector2.Dot(currentOffset, m_outward));
        float newMag = Mathf.Min(currentMag + m_kickDistance, m_maxKickDistance);
        m_kickPeakOffset = m_outward * newMag;
        m_rect.anchoredPosition = currentBasePos + m_kickPeakOffset;
        m_returnTimer = 0f;
    }

    /// <summary>タイマー・ピークオフセットをクリアして現在のベース位置に戻す。</summary>
    public void ResetToRest()
    {
        m_kickPeakOffset = Vector2.zero;
        m_returnTimer = -1f;
        if (m_rect != null) m_rect.anchoredPosition = GetCurrentBasePos();
    }

    void OnDisable()
    {
        ResetToRest();
    }

    void Update()
    {
        Vector2 currentBasePos = GetCurrentBasePos();

        // キックの戻りがない（静止している）場合でも、エイムによる移動を適用し続ける
        if (m_returnTimer < 0f)
        {
            m_rect.anchoredPosition = currentBasePos;
            return;
        }

        if (m_returnDuration <= 0f) { ResetToRest(); return; }

        // ポーズ中も進行させたいので unscaledDeltaTime
        m_returnTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(m_returnTimer / m_returnDuration);
        float curveValue = m_returnCurve != null ? m_returnCurve.Evaluate(t) : t;

        // キックのオフセットを計算し、現在のベース位置に加算する
        Vector2 kickOffset = Vector2.Lerp(m_kickPeakOffset, Vector2.zero, curveValue);
        m_rect.anchoredPosition = currentBasePos + kickOffset;

        if (t >= 1f) m_returnTimer = -1f;
    }
}
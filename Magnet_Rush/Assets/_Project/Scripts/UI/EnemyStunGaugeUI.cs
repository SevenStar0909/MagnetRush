using UnityEngine;
using UnityEngine.UI;

public class EnemyStunGaugeUI : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField] private Image m_gaugeFillImage;
    [SerializeField] private Image m_gaugeBackgroundImage;
    [SerializeField] private GameObject m_gaugeContainer;

    [Header("出現させるプレイヤーとの距離")]
    [SerializeField] private float m_displayDistance = 15f;

    [Header("ゲージの色設定（残量に応じて変化）")]
    [SerializeField] private Color m_colorHigh = Color.green;           // 残量多
    [SerializeField] private Color m_colorMid = Color.yellow;           // 残量中
    [SerializeField] private Color m_colorLow = new Color(1f, 0.5f, 0f); // 残量少（オレンジ）
    [SerializeField] private Color m_colorCritical = Color.red;         // ピンチ・スタン中

    private EnemyBossAI m_boss;
    private Transform m_bossTarget;
    private Transform m_playerTarget;

    private bool m_warnedNoBossTag;
    private bool m_isSpawned = false;

    private EnemyBossAI.BossState m_lastBossState = EnemyBossAI.BossState.Idle;
    private float m_uiStunTimer = 0f;

    void Start()
    {
        if (m_gaugeContainer != null)
        {
            m_gaugeContainer.SetActive(false);
        }

        GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
        {
            m_playerTarget = playerObj.transform;
        }

        if (!TryResolveBoss())
        { ChannelLogger.LogGuardReturn("Stun", "Boss未配置"); }
    }

    void Update()
    {
        if (m_boss == null || m_playerTarget == null || m_gaugeFillImage == null || m_gaugeBackgroundImage == null) return;

        if (!m_isSpawned)
        {
            float distance = Vector3.Distance(m_playerTarget.position, m_bossTarget.position);
            if (distance <= m_displayDistance)
            {
                m_isSpawned = true;
                if (m_gaugeContainer != null) m_gaugeContainer.SetActive(true);
            }
        }

        if (m_isSpawned)
        {
            UpdateGaugeDisplay();
        }
    }

    /// <summary>
    /// Bossの状態に応じてゲージの表示を更新する
    /// </summary>
    private void UpdateGaugeDisplay()
    {
        float displayRatio = 0f;

        bool isStunnedOrStagger = m_boss.State == EnemyBossAI.BossState.Stunned || m_boss.State == EnemyBossAI.BossState.Stagger;
        bool wasStunnedOrStagger = m_lastBossState == EnemyBossAI.BossState.Stunned || m_lastBossState == EnemyBossAI.BossState.Stagger;

        if (isStunnedOrStagger)
        {
            // よろけ(蓄積ルート)とスタン(カウンタールート)で継続時間が違うので状態で使い分ける。
            float maxDuration = ResolveBreakDuration();

            if (!wasStunnedOrStagger)
                m_uiStunTimer = maxDuration;

            m_uiStunTimer = Mathf.Max(0f, m_uiStunTimer - Time.deltaTime);

            displayRatio = maxDuration > 0f ? 1.0f - (m_uiStunTimer / maxDuration) : 1f;
        }
        else
        {
            // Stamina は内部的に「満タン→0」で減るが、プレイヤーには「スタンゲージが溜まる」見せ方にする。
            displayRatio = 1f - m_boss.Stamina.StaminaRatio;
        }

        m_lastBossState = m_boss.State;
        m_gaugeFillImage.fillAmount = displayRatio;

        ApplyGaugeColor(displayRatio, isStunnedOrStagger);
    }

    // よろけ(Stagger)とスタン(Stunned)で継続時間が違うので、現在の状態に対応する継続時間を返す。
    private float ResolveBreakDuration()
    {
        var s = m_boss.Settings;
        if (s == null) return 3.0f;
        return m_boss.State == EnemyBossAI.BossState.Stagger ? s.staggerDuration : s.staminaBreakDuration;
    }

    /// <summary>
    /// ゲージの色を残量と状態に応じて変化させる
    /// </summary>
    private void ApplyGaugeColor(float ratio, bool isStunned)
    {
        Color targetColor;

        if (isStunned)
        {
            targetColor = m_colorCritical;
        }
        else
        {
            // ratio = スタンゲージ蓄積量（0=空 → 1=満タン）。溜まるほど危険色に寄せる。
            if (ratio < 0.3f)
                targetColor = m_colorHigh;     // 30%未満は緑（まだ余裕）
            else if (ratio < 0.6f)
                targetColor = m_colorMid;      // 黄
            else if (ratio < 0.85f)
                targetColor = m_colorLow;      // オレンジ
            else
                targetColor = m_colorCritical; // 85%以上は赤（もうすぐよろけ）
        }

        m_gaugeBackgroundImage.color = targetColor;
        m_gaugeFillImage.color = targetColor;
    }

    private bool TryResolveBoss()
    {
        if (m_bossTarget != null) return true;

        GameObject bossObj;
        try { bossObj = GameObject.FindWithTag("Boss"); }
        catch (UnityException)
        {
            if (!m_warnedNoBossTag)
            {
                ChannelLogger.LogWarning("Stab", "Bossタグが未登録 (TagManager に Boss を追加)");
                m_warnedNoBossTag = true;
            }
            return false;
        }

        if (bossObj == null) return false;
        m_bossTarget = bossObj.transform;

        if (!bossObj.TryGetComponent<EnemyBossAI>(out m_boss))
        {
            ChannelLogger.LogWarning("Stab", "Bossオブジェクトに EnemyBossAI コンポーネントが見つかりません");
            return false;
        }

        return true;
    }
}
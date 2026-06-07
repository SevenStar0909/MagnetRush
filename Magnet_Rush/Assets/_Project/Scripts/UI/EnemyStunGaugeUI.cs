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
            if (!wasStunnedOrStagger)
            {
                m_uiStunTimer = m_boss.Settings != null ? m_boss.Settings.staminaBreakDuration : 3.0f;
            }

            m_uiStunTimer = Mathf.Max(0f, m_uiStunTimer - Time.deltaTime);

            float maxDuration = m_boss.Settings != null ? m_boss.Settings.staminaBreakDuration : 3.0f;
            displayRatio = maxDuration > 0f ? 1.0f - (m_uiStunTimer / maxDuration) : 1f;
        }
        else
        {
            displayRatio = m_boss.Stamina.StaminaRatio;
        }

        m_lastBossState = m_boss.State;
        m_gaugeFillImage.fillAmount = displayRatio;

        ApplyGaugeColor(displayRatio, isStunnedOrStagger);
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
            if (ratio >= 0.7f)
                targetColor = m_colorHigh;    // 70%以上は緑
            else if (ratio >= 0.4f)
                targetColor = m_colorMid;     // 40%以上は黄
            else if (ratio >= 0.15f)
                targetColor = m_colorLow;     // 15%以上はオレンジ
            else
                targetColor = m_colorCritical; // 15%未満は赤
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
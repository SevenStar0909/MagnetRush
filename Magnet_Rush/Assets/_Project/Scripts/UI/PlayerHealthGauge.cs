using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthGauge : MonoBehaviour
{
    [Header("ゲージ画像")]
    [SerializeField] private Image m_greenGauge;
    [SerializeField] private Image m_whiteGauge;

    [Header("色設定")]
    [SerializeField] private Color m_normalColor = Color.white;
    [SerializeField] private Color m_blinkColor = Color.red;

    [Header("点滅設定")]
    [SerializeField] private int m_blinkCount = 4;          // 点滅回数
    [SerializeField] private float m_blinkInterval = 0.1f;  // 点滅間隔（秒）

    [Header("白ゲージ減少設定")]
    [SerializeField] private float m_whiteShrinkDuration = 0.4f;

    private Health m_health;
    private Coroutine m_damageCoroutine; // コルーチンの二重起動・リセット管理用

    private float m_gaugeMaxWidth;

    void Start()
    {
        m_gaugeMaxWidth = m_greenGauge.rectTransform.rect.width;

        m_greenGauge.color = m_normalColor;

        GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
        {
            m_health = playerObj.GetComponent<Health>();
        }

        // イベントの登録と初期化
        if (m_health != null)
        {
            m_health.OnDamage += HandleOnDamage;
            m_health.OnHeal += HandleOnHeal;
            m_health.OnHealthReset += SnapToCurrentHealth;

            // ゲーム開始時のHPをUIに即座に反映
            RefreshGaugeInstant();
        }
        else
        {
            Debug.LogError("Playerオブジェクト、またはHealthコンポーネントが見つかりません。");
            SetNormalMode();
        }
    }

    void OnDestroy()
    {
        if (m_health != null)
        {
            m_health.OnDamage -= HandleOnDamage;
            m_health.OnHeal -= HandleOnHeal;
            m_health.OnHealthReset -= SnapToCurrentHealth;
        }
    }

    /// <summary>
    /// HealthのOnDamageイベントから自動で呼び出される
    /// </summary>
    private void HandleOnDamage(int amount)
    {
        // ダメージ時は緑ゲージだけを即座に新しいHPまで減らす
        UpdateGreenGaugeWidth(m_health.HealthRatio);

        if (m_damageCoroutine != null)
        {
            StopCoroutine(m_damageCoroutine);
        }
        m_damageCoroutine = StartCoroutine(ColorBlinkRoutine());
    }

    /// <summary>
    /// HealthのOnHealイベントから自動で呼び出される
    /// </summary>
    private void HandleOnHeal(int amount)
    {
        RefreshGaugeInstant();
    }

    /// <summary>
    ///HealthのOnHealthResetイベントから自動で呼び出される
    /// </summary>
    private void SnapToCurrentHealth()
    {
        RefreshGaugeInstant();
    }

    /// <summary>
    /// 点滅なしで即座に現在のHPをUIに反映する
    /// </summary>
    private void RefreshGaugeInstant()
    {
        if (m_damageCoroutine != null)
        {
            StopCoroutine(m_damageCoroutine);
            m_damageCoroutine = null;
        }

        float fillRatio = m_health != null ? m_health.HealthRatio : 1f;

        UpdateGreenGaugeWidth(fillRatio);
        UpdateWhiteGaugeWidth(fillRatio);
        SetNormalMode();
    }

    /// <summary>
    /// 緑ゲージの横幅を更新する
    /// </summary>
    private void UpdateGreenGaugeWidth(float fillRatio)
    {
        float targetWidth = m_gaugeMaxWidth * fillRatio;
        m_greenGauge.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        m_greenGauge.enabled = (fillRatio > 0f);
    }

    /// <summary>
    /// 白ゲージの横幅を更新する
    /// </summary>
    private void UpdateWhiteGaugeWidth(float fillRatio)
    {
        float targetWidth = m_gaugeMaxWidth * fillRatio;
        m_whiteGauge.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
    }

    private IEnumerator ColorBlinkRoutine()
    {
        // 白ゲージを表示
        m_whiteGauge.enabled = true;

        // 設定された回数だけ、色を交互に切り替える
        for (int i = 0; i < m_blinkCount; i++)
        {
            m_greenGauge.color = m_blinkColor;
            yield return new WaitForSeconds(m_blinkInterval);

            m_greenGauge.color = m_normalColor;
            yield return new WaitForSeconds(m_blinkInterval);
        }

        m_greenGauge.color = m_normalColor;

        float startWidth = m_whiteGauge.rectTransform.rect.width;
        float targetWidth = m_gaugeMaxWidth * (m_health != null ? m_health.HealthRatio : 0f);
        float elapsed = 0f;

        while (elapsed < m_whiteShrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / m_whiteShrinkDuration);

            float smoothedT = Mathf.SmoothStep(0f, 1f, t);
            float currentWidth = Mathf.Lerp(startWidth, targetWidth, smoothedT);

            m_whiteGauge.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, currentWidth);

            yield return null; // 1フレーム待つ
        }

        if (m_health != null)
        {
            UpdateWhiteGaugeWidth(m_health.HealthRatio);
        }

        // 通常状態に戻す
        SetNormalMode();
        m_damageCoroutine = null;
    }

    private void SetNormalMode()
    {
        m_greenGauge.color = m_normalColor;
        m_greenGauge.enabled = (m_health != null && m_health.HealthRatio > 0f);
    }
}
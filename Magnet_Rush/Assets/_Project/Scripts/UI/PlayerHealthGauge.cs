using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthGauge : MonoBehaviour
{
    [Header("ゲージ画像")]
    [SerializeField] private Image m_greenGauge;
    [SerializeField] private Image m_whiteGauge;

    [Header("点滅設定")]
    [SerializeField] private int m_blinkCount = 4;          // 点滅回数（白→消→白→消 の回数）
    [SerializeField] private float m_blinkInterval = 0.1f;  // 点滅間隔（秒）

    private Health m_health;
    private Coroutine m_blinkCoroutine; // コルーチンの二重起動・リセット管理用

    void Start()
    {
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
        }
    }

    /// <summary>
    /// HealthのOnDamageイベントから自動で呼び出される
    /// </summary>
    private void HandleOnDamage(int amount)
    {
        // 最新のHP割合にゲージの長さを合わせる
        float fillRatio = m_health.HealthRatio;
        m_greenGauge.fillAmount = fillRatio;
        m_whiteGauge.fillAmount = fillRatio;

        // すでに点滅中なら一度止めて、新しく点滅をやり直す（連続被弾対策）
        if (m_blinkCoroutine != null)
        {
            StopCoroutine(m_blinkCoroutine);
        }
        m_blinkCoroutine = StartCoroutine(BlinkWhite());
    }

    /// <summary>
    /// HealthのOnHealイベントから自動で呼び出される
    /// </summary>
    private void HandleOnHeal(int amount)
    {
        // 回復時は点滅させず、即座に最新のHPを反映
        RefreshGaugeInstant();
    }

    /// <summary>
    /// 点滅なしで即座に現在のHPをUIに反映する
    /// </summary>
    private void RefreshGaugeInstant()
    {
        if (m_blinkCoroutine != null)
        {
            StopCoroutine(m_blinkCoroutine);
            m_blinkCoroutine = null;
        }

        float fillRatio = m_health != null ? m_health.HealthRatio : 1f;
        m_greenGauge.fillAmount = fillRatio;
        m_whiteGauge.fillAmount = fillRatio;
        SetNormalMode();
    }

    private IEnumerator BlinkWhite()
    {
        // 緑ゲージを非表示にする
        m_greenGauge.enabled = false;

        for (int i = 0; i < m_blinkCount; i++)
        {
            m_whiteGauge.enabled = true;
            yield return new WaitForSeconds(m_blinkInterval);
            m_whiteGauge.enabled = false;
            yield return new WaitForSeconds(m_blinkInterval);
        }

        // 点滅終了 → 通常モードに戻す
        SetNormalMode();
        m_blinkCoroutine = null;
    }

    private void SetNormalMode()
    {
        m_whiteGauge.enabled = false;
        m_greenGauge.enabled = true;
    }
}
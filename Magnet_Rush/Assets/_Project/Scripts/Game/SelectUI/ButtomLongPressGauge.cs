using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class LongPressGauge : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("ゲージ設定")]
    [SerializeField] private float m_requiredPressDuration = 2f;
    [SerializeField] private Image m_gaugeImage;
    [SerializeField] private float m_transitionDelay = 0.1f;

    private Coroutine m_pressCoroutine;
    private Coroutine m_transitionCoroutine;
    private float m_currentPressTime = 0f;
    private bool m_isPressed = false;

    private void Start()
    {
        if (m_gaugeImage != null)
        {
            m_gaugeImage.fillAmount = 0f;
        }
        Debug.Log("[LongPressGauge] 初期化完了");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("[LongPressGauge] OnPointerDown 実行");

        m_isPressed = true;
        m_currentPressTime = 0f;

        if (m_pressCoroutine != null)
        {
            StopCoroutine(m_pressCoroutine);
            Debug.Log("[LongPressGauge] 既存の m_pressCoroutine を停止");
        }

        if (m_transitionCoroutine != null)
        {
            StopCoroutine(m_transitionCoroutine);
            Debug.Log("[LongPressGauge] 既存の m_transitionCoroutine を停止");
        }

        m_pressCoroutine = StartCoroutine(PressGaugeCoroutine());
        Debug.Log("[LongPressGauge] PressGaugeCoroutine を開始");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log($"[LongPressGauge] OnPointerUp 実行 - 現在時間: {m_currentPressTime:F2}秒, m_isPressed: {m_isPressed}");

        m_isPressed = false;

        if (m_pressCoroutine != null)
        {
            StopCoroutine(m_pressCoroutine);
            Debug.Log("[LongPressGauge] m_pressCoroutine を停止");
        }

        if (m_currentPressTime < m_requiredPressDuration)
        {
            Debug.Log($"[LongPressGauge] ゲージ不完全 ({m_currentPressTime:F2}秒 < {m_requiredPressDuration}秒) - リセット");
            ResetGauge();

            if (m_transitionCoroutine != null)
            {
                StopCoroutine(m_transitionCoroutine);
                Debug.Log("[LongPressGauge] m_transitionCoroutine を停止");
            }
        }
        else
        {
            Debug.Log($"[LongPressGauge] OnPointerUp: ゲージ満タン状態 ({m_currentPressTime:F2}秒 >= {m_requiredPressDuration}秒)");
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"[LongPressGauge] OnPointerExit 実行 - 現在時間: {m_currentPressTime:F2}秒, m_isPressed: {m_isPressed}");

        m_isPressed = false;

        if (m_pressCoroutine != null)
        {
            StopCoroutine(m_pressCoroutine);
            Debug.Log("[LongPressGauge] m_pressCoroutine を停止");
        }

        if (m_currentPressTime < m_requiredPressDuration)
        {
            Debug.Log($"[LongPressGauge] ゲージ不完全 ({m_currentPressTime:F2}秒 < {m_requiredPressDuration}秒) - リセット");
            ResetGauge();

            if (m_transitionCoroutine != null)
            {
                StopCoroutine(m_transitionCoroutine);
                Debug.Log("[LongPressGauge] m_transitionCoroutine を停止");
            }
        }
        else
        {
            Debug.Log($"[LongPressGauge] OnPointerExit: ゲージ満タン状態 ({m_currentPressTime:F2}秒 >= {m_requiredPressDuration}秒)");
        }
    }

    private IEnumerator PressGaugeCoroutine()
    {
        Debug.Log("[PressGaugeCoroutine] 開始");

        while (m_currentPressTime < m_requiredPressDuration)
        {
            m_currentPressTime += Time.deltaTime;

            if (m_gaugeImage != null)
            {
                m_gaugeImage.fillAmount = m_currentPressTime / m_requiredPressDuration;
            }

            yield return null;
        }

        Debug.Log($"[PressGaugeCoroutine] ループ終了 - m_isPressed: {m_isPressed}, 時間: {m_currentPressTime:F2}秒");

        if (m_isPressed && m_currentPressTime >= m_requiredPressDuration)
        {
            Debug.Log("[PressGaugeCoroutine] ゲージ満タン条件成立 - WaitForTransition 開始");

            if (m_transitionCoroutine != null)
                StopCoroutine(m_transitionCoroutine);

            m_transitionCoroutine = StartCoroutine(WaitForTransition());
        }
        else
        {
            Debug.Log($"[PressGaugeCoroutine] 遷移条件不成立 - m_isPressed: {m_isPressed}, 時間: {m_currentPressTime:F2}秒");
        }
    }

    private IEnumerator WaitForTransition()
    {
        Debug.Log($"[WaitForTransition] 開始 - {m_transitionDelay}秒待機");

        yield return new WaitForSeconds(m_transitionDelay);

        Debug.Log($"[WaitForTransition] 待機終了 - m_isPressed: {m_isPressed}, 時間: {m_currentPressTime:F2}秒");

        if (m_currentPressTime >= m_requiredPressDuration)
        {
            Debug.Log("[WaitForTransition] 遷移条件確認 - タイトルに遷移");
            OnLongPressComplete();
        }
        else
        {
            Debug.Log($"[WaitForTransition] 遷移キャンセル - m_isPressed: {m_isPressed}, 時間: {m_currentPressTime:F2}秒");
        }
    }

    private void OnLongPressComplete()
    {
        Debug.Log("[OnLongPressComplete] 実行");
        m_isPressed = false;
        SceneLoader.Instance.LoadScene(SceneLoader.SceneType.TitleScene);
    }

    private void ResetGauge()
    {
        Debug.Log("[ResetGauge] ゲージをリセット");
        m_currentPressTime = 0f;

        if (m_gaugeImage != null)
        {
            m_gaugeImage.fillAmount = 0f;
        }
    }
}
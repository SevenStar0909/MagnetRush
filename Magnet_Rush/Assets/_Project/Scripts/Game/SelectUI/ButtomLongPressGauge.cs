using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// ボタンの長押しでゲージを溜め、満タンでシーン遷移する。
/// マウス押下（IPointerDown系）とコントローラーのSubmit長押し（選択中のAボタン/Enter）の両方に対応。
/// 依存: EventSystem, Image
/// </summary>
public class LongPressGauge : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("ゲージ設定")]
    [SerializeField] private float m_requiredPressDuration = 2f;
    [SerializeField] private Image m_gaugeImage;
    [SerializeField] private float m_transitionDelay = 0.1f;
    [SerializeField] private string m_targetMapName;

    private Coroutine m_pressCoroutine;
    private Coroutine m_transitionCoroutine;
    private float m_currentPressTime = 0f;
    private bool m_isPressed = false;
    private bool m_submitHeld = false;

    private void Start()
    {
        if (m_gaugeImage != null)
        {
            m_gaugeImage.fillAmount = 0f;
        }
    }

    private void Update()
    {
        // パッド/キーボードのSubmit長押し。自分が選択中の間だけ受け付ける
        bool isSelected = EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == gameObject;
        bool held = isSelected && IsSubmitHeld();

        if (held && !m_submitHeld)
        {
            BeginPress();
        }
        else if (!held && m_submitHeld)
        {
            EndPress();
        }
        m_submitHeld = held;
    }

    /// <summary>パッドのAボタンまたはキーボードのEnter/Spaceが押下中か</summary>
    private static bool IsSubmitHeld()
    {
        var gamepad = Gamepad.current;
        if (gamepad != null && gamepad.buttonSouth.isPressed) return true;

        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.isPressed || keyboard.spaceKey.isPressed)) return true;

        return false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        BeginPress();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        EndPress();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        EndPress();
    }

    /// <summary>押下開始。ゲージの計測コルーチンを起動する</summary>
    private void BeginPress()
    {
        m_isPressed = true;
        m_currentPressTime = 0f;

        if (m_pressCoroutine != null)
            StopCoroutine(m_pressCoroutine);
        if (m_transitionCoroutine != null)
            StopCoroutine(m_transitionCoroutine);

        m_pressCoroutine = StartCoroutine(PressGaugeCoroutine());
    }

    /// <summary>押下終了。満タン前ならゲージをリセットする</summary>
    private void EndPress()
    {
        m_isPressed = false;

        if (m_pressCoroutine != null)
            StopCoroutine(m_pressCoroutine);

        if (m_currentPressTime < m_requiredPressDuration)
        {
            ResetGauge();

            if (m_transitionCoroutine != null)
                StopCoroutine(m_transitionCoroutine);
        }
    }

    private IEnumerator PressGaugeCoroutine()
    {
        while (m_currentPressTime < m_requiredPressDuration)
        {
            m_currentPressTime += Time.deltaTime;

            if (m_gaugeImage != null)
            {
                m_gaugeImage.fillAmount = m_currentPressTime / m_requiredPressDuration;
            }

            yield return null;
        }

        if (m_isPressed && m_currentPressTime >= m_requiredPressDuration)
        {
            if (m_transitionCoroutine != null)
                StopCoroutine(m_transitionCoroutine);

            m_transitionCoroutine = StartCoroutine(WaitForTransition());
        }
    }

    private IEnumerator WaitForTransition()
    {
        yield return new WaitForSeconds(m_transitionDelay);

        if (m_currentPressTime >= m_requiredPressDuration)
        {
            OnLongPressComplete();
        }
    }

    private void OnLongPressComplete()
    {
        m_isPressed = false;
        Sound.Play(SoundData.CueSheet.SE, SoundData.SE.Button);
        if (string.IsNullOrEmpty(m_targetMapName))
            SceneLoader.Instance.LoadScene(SceneLoader.SceneType.TitleScene);
        else
            SceneLoader.Instance.LoadGameWithMap(SceneLoader.SceneType.GameScene.ToString(), m_targetMapName);
    }

    private void ResetGauge()
    {
        m_currentPressTime = 0f;

        if (m_gaugeImage != null)
        {
            m_gaugeImage.fillAmount = 0f;
        }
    }
}

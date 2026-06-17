using UnityEngine;
using UnityEngine.UI; // Imageコンポーネントを扱うために追加
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonScaleAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private Vector3 m_normalScale = Vector3.one;
    [SerializeField] private Vector3 m_highlightedScale = new Vector3(1.1f, 1.1f, 1f);

    [Header("Color Settings")]
    [SerializeField] private Color m_selectedColor = Color.white;          // 選択時（本来の鮮やかな色）
    [SerializeField] private Color m_deselectedColor = new Color(0.7f, 0.7f, 0.7f, 1f); // 非選択時（グレーっぽい色）

    [Header("Animation Settings")]
    [SerializeField] private float m_duration = 0.2f;  // アニメーション時間（共通）

    private Image m_buttonImage;
    private Coroutine m_animCoroutine; // スケールと色を一括管理するコルーチン

    private void Awake()
    {
        // 自身のImageコンポーネントを取得
        m_buttonImage = GetComponent<Image>();

        // 起動時は非選択状態（グレー・通常サイズ）からスタートさせる
        transform.localScale = m_normalScale;
        if (m_buttonImage != null)
        {
            m_buttonImage.color = m_deselectedColor;
        }
    }

    /// <summary>マウスオーバー時</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        ChannelLogger.LogGuardReturn("UI", "ボタンマウスオーバー");
        StartAnimation(m_highlightedScale, m_selectedColor);
    }

    /// <summary>マウス離脱時</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        ChannelLogger.LogGuardReturn("UI", "ボタンマウス離脱");
        StartAnimation(m_normalScale, m_deselectedColor);
    }

    /// <summary>コントローラーで選択された時</summary>
    public void OnSelect(BaseEventData eventData)
    {
        StartAnimation(m_highlightedScale, m_selectedColor);
    }

    /// <summary>コントローラーで選択が外れた時</summary>
    public void OnDeselect(BaseEventData eventData)
    {
        StartAnimation(m_normalScale, m_deselectedColor);
    }

    /// <summary>アニメーションを一括で開始するヘルパーメソッド</summary>
    private void StartAnimation(Vector3 targetScale, Color targetColor)
    {
        if (m_animCoroutine != null)
            StopCoroutine(m_animCoroutine);

        m_animCoroutine = StartCoroutine(AnimateButton(targetScale, targetColor));
    }

    /// <summary>サイズと色を同時にスムーズに変化させる統合コルーチン</summary>
    private IEnumerator AnimateButton(Vector3 targetScale, Color targetColor)
    {
        Vector3 startScale = transform.localScale;
        Color startColor = m_buttonImage != null ? m_buttonImage.color : Color.white;
        float elapsed = 0f;

        while (elapsed < m_duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_duration; // 0 → 1 に変化

            // サイズのスムーズな補間 (Lerp)
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            // 色のスムーズな補間 (Lerp)
            if (m_buttonImage != null)
            {
                m_buttonImage.color = Color.Lerp(startColor, targetColor, t);
            }

            yield return null;
        }

        // 最終値を確実に設定
        transform.localScale = targetScale;
        if (m_buttonImage != null)
        {
            m_buttonImage.color = targetColor;
        }
    }
}
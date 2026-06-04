using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonScaleAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 m_normalScale = Vector3.one;
    [SerializeField] private Vector3 m_highlightedScale = new Vector3(1.1f, 1.1f, 1f);

    [SerializeField] private float m_scaleDuration = 0.2f;  // アニメーション時間

    private Coroutine m_scaleCoroutine;

    /// <summary>マウスオーバー時</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        ChannelLogger.LogGuardReturn("UI", "ボタンマウスオーバー");

        // 既存のコルーチンをキャンセル
        if (m_scaleCoroutine != null)
            StopCoroutine(m_scaleCoroutine);

        // 拡大アニメーション開始
        m_scaleCoroutine = StartCoroutine(ScaleTo(m_highlightedScale));
    }

    /// <summary>マウス離脱時</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        ChannelLogger.LogGuardReturn("UI", "ボタンマウス離脱");

        // 既存のコルーチンをキャンセル
        if (m_scaleCoroutine != null)
            StopCoroutine(m_scaleCoroutine);

        // 通常サイズに戻すアニメーション開始
        m_scaleCoroutine = StartCoroutine(ScaleTo(m_normalScale));
    }

    /// <summary>指定されたスケールまでスムーズに変化させるコルーチン</summary>
    private IEnumerator ScaleTo(Vector3 targetScale)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        // 指定時間かけてスケールを変更
        while (elapsed < m_scaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_scaleDuration;  // 0 → 1 に変化
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        // 最終値を確実に設定
        transform.localScale = targetScale;
    }
}
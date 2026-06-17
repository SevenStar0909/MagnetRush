using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonScaleAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private Vector3 m_normalScale = Vector3.one;
    [SerializeField] private Vector3 m_highlightedScale = new Vector3(1.1f, 1.1f, 1f);

    [Header("Target UI Settings")]
    [Tooltip("手前に乗っている、切り替えたいアイコンのImageをセットしてください")]
    [SerializeField] private Image m_iconImage;

    [Header("Color Settings")]
    [SerializeField] private Color m_selectedColor = Color.white;          // 選択時（本来の鮮やかな色）
    [SerializeField] private Color m_deselectedColor = new Color(0.7f, 0.7f, 0.7f, 1f); // 非選択時（グレーっぽい色）

    [Header("Sprite Settings")]
    [SerializeField] private Sprite m_normalSprite;      // 通常時（選択されていない時）のアイコン画像
    [SerializeField] private Sprite m_highlightedSprite; // 選択時（フォーカス・マウスオーバー時）のアイコン画像

    [Header("Stage Camera Settings")]
    [SerializeField] private SceneSelectUI m_sceneSelectUI;
    [SerializeField] private string m_mapSceneName;

    [Header("Animation Settings")]
    [SerializeField] private float m_duration = 0.2f;  // アニメーション時間（共通）

    private Coroutine m_animCoroutine; // スケールと色を一括管理するコルーチン

    private void Awake()
    {
        // もしインスペクターで未設定なら、自分自身のImageを取得（バックアップ用）
        if (m_iconImage == null)
        {
            m_iconImage = GetComponent<Image>();
        }

        // 起動時は非選択状態（グレー・通常サイズ・通常アイコン画像）からスタートさせる
        transform.localScale = m_normalScale;
        if (m_iconImage != null)
        {
            m_iconImage.color = m_deselectedColor;

            if (m_normalSprite != null)
            {
                m_iconImage.sprite = m_normalSprite;
            }
        }
    }

    /// <summary>マウスオーバー時</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        ChannelLogger.LogGuardReturn("UI", "ボタンマウスオーバー");
        ChangeSprite(m_highlightedSprite);
        StartAnimation(m_highlightedScale, m_selectedColor);
        TriggerStageCamera();
    }

    /// <summary>マウス離脱時</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        ChannelLogger.LogGuardReturn("UI", "ボタンマウス離脱");
        ChangeSprite(m_normalSprite);
        StartAnimation(m_normalScale, m_deselectedColor);
    }

    /// <summary>コントローラーで選択された時</summary>
    public void OnSelect(BaseEventData eventData)
    {
        ChangeSprite(m_highlightedSprite);
        StartAnimation(m_highlightedScale, m_selectedColor);
        TriggerStageCamera();
    }

    /// <summary>コントローラーで選択が外れた時</summary>
    public void OnDeselect(BaseEventData eventData)
    {
        ChangeSprite(m_normalSprite);
        StartAnimation(m_normalScale, m_deselectedColor);
    }

    private void TriggerStageCamera()
    {
        if (m_sceneSelectUI != null && !string.IsNullOrEmpty(m_mapSceneName))
        {
            m_sceneSelectUI.OnSelectStage(m_mapSceneName);
        }
    }

    /// <summary>アイコンの画像をパッと切り替える</summary>
    private void ChangeSprite(Sprite targetSprite)
    {
        if (m_iconImage != null && targetSprite != null)
        {
            m_iconImage.sprite = targetSprite;
        }
    }

    /// <summary>アニメーションを一括で開始する</summary>
    private void StartAnimation(Vector3 targetScale, Color targetColor)
    {
        if (m_animCoroutine != null)
            StopCoroutine(m_animCoroutine);

        m_animCoroutine = StartCoroutine(AnimateButton(targetScale, targetColor));
    }

    /// <summary>サイズと色を同時にスムーズに変化させるコルーチン</summary>
    private IEnumerator AnimateButton(Vector3 targetScale, Color targetColor)
    {
        Vector3 startScale = transform.localScale;
        Color startColor = m_iconImage != null ? m_iconImage.color : Color.white;
        float elapsed = 0f;

        while (elapsed < m_duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_duration;

            // ボタン全体のサイズを変更（アイコンだけでなく枠ごと大きくしたい場合はtransformのままでOK）
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            // アイコンの色のみをスムーズに補間
            if (m_iconImage != null)
            {
                m_iconImage.color = Color.Lerp(startColor, targetColor, t);
            }

            yield return null;
        }

        transform.localScale = targetScale;
        if (m_iconImage != null)
        {
            m_iconImage.color = targetColor;
        }
    }
}
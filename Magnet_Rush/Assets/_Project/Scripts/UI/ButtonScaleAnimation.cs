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

    private Image m_backgroundImage;   // ボタン自体の背景Image（自動取得用）
    private Coroutine m_animCoroutine; // スケールと色を一括管理するコルーチン

    private void Awake()
    {
        // 親（自身）のImageコンポーネントを背景用として取得
        m_backgroundImage = GetComponent<Image>();

        // もしアイコンの指定がなければ、自分自身のImageを使い回す（エラー防止）
        if (m_iconImage == null)
        {
            m_iconImage = m_backgroundImage;
        }

        // 起動時は初期状態（グレー・通常サイズ・通常アイコン画像）からスタート
        transform.localScale = m_normalScale;
        ApplyColor(m_deselectedColor);

        if (m_iconImage != null && m_normalSprite != null)
        {
            m_iconImage.sprite = m_normalSprite;
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

    /// <summary>背景とアイコンの色を直接適用するヘルパー</summary>
    private void ApplyColor(Color color)
    {
        if (m_backgroundImage != null) m_backgroundImage.color = color;
        if (m_iconImage != null && m_iconImage != m_backgroundImage) m_iconImage.color = color;
    }

    /// <summary>サイズと色（背景・アイコン両方）を同時にスムーズに変化させるコルーチン</summary>
    private IEnumerator AnimateButton(Vector3 targetScale, Color targetColor)
    {
        Vector3 startScale = transform.localScale;

        // 開始時の色を取得（背景を基準にする）
        Color startColor = m_backgroundImage != null ? m_backgroundImage.color : Color.white;
        float elapsed = 0f;

        while (elapsed < m_duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_duration;

            // スケール補間
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            // 色補間（背景とアイコン両方を同時にLerpさせる）
            Color currentColor = Color.Lerp(startColor, targetColor, t);
            ApplyColor(currentColor);

            yield return null;
        }

        // 最終確定
        transform.localScale = targetScale;
        ApplyColor(targetColor);
    }
}
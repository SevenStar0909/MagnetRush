using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// デバッグUI。磁力パラメータのランタイム変更と弾の状態一覧を表示する。
/// F1キーで表示/非表示を切り替える。
/// </summary>
public class DebugUI : MonoBehaviour
{
#if !DEBUG && !UNITY_EDITOR
    void Awake() { gameObject.SetActive(false); }
#endif

    [FormerlySerializedAs("panel")]
    [SerializeField] private GameObject m_panel;
    [FormerlySerializedAs("forceSlider")]
    [SerializeField] private Slider m_forceSlider;
    [FormerlySerializedAs("rangeSlider")]
    [SerializeField] private Slider m_rangeSlider;
    [FormerlySerializedAs("forceLabel")]
    [SerializeField] private TextMeshProUGUI m_forceLabel;
    [FormerlySerializedAs("rangeLabel")]
    [SerializeField] private TextMeshProUGUI m_rangeLabel;
    [FormerlySerializedAs("bulletListText")]
    [SerializeField] private TextMeshProUGUI m_bulletListText;
    [FormerlySerializedAs("magnetSettings")]
    [SerializeField] private MagnetSettings m_magnetSettings;

    void Start()
    {
        if (m_panel != null) m_panel.SetActive(false);

        if (m_forceSlider != null && m_magnetSettings != null)
        {
            m_forceSlider.minValue = 1f;
            m_forceSlider.maxValue = 100f;
            m_forceSlider.value = m_magnetSettings.magnetForce;
            m_forceSlider.onValueChanged.AddListener(OnForceChanged);
        }

        if (m_rangeSlider != null && m_magnetSettings != null)
        {
            m_rangeSlider.minValue = 1f;
            m_rangeSlider.maxValue = 50f;
            m_rangeSlider.value = m_magnetSettings.magnetRange;
            m_rangeSlider.onValueChanged.AddListener(OnRangeChanged);
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            if (m_panel != null) m_panel.SetActive(!m_panel.activeSelf);
        }

        UpdateBulletList();
        UpdateLabels();
    }

    private void OnForceChanged(float value)
    {
        if (m_magnetSettings != null) m_magnetSettings.magnetForce = Mathf.Clamp(value, 1f, 100f);
    }

    private void OnRangeChanged(float value)
    {
        if (m_magnetSettings != null) m_magnetSettings.magnetRange = Mathf.Clamp(value, 1f, 50f);
    }

    private void UpdateLabels()
    {
        if (m_magnetSettings == null) { ChannelLogger.LogGuardReturn("UI", "MagnetSettings未設定"); return; }
        if (m_forceLabel != null) m_forceLabel.text = $"磁力: {m_magnetSettings.magnetForce:F1}";
        if (m_rangeLabel != null) m_rangeLabel.text = $"範囲: {m_magnetSettings.magnetRange:F1}";
    }

    private void UpdateBulletList()
    {
        if (m_bulletListText == null || BulletManager.Instance == null) { ChannelLogger.LogGuardReturn("UI", "弾リストUIまたはBulletManagerなし"); return; }

        // BulletManagerの内部カウントを使用（GetComponentsInChildrenは不正確）
        int count = BulletManager.Instance.CurrentCount;
        int max = BulletManager.Instance.MaxBullets;
        m_bulletListText.text = count == 0
            ? "弾: なし"
            : $"弾: {count}/{max}";
    }

    void OnDestroy()
    {
        if (m_forceSlider != null) m_forceSlider.onValueChanged.RemoveListener(OnForceChanged);
        if (m_rangeSlider != null) m_rangeSlider.onValueChanged.RemoveListener(OnRangeChanged);
    }
}

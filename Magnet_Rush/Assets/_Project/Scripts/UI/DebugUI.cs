using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// デバッグUI。磁力パラメータのランタイム変更と弾の状態一覧を表示する。
/// F1キーで表示/非表示を切り替える。
/// </summary>
public class DebugUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Slider forceSlider;
    [SerializeField] private Slider rangeSlider;
    [SerializeField] private TextMeshProUGUI forceLabel;
    [SerializeField] private TextMeshProUGUI rangeLabel;
    [SerializeField] private TextMeshProUGUI bulletListText;
    [SerializeField] private MagnetSettings magnetSettings;

    void Start()
    {
        if (panel != null) panel.SetActive(false);

        if (forceSlider != null && magnetSettings != null)
        {
            forceSlider.minValue = 1f;
            forceSlider.maxValue = 100f;
            forceSlider.value = magnetSettings.magnetForce;
            forceSlider.onValueChanged.AddListener(OnForceChanged);
        }

        if (rangeSlider != null && magnetSettings != null)
        {
            rangeSlider.minValue = 1f;
            rangeSlider.maxValue = 50f;
            rangeSlider.value = magnetSettings.magnetRange;
            rangeSlider.onValueChanged.AddListener(OnRangeChanged);
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            if (panel != null) panel.SetActive(!panel.activeSelf);
        }

        UpdateBulletList();
        UpdateLabels();
    }

    private void OnForceChanged(float value)
    {
        if (magnetSettings != null) magnetSettings.magnetForce = Mathf.Clamp(value, 1f, 100f);
    }

    private void OnRangeChanged(float value)
    {
        if (magnetSettings != null) magnetSettings.magnetRange = Mathf.Clamp(value, 1f, 50f);
    }

    private void UpdateLabels()
    {
        if (magnetSettings == null) return;
        if (forceLabel != null) forceLabel.text = $"磁力: {magnetSettings.magnetForce:F1}";
        if (rangeLabel != null) rangeLabel.text = $"範囲: {magnetSettings.magnetRange:F1}";
    }

    private void UpdateBulletList()
    {
        if (bulletListText == null || BulletManager.Instance == null) return;

        // BulletManagerの内部カウントを使用（GetComponentsInChildrenは不正確）
        int count = BulletManager.Instance.CurrentCount;
        int max = BulletManager.Instance.MaxBullets;
        bulletListText.text = count == 0
            ? "弾: なし"
            : $"弾: {count}/{max}";
    }

    void OnDestroy()
    {
        if (forceSlider != null) forceSlider.onValueChanged.RemoveListener(OnForceChanged);
        if (rangeSlider != null) rangeSlider.onValueChanged.RemoveListener(OnRangeChanged);
    }
}

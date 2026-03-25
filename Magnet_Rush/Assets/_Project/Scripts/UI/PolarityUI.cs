using UnityEngine;
using TMPro;

/// <summary>
/// 磁極表示UIの表示・更新。PolarityControllerのイベントを購読する。
/// </summary>
public class PolarityUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI polarityText;

    private PolarityController polarityController;

    void Start()
    {
        var player = GameObject.FindWithTag(GameTags.Player);
        if (player != null)
        {
            polarityController = player.GetComponent<PolarityController>();
            if (polarityController != null)
            {
                polarityController.OnPolarityChanged += UpdateDisplay;
                UpdateDisplay(polarityController.CurrentPole);
            }
        }
    }

    void OnDestroy()
    {
        if (polarityController != null)
        {
            polarityController.OnPolarityChanged -= UpdateDisplay;
        }
    }

    private void UpdateDisplay(MagneticPole pole)
    {
        if (polarityText == null) return;

        polarityText.text = pole == MagneticPole.S ? "S" : "N";
        polarityText.color = pole == MagneticPole.S
            ? new Color(1f, 0.3f, 0.3f)
            : new Color(0.3f, 0.5f, 1f);
    }
}

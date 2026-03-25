using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// レティクルUI。エイム状態と極性に応じてスプライトを切り替える。
/// </summary>
public class ReticleUI : MonoBehaviour
{
    [SerializeField] private Image reticleImage;

    [Header("Hipfire (通常時)")]
    [SerializeField] private Sprite hipfireS;
    [SerializeField] private Sprite hipfireN;

    [Header("Aim (エイム時)")]
    [SerializeField] private Sprite aimS;
    [SerializeField] private Sprite aimN;

    private AimController aimController;
    private PolarityController polarityController;
    private MagneticPole currentPole = MagneticPole.S;

    void Start()
    {
        var player = GameObject.FindWithTag(GameTags.Player);
        if (player != null)
        {
            aimController = player.GetComponent<AimController>();
            polarityController = player.GetComponent<PolarityController>();

            if (polarityController != null)
            {
                polarityController.OnPolarityChanged += OnPolarityChanged;
                currentPole = polarityController.CurrentPole;
            }
        }

        UpdateSprite();
    }

    void OnDestroy()
    {
        if (polarityController != null)
            polarityController.OnPolarityChanged -= OnPolarityChanged;
    }

    void Update()
    {
        UpdateSprite();
    }

    private void OnPolarityChanged(MagneticPole pole)
    {
        currentPole = pole;
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (reticleImage == null) return;

        bool aiming = aimController != null && aimController.IsAiming;

        if (aiming)
            reticleImage.sprite = currentPole == MagneticPole.S ? aimS : aimN;
        else
            reticleImage.sprite = currentPole == MagneticPole.S ? hipfireS : hipfireN;
    }
}
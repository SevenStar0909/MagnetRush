using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// レティクルUI。エイム状態と極性に応じてスプライトを切り替える。
/// </summary>
public class ReticleUI : MonoBehaviour
{
    [FormerlySerializedAs("reticleImage")]
    [SerializeField] private Image m_reticleImage;

    [Header("Hipfire (通常時)")]
    [FormerlySerializedAs("hipfireS")]
    [SerializeField] private Sprite m_hipfireS;
    [FormerlySerializedAs("hipfireN")]
    [SerializeField] private Sprite m_hipfireN;

    [Header("Aim (エイム時)")]
    [FormerlySerializedAs("aimS")]
    [SerializeField] private Sprite m_aimS;
    [FormerlySerializedAs("aimN")]
    [SerializeField] private Sprite m_aimN;

    private AimController m_aimController;
    private PolarityController m_polarityController;
    private MagneticPole m_currentPole = MagneticPole.S;

    void Start()
    {
        var player = GameObject.FindWithTag(GameTags.Player);
        if (player != null)
        {
            m_aimController = player.GetComponent<AimController>();
            m_polarityController = player.GetComponent<PolarityController>();

            if (m_polarityController != null)
            {
                m_polarityController.OnPolarityChanged += OnPolarityChanged;
                m_currentPole = m_polarityController.CurrentPole;
            }
        }

        UpdateSprite();
    }

    void OnDestroy()
    {
        if (m_polarityController != null)
            m_polarityController.OnPolarityChanged -= OnPolarityChanged;
    }

    void Update()
    {
        UpdateSprite();
    }

    private void OnPolarityChanged(MagneticPole pole)
    {
        m_currentPole = pole;
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (m_reticleImage == null) return;

        bool aiming = m_aimController != null && m_aimController.IsAiming;

        if (aiming)
            m_reticleImage.sprite = m_currentPole == MagneticPole.S ? m_aimS : m_aimN;
        else
            m_reticleImage.sprite = m_currentPole == MagneticPole.S ? m_hipfireS : m_hipfireN;
    }
}

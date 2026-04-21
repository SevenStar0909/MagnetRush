using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// レティクルUI。エイム状態と極性に応じてスプライトを切り替える。
/// PolarityController / AimController を購読する。
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

    private PolarityController m_polarity;
    private AimController m_aim;
    private MagneticPole m_currentPole = MagneticPole.S;

    void Start()
    {
        var playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
        {
            m_polarity = playerObj.GetComponent<PolarityController>();
            m_aim = playerObj.GetComponent<AimController>();

            if (m_polarity != null)
            {
                m_polarity.OnPolarityChanged += OnPolarityChanged;
                m_currentPole = m_polarity.CurrentPole;
            }
        }

        UpdateSprite();
    }

    void OnDestroy()
    {
        if (m_polarity != null)
            m_polarity.OnPolarityChanged -= OnPolarityChanged;
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
        if (m_reticleImage == null) { ChannelLogger.LogGuardReturn("UI", "レティクルImage未設定"); return; }

        bool aiming = m_aim != null && m_aim.IsAiming;

        if (aiming)
            m_reticleImage.sprite = m_currentPole == MagneticPole.S ? m_aimS : m_aimN;
        else
            m_reticleImage.sprite = m_currentPole == MagneticPole.S ? m_hipfireS : m_hipfireN;
    }
}

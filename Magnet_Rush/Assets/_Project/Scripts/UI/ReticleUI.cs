using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// レティクルUI。エイム状態でグループ切替、極性でスプライト切替、発射時に各ラインへキック配信。
/// 依存: AimController, PoleController, PlayerEvents（Player Tag のオブジェクトから取得）
/// </summary>
public class ReticleUI : MonoBehaviour
{
    [SerializeField] private ReticleSettings m_settings;

    [Header("Aim (+) Group")]
    [SerializeField] private GameObject m_aimGroup;
    [SerializeField] private ReticleLine[] m_aimLines;
    [SerializeField] private Image[] m_aimImages;
    [SerializeField] private Sprite m_aimLineS;
    [SerializeField] private Sprite m_aimLineN;

    [Header("Hipfire (X) Group")]
    [SerializeField] private GameObject m_hipfireGroup;
    [SerializeField] private ReticleLine[] m_hipfireLines;
    [SerializeField] private Image[] m_hipfireImages;
    [SerializeField] private Sprite m_hipfireLineS;
    [SerializeField] private Sprite m_hipfireLineN;

    private AimController m_aimController;
    private PoleController m_poleController;
    private PlayerEvents m_playerEvents;
    private MagneticPole m_currentPole = MagneticPole.S;

    void Awake()
    {
        if (m_settings == null) { ChannelLogger.LogGuardReturn("UI", "ReticleSettings未設定"); return; }
        ConfigureLines(m_aimLines);
        ConfigureLines(m_hipfireLines);
    }

    void Start()
    {
        var player = GameObject.FindWithTag(GameTags.Player);
        if (player != null)
        {
            m_aimController = player.GetComponent<AimController>();
            m_poleController = player.GetComponent<PoleController>();
            m_playerEvents = player.GetComponent<PlayerEvents>();

            if (m_poleController != null)
            {
                m_poleController.OnPoleChanged += OnPoleChanged;
                m_currentPole = m_poleController.CurrentPole;
            }
            if (m_playerEvents != null && m_playerEvents.OnShoot != null)
            {
                m_playerEvents.OnShoot.AddListener(OnShoot);
            }
        }

        UpdateSprites();
        UpdateGroups();
    }

    void OnDestroy()
    {
        if (m_poleController != null)
            m_poleController.OnPoleChanged -= OnPoleChanged;
        if (m_playerEvents != null && m_playerEvents.OnShoot != null)
            m_playerEvents.OnShoot.RemoveListener(OnShoot);
    }

    void Update()
    {
        UpdateGroups();
    }

    private void ConfigureLines(ReticleLine[] lines)
    {
        if (lines == null) return;
        foreach (var l in lines)
        {
            if (l != null)
                l.Configure(m_settings.kickDistance, m_settings.maxKickDistance, m_settings.returnDuration, m_settings.returnCurve);
        }
    }

    private void OnPoleChanged(MagneticPole pole)
    {
        m_currentPole = pole;
        UpdateSprites();
    }

    private void OnShoot()
    {
        // 反転: エイム中(LT押下)は Hipfire(X) を表示しキックする
        bool aiming = m_aimController != null && m_aimController.IsAiming;
        var lines = aiming ? m_hipfireLines : m_aimLines;
        if (lines == null) return;
        foreach (var line in lines)
        {
            if (line != null) line.Kick();
        }
    }

    private void UpdateSprites()
    {
        Sprite aimSprite = m_currentPole == MagneticPole.S ? m_aimLineS : m_aimLineN;
        Sprite hipfireSprite = m_currentPole == MagneticPole.S ? m_hipfireLineS : m_hipfireLineN;

        if (m_aimImages != null)
            foreach (var img in m_aimImages) if (img != null) img.sprite = aimSprite;
        if (m_hipfireImages != null)
            foreach (var img in m_hipfireImages) if (img != null) img.sprite = hipfireSprite;
    }

    private void UpdateGroups()
    {
        // 反転: エイム中(LT押下)は Hipfire(X)、解放時は Aim(+) を表示
        bool aiming = m_aimController != null && m_aimController.IsAiming;
        if (m_aimGroup != null) m_aimGroup.SetActive(!aiming);
        if (m_hipfireGroup != null) m_hipfireGroup.SetActive(aiming);
    }
}

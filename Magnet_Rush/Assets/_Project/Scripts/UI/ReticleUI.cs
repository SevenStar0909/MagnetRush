using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// レティクルUI。通常時は等倍、エイム時は中心に向かって線を寄せて表示。極性でスプライト切替、発射時にキック配信。
/// 依存: AimAbility, PoleAbility, PlayerEvents（Player Tag のオブジェクトから取得）
/// </summary>
public class ReticleUI : MonoBehaviour
{
    [SerializeField] private ReticleSettings m_settings;

    [Header("Reticle (X) Group")]
    [SerializeField] private ReticleLine[] m_hipfireLines;
    [SerializeField] private Image[] m_hipfireImages;
    [SerializeField] private Sprite m_hipfireLineS;
    [SerializeField] private Sprite m_hipfireLineN;

    [Header("Aim Transition Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float m_aimDistancePercent = 0.6f; // エイム時に中心までの距離の何%まで寄せるか (0で中心、1で通常位置)
    [SerializeField] private float m_transitionSpeed = 15f;  // 寄せる/戻すアニメーションの速さ

    private AimAbility m_aimController;
    private PoleAbility m_poleController;
    private PlayerEvents m_playerEvents;
    private MagneticPole m_currentPole = MagneticPole.S;

    private float m_currentAimAmount = 0f; // 0 (通常) ～ 1 (エイム)

    void Awake()
    {
        if (m_settings == null) { ChannelLogger.LogGuardReturn("UI", "ReticleSettings未設定"); return; }
        // レティクル線に、キック設定だけでなくエイム時の寄せ設定も渡すように変更
        ConfigureLines(m_hipfireLines);
    }

    void Start()
    {
        var player = GameObject.FindWithTag(GameTags.Player);
        if (player != null)
        {
            m_aimController = player.GetComponent<AimAbility>();
            m_poleController = player.GetComponent<PoleAbility>();
            m_playerEvents = player.GetComponent<PlayerEvents>();

            if (m_poleController != null)
            {
                m_poleController.OnPoleChanged += OnPoleChanged;
                m_currentPole = m_poleController.CurrentPole;
            }
            if (m_playerEvents != null && m_playerEvents.onShoot != null)
            {
                m_playerEvents.onShoot.AddListener(OnShoot);
            }
        }

        UpdateSprites();
    }

    void OnDestroy()
    {
        if (m_poleController != null)
            m_poleController.OnPoleChanged -= OnPoleChanged;
        if (m_playerEvents != null && m_playerEvents.onShoot != null)
            m_playerEvents.onShoot.RemoveListener(OnShoot);
    }

    void Update()
    {
        // 毎フレーム、エイムによる「寄せ具合」を計算して各ラインに伝える
        UpdateAimTransition();
    }

    private void ConfigureLines(ReticleLine[] lines)
    {
        if (lines == null) return;
        foreach (var l in lines)
        {
            if (l != null)
                // ReticleLine の Configure メソッドの引数を拡張（または別のメソッドにする）して、エイム設定も渡す
                l.Configure(m_settings.kickDistance, m_settings.maxKickDistance, m_settings.returnDuration, m_settings.returnCurve, m_aimDistancePercent);
        }
    }

    private void OnPoleChanged(MagneticPole pole)
    {
        m_currentPole = pole;
        UpdateSprites();
    }

    private void OnShoot()
    {
        if (m_hipfireLines == null) return;
        foreach (var line in m_hipfireLines)
        {
            if (line != null) line.Kick();
        }
    }

    private void UpdateSprites()
    {
        Sprite currentSprite = m_currentPole == MagneticPole.S ? m_hipfireLineS : m_hipfireLineN;

        if (m_hipfireImages != null)
            foreach (var img in m_hipfireImages)
                if (img != null) img.sprite = currentSprite;
    }

    /// <summary>
    /// エイム状態に応じて、中心への「寄せ具合」を滑らかに計算し、すべてのラインに伝える
    /// </summary>
    private void UpdateAimTransition()
    {
        if (m_hipfireLines == null) return;

        bool aiming = m_aimController != null && m_aimController.IsAiming;
        float targetAmount = aiming ? 1f : 0f;

        // 現在のエイム度合いから目標のエイム度合いへ滑らかに補間
        m_currentAimAmount = Mathf.Lerp(m_currentAimAmount, targetAmount, Time.deltaTime * m_transitionSpeed);

        // すべてのラインにエイム度合いを伝える
        foreach (var line in m_hipfireLines)
        {
            if (line != null) line.SetAimAmount(m_currentAimAmount);
        }
    }
}
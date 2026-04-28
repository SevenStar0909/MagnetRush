using UnityEngine;
using UnityEngine.InputSystem;

public class EnemyBossBaseA_Animator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("�쓮�Ώۂ� Animator�i���ݒ�Ȃ�q�I�u�W�F�N�g����擾�j")]
    [SerializeField] private Animator m_animator;

    [Tooltip("Hitbox�B���ݒ�Ȃ烋�[�g�̎q����擾")]
    [SerializeField] private Hitbox m_hitbox;

    [Tooltip("AI 司令塔。未設定なら親の GetComponentInParent<EnemyBossAI>()")]
    [SerializeField] private EnemyBossAI m_ai;

    [Tooltip("腕の打撃ヒットボックス。未設定なら子の GetComponentInChildren<BossArmHitbox>()")]
    [SerializeField] private BossArmHitbox m_armHitbox;

    [Header("Debug")]
    [SerializeField] private bool m_enableDebugInput = true;

    [Header("Animator Parameter Names (Inspector �P��ӏ��Ǘ�)")]
    [SerializeField] private string m_attackName = "Attack";
    [SerializeField] private string m_attackFinishedName = "AttackFinished";
    [SerializeField] private string m_beInterruptedName = "BeInterrupted";
    [SerializeField] private string m_stunEndName = "StunEnd";
    [SerializeField] private string m_canInterruptName = "CanInterrupt";
    [SerializeField] private string m_isStunnedName = "IsStunned";
    [SerializeField] private string m_attackMotionStateName = "AttackMotionAnim";
    [SerializeField] private string m_stunStateName = "AttackStunAnim";

    private int m_hAttack;
    private int m_hAttackFinished;
    private int m_hBeInterrupted;
    private int m_hStunEnd;
    private int m_hCanInterrupt;
    private int m_hIsStunned;

    void Awake()
    {
        if (m_animator == null)
            m_animator = GetComponentInChildren<Animator>();

        if (m_hitbox == null)
            m_hitbox = transform.root.GetComponentInChildren<Hitbox>();

        if (m_ai == null)
            m_ai = GetComponentInParent<EnemyBossAI>();

        if (m_armHitbox == null)
            m_armHitbox = GetComponentInChildren<BossArmHitbox>(includeInactive: true);

        m_hAttack = Animator.StringToHash(m_attackName);
        m_hAttackFinished = Animator.StringToHash(m_attackFinishedName);
        m_hBeInterrupted = Animator.StringToHash(m_beInterruptedName);
        m_hStunEnd = Animator.StringToHash(m_stunEndName);
        m_hCanInterrupt = Animator.StringToHash(m_canInterruptName);
        m_hIsStunned = Animator.StringToHash(m_isStunnedName);

        if (m_animator == null)
        {
            ChannelLogger.LogGuardReturn("Enemy", "EnemyBossBaseA_Animator.m_animator �����A�T�C���ł�");
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (m_hitbox != null)
            m_hitbox.OnHitEvent += HandleHit;
    }

    void OnDisable()
    {
        if (m_hitbox != null)
            m_hitbox.OnHitEvent -= HandleHit;
    }

    void Start()
    {
        ValidateAnimatorParameters();
    }

    void Update()
    {
        if (!m_enableDebugInput) return;

        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
            TriggerBeInterrupted();
    }

    public bool IsCanInterrupt => m_animator != null && m_animator.GetBool(m_hCanInterrupt);
    public bool IsStunned => m_animator != null && m_animator.GetBool(m_hIsStunned);

    public bool IsInAttackMotion
    {
        get
        {
            if (m_animator == null) return false;
            var info = m_animator.GetCurrentAnimatorStateInfo(0);
            return info.IsName(m_attackMotionStateName);
        }
    }

    public bool IsInStun
    {
        get
        {
            if (m_animator == null) return false;
            var info = m_animator.GetCurrentAnimatorStateInfo(0);
            return info.IsName(m_stunStateName);
        }
    }

    public void TriggerAttack()
    {
        if (m_animator != null) m_animator.SetTrigger(m_hAttack);
    }

    public void TriggerAttackFinished()
    {
        if (m_animator != null) m_animator.SetTrigger(m_hAttackFinished);
    }

    public void TriggerBeInterrupted()
    {
        if ((m_animator != null) && (m_animator.GetBool(m_hCanInterrupt)) == false) return;
        m_animator.SetTrigger(m_hBeInterrupted);
    }

    public void TriggerStunEnd()
    {
        if (m_animator != null) m_animator.SetTrigger(m_hStunEnd);
    }

    public void SetCanInterrupt(bool value)
    {
        if (m_animator != null) m_animator.SetBool(m_hCanInterrupt, value);
    }

    public void SetIsStunned(bool value)
    {
        if (m_animator != null) m_animator.SetBool(m_hIsStunned, value);
    }

    public void SetCanInterruptTrue()
    {
        SetCanInterrupt(true);
    }

    public void SetCanInterruptFalse()
    {
        SetCanInterrupt(false);
    }

    public void SetIsStunnedTrue()
    {
        SetIsStunned(true);
    }

    public void SetIsStunnedFalse()
    {
        SetIsStunned(false);
    }

    /// <summary>
    /// Hitbox ����̃q�b�g�C�x���g����������B��e�����瑦���f�g���K�[�𑗂�B
    /// </summary>
    private void HandleHit(HitData hit)
    {
        TriggerBeInterrupted();
    }

    /// <summary>AnimationEvent: 攻撃モーションの当たり判定窓開始</summary>
    public void EnableArmHitboxEvent()
    {
        if (m_armHitbox == null) { ChannelLogger.LogGuardReturn("Enemy", "BossArmHitbox 未設定"); return; }
        m_armHitbox.EnableHitbox();
    }

    /// <summary>AnimationEvent: 攻撃モーションの当たり判定窓終了</summary>
    public void DisableArmHitboxEvent()
    {
        if (m_armHitbox == null) { ChannelLogger.LogGuardReturn("Enemy", "BossArmHitbox 未設定"); return; }
        m_armHitbox.DisableHitbox();
    }

    /// <summary>AnimationEvent: AttackMotion クリップ末尾</summary>
    public void OnAttackFinishedEvent()
    {
        if (m_ai == null) { ChannelLogger.LogGuardReturn("Enemy", "EnemyBossAI 未設定"); return; }
        m_ai.OnAttackFinished();
    }

    /// <summary>AnimationEvent: AttackStun クリップ末尾</summary>
    public void OnStunEndEvent()
    {
        if (m_ai == null) { ChannelLogger.LogGuardReturn("Enemy", "EnemyBossAI 未設定"); return; }
        m_ai.OnStunEnd();
    }

    /// <summary>
    /// Animator Controller �ɕK�v�ȃp�����[�^����`����Ă��邩���`�F�b�N���A����Ȃ����̂�����΃G���[���o���B
    /// </summary>
    private void ValidateAnimatorParameters()
    {
        if (m_animator == null || m_animator.runtimeAnimatorController == null) return;

        var expected = new (string name, string purpose)[]
        {
            (m_attackName, "Attack (Trigger)"),
            (m_attackFinishedName, "AttackFinished (Trigger)"),
            (m_beInterruptedName, "BeInterrupted (Trigger)"),
            (m_stunEndName, "StunEnd (Trigger)"),
            (m_canInterruptName, "CanInterrupt (Bool)"),
            (m_isStunnedName, "IsStunned (Bool)"),
        };

        var existing = new System.Collections.Generic.HashSet<string>();
        foreach (var p in m_animator.parameters)
            existing.Add(p.name);

        foreach (var (name, purpose) in expected)
        {
            if (!existing.Contains(name))
                Debug.LogError(
                    $"[EnemyBossBaseA_Animator] Animator �p�����[�^ '{name}' ({purpose}) �� Controller �ɒ�`����Ă��܂���B",
                    this);
        }
    }
}

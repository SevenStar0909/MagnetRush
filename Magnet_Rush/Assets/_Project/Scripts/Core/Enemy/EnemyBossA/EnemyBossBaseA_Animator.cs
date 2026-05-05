using UnityEngine;
using UnityEngine.InputSystem;

public class EnemyBossBaseA_Animator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("駆動対象の Animator（未設定なら子オブジェクトから取得）")]
    [SerializeField] private Animator m_animator;

    [Tooltip("Hitbox。未設定ならルートの子から取得")]
    [SerializeField] private Hitbox m_hitbox;

    [Tooltip("AI(EnemyBossAI)。AnimationEvent で OnAttackFinished/OnStunEnd を転送する")]
    [SerializeField] private EnemyBossAI m_ai;

    [Tooltip("腕の近接Hitbox。AnimationEvent で Enable/Disable を転送する")]
    [SerializeField] private BossArmHitbox m_armHitbox;

    [Header("Debug")]
    [SerializeField] private bool m_enableDebugInput = true;

    [Header("Animator Parameter Names (Inspector 単一箇所管理)")]
    [SerializeField] private string m_attackName = "Attack";
    [SerializeField] private string m_attackFinishedName = "AttackFinished";
    [SerializeField] private string m_beInterruptedName = "BeInterrupted";
    [SerializeField] private string m_stunEndName = "StunEnd";
    [SerializeField] private string m_canInterruptName = "CanInterrupt";
    [SerializeField] private string m_isStunnedName = "IsStunned";

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
            m_ai = transform.root.GetComponentInChildren<EnemyBossAI>();

        if (m_armHitbox == null)
            m_armHitbox = transform.root.GetComponentInChildren<BossArmHitbox>(true);

        m_hAttack = Animator.StringToHash(m_attackName);
        m_hAttackFinished = Animator.StringToHash(m_attackFinishedName);
        m_hBeInterrupted = Animator.StringToHash(m_beInterruptedName);
        m_hStunEnd = Animator.StringToHash(m_stunEndName);
        m_hCanInterrupt = Animator.StringToHash(m_canInterruptName);
        m_hIsStunned = Animator.StringToHash(m_isStunnedName);

        if (m_animator == null)
        {
            ChannelLogger.LogGuardReturn("Enemy", "EnemyBossBaseA_Animator.m_animator が未アサインです");
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

    /// <summary>AttackStance または AttackMotion 中なら true。AI が攻撃中判定に使う。</summary>
    public bool IsAttacking
    {
        get
        {
            if (m_animator == null) return false;
            int hash = m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            return hash == s_hAttackStanceState || hash == s_hAttackMotionState;
        }
    }

    /// <summary>AttackMotion 中（振りかぶり～振り抜き）なら true。AI が腕Hitbox期待中の判定に使う。</summary>
    public bool IsInAttackMotion
    {
        get
        {
            if (m_animator == null) return false;
            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == s_hAttackMotionState;
        }
    }

    /// <summary>AttackStun 中なら true。Bool ではなく現在 State を見ることで AnimEvent 配線漏れに対しても堅牢。</summary>
    public bool IsStunned
    {
        get
        {
            if (m_animator == null) return false;
            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == s_hAttackStunState;
        }
    }

    // Animator State 名のハッシュ（State 名は EnemyBossA_Animator.controller の Layer0 上の State 名と一致する必要がある）
    private static readonly int s_hAttackStanceState = Animator.StringToHash("AttackStanceAnim");
    private static readonly int s_hAttackMotionState = Animator.StringToHash("AttackMotionAnim");
    private static readonly int s_hAttackStunState   = Animator.StringToHash("AttackStunAnim");

    // === AnimationEvent から直接呼ばれるエントリ（Forwarder ではなく Animator 直に置く） ===

    /// <summary>AttackMotion 振り始まりのAnimEventから呼ばれる。腕HitboxをONに。</summary>
    public void EnableArmHitboxEvent()
    {
        if (m_armHitbox != null) m_armHitbox.EnableHitbox();
    }

    /// <summary>AttackMotion 振り終わりのAnimEventから呼ばれる。腕HitboxをOFFに。</summary>
    public void DisableArmHitboxEvent()
    {
        if (m_armHitbox != null) m_armHitbox.DisableHitbox();
    }

    /// <summary>AttackMotion clip 末尾のAnimEventから呼ばれる。AIにStaggerへの遷移を通知。</summary>
    public void OnAttackFinishedEvent()
    {
        if (m_ai != null) m_ai.OnAttackFinished();
    }

    /// <summary>AttackStun clip 末尾のAnimEventから呼ばれる。AIにStaggerへの遷移を通知。</summary>
    public void OnStunEndEvent()
    {
        if (m_ai != null) m_ai.OnStunEnd();
    }

    /// <summary>
    /// Hitbox からのヒットイベントを処理する。被弾したら即中断トリガーを送る。
    /// </summary>
    private void HandleHit(HitData hit)
    {
        TriggerBeInterrupted();
    }

    /// <summary>
    /// Animator Controller に必要なパラメータが定義されているかをチェックし、足りないものがあればエラーを出す。
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
                    $"[EnemyBossBaseA_Animator] Animator パラメータ '{name}' ({purpose}) が Controller に定義されていません。",
                    this);
        }
    }
}

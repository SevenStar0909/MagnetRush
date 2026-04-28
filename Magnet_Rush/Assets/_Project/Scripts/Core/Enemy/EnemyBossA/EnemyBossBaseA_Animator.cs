using UnityEngine;
using UnityEngine.InputSystem;

public class EnemyBossBaseA_Animator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("駆動対象の Animator（未設定なら子オブジェクトから取得）")]
    [SerializeField] private Animator m_animator;

    [Tooltip("Hitbox。未設定ならルートの子から取得")]
    [SerializeField] private Hitbox m_hitbox;

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

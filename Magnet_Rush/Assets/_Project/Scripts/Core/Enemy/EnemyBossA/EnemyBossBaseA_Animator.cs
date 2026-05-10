using UnityEngine;
using UnityEngine.InputSystem;

public class EnemyBossBaseA_Animator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("駆動対象の Animator（未設定なら子オブジェクトから取得）")]
    [SerializeField] private Animator m_animator;

    [Tooltip("Hitbox。未設定ならルートの子から取得")]
    [SerializeField] private Hitbox m_hitbox;

    [Tooltip("AI(EnemyBossAI)。AnimationEvent で OnAttackFinished/OnStunEnd/OnRushFinished/OnMissileFinished を転送する")]
    [SerializeField] private EnemyBossAI m_ai;

    [Tooltip("腕の近接Hitbox。AnimationEvent で Enable/Disable を転送する")]
    [SerializeField] private BossArmHitbox m_armHitbox;

    [Header("Debug")]
    [SerializeField] private bool m_enableDebugInput = true;

    [Header("Animator Parameter Names (Inspector 単一箇所管理)")]
    [SerializeField] private string m_attackName              = "Attack";               //近接攻撃開始トリガー
    [SerializeField] private string m_attackFinishedName      = "AttackFinished";       //近接攻撃終了トリガー（idleへの遷移タイミング）
    [SerializeField] private string m_fireMissileName         = "FireMissile";          //ミサイル発射トリガー
    [SerializeField] private string m_fireMissileFinishedName = "FireMissileFinished";  //ミサイル発射終了トリガー（idleへの遷移タイミング）
    [SerializeField] private string m_attackRushName          = "AttackRush";           //ラッシュ攻撃トリガー
    [SerializeField] private string m_attackRushFinishedName  = "AttackRushFinished";   //ラッシュ攻撃終了トリガー（idleへの遷移タイミング）
    [SerializeField] private string m_isStunnedName           = "IsStunned";            //スタン中フラグ
    [SerializeField] private string m_canInterruptName        = "CanInterrupt";         //中断可能フラグ
    [SerializeField] private string m_stunEndName             = "StunEnd";              //スタン終了トリガー（idleへの遷移タイミング）
    [SerializeField] private string m_beInterruptedName       = "BeInterrupted";        //被弾中断トリガー

    private int m_hAttack;
    private int m_hAttackFinished;
    private int m_hFireMissile;
    private int m_hFireMissileFinished;
    private int m_hAttackRush;
    private int m_hAttackRushFinished;
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
        m_hFireMissile = Animator.StringToHash(m_fireMissileName);
        m_hFireMissileFinished = Animator.StringToHash(m_fireMissileFinishedName); // ★追加
        m_hAttackRush = Animator.StringToHash(m_attackRushName);
        m_hAttackRushFinished = Animator.StringToHash(m_attackRushFinishedName);   // ★追加

        m_hBeInterrupted = Animator.StringToHash(m_beInterruptedName);
        m_hStunEnd = Animator.StringToHash(m_stunEndName);
        m_hCanInterrupt = Animator.StringToHash(m_canInterruptName);
        m_hIsStunned = Animator.StringToHash(m_isStunnedName);

        if (m_animator == null)
        {
            ChannelLogger.LogGuardReturn("EnemyBossA", "EnemyBossBaseA_Animator.m_animator が未アサインです");
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

    public void TriggerAttackRush()
    {
        if (m_animator != null) m_animator.SetTrigger(m_hAttackRush);
    }

    public void TriggerAttackRushFinished()
    {
        if (m_animator != null) m_animator.SetTrigger(m_hAttackRushFinished);
    }

    public void TriggerMissile()
    {
        if (m_animator != null) m_animator.SetTrigger(m_hFireMissile);
    }

    public void TriggerMissileFinished()
    {
        if (m_animator != null) m_animator.SetTrigger(m_hFireMissileFinished);
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

    public void SetCanInterruptTrue() => SetCanInterrupt(true);
    public void SetCanInterruptFalse() => SetCanInterrupt(false);
    public void SetIsStunnedTrue() => SetIsStunned(true);
    public void SetIsStunnedFalse() => SetIsStunned(false);

    public bool IsAttacking
    {
        get
        {
            if (m_animator == null) return false;
            int hash = m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            return hash == s_hAttackStanceState || hash == s_hAttackMotionState;
        }
    }

    public bool IsInAttackMotion
    {
        get
        {
            if (m_animator == null) return false;
            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == s_hAttackMotionState;
        }
    }

    public bool IsInRush
    {
        get
        {
            if (m_animator == null) return false;
            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == s_hRushState;
        }
    }

    public bool IsInMissile
    {
        get
        {
            if (m_animator == null) return false;
            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == s_hMissileState;
        }
    }

    public bool IsStunned
    {
        get
        {
            if (m_animator == null) return false;
            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == s_hAttackStunState;
        }
    }

    private static readonly int s_hAttackStanceState = Animator.StringToHash("AttackStanceAnim");
    private static readonly int s_hAttackMotionState = Animator.StringToHash("AttackMotionAnim");
    private static readonly int s_hAttackStunState = Animator.StringToHash("AttackStunAnim");
    private static readonly int s_hAttackStaggerState = Animator.StringToHash("StaggerAnim");
    private static readonly int s_hRushState = Animator.StringToHash("RushAnim");
    private static readonly int s_hMissileState = Animator.StringToHash("MissileAnim");

    public void EnableArmHitboxEvent()
    {
        if (m_armHitbox != null) m_armHitbox.EnableHitbox();
    }

    public void DisableArmHitboxEvent()
    {
        if (m_armHitbox != null) m_armHitbox.DisableHitbox();
    }

    public void OnAttackFinishedEvent()
    {
        TriggerAttackFinished(); // ★追加：Animator側の遷移条件を満たす
        if (m_ai != null) m_ai.OnAttackFinished();
    }

    public void OnStunEndEvent()
    {
        TriggerStunEnd(); // ★追加
        if (m_ai != null) m_ai.OnStunEnd();
    }

    public void OnRushFinishedEvent()
    {
        TriggerAttackRushFinished(); // ★追加：RushAnim -> Idle の条件を満たす
        if (m_ai != null) m_ai.OnRushFinished();
    }

    public void OnRushFinished()
    {
        if (m_ai != null) m_ai.OnRushFinished();
    }

    public void OnMissileFinishedEvent()
    {
        TriggerMissileFinished(); // ★追加
        if (m_ai != null) m_ai.OnMissileFinished();
    }

    private void HandleHit(HitData hit)
    {
        TriggerBeInterrupted();
    }

    private void ValidateAnimatorParameters()
    {
        if (m_animator == null || m_animator.runtimeAnimatorController == null) return;

        var expected = new (string name, string purpose)[]
        {
            (m_attackName, "Attack (Trigger)"),
            (m_attackFinishedName, "AttackFinished (Trigger)"),
            (m_fireMissileName, "FireMissile (Trigger)"),
            (m_fireMissileFinishedName, "FireMissileFinished (Trigger)"),
            (m_attackRushName, "AttackRush (Trigger)"),
            (m_attackRushFinishedName,"AttackRushFinished (Trigger)"),
            (m_canInterruptName, "CanInterrupt (Bool)"),
            (m_beInterruptedName, "BeInterrupted (Trigger)"),
            (m_isStunnedName, "IsStunned (Bool)"),
            (m_stunEndName, "StunEnd (Trigger)"),
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
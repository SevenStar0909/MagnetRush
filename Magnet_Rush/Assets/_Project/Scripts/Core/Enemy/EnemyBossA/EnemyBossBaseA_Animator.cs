using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class EnemyBossBaseA_Animator : MonoBehaviour
{
    private Coroutine m_freezeRoutine;

    [Header("References")]
    [Tooltip("駆動対象の Animator（未設定なら子オブジェクトから取得）")]
    [SerializeField] private Animator m_animator;

    [Tooltip("AI(EnemyBossAI)。AnimationEvent で OnAttackFinished/OnStunEnd/OnRushFinished/OnMissileFinished を転送する")]
    [SerializeField] private EnemyBossAI m_ai;

    [Tooltip("右手攻撃と Rush 攻撃の Hitbox 管理")]
    [SerializeField] private BossAttackHitboxes m_attackHitboxes;

    /// <summary>
    /// ミサイル生成用
    /// </summary>
    [Tooltip("ボス基底。ミサイルのターゲット取得に使用")]
    [SerializeField] private EnemyBossBase m_boss;
    // RUSH エフェクト on off 
    [Tooltip("RUSH エフェクト")]
    [SerializeField] private ParticleSystem m_rushEffect;
    [Tooltip("Dust エフェクト")]
    [SerializeField] private ParticleSystem m_dustEffect;

    [Header("ふり降ろし砂埃")]
    [Tooltip("右手が地面についた時に出す Dust の位置調整。AttackHitboxの地面中心からワールド座標でずらす")]
    [SerializeField] private Vector3 m_armImpactDustOffset = new Vector3(0f, 0.05f, 0f);
    [Tooltip("振り下ろし着地時の Dust サイズを右手 AttackHitbox の横幅に合わせる")]
    [SerializeField] private bool m_matchArmImpactDustScaleToHitbox = true;
    [Tooltip("右手 AttackHitbox 基準の Dust サイズ倍率。小さくしたい時はこの値を下げる")]
    [SerializeField, Range(0.1f, 2f)] private float m_armImpactDustScaleMultiplier = 0.55f;

    //[Tooltip("ミサイル生成位置。未設定ならこのオブジェクト位置を使用")]
    //[SerializeField] private Transform[] m_missileSpawnPoints;

    [Header("Debug")]
    [SerializeField] private bool m_enableDebugInput = true;

    [Header("Animator Parameter Names (Inspector 単一箇所管理)")]
    [SerializeField] private string m_attackName = "Attack";               //近接攻撃開始トリガー
    [SerializeField] private string m_attackFinishedName = "AttackFinished";       //近接攻撃終了トリガー（idleへの遷移タイミング）
    [SerializeField] private string m_fireMissileName = "FireMissile";          //ミサイル発射トリガー
    [SerializeField] private string m_fireMissileFinishedName = "FireMissileFinished";  //ミサイル発射終了トリガー（idleへの遷移タイミング）
    [SerializeField] private string m_attackRushName = "AttackRush";           //ラッシュ攻撃トリガー
    [SerializeField] private string m_attackRushFinishedName = "AttackRushFinished";   //ラッシュ攻撃終了トリガー（idleへの遷移タイミング）
    [SerializeField] private string m_beInterruptedName = "BeInterrupted";        //被弾中断トリガー

    [SerializeField] private string m_isStunnedName = "IsStunned";            //スタン中フラグ
    [SerializeField] private string m_stunEndName = "StunEnd";              //スタン終了トリガー（idleへの遷移タイミング）
    [SerializeField] private string m_staggerEndName = "StaggerEnd";              //スタガー終了トリガー（idleへの遷移タイミング）

    private int m_hAttack;
    private int m_hAttackFinished;
    private int m_hFireMissile;
    private int m_hFireMissileFinished;
    private int m_hAttackRush;
    private int m_hAttackRushFinished;
    private int m_hBeInterrupted;

    private int m_hStunEnd;
    private int m_hIsStunned;
    private int m_hStaggerEnd;

    private Vector3 m_dustDefaultLocalPosition;
    private Quaternion m_dustDefaultLocalRotation;
    private Vector3 m_dustDefaultLocalScale;
    private bool m_hasDustDefaultTransform;

    void Awake()
    {
        if (m_animator == null)
            m_animator = GetComponentInChildren<Animator>();

        if (m_ai == null)
            m_ai = transform.root.GetComponentInChildren<EnemyBossAI>();

        if (m_boss == null)
            m_boss = transform.root.GetComponentInChildren<EnemyBossBase>();

        if (m_attackHitboxes == null)
            m_attackHitboxes = transform.root.GetComponentInChildren<BossAttackHitboxes>(true);

        m_hAttack = Animator.StringToHash(m_attackName);
        m_hAttackFinished = Animator.StringToHash(m_attackFinishedName);
        m_hFireMissile = Animator.StringToHash(m_fireMissileName);
        m_hFireMissileFinished = Animator.StringToHash(m_fireMissileFinishedName);
        m_hAttackRush = Animator.StringToHash(m_attackRushName);
        m_hAttackRushFinished = Animator.StringToHash(m_attackRushFinishedName);

        m_hBeInterrupted = Animator.StringToHash(m_beInterruptedName);

        m_hStunEnd = Animator.StringToHash(m_stunEndName);
        m_hIsStunned = Animator.StringToHash(m_isStunnedName);
        m_hStaggerEnd = Animator.StringToHash(m_staggerEndName);

        CacheDustDefaultTransform();

        if (m_animator == null)
        {
            ChannelLogger.LogGuardReturn("EnemyBossA", "EnemyBossBaseA_Animator.m_animator が未アサインです");
            enabled = false;
        }
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

    // Triggerとは
    // Animator Controller のパラメータの一種で、SetTrigger() で発火させると、Animator内の遷移条件として使用できる。
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

    // staggerにいく
    // 仕様: AttackStance/AttackMotion 中は常に中断可能（手を叩けば Stagger 発火）。
    public void TriggerBeInterrupted()
    {
        if (m_animator == null) return;
        m_animator.SetTrigger(m_hBeInterrupted);
    }

    public void TriggerStunEnd()
    {
        if (m_animator != null) m_animator.SetTrigger(m_hStunEnd);
    }

    public void TriggerStaggerEnd()
    {
        if (m_animator != null) m_animator.SetTrigger(m_hStaggerEnd);
    }

    // setとは
    // Animator Controller のパラメータの一種で、SetBool() で true/false を設定する。Animator内の遷移条件や、Isプロパティの判定に使用できる。
    public void SetIsStunned(bool value)
    {
        if (m_animator != null) m_animator.SetBool(m_hIsStunned, value);
    }
    public void SetIsStunnedTrue() => SetIsStunned(true);
    public void SetIsStunnedFalse() => SetIsStunned(false);

    // Isとは
    // Animatorの現在の状態を取得するためのプロパティ。Animator内のステート名をハッシュ化して比較する。
    public virtual bool IsAttacking
    {
        get
        {
            if (m_animator == null) return false;
            int hash = m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            return IsState(hash, s_hAttackStanceState, s_hAttackMotionState);
        }
    }

    public virtual bool IsInAttackMotion
    {
        get
        {
            if (m_animator == null) return false;
            return IsState(m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash, s_hAttackMotionState);
        }
    }

    public virtual bool IsInAttackStance
    {
        get
        {
            if (m_animator == null) return false;
            return IsState(m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash, s_hAttackStanceState);
        }
    }

    public virtual bool IsInRush
    {
        get
        {
            if (m_animator == null) return false;
            return IsState(m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash, s_hRushState);
        }
    }

    public virtual bool IsInMissile
    {
        get
        {
            if (m_animator == null) return false;
            return IsState(m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash, s_hMissileState);
        }
    }

    public virtual bool IsStunned
    {
        get
        {
            if (m_animator == null) return false;
            int hash = m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            return IsState(hash, s_hAttackStunState, s_hAttackStunkeepState);
        }
    }

    public virtual bool IsInStagger
    {
        get
        {
            if (m_animator == null) return false;
            int hash = m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            return IsState(hash, s_hAttackStaggerState, s_hAttackStaggerkeepState);
        }
    }

    public virtual bool IsStanding
    {
        get
        {
            if (m_animator == null) return false;
            int hash = m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            return IsState(hash, s_hStunToStandState, s_hStaggerToStandState);
        }
    }

    public virtual bool IsIdle
    {
        get
        {
            if (m_animator == null) return false;
            return IsState(m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash, s_hIdleState);
        }
    }

    // ステート名をハッシュ化してキャッシュ。Animator内のステートと完全一致させる必要がある。
    private static readonly int s_hAttackStanceState = Animator.StringToHash("AttackStanceAnim");
    private static readonly int s_hAttackMotionState = Animator.StringToHash("AttackMotionAnim");
    private static readonly int s_hAttackStunState = Animator.StringToHash("StunAnim");
    private static readonly int s_hAttackStunkeepState = Animator.StringToHash("StunkeepAnim");
    private static readonly int s_hAttackStaggerState = Animator.StringToHash("StaggerAnim");
    private static readonly int s_hAttackStaggerkeepState = Animator.StringToHash("StaggerkeepAnim");
    private static readonly int s_hStunToStandState = Animator.StringToHash("StunToStand");
    private static readonly int s_hStaggerToStandState = Animator.StringToHash("StaggerToStand");
    private static readonly int s_hIdleState = Animator.StringToHash("Idle");
    private static readonly int s_hRushState = Animator.StringToHash("RushAnim");
    private static readonly int s_hMissileState = Animator.StringToHash("MissileAnim");

    protected int CurrentStateHash => m_animator != null ? m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash : 0;
    protected static bool IsState(int hash, int state) => hash == state;
    protected static bool IsState(int hash, int stateA, int stateB) => hash == stateA || hash == stateB;

    // AnimationEvent で呼び出す関数群。攻撃の当たり判定の有効化/無効化や、AIへの通知を行う。
    public void EnableArmHitboxEvent()
    {
        if (m_attackHitboxes != null) m_attackHitboxes.EnableArmHitbox();
        PlayArmImpactDustEffect();
        Sound.Play(SoundData.CueSheet.SE, SoundData.SE.BossEnemySlam);
        ChannelLogger.Log("EnemyBoss", $"BossArmEvent step2 is Triggered ");
    }

    public void DisableArmHitboxEvent()
    {
        if (m_attackHitboxes != null) m_attackHitboxes.DisableArmHitbox();
    }

    public void EnableRushHitboxEvent()
    {
        if (m_attackHitboxes != null) m_attackHitboxes.EnableRushHitbox();
        ChannelLogger.Log("EnemyBoss", $"BossRushEvent step2 is Triggered ");
    }

    public void DisableRushHitboxEvent()
    {
        if (m_attackHitboxes != null) m_attackHitboxes.DisableRushHitbox();
    }

    public void EnableWindEffectEvent()
    {
        if (m_rushEffect != null)
        {
            m_rushEffect.Play();
        }
    }

    public void DisableWindEffectEvent()
    {
        if (m_rushEffect != null)
        {
            m_rushEffect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear );
        }

    }

    public void DisableWindEffectAnimationEvent()
    {
        DisableWindEffectEvent();
        // Wind 終了後は回復姿勢。残りの Rush アニメーションの Root Motion で本体を動かさない。
        if (m_animator != null) m_animator.applyRootMotion = false;
        if (m_ai != null) m_ai.StopRushMovement();
    }
    public void EnableDustEffectEvent()
    {
        if (m_dustEffect != null)
        {
            RestoreDustDefaultTransform();
            m_dustEffect.Play();
        }
    }

    public void DisableDustEffectEvent()
    {
        if (m_dustEffect != null)
        {
            m_dustEffect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear );
        }

    }

    public void OnAttackFinishedEvent()
    {
        TriggerAttackFinished(); // ★追加：Animator側の遷移条件を満たす
        if (m_ai != null) m_ai.OnAttackFinished();
    }

    public void OnRushFinishedEvent()
    {
        TriggerAttackRushFinished(); // ★追加：RushAnim -> Idle の条件を満たす
        if (m_ai != null) m_ai.OnRushFinished();
    }

    public void OnMissileFinishedEvent()
    {
        TriggerMissileFinished(); // ★追加
        if (m_ai != null) m_ai.OnMissileFinished();
    }

    // ミサイル生成イベント。AnimationEvent で呼び出す。ミサイルPrefabからインスタンスを生成し、プレイヤーの方向に向けて発射する。
    public void OnMissileFireEvent()
    {
        if (m_ai != null) m_ai.OnMissileFireEvent();
    }

    /// <summary>
    /// AnimationEvent から呼ぶ。指定秒だけ Animator.speed=0 でフリーズしてから 1 に戻す。
    /// クリップ内で「腕上げきった瞬間」「地面激突した瞬間」など特定フレームに打って溜め/硬直を作る。
    /// 多重呼び出し時は前のコルーチンをキャンセルして新しいタイマーで上書きする。
    /// </summary>
    public void FreezeAnim(float seconds)
    {
        if (m_animator == null) { ChannelLogger.LogGuardReturn("EnemyBossA", "Animator未設定"); return; }
        if (seconds <= 0f) { ChannelLogger.LogGuardReturn("EnemyBossA", $"FreezeAnim 秒数が0以下: {seconds}"); return; }

        if (m_freezeRoutine != null) StopCoroutine(m_freezeRoutine);
        m_freezeRoutine = StartCoroutine(FreezeRoutine(seconds));
    }

    private IEnumerator FreezeRoutine(float seconds)
    {
        m_animator.speed = 0f;
        yield return new WaitForSeconds(seconds);
        m_animator.speed = 1f;
        m_freezeRoutine = null;
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
                (m_beInterruptedName, "BeInterrupted (Trigger)"),
                (m_isStunnedName, "IsStunned (Bool)"),
                (m_stunEndName, "StunEnd (Trigger)"),
                (m_staggerEndName, "StaggerEnd (Trigger)"),
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

    public void ResetStunEnd()
    {
        if (m_animator != null) m_animator.ResetTrigger(m_hStunEnd);
    }

    public void ResetStaggerEnd()
    {
        if (m_animator != null) m_animator.ResetTrigger(m_hStaggerEnd);
    }

    private void CacheDustDefaultTransform()
    {
        if (m_dustEffect == null) return;

        Transform dustTransform = m_dustEffect.transform;
        m_dustDefaultLocalPosition = dustTransform.localPosition;
        m_dustDefaultLocalRotation = dustTransform.localRotation;
        m_dustDefaultLocalScale = dustTransform.localScale;
        m_hasDustDefaultTransform = true;
    }

    private void RestoreDustDefaultTransform()
    {
        if (m_dustEffect == null || !m_hasDustDefaultTransform) return;

        Transform dustTransform = m_dustEffect.transform;
        dustTransform.localPosition = m_dustDefaultLocalPosition;
        dustTransform.localRotation = m_dustDefaultLocalRotation;
        dustTransform.localScale = m_dustDefaultLocalScale;
    }

    private void PlayArmImpactDustEffect()
    {
        if (m_dustEffect == null) return;

        if (m_attackHitboxes != null
            && m_attackHitboxes.TryGetArmHitboxDustPose(out Vector3 position, out float diameter))
        {
            Transform dustTransform = m_dustEffect.transform;
            dustTransform.position = position + m_armImpactDustOffset;

            if (m_matchArmImpactDustScaleToHitbox)
            {
                float parentScale = GetMaxAbsScale(dustTransform.parent);
                float targetScale = diameter * m_armImpactDustScaleMultiplier;
                float localScale = parentScale > Mathf.Epsilon ? targetScale / parentScale : targetScale;
                dustTransform.localScale = Vector3.one * Mathf.Max(0.01f, localScale);
            }
        }

        m_dustEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        m_dustEffect.Play(true);
    }

    private static float GetMaxAbsScale(Transform target)
    {
        if (target == null) return 1f;

        Vector3 scale = target.lossyScale;
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }
}

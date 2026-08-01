using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBossBase))]
public class EnemyBoss02AI : MonoBehaviour, IStabReceiver, IDamageGuard
{
    private enum Boss02State
    {
        Idle,
        Attack,
        Move,
        MoveEnd,
        RushStance,
        Rush,
        Down,
        DownEnd
    }

    [Header("References")]
    [Tooltip("Boss02のAnimator制御ラッパー。通常は子オブジェクトから自動取得されます。")]
    [SerializeField] private EnemyBoss02Animator m_animator;
    [Tooltip("通常攻撃と突進攻撃の当たり判定を管理するコンポーネント。")]
    [SerializeField] private EnemyBoss02Hitboxes m_hitboxes;
    [Tooltip("突進中にBoss自身へ磁極を付与するためのMagnetizable。未設定なら同じGameObjectから取得します。")]
    [SerializeField] private Magnetizable m_selfMagnetizable;
    [Tooltip("プレイヤーのStab演出で参照する位置。未設定ならBoss本体のTransformを使用します。")]
    [SerializeField] private Transform m_stabAnchor;
    [Tooltip("Stab処刑攻撃時に使用する演出設定。")]
    [SerializeField] private StabFinisherSettings m_stabFinisherSettings;

    [Header("Battle")]
    [Tooltip("trueの間だけBoss02のAIが行動します。falseではIdleで待機します。")]
    public bool isBattleing = false;
    [Tooltip("状態遷移ログを出すかどうか。調整中の確認用です。")]
    [SerializeField] private bool m_logStateChange = true;

    [Header("Decision Ranges")]
    [Tooltip("近距離判定。AttackRange外かつこの距離以内では、直線移動で接近してから通常攻撃します。")]
    [SerializeField] private float m_shortRange = 20.0f;
    [Tooltip("中距離判定。ShortRange外かつこの距離以内では、弧を描く接近か突進を重みで選びます。この距離より外では突進のみ行います。")]
    [SerializeField] private float m_meleeRange = 35.0f;
    [Tooltip("中距離で弧を描く接近を選ぶ重み。Rush Weightとの比率で確率が決まります。")]
    [SerializeField] private float m_arcApproachWeight = 50.0f;
    [Tooltip("中距離で突進を選ぶ重み。Arc Approach Weightとの比率で確率が決まります。")]
    [SerializeField] private float m_rushWeight = 50.0f;
    [Tooltip("この回数だけMoveに入った後、次の行動選択を必ずRushにします。0以下で無効です。")]
    [SerializeField] private int m_forceRushAfterMoveCount = 3;

    [Header("Attack")]
    [Tooltip("通常攻撃を開始できる距離。この距離以内ならその場でひっかき攻撃します。")]
    [SerializeField] private float m_attackRange = 4.0f;
    [Tooltip("通常攻撃状態の継続時間。終了するとIdleへ戻ります。")]
    [SerializeField] private float m_attackDuration = 3.56f;
    [Tooltip("通常攻撃の当たり判定を有効にする時間。Attack開始からの秒数です。")]
    [SerializeField] private float m_attackHitStartTime = 1.33f;
    [Tooltip("通常攻撃の当たり判定を無効にする時間。Attack開始からの秒数です。")]
    [SerializeField] private float m_attackHitEndTime = 1.75f;

    [Header("Move")]
    [Tooltip("接近移動を続ける最大時間。時間切れになると攻撃せずMoveEndへ移行します。")]
    [SerializeField] private float m_moveDuration = 2.0f;
    [Tooltip("MoveEnd状態の継続時間。MoveEndからAttackへつなぐ時の待ち時間です。")]
    [SerializeField] private float m_moveEndDuration = 0.77f;
    [Tooltip("移動速度倍率の予備値。直線/弧移動の倍率が0以下の場合に使用します。")]
    [SerializeField] private float m_moveSpeedMultiplier = 1.35f;
    [Tooltip("直線接近時の移動速度倍率。EnemyBossSettingsのMove Speedに掛けられます。")]
    [SerializeField] private float m_directMoveSpeedMultiplier = 2.0f;
    [Tooltip("弧を描く接近時の移動速度倍率。EnemyBossSettingsのMove Speedに掛けられます。")]
    [SerializeField] private float m_arcMoveSpeedMultiplier = 1.6f;
    [Tooltip("接近移動を止める距離。AttackRangeより小さい場合はAttackRangeが優先されます。")]
    [SerializeField] private float m_moveStopDistance = 4.0f;
    [Tooltip("trueにすると接近完了後にMoveEndを飛ばして即Attackします。falseならMoveEndからAttackへ遷移します。")]
    [SerializeField] private bool m_skipMoveEndBeforeAttack = false;
    [Tooltip("弧を描く接近時の横方向成分の強さ。大きいほど回り込みが強くなります。")]
    [SerializeField] private float m_arcMoveLateralStrength = 0.75f;

    [Header("Rush")]
    [Tooltip("突進前の溜め時間。Rush Stanceアニメーションを再生してプレイヤーを狙います。")]
    [SerializeField] private float m_rushStanceDuration = 2.63f;
    [Tooltip("突進で開始位置から目標位置へ移動する時間。短いほど高速になります。")]
    [SerializeField] private float m_rushTravelSeconds = 0.35f;
    [Tooltip("突進後の状態継続時間。突進移動時間より長い場合、残り時間はRushEndとして待機します。")]
    [SerializeField] private float m_rushEndDuration = 2.66f;
    [Tooltip("突進の目標をプレイヤー位置からどれだけ奥へ伸ばすか。大きいほど通り過ぎます。")]
    [SerializeField] private float m_rushOvershootDistance = 4.0f;
    [Tooltip("突進軌道の山なり高さ。0にすると直線突進になります。")]
    [SerializeField] private float m_rushArcHeight = 2.0f;
    [Tooltip("同極反発が発生した時、Bossが弾き返される移動時間。")]
    [SerializeField] private float m_rushRepelDuration = 0.35f;
    [Tooltip("同極反発で弾き返される時の山なり高さ。")]
    [SerializeField] private float m_rushRepelArcHeight = 2.0f;
    [Tooltip("突進中にBoss自身へ付与する磁極。プレイヤーが同極なら反発してDownへ移行します。")]
    [SerializeField] private MagneticPole m_rushAuraPole = MagneticPole.S;

    [Header("Down")]
    [Tooltip("trueならDown状態から一定時間後に自動で起き上がります。falseならStabなど外部処理待ちになります。")]
    [SerializeField] private bool m_autoRecoverFromDown = true;
    [Tooltip("Down状態から自動で起き上がり始めるまでの時間。")]
    [SerializeField] private float m_downRecoverDelay = 5.0f;
    [Tooltip("DownEnd状態の継続時間。終了するとIdleへ戻ります。")]
    [SerializeField] private float m_downEndDuration = 4.9f;

    private EnemyBossBase m_boss;
    private EnemyBossSettings m_settings;
    private Transform m_player;
    private Stamina m_stamina;
    private Health m_health;
    private Entity m_selfEntity;
    private Hitbox[] m_bodyHitboxes = Array.Empty<Hitbox>();

    private Boss02State m_state = Boss02State.Idle;
    private float m_stateTimer;
    private float m_cooldownTimer;
    private bool m_attackHitboxEnabled;
    private bool m_rushHitboxEnabled;
    private bool m_attackAfterMoveEnd;
    private bool m_useArcMove;
    private int m_moveEntryCount;
    private float m_arcMoveSideSign = 1f;
    private Vector3 m_rushStartPosition;
    private Vector3 m_rushTargetPosition;
    private bool m_rushRepelActive;
    private Vector3 m_rushRepelStartPosition;
    private Vector3 m_rushRepelTargetPosition;

    public bool CanReceiveStab => m_state == Boss02State.Down;
    public Transform StabAnchor => m_stabAnchor != null ? m_stabAnchor : transform;
    public int StabChoreographyIndex => 1;
    public StabFinisherSettings StabFinisherSettings => m_stabFinisherSettings;
    public bool CanTakeDamage(HitData hit) => m_state != Boss02State.DownEnd;
    private float AttackRange => Mathf.Max(0f, m_attackRange);
    private float ShortRange => Mathf.Max(AttackRange, m_shortRange);
    private float MeleeRange => Mathf.Max(ShortRange, m_meleeRange);
    private float ApproachStopDistance => Mathf.Max(m_moveStopDistance, AttackRange);

    private void Awake()
    {
        m_boss = GetComponent<EnemyBossBase>();
        m_stamina = GetComponent<Stamina>();
        m_health = GetComponent<Health>();
        m_selfEntity = GetComponent<Entity>();
        m_bodyHitboxes = GetComponentsInChildren<Hitbox>(true);

        if (m_animator == null)
            m_animator = GetComponentInChildren<EnemyBoss02Animator>(true);

        if (m_hitboxes == null)
            m_hitboxes = GetComponentInChildren<EnemyBoss02Hitboxes>(true);

        if (m_selfMagnetizable == null)
            m_selfMagnetizable = GetComponent<Magnetizable>();
    }

    private void OnEnable()
    {
        if (m_stamina != null)
            m_stamina.OnBreak += EnterDown;

        for (int i = 0; i < m_bodyHitboxes.Length; i++)
        {
            if (m_bodyHitboxes[i] != null)
                m_bodyHitboxes[i].OnHitEvent += OnBodyHit;
        }
    }

    private void OnDisable()
    {
        if (m_stamina != null)
            m_stamina.OnBreak -= EnterDown;

        for (int i = 0; i < m_bodyHitboxes.Length; i++)
        {
            if (m_bodyHitboxes[i] != null)
                m_bodyHitboxes[i].OnHitEvent -= OnBodyHit;
        }

        m_hitboxes?.DisableAll();
        ClearSelfMagnet();
    }

    private void Start()
    {
        ResolvePlayer();
        m_settings = m_boss != null ? m_boss.StatusData : null;
        m_cooldownTimer = m_settings != null ? m_settings.attackInterval : 0f;
    }

    private void Update()
    {
        if (m_boss == null || m_settings == null || m_animator == null)
            return;

        if (m_player == null)
            ResolvePlayer();

        float dt = Time.deltaTime;
        m_stateTimer += dt;
        m_cooldownTimer = Mathf.Max(0f, m_cooldownTimer - dt);

        if (!isBattleing)
        {
            if (m_state != Boss02State.Idle)
                ChangeState(Boss02State.Idle);

            ClearSelfMagnet();
            m_boss.SlowDown(dt);
            return;
        }

        switch (m_state)
        {
            case Boss02State.Idle:
                TickIdle(dt);
                break;
            case Boss02State.Attack:
                TickAttack(dt);
                break;
            case Boss02State.Move:
                TickMove(dt);
                break;
            case Boss02State.MoveEnd:
                TickMoveEnd(dt);
                break;
            case Boss02State.RushStance:
                TickRushStance(dt);
                break;
            case Boss02State.Rush:
                TickRush(dt);
                break;
            case Boss02State.Down:
                TickDown(dt);
                break;
            case Boss02State.DownEnd:
                TickDownEnd(dt);
                break;
        }
    }

    private void LateUpdate()
    {
        ClearDestroyedSelfMagnetField();
    }

    public void SetBattlingOn() => SetBattleing(true);
    public void setisBattleing() => SetBattleing(!isBattleing);
    public void setisBattleing(bool value) => SetBattleing(value);
    public void SetBattleing(bool value) => isBattleing = value;

    public void OnStabHit(StabHitData data)
    {
        if (!CanReceiveStab)
            return;

        ApplyStabDamage(data);
        if (m_stamina != null)
            m_stamina.ResetStamina();

        m_rushRepelActive = false;
        m_animator.TriggerDownEnd();
        ChangeState(Boss02State.DownEnd);
    }

    public bool TryStartRushRepel(Collider playerCollider)
    {
        if (m_state != Boss02State.Rush || !m_rushHitboxEnabled)
            return false;

        Magnetizable playerMagnetizable = playerCollider != null
            ? playerCollider.GetComponentInParent<Magnetizable>()
            : null;

        if (!IsSamePoleAsSelf(playerMagnetizable))
            return false;

        Vector3 playerPosition = playerCollider != null ? playerCollider.transform.position : transform.position;
        m_rushRepelStartPosition = transform.position;
        m_rushRepelTargetPosition = (m_rushStartPosition + playerPosition) * 0.5f;
        m_rushRepelTargetPosition.y = m_rushRepelStartPosition.y;
        m_rushRepelActive = true;

        m_hitboxes?.DisableAll();
        m_attackHitboxEnabled = false;
        m_rushHitboxEnabled = false;
        ClearSelfMagnet();
        EnterDown();
        return true;
    }

    private void TickIdle(float dt)
    {
        m_boss.SlowDown(dt);
        FacePlayer(dt);

        if (m_player == null || m_cooldownTimer > 0f)
            return;

        if (ShouldForceRushByMoveCount())
        {
            BeginRush();
            return;
        }

        float distance = DistanceToPlayer();
        if (distance <= AttackRange)
        {
            BeginAttack();
            return;
        }

        if (distance <= ShortRange)
        {
            BeginMove(useArcMove: false, attackAfterMoveEnd: true);
            return;
        }

        if (distance <= MeleeRange)
        {
            if (ChooseRushOverArcApproach())
                BeginRush();
            else
                BeginMove(useArcMove: true, attackAfterMoveEnd: true);

            return;
        }

        BeginRush();
    }

    private void TickAttack(float dt)
    {
        m_boss.SlowDown(dt);
        FacePlayer(dt, m_settings.attackMotionFaceDeadZoneDeg);

        if (!m_attackHitboxEnabled && m_stateTimer >= m_attackHitStartTime)
        {
            m_attackHitboxEnabled = true;
            m_hitboxes?.EnableAttack();
        }

        if (m_attackHitboxEnabled && m_stateTimer >= m_attackHitEndTime)
        {
            m_attackHitboxEnabled = false;
            m_hitboxes?.DisableAttack();
        }

        if (m_stateTimer >= m_attackDuration)
            FinishAction();
    }

    private void TickMove(float dt)
    {
        if (m_player == null)
        {
            EndMove(attackAfterMoveEnd: false);
            return;
        }

        Vector3 toPlayer = GetDirectionToPlayer();
        float distance = DistanceToPlayer();
        if (distance <= ApproachStopDistance)
        {
            if (m_attackAfterMoveEnd && m_skipMoveEndBeforeAttack)
            {
                BeginAttack();
                return;
            }

            EndMove(attackAfterMoveEnd: true);
            return;
        }

        if (m_stateTimer >= m_moveDuration)
        {
            EndMove(attackAfterMoveEnd: false);
            return;
        }

        Vector3 moveDirection = m_useArcMove ? GetArcMoveDirection(toPlayer) : toPlayer;
        m_boss.AccelerateToward(moveDirection, dt, GetCurrentMoveSpeedMultiplier());
    }

    private void TickMoveEnd(float dt)
    {
        m_boss.SlowDown(dt);
        if (m_stateTimer < m_moveEndDuration)
            return;

        if (m_attackAfterMoveEnd && m_player != null && DistanceToPlayer() <= ApproachStopDistance)
        {
            BeginAttack();
            return;
        }

        m_animator.TriggerIdle();
        FinishAction();
    }

    private void TickRushStance(float dt)
    {
        m_boss.SlowDown(dt);
        FacePlayer(dt);

        if (m_stateTimer < m_rushStanceDuration)
            return;

        LockRushTarget();
        m_animator.TriggerRushEnd();
        m_rushStartPosition = transform.position;
        m_rushHitboxEnabled = true;
        m_hitboxes?.EnableRush();
        ChangeState(Boss02State.Rush);
    }

    private void TickRush(float dt)
    {
        float travelSeconds = Mathf.Max(0.01f, m_rushTravelSeconds);
        float t = Mathf.Clamp01(m_stateTimer / travelSeconds);
        Vector3 position = Vector3.Lerp(m_rushStartPosition, m_rushTargetPosition, t);
        position.y += Mathf.Sin(t * Mathf.PI) * m_rushArcHeight;
        transform.position = position;

        Vector3 direction = m_rushTargetPosition - m_rushStartPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            m_boss.FaceToward(direction.normalized, dt);

        if (m_rushHitboxEnabled && m_stateTimer >= travelSeconds)
        {
            m_rushHitboxEnabled = false;
            m_hitboxes?.DisableRush();
        }

        if (m_stateTimer >= Mathf.Max(travelSeconds, m_rushEndDuration))
            FinishAction();
    }

    private void TickDown(float dt)
    {
        m_boss.SlowDown(dt);

        if (m_rushRepelActive)
            TickRushRepel();

        if (m_autoRecoverFromDown && m_stateTimer >= m_downRecoverDelay)
        {
            if (m_stamina != null)
                m_stamina.ResetStamina();

            m_animator.TriggerDownEnd();
            ChangeState(Boss02State.DownEnd);
        }
    }

    private void TickDownEnd(float dt)
    {
        m_boss.SlowDown(dt);
        if (m_stateTimer >= m_downEndDuration)
            FinishAction();
    }

    private void TickRushRepel()
    {
        float duration = Mathf.Max(0.01f, m_rushRepelDuration);
        float t = Mathf.Clamp01(m_stateTimer / duration);
        Vector3 position = Vector3.Lerp(m_rushRepelStartPosition, m_rushRepelTargetPosition, t);
        position.y += Mathf.Sin(t * Mathf.PI) * m_rushRepelArcHeight;
        transform.position = position;

        if (t >= 1f)
            m_rushRepelActive = false;
    }

    private void BeginAttack()
    {
        m_attackAfterMoveEnd = false;
        m_useArcMove = false;
        m_attackHitboxEnabled = false;
        m_animator.TriggerAttack();
        ChangeState(Boss02State.Attack);
    }

    private void BeginMove(bool useArcMove, bool attackAfterMoveEnd)
    {
        m_useArcMove = useArcMove;
        m_attackAfterMoveEnd = attackAfterMoveEnd;
        m_moveEntryCount++;
        m_arcMoveSideSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        m_animator.TriggerMove();
        ChangeState(Boss02State.Move);
    }

    private void EndMove(bool attackAfterMoveEnd)
    {
        m_attackAfterMoveEnd = attackAfterMoveEnd;
        m_animator.TriggerMoveEnd();
        ChangeState(Boss02State.MoveEnd);
    }

    private void BeginRush()
    {
        m_moveEntryCount = 0;
        ApplySelfMagnet();
        m_animator.TriggerRush();
        ChangeState(Boss02State.RushStance);
    }

    private void EnterDown()
    {
        if (m_state == Boss02State.Down || m_state == Boss02State.DownEnd)
            return;

        m_hitboxes?.DisableAll();
        ClearSelfMagnet();
        m_attackHitboxEnabled = false;
        m_rushHitboxEnabled = false;
        m_attackAfterMoveEnd = false;
        m_useArcMove = false;
        m_animator.TriggerDown();
        ChangeState(Boss02State.Down);
    }

    private void FinishAction()
    {
        m_hitboxes?.DisableAll();
        ClearSelfMagnet();
        m_attackHitboxEnabled = false;
        m_rushHitboxEnabled = false;
        m_rushRepelActive = false;
        m_attackAfterMoveEnd = false;
        m_useArcMove = false;
        m_cooldownTimer = m_settings != null ? m_settings.attackInterval : 0f;
        ChangeState(Boss02State.Idle);
    }

    private void ChangeState(Boss02State next)
    {
        if (m_state == next)
            return;

        if (m_logStateChange)
            ChannelLogger.Log("EnemyBoss02", $"[EnemyBoss02AI] {m_state} -> {next}");

        m_state = next;
        m_stateTimer = 0f;
    }

    private bool ShouldForceRushByMoveCount()
    {
        return m_forceRushAfterMoveCount > 0 && m_moveEntryCount >= m_forceRushAfterMoveCount;
    }

    private bool ChooseRushOverArcApproach()
    {
        float rushWeight = Mathf.Max(0f, m_rushWeight);
        float arcWeight = Mathf.Max(0f, m_arcApproachWeight);
        float totalWeight = rushWeight + arcWeight;
        if (totalWeight <= 0f)
            return true;

        return UnityEngine.Random.value * totalWeight < rushWeight;
    }

    private Vector3 GetArcMoveDirection(Vector3 toPlayer)
    {
        if (toPlayer.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Vector3 side = Vector3.Cross(Vector3.up, toPlayer).normalized * m_arcMoveSideSign;
        Vector3 direction = toPlayer + side * Mathf.Max(0f, m_arcMoveLateralStrength);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : toPlayer;
    }

    private float GetCurrentMoveSpeedMultiplier()
    {
        float speedMultiplier = m_useArcMove ? m_arcMoveSpeedMultiplier : m_directMoveSpeedMultiplier;
        if (speedMultiplier <= 0f)
            speedMultiplier = m_moveSpeedMultiplier;

        return Mathf.Max(0f, speedMultiplier);
    }

    private void LockRushTarget()
    {
        Vector3 start = transform.position;
        Vector3 playerPosition = m_player != null ? m_player.position : start + transform.forward;
        Vector3 direction = playerPosition - start;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;
        direction.Normalize();

        Vector3 target = playerPosition + direction * Mathf.Max(0f, m_rushOvershootDistance);
        target.y = start.y;
        m_rushTargetPosition = target;
    }

    private bool IsSamePoleAsSelf(Magnetizable other)
    {
        return m_selfMagnetizable != null
            && m_selfMagnetizable.IsActive
            && m_selfMagnetizable.Pole != MagneticPole.None
            && other != null
            && other.IsActive
            && other.Pole == m_selfMagnetizable.Pole;
    }

    private void ApplySelfMagnet()
    {
        if (m_selfMagnetizable == null || m_rushAuraPole == MagneticPole.None)
            return;

        ClearDestroyedSelfMagnetField();
        m_selfMagnetizable.SetPole(m_rushAuraPole);
    }

    private void ClearSelfMagnet()
    {
        if (m_selfMagnetizable != null && m_selfMagnetizable.IsActive)
            m_selfMagnetizable.Deactivate();

        ClearSelfMagnetField();
    }

    private void ClearSelfMagnetField()
    {
        if (m_selfEntity != null)
            m_selfEntity.magnetField = null;
    }

    private void ClearDestroyedSelfMagnetField()
    {
        if (m_selfEntity == null)
            return;

        if (m_selfEntity.magnetField is MagnetField field && field == null)
            m_selfEntity.magnetField = null;
    }

    private void OnBodyHit(HitData hit)
    {
        if (m_stamina == null || m_settings == null || hit.source == gameObject)
            return;

        int amount = Mathf.Max(1, Mathf.CeilToInt(m_settings.MaxStaminaSafe() * Mathf.Clamp(m_settings.stunGaugePercentPerBodyHit, 1, 100) / 100f));
        m_stamina.Consume(amount);
    }

    private void ApplyStabDamage(StabHitData data)
    {
        if (m_health == null || m_settings == null)
            return;

        int maxHp = Mathf.Max(1, m_health.MaxHealth);
        int segments = Mathf.Max(1, m_settings.healthBarSegments);
        int currentBarsRemaining = Mathf.CeilToInt((float)m_health.CurrentHealth * segments / maxHp);
        int targetBarsRemaining = Mathf.Max(0, currentBarsRemaining - 1);
        int targetHp = Mathf.FloorToInt((float)targetBarsRemaining * maxHp / segments);
        int damage = Mathf.Max(1, m_health.CurrentHealth - targetHp);

        m_health.DamageIgnoreCooldown(damage);
    }

    private float DistanceToPlayer()
    {
        if (m_player == null)
            return float.MaxValue;

        Vector3 offset = m_player.position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    private Vector3 GetDirectionToPlayer()
    {
        if (m_player == null)
            return Vector3.zero;

        Vector3 direction = m_player.position - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private void FacePlayer(float dt, float deadZoneDeg = 0f)
    {
        Vector3 direction = GetDirectionToPlayer();
        if (direction.sqrMagnitude > 0.0001f)
            m_boss.FaceToward(direction, dt, deadZoneDeg);
    }

    private void ResolvePlayer()
    {
        if (m_boss != null && m_boss.Player != null)
        {
            m_player = m_boss.Player;
            return;
        }

        GameObject playerObject = GameObject.FindWithTag(GameTags.Player);
        if (playerObject != null)
            m_player = playerObject.transform;
    }
}

internal static class EnemyBoss02SettingsExtensions
{
    public static int MaxStaminaSafe(this EnemyBossSettings settings)
    {
        return settings != null ? Mathf.Max(1, settings.maxStamina) : 1;
    }
}

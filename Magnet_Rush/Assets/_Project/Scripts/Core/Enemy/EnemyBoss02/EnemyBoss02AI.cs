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
    [SerializeField] private EnemyBoss02Animator m_animator;
    [SerializeField] private EnemyBoss02Hitboxes m_hitboxes;
    [SerializeField] private Magnetizable m_selfMagnetizable;
    [SerializeField] private Transform m_stabAnchor;
    [SerializeField] private StabFinisherSettings m_stabFinisherSettings;

    [Header("Battle")]
    public bool isBattleing = false;
    [SerializeField] private bool m_logStateChange = true;

    [Header("Attack")]
    [SerializeField] private float m_attackDuration = 3.56f;
    [SerializeField] private float m_attackHitStartTime = 1.33f;
    [SerializeField] private float m_attackHitEndTime = 1.75f;

    [Header("Move")]
    [SerializeField] private float m_moveDuration = 2.0f;
    [SerializeField] private float m_moveEndDuration = 0.77f;
    [SerializeField] private float m_moveSpeedMultiplier = 1.35f;
    [SerializeField] private float m_moveStopDistance = 4.0f;

    [Header("Rush")]
    [SerializeField] private float m_rushStanceDuration = 2.63f;
    [SerializeField] private float m_rushTravelSeconds = 0.35f;
    [SerializeField] private float m_rushEndDuration = 2.66f;
    [SerializeField] private float m_rushOvershootDistance = 4.0f;
    [SerializeField] private float m_rushArcHeight = 2.0f;
    [SerializeField] private float m_rushRepelDuration = 0.35f;
    [SerializeField] private float m_rushRepelArcHeight = 2.0f;
    [SerializeField] private MagneticPole m_rushAuraPole = MagneticPole.S;

    [Header("Down")]
    [SerializeField] private bool m_autoRecoverFromDown = true;
    [SerializeField] private float m_downRecoverDelay = 5.0f;
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

        float distance = DistanceToPlayer();
        if (distance <= m_settings.attackRange)
        {
            BeginAttack();
            return;
        }

        if (distance <= m_settings.rushAttackRange)
        {
            BeginRush();
            return;
        }

        BeginMove();
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
            EndMove();
            return;
        }

        Vector3 toPlayer = GetDirectionToPlayer();
        float distance = DistanceToPlayer();
        if (distance <= Mathf.Max(m_moveStopDistance, m_settings.attackRange) || m_stateTimer >= m_moveDuration)
        {
            EndMove();
            return;
        }

        m_boss.AccelerateToward(toPlayer, dt, m_moveSpeedMultiplier);
    }

    private void TickMoveEnd(float dt)
    {
        m_boss.SlowDown(dt);
        if (m_stateTimer >= m_moveEndDuration)
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
        m_attackHitboxEnabled = false;
        m_animator.TriggerAttack();
        ChangeState(Boss02State.Attack);
    }

    private void BeginMove()
    {
        m_animator.TriggerMove();
        ChangeState(Boss02State.Move);
    }

    private void EndMove()
    {
        m_animator.TriggerMoveEnd();
        ChangeState(Boss02State.MoveEnd);
    }

    private void BeginRush()
    {
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

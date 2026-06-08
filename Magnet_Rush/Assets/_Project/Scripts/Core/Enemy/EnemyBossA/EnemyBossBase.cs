using UnityEngine;

/// <summary>
/// 敵の基底クラス。Entityを継承しHealth・IMagnetTargetを共有する。
/// 移動はUpdateEntity() → EntityController経由。AIサブクラスがAccelerateToward等で速度を駆動する。
/// </summary>
[RequireComponent(typeof(Stamina))]
public class EnemyBossBase : Entity
{
    [Header("Data")]
    [SerializeField] private EnemyBossSettings m_statusData;

    [Header("References")]
    [SerializeField] private Transform m_player;

    [Header("Magnetic")]
    [Tooltip("no use for now")]
    //[SerializeField] private MagneticMover m_mover;

    public EnemyBossSettings StatusData => m_statusData;
    public Transform Player => m_player;

    protected override float Gravity => m_statusData != null ? m_statusData.gravity : base.Gravity;
    protected override float SnapForce => m_statusData != null ? m_statusData.snapForce : base.SnapForce;
    protected override float ExternalDrag => m_statusData != null ? m_statusData.externalDrag : base.ExternalDrag;
    protected override float GroundCheckDistance => m_statusData != null ? m_statusData.groundCheckDistance : base.GroundCheckDistance;
    protected override LayerMask GroundLayer => (m_statusData != null && m_statusData.groundLayer != 0) ? m_statusData.groundLayer : base.GroundLayer;

    // 接地が一瞬切れても数フレームは接地扱いを維持し、めり込み押し出しや床の継ぎ目での「急に沈む」を防ぐ。
    protected override int GroundGraceFrames => 3;

    protected override void Awake()
    {
        base.Awake();

        if (m_health != null && m_statusData != null)
            m_health.SetMaxHealth(m_statusData.maxHp);

        if (m_stamina != null && m_statusData != null)
        {
            m_stamina.SetMaxStamina(m_statusData.maxStamina);
            m_stamina.SetRecovery(m_statusData.staminaRecovery, m_statusData.staminaRecoveryCooldown);
        }

        // 質量は Magnetizable.m_initialMass=Infinity をプレハブで直接指定。
        // 旧 override は Awake 順序競合で1に上書きされるバグの温床だったので撤去。
        GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
            m_player = playerObj.transform;

        if (m_health != null)
            m_health.OnDie += Die;

        if (m_controller == null)
            Debug.LogWarning($"[EnemyBase] {name}: EntityControllerがありません。衝突判定なしで動作します", this);
    }

    void OnDestroy()
    {
        if (m_health != null)
            m_health.OnDie -= Die;
    }

    void Update()
    {
        UpdateEntity(Time.deltaTime);
    }

    public void AccelerateToward(Vector3 worldDirection, float dt)
    {
        AccelerateToward(worldDirection, dt, 1f);
    }

    /// <summary>
    /// topSpeedMul で moveSpeed の上限を倍率調整して加速する（rush 攻撃等で一時的に高速化する用途）。
    /// </summary>
    public void AccelerateToward(Vector3 worldDirection, float dt, float topSpeedMul)
    {
        if (worldDirection.sqrMagnitude > 0.01f)
        {
            Vector3 localDir = Quaternion.FromToRotation(transform.up, Vector3.up) * worldDirection;
            localDir = localDir.normalized;

            Accelerate(localDir, m_statusData.turningDrag,
                       m_statusData.acceleration, m_statusData.moveSpeed * topSpeedMul, dt);
            FaceDirection(worldDirection, m_statusData.rotationSpeed, dt);
        }
    }

    /// <summary>横移動を減速する。</summary>
    public void SlowDown(float dt)
    {
        Decelerate(m_statusData.deceleration, dt);
    }

    /// <summary>指定方向を向く。deadZoneDegを指定すると現在向きとの角度がそれ以下なら回転しない（追尾ねじれ防止）。</summary>
    public void FaceToward(Vector3 direction, float dt, float deadZoneDeg = 0f)
    {
        if (deadZoneDeg > 0f)
        {
            Vector3 currentFlat = transform.forward; currentFlat.y = 0f;
            Vector3 targetFlat = direction; targetFlat.y = 0f;
            if (currentFlat.sqrMagnitude > 0.0001f && targetFlat.sqrMagnitude > 0.0001f
                && Vector3.Angle(currentFlat, targetFlat) <= deadZoneDeg)
                return;
        }
        FaceDirection(direction, m_statusData.rotationSpeed, dt);
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}

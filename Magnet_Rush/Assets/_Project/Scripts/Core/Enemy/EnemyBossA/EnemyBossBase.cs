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

        var magnetizable = GetComponent<Magnetizable>();
        if (magnetizable != null)
            magnetizable.mass = float.PositiveInfinity;

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
        if (worldDirection.sqrMagnitude > 0.01f)
        {
            Vector3 localDir = Quaternion.FromToRotation(transform.up, Vector3.up) * worldDirection;
            localDir = localDir.normalized;

            Accelerate(localDir, m_statusData.turningDrag,
                       m_statusData.acceleration, m_statusData.moveSpeed, dt);
            FaceDirection(worldDirection, m_statusData.rotationSpeed, dt);
        }
    }

    /// <summary>横移動を減速する。</summary>
    public void SlowDown(float dt)
    {
        Decelerate(m_statusData.deceleration, dt);
    }

    /// <summary>指定方向を向く。</summary>
    public void FaceToward(Vector3 direction, float dt)
    {
        FaceDirection(direction, m_statusData.rotationSpeed, dt);
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}

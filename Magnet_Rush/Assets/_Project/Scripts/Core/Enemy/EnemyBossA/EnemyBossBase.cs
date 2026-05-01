using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 敵の基底クラス。Entityを継承しHealth・IMagnetTargetを共有する。
/// 移動はUpdateEntity() → EntityController経由。AIサブクラスがAccelerateToward等で速度を駆動する。
/// </summary>
public class EnemyBossBase : Entity
{
    [Header("Data")]
    [FormerlySerializedAs("statusData")]
    [SerializeField] private EnemyBossSettings m_statusData;

    [Header("References")]
    [FormerlySerializedAs("player")]
    [SerializeField] private Transform m_player;

    [Header("Magnetic")]
    [Tooltip("no use for now")]
    //[FormerlySerializedAs("MagneticMover")]
    //[SerializeField] private MagneticMover m_mover;

    public EnemyBossSettings StatusData => m_statusData;
    public Transform Player => m_player;

    /// <summary>MagneticMover が磁力移動モード中かどうか。AI はこの間スキップされる。</summary>
    //public bool IsMagnetControlled => m_mover != null && m_mover.IsMagnetActive;

    protected override float Gravity => m_statusData != null ? m_statusData.gravity : base.Gravity;
    protected override float SnapForce => m_statusData != null ? m_statusData.snapForce : base.SnapForce;
    protected override float ExternalDrag => m_statusData != null ? m_statusData.externalDrag : base.ExternalDrag;
    protected override float GroundCheckDistance => m_statusData != null ? m_statusData.groundCheckDistance : base.GroundCheckDistance;
    protected override LayerMask GroundLayer => (m_statusData != null && m_statusData.groundLayer != 0) ? m_statusData.groundLayer : base.GroundLayer;

    protected override void Awake()
    {
        base.Awake();
        //m_mover = GetComponentInChildren<MagneticMover>();

        var magnetizable = GetComponent<Magnetizable>();
        if (magnetizable != null)
            magnetizable.mass = float.PositiveInfinity;

        // Playerタグ付きオブジェクトを常に取得
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
        //UpdateEntity(Time.deltaTime);
    }

    /// <summary>指定方向（ワールド空間）に加速する。AIが計算した移動方向を渡す。</summary>
    public void AccelerateToward(Vector3 worldDirection, float dt)
    {
        if (worldDirection.sqrMagnitude > 0.01f)
        {
            // Accelerate()はlateralVelocity（ローカル空間）で計算するため変換が必要
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

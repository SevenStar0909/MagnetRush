using UnityEngine;

[DisallowMultipleComponent]
public class EnemyWalkBase : Entity
{
    [Header("Data")]
    [SerializeField] private EnemySettings m_statusData;

    [Header("References")]
    [SerializeField] private Transform m_player;

    [Header("Magnetic")]
    [SerializeField] private MagneticMover m_mover;

    public EnemySettings StatusData => m_statusData;
    public Transform Player => m_player;
    public bool IsMagnetControlled => m_mover != null && m_mover.IsMagnetActive;

    private Vector3 m_spawnPosition;
    private Quaternion m_spawnRotation;
    private bool m_spawnCaptured;

    /// <summary>最初に配置された位置。死亡演出で敵をここへ戻すのに使う。未確定時は現在位置を返す。</summary>
    public Vector3 SpawnPosition => m_spawnCaptured ? m_spawnPosition : transform.position;

    /// <summary>最初に配置された向き。</summary>
    public Quaternion SpawnRotation => m_spawnCaptured ? m_spawnRotation : transform.rotation;

    protected override float Gravity => m_statusData != null ? m_statusData.gravity : base.Gravity;
    protected override float SnapForce => m_statusData != null ? m_statusData.snapForce : base.SnapForce;
    protected override float ExternalDrag => m_statusData != null ? m_statusData.externalDrag : base.ExternalDrag;
    protected override float GroundCheckDistance => m_statusData != null ? m_statusData.groundCheckDistance : base.GroundCheckDistance;
    protected override LayerMask GroundLayer => (m_statusData != null && m_statusData.groundLayer != 0)
        ? m_statusData.groundLayer
        : base.GroundLayer;

    protected override void Awake()
    {
        base.Awake();

        if (m_mover == null)
            m_mover = GetComponentInChildren<MagneticMover>();

        if (m_health != null && m_statusData != null)
            m_health.SetMaxHealth(m_statusData.maxHp);

        CachePlayer();

        if (m_health != null)
            m_health.OnDie += Die;
    }

    private void OnDestroy()
    {
        if (m_health != null)
            m_health.OnDie -= Die;
    }

    private void Start()
    {
        // スポーン地点を記録する。スポナーが Instantiate 後に位置を設定する場合に備え Start で確定させる。
        m_spawnPosition = transform.position;
        m_spawnRotation = transform.rotation;
        m_spawnCaptured = true;
    }

    private void Update()
    {
        CachePlayer();
        UpdateEntity(Time.deltaTime);
    }

    public void AccelerateToward(Vector3 worldDirection, float dt)
    {
        if (m_statusData == null)
            return;

        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= 0.0001f)
            return;

        Vector3 localDirection = Quaternion.FromToRotation(transform.up, Vector3.up) * worldDirection.normalized;
        Accelerate(
            localDirection,
            m_statusData.turningDrag,
            m_statusData.acceleration,
            m_statusData.moveSpeed,
            dt
        );
        FaceToward(worldDirection, dt);
    }

    public void SlowDown(float dt)
    {
        if (m_statusData == null)
            return;

        Decelerate(m_statusData.deceleration, dt);
    }

    public void FaceToward(Vector3 direction, float dt)
    {
        if (m_statusData == null)
            return;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        FaceDirection(direction.normalized, m_statusData.rotationSpeed, dt);
    }

    private void CachePlayer()
    {
        if (m_player != null)
            return;

        GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
            m_player = playerObj.transform;
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}

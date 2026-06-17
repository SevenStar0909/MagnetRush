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

    [Header("Disappear Effect")]
    [SerializeField] private GameObject m_disappearEffectPrefab;
    [SerializeField] private float m_disappearEffectLifetime = 0.75f;

    public EnemySettings StatusData => m_statusData;
    public Transform Player => m_player;
    public bool IsMagnetControlled => m_mover != null && m_mover.IsMagnetActive;

    private Vector3 m_spawnPosition;
    private Quaternion m_spawnRotation;
    private bool m_spawnCaptured;
    private bool m_disappearEffectPlayed;

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

    // 接地が一瞬切れても数フレームは接地扱いを維持し、めり込み押し出しや床の継ぎ目での「急に沈む」を防ぐ。
    protected override int GroundGraceFrames => 3;

    // 磁力で引かれている間は重力・接地スナップを切り、磁力だけで着弾点まで引き寄せられるようにする
    // （床に縛られて手前の壁で止まる・ぴょんぴょん跳ねるのを防ぐ）。
    protected override bool SuppressEnvironmentPhysics => m_mover != null && m_mover.IsMagnetActive;

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
        // 磁力で壁・地面にビタ止め中は重力・移動を凍結（位置を固定）
        if (m_mover != null && m_mover.IsStuck) { ChannelLogger.LogGuardReturn("Enemy", "磁力ビタ止め中のため移動凍結"); return; }
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
        TriggerDisappearEffect();
        Destroy(gameObject);
    }

    private void TriggerDisappearEffect()
    {
        if (m_disappearEffectPlayed)
            return;

        m_disappearEffectPlayed = true;

        if (m_disappearEffectPrefab == null)
            return;

        GameObject effectObject = Instantiate(m_disappearEffectPrefab, transform.position, Quaternion.identity);

        if (m_disappearEffectLifetime > 0f)
            Destroy(effectObject, m_disappearEffectLifetime);
    }
}

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Magnetizable))]
public class EnemyMissile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float m_maxSpeed = 18f;    // 最高速度
    [SerializeField] private float m_acceleration = 40f; // 加速度(m/s²)
    [SerializeField] private float m_turnRate = 10f;    // 旋回速度
    [SerializeField] private float m_seekDelay = 0.2f;  // 発射後すぐは誘導しない時間
    [SerializeField] private float m_arrivalDistance = 0.6f; // ターゲットに近づきすぎないようにする距離

    [Header("Targeting")]
    [SerializeField] private Vector3 m_targetOffset;    // ターゲットのどこを狙うかのオフセット
    [SerializeField] private float m_targetRefreshInterval = 0.1f; // ターゲットの再検索間隔

    [Header("References")]
    [SerializeField] private Transform m_player;    // プレイヤーへの参照（Inspectorで設定、もしくは起動時に自動検索）

    [Header("Combat")]
    [SerializeField] private int m_damage = 1;  
    [SerializeField] private float m_lifetime = 6f;

    private Rigidbody m_rb;
    private Magnetizable m_selfMagnetizable;
    private Magnetizable m_currentTarget;
    private float m_timer;
    private float m_seekTimer;
    private float m_refreshTimer;
    private bool m_initialized;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_selfMagnetizable = GetComponent<Magnetizable>();

        if (m_player == null)
        {
            GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
            if (playerObj != null)
                m_player = playerObj.transform;
        }
    }

    private void Start()
    {
        // 外部からInitializeされない限り、起動時に自動初期化
        // テスト用
        if (!m_initialized)
            Initialize(m_player, transform.forward);
    }

    public void Initialize(Transform target, Vector3 initialDirection)
    {
        if (target != null)
            m_player = target;

        if (m_player == null)
        {
            GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
            if (playerObj != null)
                m_player = playerObj.transform;
        }

        m_timer = m_lifetime;
        m_seekTimer = m_seekDelay;
        m_refreshTimer = 0f;
        m_initialized = true;

        Vector3 dir = initialDirection.sqrMagnitude > 0f ? initialDirection.normalized : transform.forward;
        m_rb.linearVelocity = dir * Mathf.Max(0.1f, m_maxSpeed * 0.6f);
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    public void SetPlayer(Transform player)
    {
        m_player = player;
    }

    private void Update()
    {
        if (!m_initialized) { ChannelLogger.LogGuardReturn("Enemy", "Missile未初期化"); return; }

        m_timer -= Time.deltaTime;
        if (m_timer <= 0f)
            Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (!m_initialized || m_rb == null) { ChannelLogger.LogGuardReturn("Enemy", "Missile未初期化またはRigidbodyなし"); return; }

        if (m_seekTimer > 0f)
        {
            m_seekTimer -= Time.fixedDeltaTime;
            return;
        }

        UpdateMagnetTarget();

        Vector3 desiredVelocity = ResolveDesiredVelocity();
        Vector3 steering = desiredVelocity - m_rb.linearVelocity;
        steering = Vector3.ClampMagnitude(steering, m_acceleration * Time.fixedDeltaTime);

        Vector3 nextVelocity = m_rb.linearVelocity + steering;
        m_rb.linearVelocity = Vector3.ClampMagnitude(nextVelocity, m_maxSpeed);

        if (m_rb.linearVelocity.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(m_rb.linearVelocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, m_turnRate * Time.fixedDeltaTime);
        }
    }

    private void UpdateMagnetTarget()
    {
        if (m_selfMagnetizable == null || !m_selfMagnetizable.IsActive)
        {
            m_currentTarget = null;
            return;
        }

        m_refreshTimer -= Time.fixedDeltaTime;
        if (m_refreshTimer > 0f)
            return;

        m_refreshTimer = Mathf.Max(0.02f, m_targetRefreshInterval);
        m_currentTarget = FindNearestOppositeMagnetizable();
    }

    private Vector3 ResolveDesiredVelocity()
    {
        Vector3 forward = transform.forward;
        Transform target = ResolveTargetTransform();
        if (target == null)
            return forward * m_maxSpeed;

        Vector3 targetPos = target.position + m_targetOffset;
        Vector3 toTarget = targetPos - transform.position;

        if (toTarget.sqrMagnitude < m_arrivalDistance * m_arrivalDistance)
            return forward * m_maxSpeed;

        return toTarget.normalized * m_maxSpeed;
    }

    private Transform ResolveTargetTransform()
    {
        return m_currentTarget != null ? m_currentTarget.transform : m_player;
    }

    private Magnetizable FindNearestOppositeMagnetizable()
    {
        if (m_selfMagnetizable == null)
            return null;

        MagneticPole selfPole = m_selfMagnetizable.Pole;
        if (selfPole == MagneticPole.None)
            return null;

        Magnetizable[] all = FindObjectsByType<Magnetizable>(FindObjectsSortMode.None);

        Magnetizable nearest = null;
        float detectionRange = ResolveDetectionRange();
        float nearestSqr = detectionRange * detectionRange;
        Vector3 origin = transform.position;

        for (int i = 0; i < all.Length; i++)
        {
            Magnetizable candidate = all[i];
            if (candidate == null) continue;
            if (candidate == m_selfMagnetizable) continue;
            if (!candidate.IsActive) continue;
            if (candidate.Pole == MagneticPole.None) continue;
            if (candidate.Pole == selfPole) continue;

            Vector3 delta = candidate.transform.position - origin;
            float sqr = delta.sqrMagnitude;

            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private float ResolveDetectionRange()
    {
        if (MagnetManager.Instance != null && MagnetManager.Instance.Settings != null)
            return MagnetManager.Instance.Settings.magnetRange;

        return 10f;
    }

    // Layer Matrix で「当たる相手」を一元管理する設計（原則1）。PlayerBullet × EnemyBullet は OFF。
    // MagnetBullet は SphereCast で EnemyBullet レイヤを直接拾うので、Matrix OFF でも磁化検知は機能する。
    // コリジョンコールバック内で相手の型/タグ判定はしない（原則4）。
    private void OnTriggerEnter(Collider other)
    {
        var hittable = other.GetComponentInParent<IHittable>();
        if (hittable != null)
        {
            hittable.OnHit(new HitData
            {
                damage = m_damage,
                hitPoint = other.ClosestPoint(transform.position),
                knockbackDir = m_rb != null ? m_rb.linearVelocity.normalized : transform.forward,
                source = gameObject
            });
        }

        Destroy(gameObject);
    }
}
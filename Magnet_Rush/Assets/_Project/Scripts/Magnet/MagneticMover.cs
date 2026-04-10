using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敵やオブジェクトが磁力で引かれたときに物理移動する応答コンポーネント。
/// NavMeshAgent を一時無効化し、Rigidbody で物理移動する。
/// 磁化中はPhysics.gravityで落下し、磁化解除後にNavMesh復帰する。
/// </summary>
[RequireComponent(typeof(Magnetizable))]
[RequireComponent(typeof(Rigidbody))]
public class MagneticMover : MonoBehaviour, IMagneticResponse
{
    [SerializeField] private MagneticMoverSettings m_settings;

    private Magnetizable m_magnetizable;
    private Rigidbody m_rb;
    private NavMeshAgent m_agent;
    private CapsuleCollider m_capsule;

    private bool m_isMagnetActive;
    private float m_lastForceTime;
    private int m_recoveryAttempts;
    private Vector3 m_lastValidNavMeshPos;
    private RigidbodyConstraints m_savedConstraints;

    public bool IsResponseActive => m_magnetizable != null && m_magnetizable.IsActive;

    /// <summary>磁力移動モード中かどうか。EnemyBase がAI処理スキップの判定に使用。</summary>
    public bool IsMagnetActive => m_isMagnetActive;

    void Awake()
    {
        m_magnetizable = GetComponent<Magnetizable>();
        m_rb = GetComponent<Rigidbody>();
        m_agent = GetComponent<NavMeshAgent>();
        m_capsule = GetComponent<CapsuleCollider>();
    }

    public void OnMagnetForce(Vector3 force, Vector3 sourcePosition)
    {
        if (m_settings == null) return;

        if (!m_isMagnetActive)
            ActivateMagnetMode();

        m_lastForceTime = Time.time;

        m_rb.AddForce(force, m_settings.forceMode);
        m_rb.linearVelocity = Vector3.ClampMagnitude(m_rb.linearVelocity, m_settings.maxSpeed);
    }

    public void OnMagnetContact(Magnetizable self, Magnetizable other)
    {
        // SnapResolver が接触固定を処理する
    }

    void FixedUpdate()
    {
        if (!m_isMagnetActive) return;

        // 磁力モード中はRigidbodyに重力を適用（Entity.EntityStepが走らないため）
        m_rb.AddForce(Physics.gravity, ForceMode.Acceleration);

        // 接地判定: 足元にレイを飛ばして地面があるかチェック
        bool grounded = Physics.Raycast(transform.position, Vector3.down, 0.3f);
        // 接地中は摩擦で減速、空中は自然な物理挙動
        m_rb.linearDamping = grounded ? 8f : 0.5f;
    }

    void Update()
    {
        if (!m_isMagnetActive) return;

        // 磁化がまだアクティブなら復帰しない（復帰→再度飛ばされるワープループ防止）
        if (m_magnetizable != null && m_magnetizable.IsActive)
        {
            m_lastForceTime = Time.time;
            return;
        }

        if (Time.time - m_lastForceTime > m_settings.recoveryDelay)
            TryRecoverNavMesh();
    }

    private void ActivateMagnetMode()
    {
        m_isMagnetActive = true;
        m_recoveryAttempts = 0;

        if (m_agent != null && m_agent.enabled)
        {
            m_lastValidNavMeshPos = transform.position;
            m_agent.enabled = false;
        }

        m_rb.isKinematic = false;
        m_rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        m_savedConstraints = m_rb.constraints;
        m_rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void RestoreNavMeshState(Vector3 position)
    {
        m_rb.linearVelocity = Vector3.zero;
        m_rb.linearDamping = 0f;
        m_rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        m_rb.constraints = m_savedConstraints;
        m_rb.isKinematic = true;
        transform.position = position;
        m_agent.enabled = true;
        m_isMagnetActive = false;
        m_recoveryAttempts = 0;
    }

    private void TryRecoverNavMesh()
    {
        if (m_agent == null)
        {
            m_isMagnetActive = false;
            return;
        }

        if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
        {
            RestoreNavMeshState(hit.position);
        }
        else
        {
            m_recoveryAttempts++;
            if (m_recoveryAttempts >= m_settings.maxRecoveryAttempts)
            {
                RestoreNavMeshState(m_lastValidNavMeshPos);
            }
        }
    }

    void OnDisable()
    {
        if (m_isMagnetActive && m_agent != null)
        {
            m_rb.isKinematic = true;
            m_agent.enabled = true;
            m_isMagnetActive = false;
        }
    }
}

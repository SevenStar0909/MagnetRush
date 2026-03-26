using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敵やオブジェクトが磁力で引かれたときに物理移動する応答コンポーネント。
/// 仕様書: 「敵の引き寄せ」「身代わり」「押し潰し」で使用。
/// NavMeshAgent を一時無効化し、Rigidbody で物理移動する。
/// </summary>
[RequireComponent(typeof(Magnetizable))]
[RequireComponent(typeof(Rigidbody))]
public class MagneticMover : MonoBehaviour, IMagneticResponse
{
    [SerializeField] private float m_maxSpeed = 15f;
    [SerializeField] private float m_recoveryDelay = 1f;
    [SerializeField] private int m_maxRecoveryAttempts = 10;

    private Magnetizable m_magnetizable;
    private Rigidbody m_rb;
    private NavMeshAgent m_agent;

    private bool m_isMagnetActive;
    private float m_lastForceTime;
    private int m_recoveryAttempts;
    private Vector3 m_lastValidNavMeshPos;

    public bool IsResponseActive => m_magnetizable != null && m_magnetizable.IsActive;

    /// <summary>磁力移動モード中かどうか。EnemyBase がAI処理スキップの判定に使用。</summary>
    public bool IsMagnetActive => m_isMagnetActive;

    void Awake()
    {
        m_magnetizable = GetComponent<Magnetizable>();
        m_rb = GetComponent<Rigidbody>();
        m_agent = GetComponent<NavMeshAgent>();
    }

    public void OnMagnetForce(Vector3 force, Vector3 sourcePosition)
    {
        if (!m_isMagnetActive)
            ActivateMagnetMode();

        m_lastForceTime = Time.time;

        m_rb.AddForce(force, ForceMode.Force);
        m_rb.linearVelocity = Vector3.ClampMagnitude(m_rb.linearVelocity, m_maxSpeed);
    }

    public void OnMagnetContact(Magnetizable self, Magnetizable other)
    {
        // SnapResolver が接触固定を処理する
    }

    void Update()
    {
        if (!m_isMagnetActive) return;

        if (Time.time - m_lastForceTime > m_recoveryDelay)
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
            m_rb.isKinematic = true;
            m_rb.linearVelocity = Vector3.zero;
            transform.position = hit.position;
            m_agent.enabled = true;
            m_isMagnetActive = false;
            m_recoveryAttempts = 0;
        }
        else
        {
            m_recoveryAttempts++;
            if (m_recoveryAttempts >= m_maxRecoveryAttempts)
            {
                m_rb.isKinematic = true;
                m_rb.linearVelocity = Vector3.zero;
                transform.position = m_lastValidNavMeshPos;
                m_agent.enabled = true;
                m_isMagnetActive = false;
                m_recoveryAttempts = 0;
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

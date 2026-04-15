using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 磁力によるAI一時停止とNavMesh位置同期を管理する。
/// 力はIMagnetTarget(Entity.externalVelocity)経由で適用し、
/// EntityStep()が重力・衝突を処理する。
/// </summary>
[RequireComponent(typeof(Magnetizable))]
public class MagneticMover : MonoBehaviour, IMagneticResponse
{
    [SerializeField] private MagneticMoverSettings m_settings;

    private Magnetizable m_magnetizable;
    private NavMeshAgent m_agent;
    private IMagnetTarget m_magnetTarget;

    private bool m_isMagnetActive;
    private bool m_isHoldActive;  // PD 保持による AI 停止。距離減衰力ルート (m_isMagnetActive) と独立管理
    private float m_lastForceTime;
    private int m_recoveryAttempts;

    public bool IsResponseActive => m_magnetizable != null && m_magnetizable.IsActive;

    /// <summary>磁力移動モード中かどうか。AIはこの間スキップされる。</summary>
    public bool IsMagnetActive => m_isMagnetActive;

    void Awake()
    {
        m_magnetizable = GetComponent<Magnetizable>();
        m_agent = GetComponent<NavMeshAgent>();
        m_magnetTarget = GetComponent<IMagnetTarget>();
    }

    public void OnMagnetForce(Vector3 force, Vector3 sourcePosition)
    {
        // PD 保持中のみ既存ルートを弾く（距離減衰力は通常通り毎フレーム適用したい）
        if (m_isHoldActive) { ChannelLogger.LogGuardReturn("Magnet", "PD保持中は距離減衰力を無視"); return; }

        if (m_settings == null) { ChannelLogger.LogGuardReturn("Magnet", "Mover設定なし"); return; }

        m_isMagnetActive = true;
        m_lastForceTime = Time.time;

        // 力をスケーリングしてEntityに渡す。蓄積・減衰はEntity.externalVelocityが処理する。
        if (m_magnetTarget != null)
        {
            Vector3 scaledForce = force * m_settings.forceMultiplier;
            Vector3 clampedForce = Vector3.ClampMagnitude(scaledForce, m_settings.maxSpeed);
            m_magnetTarget.ApplyMagnetForce(clampedForce);
        }
    }

    /// <summary>
    /// PDホルダーから吸着の Enter/Exit を受け取り、NavMeshAgent 停止/復帰を明示制御する。
    /// active=true: AI停止 (距離減衰力と同じ扱い)。active=false: NavMeshAgent 復帰。
    /// </summary>
    public void SetHoldActive(bool active)
    {
        m_isHoldActive = active;
        if (active)
        {
            m_isMagnetActive = true;
            m_lastForceTime = Time.time;
        }
        else
        {
            RecoverFromMagnet();
        }
    }

    public void OnMagnetContact(Magnetizable self, Magnetizable other)
    {
        // SnapResolver が接触固定を処理する
    }

    void Update()
    {
        if (!m_isMagnetActive) { ChannelLogger.LogGuardReturn("Magnet", "磁力移動モード非アクティブ"); return; }

        // OnMagnetForceが呼ばれなくなった（範囲外）→ recoveryDelay後にAI再開
        // externalVelocityの減衰はEntity側で処理されるため、ここではタイミングのみ管理
        if (Time.time - m_lastForceTime > m_settings.recoveryDelay)
            RecoverFromMagnet();
    }

    private void RecoverFromMagnet()
    {
        if (m_agent != null && m_agent.enabled)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
            {
                m_agent.nextPosition = hit.position;
                m_isMagnetActive = false;
                m_recoveryAttempts = 0;
            }
            else
            {
                m_recoveryAttempts++;
                if (m_recoveryAttempts >= m_settings.maxRecoveryAttempts)
                {
                    m_agent.Warp(transform.position);
                    m_isMagnetActive = false;
                    m_recoveryAttempts = 0;
                }
            }
        }
        else
        {
            // NavMeshAgentなし（将来の飛行敵等）→ フラグ解除のみ
            m_isMagnetActive = false;
        }
    }
}

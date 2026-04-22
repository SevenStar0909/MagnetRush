using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 磁力によるAI一時停止とNavMesh位置同期を管理する。
/// 力はIMagnetTarget(Entity.externalVelocity)経由で適用し、
/// UpdateEntity()が重力・衝突を処理する。
/// 状態は MoverState enum で一元管理する (Idle / DistanceForce / Holding)。
/// </summary>
[RequireComponent(typeof(Magnetizable))]
public class MagneticMover : MonoBehaviour, IMagneticResponse
{
    private enum MoverState
    {
        Idle,            // 磁力非アクティブ、AI通常動作
        DistanceForce,   // 距離減衰引力受信中 (OnMagnetForce ルート)
        Holding          // PD 保持中 (MagnetManager.ProcessHold ルート)
    }

    [SerializeField] private MagneticMoverSettings m_settings;

    private Magnetizable m_magnetizable;
    private NavMeshAgent m_agent;
    private IMagnetTarget m_magnetTarget;

    private MoverState m_state = MoverState.Idle;
    private float m_lastForceTime;
    private int m_recoveryAttempts;

    public bool IsResponseActive => m_magnetizable != null && m_magnetizable.IsActive;

    /// <summary>磁力移動モード中かどうか (Idle 以外)。AI はこの間スキップされる。</summary>
    public bool IsMagnetActive => m_state != MoverState.Idle;

    void Awake()
    {
        m_magnetizable = GetComponent<Magnetizable>();
        m_agent = GetComponent<NavMeshAgent>();
        m_magnetTarget = GetComponent<IMagnetTarget>();
    }

    public void OnMagnetForce(Vector3 force, Vector3 sourcePosition)
    {
        // PD 保持中のみ既存ルートを弾く（距離減衰力は毎フレーム適用したい）
        if (m_state == MoverState.Holding) { ChannelLogger.LogGuardReturn("Magnet", "PD保持中は距離減衰力を無視"); return; }

        if (m_settings == null) { ChannelLogger.LogGuardReturn("Magnet", "Mover設定なし"); return; }

        m_state = MoverState.DistanceForce;
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
    /// active=true: Holding に遷移。active=false: RecoverFromMagnet で Idle に戻す。
    /// </summary>
    public void SetHoldActive(bool active)
    {
        if (active)
        {
            m_state = MoverState.Holding;
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
        if (m_state == MoverState.Idle) { ChannelLogger.LogGuardReturn("Magnet", "磁力移動モード非アクティブ"); return; }

        // PD 保持中はタイムアウト復帰しない (SetHoldActive(false) で明示的に解除される)
        if (m_state == MoverState.Holding) return;

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
                m_state = MoverState.Idle;
                m_recoveryAttempts = 0;
            }
            else
            {
                m_recoveryAttempts++;
                if (m_recoveryAttempts >= m_settings.maxRecoveryAttempts)
                {
                    m_agent.Warp(transform.position);
                    m_state = MoverState.Idle;
                    m_recoveryAttempts = 0;
                }
            }
        }
        else
        {
            // NavMeshAgentなし（将来の飛行敵等）→ フラグ解除のみ
            m_state = MoverState.Idle;
        }
    }
}

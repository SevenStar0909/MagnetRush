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
        Holding,         // PD 保持中 (MagnetManager.ProcessHold ルート)
        Stuck            // 壁・地面に押し付けられて静止中（ビタ止め）
    }

    [SerializeField] private MagneticMoverSettings m_settings;

    private Magnetizable m_magnetizable;
    private NavMeshAgent m_agent;
    private IMagnetTarget m_magnetTarget;
    private Entity m_entity;

    private MoverState m_state = MoverState.Idle;
    private float m_lastForceTime;
    private int m_recoveryAttempts;
    private bool m_heldActive;   // PD保持(ProcessHold)を受けている間 true。Stuck の解除判定に使う

    public bool IsResponseActive => m_magnetizable != null && m_magnetizable.IsActive;

    /// <summary>磁力移動モード中かどうか (Idle 以外)。AI はこの間スキップされる。</summary>
    public bool IsMagnetActive => m_state != MoverState.Idle;

    /// <summary>壁・地面に押し付けられて静止中（ビタ止め）か。敵Baseはこの間 UpdateEntity をスキップして凍結する。</summary>
    public bool IsStuck => m_state == MoverState.Stuck;

    void Awake()
    {
        m_magnetizable = GetComponent<Magnetizable>();
        m_agent = GetComponent<NavMeshAgent>();
        m_magnetTarget = GetComponent<IMagnetTarget>();
        m_entity = m_magnetTarget as Entity;
    }

    public void OnMagnetForce(Vector3 force, Vector3 sourcePosition)
    {
        // PD 保持中のみ既存ルートを弾く（距離減衰力は毎フレーム適用したい）
        if (m_state == MoverState.Holding) { ChannelLogger.LogGuardReturn("Magnet", "PD保持中は距離減衰力を無視"); return; }

        if (m_settings == null) { ChannelLogger.LogGuardReturn("Magnet", "Mover設定なし"); return; }

        // ビタ止め中は力を蓄積しない（完全静止を保つ）。磁力が続いているタイミングだけ記録し、止まったら解除に使う
        if (m_state == MoverState.Stuck) { m_lastForceTime = Time.time; return; }

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
        m_heldActive = active;
        if (active)
        {
            m_lastForceTime = Time.time;
            // ビタ止め中は Stuck を維持する（Holding に落とすと凍結が解けてしまう）
            if (m_state != MoverState.Stuck) m_state = MoverState.Holding;
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

#if UNITY_EDITOR
        // TODO: ビタ止めデバッグ用。原因特定後に削除する
        if (m_entity != null && Time.frameCount % 15 == 0)
        {
            Vector3 dbgPull = m_entity.externalVelocity + m_entity.holdVelocity;
            bool dbgHit = m_settings != null && m_entity.IsPulledIntoSurface(
                m_settings.magnetStickLayer, m_settings.magnetStickCheckDistance, m_settings.magnetStickMinPullSpeed);
            ChannelLogger.Log("Magnet", $"[Stick診断] {name} state={m_state} pull={dbgPull.magnitude:F2} surfaceHit={dbgHit} active={IsResponseActive} held={m_heldActive} age={(Time.time - m_lastForceTime):F2} layer={(m_settings != null ? m_settings.magnetStickLayer.value : 0)}");
        }
#endif

        // ビタ止め中: 毎フレーム速度をゼロに保ち完全静止。
        // 解除は「磁化が解けた」または「保持中でなく一定時間 磁力が来ていない」。
        // 保持(held)中は磁力源が消えたときの SetHoldActive(false) で剥がれる（早すぎる解除を防ぐ）。
        if (m_state == MoverState.Stuck)
        {
            ZeroVelocities();
            bool magnetGone = !IsResponseActive
                || (!m_heldActive && Time.time - m_lastForceTime > m_settings.recoveryDelay);
            if (magnetGone) RecoverFromMagnet();
            return;
        }

        // 距離減衰でも PD 保持中でも、引かれて壁・地面に密着したらビタ止めへ（Holding の return より前で判定する）
        if (TryStick()) return;

        // PD 保持中はタイムアウト復帰しない (SetHoldActive(false) で明示的に解除される)
        if (m_state == MoverState.Holding) return;

        // OnMagnetForceが呼ばれなくなった（範囲外）→ recoveryDelay後にAI再開
        // externalVelocityの減衰はEntity側で処理されるため、ここではタイミングのみ管理
        if (Time.time - m_lastForceTime > m_settings.recoveryDelay)
            RecoverFromMagnet();
    }

    /// <summary>引かれた方向の壁・地面に押し付けられていればビタ止め(Stuck)へ遷移する。</summary>
    private bool TryStick()
    {
        if (m_settings == null || !m_settings.enableMagnetStick) return false;
        if (m_entity == null) return false;
        if (!IsResponseActive) return false;   // 磁化中のみ（プレイヤーの CanMagnetStick と同条件）
        if (!m_entity.IsPulledIntoSurface(m_settings.magnetStickLayer,
                m_settings.magnetStickCheckDistance, m_settings.magnetStickMinPullSpeed))
            return false;

        m_state = MoverState.Stuck;
        m_lastForceTime = Time.time;
        ZeroVelocities();   // 蓄積した磁力を消して完全静止
        return true;
    }

    /// <summary>Entityの全速度成分をゼロにする（ビタ止めの静止維持・剥がれ時の吹っ飛び防止）。</summary>
    private void ZeroVelocities()
    {
        if (m_entity == null) { ChannelLogger.LogGuardReturn("Magnet", "Entity未取得のため速度クリア不可"); return; }
        m_entity.velocity = Vector3.zero;
        m_entity.externalVelocity = Vector3.zero;
        m_entity.holdVelocity = Vector3.zero;
    }

    private void RecoverFromMagnet()
    {
        m_heldActive = false;
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

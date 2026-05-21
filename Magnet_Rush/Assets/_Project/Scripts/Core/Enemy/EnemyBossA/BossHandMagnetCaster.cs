using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ボスの右手 Magnetizable が AttackStance / AttackMotion 中に磁化された時、
/// ボスを中心に球範囲で PhysicsObject を検出し、対象 Magnetizable に手の逆極を付与する。
/// 同時に Rigidbody.linearDamping を一時的に上書きしてオーバーシュートを抑える。
/// 付与した対象は Dictionary（key=Magnetizable, value=元の linearDamping）で記録し、
/// 手の極が None になる or 攻撃姿勢から抜けた瞬間に磁極・damping ともに元に戻す。
/// 吸引・接触・物理応答は全て既存 MagnetManager / MagneticMover に委譲する。
/// 依存: 右手 Magnetizable, EnemyBossAI, EnemyBossBase, EnemyBossSettings
/// </summary>
public class BossHandMagnetCaster : MonoBehaviour
{
    [Header("References")]
    [Tooltip("極変化を監視する右手の Magnetizable")]
    [SerializeField] private Magnetizable m_handMagnetizable;

    [Tooltip("State 参照用。Awake で GetComponentInParent で取得もできるがプレハブで明示アサインする想定")]
    [SerializeField] private EnemyBossAI m_bossAI;

    [Tooltip("範囲キャストの中心座標（ボスのピボット）取得用")]
    [SerializeField] private EnemyBossBase m_boss;

    [Header("Debug")]
    [SerializeField] private bool m_logCast = true;

    // key=Magnetizable, value=元の Rigidbody.linearDamping。ClearAffected で復元する
    private readonly Dictionary<Magnetizable, float> m_affected = new Dictionary<Magnetizable, float>();
    private static readonly Collider[] s_overlapBuffer = new Collider[64];
    private EnemyBossSettings m_settings;

    private void Awake()
    {
        if (m_handMagnetizable == null) m_handMagnetizable = GetComponent<Magnetizable>();
        if (m_boss != null) m_settings = m_boss.StatusData;
    }

    private void OnEnable()
    {
        if (m_handMagnetizable != null)
            m_handMagnetizable.OnPoleChanged += HandlePoleChanged;
    }

    private void OnDisable()
    {
        if (m_handMagnetizable != null)
            m_handMagnetizable.OnPoleChanged -= HandlePoleChanged;

        ClearAffected();
    }

    private void Update()
    {
        // State 監視はここでだけ。OnPoleChanged は Magnetizable 側から push される
        if (m_affected.Count == 0) return;
        if (IsBossInCastableState()) return;

        // 攻撃姿勢から離脱した瞬間に付与した磁極を全部クリア
        ClearAffected();
    }

    private void HandlePoleChanged(MagneticPole newPole)
    {
        // 仕様: 極が消えたら付与済みも全部クリア
        if (newPole == MagneticPole.None)
        {
            ClearAffected();
            return;
        }

        if (!IsBossInCastableState())
        {
            ChannelLogger.LogGuardReturn("EnemyBossA", "AttackStance/AttackMotion 以外なのでキャストしない");
            return;
        }

        Cast(newPole);
    }

    private bool IsBossInCastableState()
    {
        if (m_bossAI == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "m_bossAI 未アサインのため state 判定不能"); return false; }

        var s = m_bossAI.State;
        return s == EnemyBossAI.BossState.AttackStance || s == EnemyBossAI.BossState.AttackMotion;
    }

    private void Cast(MagneticPole handPole)
    {
        if (m_boss == null || m_settings == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "Boss / Settings 未取得でキャスト不可"); return; }

        MagneticPole opposite = handPole == MagneticPole.N ? MagneticPole.S : MagneticPole.N;
        int layerMask = 1 << PhysicsLayers.PhysicsObject;
        Vector3 center = m_boss.transform.position;
        float radius = m_settings.magnetCastRadius;

        int count = Physics.OverlapSphereNonAlloc(center, radius, s_overlapBuffer, layerMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = s_overlapBuffer[i];
            if (col == null) continue;

            // ドーム化: ボスのピボット Y より下にある物体は対象外（床下や崖下を除外）
            if (col.bounds.center.y < center.y) continue;

            // 手自身（Hitbox の Magnetizable）は除外
            Magnetizable mag = col.GetComponentInParent<Magnetizable>();
            if (mag == null) continue;
            if (mag == m_handMagnetizable) continue;

            // 既に登録済みなら極の再付与のみ、damping は二重保存しない（元値が上書きされる事故防止）
            if (!m_affected.ContainsKey(mag))
            {
                var rb = mag.GetComponent<Rigidbody>();
                float originalDamping = rb != null ? rb.linearDamping : 0f;
                m_affected[mag] = originalDamping;
                if (rb != null) rb.linearDamping = m_settings.magnetCastDamping;
            }

            mag.SetPole(opposite);
        }

        if (m_logCast)
            ChannelLogger.Log("EnemyBossA", $"[BossHandMagnetCaster] cast hand={handPole} opposite={opposite} radius={radius} hits={count} affected={m_affected.Count}");
    }

    private void ClearAffected()
    {
        if (m_affected.Count == 0) return;
        foreach (var kvp in m_affected)
        {
            var mag = kvp.Key;
            if (mag == null) continue;

            // 元の linearDamping を復元してから磁極解除
            var rb = mag.GetComponent<Rigidbody>();
            if (rb != null) rb.linearDamping = kvp.Value;

            mag.Deactivate();
        }
        m_affected.Clear();

        if (m_logCast)
            ChannelLogger.Log("EnemyBossA", "[BossHandMagnetCaster] cleared affected");
    }
}

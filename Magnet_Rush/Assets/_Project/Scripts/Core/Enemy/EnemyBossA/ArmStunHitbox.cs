using System;
using UnityEngine;

/// <summary>
/// ボス右手専用の被弾判定（振り上げカウンター）。
/// 振り上げ攻撃中（AttackStance/AttackMotion）に手へ当たった時だけ Stun を発火する（スタブ可）。
/// それ以外は通常の Health ダメージのみ。
/// IHittable を実装し、GetComponentInParent&lt;IHittable&gt; で右手子コライダー側から最近祖として捕捉される。
/// スタンゲージ（よろけ）の蓄積はボス本体ヒット側（EnemyBossAI.OnBodyHit）が担当する。
/// 依存: Health, EnemyBossBaseA_Animator
/// </summary>
public sealed class ArmStunHitbox : MonoBehaviour, IHittable
{
    [SerializeField] private Health m_health;
    [SerializeField] private EnemyBossBaseA_Animator m_animator;

    [SerializeField]
    [Tooltip("ヒット解決上の所属グループ。ボスなので通常 Enemy")]
    private HitGroup m_hitGroup = HitGroup.Enemy;

    /// <summary>所属グループ。攻撃側との比較で自傷・同士討ちを弾く。</summary>
    public HitGroup HitGroup => m_hitGroup;

    public event Action<HitData> OnHitEvent;

    void Awake()
    {
        if (m_health == null)
            m_health = GetComponentInParent<Health>();

        if (m_animator == null)
            m_animator = GetComponentInParent<EnemyBossBaseA_Animator>();

        if (m_health == null)
            ChannelLogger.LogError("EnemyBossA", $"[ArmStunHitbox] {name}: Health 未取得");
        if (m_animator == null)
            ChannelLogger.LogError("EnemyBossA", $"[ArmStunHitbox] {name}: EnemyBossBaseA_Animator 未取得");
    }

    public void OnHit(HitData hit)
    {
        if (m_health == null)
        {
            ChannelLogger.LogGuardReturn("EnemyBossA", "Health未設定");
            return;
        }

        // HPダメージは全状態で適用
        m_health.Damage(hit.damage);

        // 振り上げ攻撃中（AttackStance/AttackMotion）に手へ当たった＝逆極カウンター成立。
        // この時だけスタン（Stunned）を発火する。スタン中はプレイヤーがスタブを決められる。
        // IsStunned は bool なので連打されても入り直さない（旧 BeInterrupted 連打のループは起きない）。
        // スタンゲージ（よろけ）の蓄積はボス本体ヒット側（EnemyBossAI.OnBodyHit）が担当するので、ここでは触らない。
        if (m_animator != null && (m_animator.IsInAttackStance || m_animator.IsInAttackMotion))
        {
            m_animator.SetIsStunnedTrue();
            ChannelLogger.Log("EnemyBossA",
                $"[ArmStunHitbox] 振り上げカウンター成立 → Stun src={(hit.source != null ? hit.source.name : "null")}");
        }

        OnHitEvent?.Invoke(hit);
    }
}

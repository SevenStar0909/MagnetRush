using UnityEngine;

/// <summary>
/// EnemyBossBaseA_Animator の IsStunned / IsInStagger と同期して ParticleSystem を再生/停止する。
/// FX プレハブをボスに nest した上で、被弾スタンエフェクトなど「特定状態だけ表示したい」用途。
/// 依存: ParticleSystem
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class ParticleStunStaggerGate : MonoBehaviour
{
    [Tooltip("監視対象の Animator。未設定なら階層から自動取得")]
    [SerializeField] private EnemyBossBaseA_Animator m_animator;

    [Tooltip("Stagger 中も再生対象に含める。false なら Stun 中のみ再生（例: Dust エフェクト）")]
    [SerializeField] private bool m_includeStagger = true;

    private ParticleSystem m_ps;
    private bool m_wasActive;

    private void Awake()
    {
        m_ps = GetComponent<ParticleSystem>();

        if (m_animator == null)
            m_animator = transform.root.GetComponentInChildren<EnemyBossBaseA_Animator>(true);

        if (m_animator == null)
        {
            ChannelLogger.LogGuardReturn("Entity", $"[{name}] EnemyBossBaseA_Animator が見つからない");
            return;
        }

        if (m_ps != null)
            m_ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        if (m_animator == null || m_ps == null) return;

        bool active = m_animator.IsStunned || (m_includeStagger && m_animator.IsInStagger);
        if (active == m_wasActive) return;

        if (active)
            m_ps.Play(true);
        else
            m_ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        m_wasActive = active;
    }
}

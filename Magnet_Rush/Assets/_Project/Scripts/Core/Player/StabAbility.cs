using UnityEngine;

/// <summary>
/// スタブ攻撃能力。RB 入力でボススタン中＋接近時に StabPlayerState へ遷移し、AnimEvent でヒット通知する。
/// 基底: Ability（共通の依存 m_input / m_player / m_events / m_states は基底で取得済み）
/// Boss は Additive シーンで遅延ロードされるため Awake では取得せず、初回 Stab() 呼び出し時に解決する。
/// </summary>
public class StabAbility : Ability
{
    private Transform m_bossTarget;
    private IStabReceiver m_bossReceiver;
    private bool m_warnedNoBossTag;

    /// <summary>RB 入力でボススタン中＋接近時に StabPlayerState へ遷移する。</summary>
    public void Stab()
    {
        if (!m_input.IsStabPressed) return;

        if (m_player.Settings == null)
        { ChannelLogger.LogGuardReturn("Stab", "PlayerSettings未設定"); return; }

        if (!TryResolveBoss())
        { ChannelLogger.LogGuardReturn("Stab", "Boss未配置"); return; }

        if (m_bossReceiver != null && !m_bossReceiver.CanReceiveStab)
        { ChannelLogger.LogGuardReturn("Stab", "ボスがスタンしていない"); return; }

        if (Vector3.Distance(m_player.transform.position, m_bossTarget.position) > m_player.Settings.stabRange)
        { ChannelLogger.LogGuardReturn("Stab", "ボスの距離がスタブ攻撃の範囲外"); return; }

        m_input.ConsumeStab();
        m_states.Change<StabPlayerState>();
    }

    /// <summary>AnimEvent から呼ばれるヒット通知。突き刺しの瞬間に発火。</summary>
    public void OnStabHitEvent()
    {
        if (m_bossReceiver != null && m_player.Settings != null)
        {
            m_bossReceiver.OnStabHit(new StabHitData
            {
                damage = m_player.Settings.stabDamage,
                hitPoint = m_player.transform.position,
                source = m_player.gameObject,
            });
        }
        m_events.FireStab();
    }

    /// <summary>Boss を遅延解決。タグ未登録時もログ1回のみで安全に false を返す。</summary>
    private bool TryResolveBoss()
    {
        if (m_bossTarget != null) return true;

        GameObject bossObj;
        try { bossObj = GameObject.FindWithTag("Boss"); }
        catch (UnityException)
        {
            if (!m_warnedNoBossTag)
            {
                ChannelLogger.LogWarning("Stab", "Bossタグが未登録 (TagManager に Boss を追加)");
                m_warnedNoBossTag = true;
            }
            return false;
        }

        if (bossObj == null) return false;
        m_bossTarget = bossObj.transform;
        m_bossReceiver = bossObj.GetComponent<IStabReceiver>();
        return true;
    }
}

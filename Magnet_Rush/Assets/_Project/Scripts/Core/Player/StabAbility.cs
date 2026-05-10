using UnityEngine;

/// <summary>
/// スタブ攻撃能力。RB 入力でボススタン中＋接近時に StabPlayerState へ遷移し、AnimEvent でヒット通知する。
/// 基底: Ability（共通の依存 m_input / m_player / m_events / m_states は基底で取得済み）
/// </summary>
public class StabAbility : Ability
{
    private Transform m_bossTarget;

    protected override void Awake()
    {
        base.Awake();

        // スタブ攻撃のターゲットとなるボスをタグ検索して取得
        GameObject bossObj = GameObject.FindWithTag("Boss");
        if (bossObj != null)
        {
            m_bossTarget = bossObj.transform;
        }
    }

    /// <summary>RB 入力でボススタン中＋接近時に StabPlayerState へ遷移する。</summary>
    public void Stab()
    {
        // --- スタブ攻撃の発動条件をチェックして、条件を満たす場合は StabPlayerState へ遷移する ---

        // RB 入力がない場合は発動しない
        if (!m_input.IsStabPressed) return;

        // ボスがスタンしていない場合は発動しない
        // if(!bossPublicApi.IsStunned) return;

        // プレイヤーとボスの距離がスタブ攻撃の有効範囲を超えている場合は発動しない
        if (Vector3.Distance(m_player.transform.position, m_bossTarget.position) > m_player.Settings.stabRange) return; 

        m_input.ConsumeStab();
        m_states.Change<StabPlayerState>();
    }

    /// <summary>AnimEvent から呼ばれるヒット通知。</summary>
    public void OnStabHitEvent()
    {
        // bossReceiver.OnStabHit(new StabHitData {});
        m_events.FireStab();
    }
}

using UnityEngine;

/// <summary>
/// スタブ攻撃能力。RB 入力でボススタン中＋接近時に StabPlayerState へ遷移し、AnimEvent でヒット通知する。
/// PR0 (jump-stab-prep) では Stab() / OnStabHitEvent() メソッドのみ空実装。実装本体は feature/stab で行う。
/// 基底: Ability（共通の依存 m_input / m_player / m_events / m_states は基底で取得済み）
/// </summary>
public class StabAbility : Ability
{
    /// <summary>RB 入力でボススタン中＋接近時に StabPlayerState へ遷移する。本実装は feature/stab で。</summary>
    public void Stab()
    {
        // 実装は feature/stab で追加する
    }

    /// <summary>AnimEvent から呼ばれるヒット通知。本実装は feature/stab で。</summary>
    public void OnStabHitEvent()
    {
        // 実装は feature/stab で追加する
    }
}

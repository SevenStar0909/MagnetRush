using UnityEngine;

/// <summary>
/// スタブ攻撃能力。RB 入力でボススタン中＋接近時に StabPlayerState へ遷移し、AnimEvent でヒット通知する。
/// PR0 (jump-stab-prep) では空。実装は feature/stab で行う。
/// 基底: Ability（共通の依存 m_input / m_player / m_events / m_states は基底で取得済み）
/// </summary>
public class StabAbility : Ability
{
    // 実装は feature/stab で Stab() / OnStabHitEvent() を追加する
}

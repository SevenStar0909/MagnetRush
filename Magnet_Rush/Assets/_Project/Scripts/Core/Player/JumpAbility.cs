using UnityEngine;

/// <summary>
/// ジャンプ能力。A 入力で接地中ならジャンプ初速を Entity.verticalVelocity に与えて FallPlayerState へ遷移する。
/// PR0 (jump-stab-prep) では空。実装は feature/jump で行う。
/// 基底: Ability（共通の依存 m_input / m_player / m_events / m_states は基底で取得済み）
/// </summary>
public class JumpAbility : Ability
{
    // 実装は feature/jump で TryJump() を追加する
}

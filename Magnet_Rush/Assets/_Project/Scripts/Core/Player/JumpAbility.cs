using UnityEngine;

/// <summary>
/// ジャンプ能力。A 入力で接地中ならジャンプ初速を Entity.verticalVelocity に与えて FallPlayerState へ遷移する。
/// PR0 (jump-stab-prep) では Jump() メソッドのみ空実装。実装本体は feature/jump で行う。
/// 基底: Ability（共通の依存 m_input / m_player / m_events / m_states は基底で取得済み）
/// </summary>
public class JumpAbility : Ability
{

    protected override void Awake()
    {
        // Awake() は base.Awake() のみ
        base.Awake();
    }

    /// <summary>A 入力で接地中ならジャンプ初速を Entity.verticalVelocity に与える。本実装は feature/jump で。</summary>
    public void Jump()
    {
        // 実装は feature/jump で追加する
        // A入力確認 ＆ 接地判定
        if (m_input.IsJumpPressed && m_player.IsGrounded)
        {
            m_input.ConsumeJump();
            m_player.verticalVelocity = m_player.Settings.jumpInitialVelocity;
            // FallPlayerStateはジャンプ上昇中も含めた「空中状態」として扱う
            m_states.Change<FallPlayerState>();
        }
    }
}

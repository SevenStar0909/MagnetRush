/// <summary>
/// スタブ攻撃モーション中の状態。AnimEvent or タイムアウトで通常状態へ復帰する。
/// OnStep が空 = TickAllAbilities() 呼ばない = スタブモーション中の他入力ロックが自動実現される。
/// 基底: EntityState&lt;Player&gt;
/// </summary>
public class StabPlayerState : EntityState<Player>
{
    protected override void OnEnter(Player player)
    {
        // State 遷移時に Animator Trigger を確実に撃つ。
        // AnimEvent 駆動の発火に依存すると chicken-and-egg (アニメ未再生→イベント未発火→Trigger未発火) に陥るため。
        player.events.FireStab();
    }

    protected override void OnExit(Player player) { }

    protected override void OnStep(Player player, float dt)
    {
        if (timeSinceEntered >= player.Settings.stabMotionMaxDuration)
        {
            ChannelLogger.LogWarning("Stab", "モーションタイムアウト復帰 (AnimEvent OnStabMotionFinishedEvent 配線漏れの可能性)");
            ReturnToNormal(player);
        }
    }

    public void OnStabMotionFinished(Player p) => ReturnToNormal(p);

    private void ReturnToNormal(Player p)
    {
        if (p.input.MoveInput.sqrMagnitude > 0.01f)
            p.states.Change<MovePlayerState>();
        else
            p.states.Change<IdlePlayerState>();
    }
}

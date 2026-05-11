/// <summary>
/// スタブ攻撃モーション中の状態。AnimEvent or タイムアウトで通常状態へ復帰する。
/// State-driven 構造により OnStep が空 = 全能力呼び出しスキップ = スタブモーション中の他入力ロックが自動実現される。
/// 基底: EntityState&lt;Player&gt;
/// </summary>
public class StabPlayerState : EntityState<Player>
{
    protected override void OnEnter(Player player) { }
    protected override void OnExit(Player player) { }
    protected override void OnStep(Player player, float dt)
    {
        // スタブモーションの最大持続時間を超えたら強制的に通常状態へ復帰させる。
        if (timeSinceEntered >= player.Settings.stabMotionMaxDuration)
        {
            ReturnToNormal(player);
        }
    }

    public void OnStabMotionFinished(Player p) => ReturnToNormal(p);

    private void ReturnToNormal(Player p)
    {
        // スタブ終了後に移動入力があれば移動状態へ。なければ待機状態へ。
        if (p.input.MoveInput.sqrMagnitude > 0.01f)
        {
            p.states.Change<MovePlayerState>();
        }
        else
        {
            p.states.Change<IdlePlayerState>();
        }
    }
}
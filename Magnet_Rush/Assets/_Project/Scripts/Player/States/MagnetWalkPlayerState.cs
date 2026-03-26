/// <summary>
/// 壁面歩行ステート。磁力場によりtransform.upが書き換わった状態での移動。
/// localVelocityパターンにより自動的に壁面上を移動する。
/// </summary>
public class MagnetWalkPlayerState : EntityState<Player>
{
    public override void Step(float dt)
    {
        // localVelocityで自動的に壁面移動
        if (entity.input.MoveInput.sqrMagnitude > 0.01f)
            entity.AccelerateToInputDirection(dt);
        else
            entity.SlowDown(dt);

        // フィールド離脱 → 通常に戻る
        if (entity.magnetField == null || entity.magnetField.IsDestroyed)
        {
            if (entity.input.MoveInput.sqrMagnitude > 0.01f)
                manager.Change<MovePlayerState>();
            else
                manager.Change<IdlePlayerState>();
        }
    }
}
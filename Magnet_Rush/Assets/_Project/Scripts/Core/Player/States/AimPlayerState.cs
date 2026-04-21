/// <summary>
/// エイム状態。カメラ方向を向きながらストレイフ移動する。
/// </summary>
public class AimPlayerState : EntityState<Player>
{
    public override void UpdateState(float dt)
    {
        // ストレイフ移動：カメラ方向を向き、速度半減
        m_entity.MoveWithInputStrafe(dt);
        m_entity.polarity.Switch();
        m_entity.aim.HandleAimInput();   // aimReleaseGrace が切れると StopAim() → 別ステートに遷移
        m_entity.shooting.Fire();
        m_entity.shooting.SelfFire();
        m_entity.shooting.Reload();

        if (m_entity.input.MoveInput.sqrMagnitude < 0.01f)
        {
            m_entity.SlowDown(dt);
        }
    }
}

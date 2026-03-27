using UnityEngine;

/// <summary>
/// プレイヤー用のステートマシン。Idle/Move/Die/Aimステートを管理する。
/// </summary>
public class PlayerStateManager : EntityStateManager<Player>
{
    void Awake()
    {
        var player = GetComponent<Player>();

        RegisterState(new IdlePlayerState());
        RegisterState(new MovePlayerState());
        RegisterState(new DiePlayerState());
        RegisterState(new AimPlayerState());

        Initialize(player);
    }

    void Start()
    {
        Change<IdlePlayerState>();
    }
}

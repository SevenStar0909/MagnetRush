using UnityEngine;
using MagnetRush.Entity;
using MagnetRush.Player.States;

namespace MagnetRush.Player
{
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
}
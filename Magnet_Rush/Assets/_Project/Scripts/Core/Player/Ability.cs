using UnityEngine;

/// <summary>
/// プレイヤーが持つ能力の基底クラス。
/// 共通の依存（PlayerInputHandler / Player / PlayerEvents / PlayerStateManager）を Awake で取得する。
/// 派生クラス（AimAbility / ShootingAbility / PoleAbility 等）はこれを継承して具体的な発動条件・効果を実装する。
/// 詳細は docs/プレイヤー実装_アビリティ統一.md 参照。
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(PlayerEvents))]
[RequireComponent(typeof(PlayerStateManager))]
public abstract class Ability : MonoBehaviour
{
    protected PlayerInputHandler m_input;
    protected Player m_player;
    protected PlayerEvents m_events;
    protected PlayerStateManager m_states;

    protected virtual void Awake()
    {
        m_input = GetComponent<PlayerInputHandler>();
        m_player = GetComponent<Player>();
        m_events = GetComponent<PlayerEvents>();
        m_states = GetComponent<PlayerStateManager>();
    }
}

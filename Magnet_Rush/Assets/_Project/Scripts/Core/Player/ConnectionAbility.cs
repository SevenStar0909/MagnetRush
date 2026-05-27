using System;
using UnityEngine;

[RequireComponent(typeof(Player))]
public class ConnectionAbility : MonoBehaviour
{
    private Player m_player;
    private MagneticConnection m_subscribed;

    void Awake() 
    { 
        m_player = GetComponent<Player>();
    }

    void Update()
    {
        if (m_player.states.current is ConnectionPullPlayerState)
        {
            return;
        }

        var current = MagnetManager.Instance?.ActiveConnection;

        if (current == m_subscribed && current != null)
        {
            return;
        }

        if (m_subscribed != null)
        {
            m_subscribed.OnActivatedChanged -= OnActivatedChanged;
        }

        m_subscribed = current;

        if (m_subscribed != null)
        {
            m_subscribed.OnActivatedChanged += OnActivatedChanged;
            if (m_subscribed.IsActivated && m_subscribed.PlayerSide.VirtualMass <= m_subscribed.TargetSide.VirtualMass)
            {
                if (m_player.states.current is not ConnectionPullPlayerState)
                {
                    Debug.Log("【ConnectionAbility】線が登録され、かつプレイヤーが動く条件なので新しくStartPullします");
                    StartPull();
                }
            }
        }
    }

    public void StartPull()
    {
        Debug.Log("【超重要】StartPullが呼び出されました！今からステートを切り替えます！");
        m_player.states.Change<ConnectionPullPlayerState>();
    }

    public void StopPull()
    {
        if (!m_player.IsGrounded)
        {
            m_player.states.Change<FallPlayerState>();
        }
        else if (m_player.input.MoveInput.sqrMagnitude > 0.01f)
        {
            m_player.states.Change<MovePlayerState>();
        }
        else
        {
            m_player.states.Change<IdlePlayerState>();
        }
    }

    private void OnActivatedChanged(bool activated)
    {
        if (activated)
        {
            if (m_subscribed != null && m_subscribed.IsActivated && m_subscribed.PlayerSide.VirtualMass <= m_subscribed.TargetSide.VirtualMass)
            {
                if (m_player.states.current is not ConnectionPullPlayerState)
                {
                    StartPull();
                }
            }
        }
        else
        {
            // activated が false になったら、何が何でも確実に通常状態へ戻す！
            Debug.Log("【ConnectionAbility】引力終了のイベントを受信。StopPullします。");
            StopPull();
        }
    }
}
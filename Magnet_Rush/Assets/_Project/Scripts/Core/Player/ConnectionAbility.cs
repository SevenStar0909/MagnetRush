using System;
using UnityEngine;

public class ConnectionAbility : MonoBehaviour
{
    private Player m_player;
    private MagneticConnection m_subscribed;

    void Awake() { m_player = GetComponent<Player>(); }

    void Update()
    {
        var current = MagnetManager.Instance?.ActiveConnection;
        if (current == m_subscribed) return;
        if (m_subscribed != null) m_subscribed.OnActivatedChanged -= OnActivatedChanged;
        m_subscribed = current;
        if (m_subscribed != null) m_subscribed.OnActivatedChanged += OnActivatedChanged;
    }

    public void StartPull()
    {
        m_player.states.Change<ConnectionPullPlayerState>();
    }

    public void StopPull()
    {
        if (!m_player.IsGrounded)
            m_player.states.Change<FallPlayerState>();
        else if (m_player.input.MoveInput.sqrMagnitude > 0.01f)
            m_player.states.Change<MovePlayerState>();
        else
            m_player.states.Change<IdlePlayerState>();
    }

    private void OnActivatedChanged(bool activated)
    {
        if (activated) StartPull();
        else StopPull();
    }
}
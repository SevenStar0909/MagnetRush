using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤー死亡 → 待機 → リスポーンの timing オーケストレータ。
/// Player の sibling コンポーネントとして同 GameObject に付ける。
/// 実際のリセットは <see cref="Player.Respawn"/> に集約されているため、本コンポーネントは
/// 「Health.OnDie を購読して m_respawnDelay 秒待ち、Player.Respawn() を叩く」ことだけを担う。
///
/// 設計: <c>GameManager</c> ではなく Player asmdef 配下に置く理由は、<c>MagnetRush.Game</c> →
/// <c>MagnetRush.Player</c> 参照は循環依存になるため。代わりに Player と並ぶ位置に専任コンポーネントを
/// 置き、Player ライフサイクルに自動追従させる。
/// 依存: Player(同 GameObject)、Health(同 GameObject)
/// </summary>
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(Health))]
public class PlayerRespawner : MonoBehaviour
{
    [Header("[Respawn]")]
    [Tooltip("プレイヤー死亡から Respawn() 呼び出しまでの遅延秒数。Die モーション再生時間に合わせて調整する。")]
    [SerializeField] private float m_respawnDelay = 0.5f;

    private Player m_player;
    private Health m_health;

    void Awake()
    {
        m_player = GetComponent<Player>();
        m_health = GetComponent<Health>();
    }

    void OnEnable()
    {
        if (m_health != null)
        {
            m_health.OnDie += OnPlayerDie;
        }
    }

    void OnDisable()
    {
        if (m_health != null)
        {
            m_health.OnDie -= OnPlayerDie;
        }
    }

    /// <summary>Player の死亡通知を受けてリスポーンコルーチンを起動する。</summary>
    private void OnPlayerDie()
    {
        // Health.Damage が IsDead ガードで二重発火を防いでいるため、本ハンドラも 1 死亡につき 1 回しか呼ばれない前提
        StartCoroutine(RespawnAfterDelay());
    }

    /// <summary>m_respawnDelay 秒待機して Player.Respawn() を呼ぶ。</summary>
    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(m_respawnDelay);
        if (m_player != null)
        {
            m_player.Respawn();
        }
        else
        {
            ChannelLogger.LogGuardReturn("Player", "Player参照null — Respawn スキップ");
        }
    }
}

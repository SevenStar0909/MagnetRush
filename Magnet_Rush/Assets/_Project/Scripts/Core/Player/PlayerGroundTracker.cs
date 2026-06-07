using UnityEngine;

/// <summary>
/// プレイヤーが最後に接地していたワールド座標を記録する。
/// 奈落（デスボックス）からのソフトリスポーン先として Player.FallRespawn が参照する。
/// 接地中フレームのみ記録するので、空中に出た瞬間の値＝「落ちる直前の足場」が残る。
/// 依存: Player(同 GameObject, Entity.IsGrounded)
/// </summary>
[RequireComponent(typeof(Player))]
public class PlayerGroundTracker : MonoBehaviour
{
    private Player m_player;
    private bool m_hasGrounded;
    private Vector3 m_lastGroundedPosition;

    void Awake()
    {
        m_player = GetComponent<Player>();
    }

    void LateUpdate()
    {
        // 移動・接地判定が確定した後（LateUpdate）に記録する
        if (m_player != null && m_player.IsGrounded)
        {
            m_lastGroundedPosition = transform.position;
            m_hasGrounded = true;
        }
    }

    /// <summary>
    /// 最後に接地した位置を返す。一度も接地していなければ false（呼び出し側は固定スポーンへフォールバックする）。
    /// </summary>
    /// <param name="position">最後に接地したワールド座標</param>
    public bool TryGetLastGrounded(out Vector3 position)
    {
        position = m_lastGroundedPosition;
        return m_hasGrounded;
    }
}

using UnityEngine;

/// <summary>
/// デスボックス。マップの奈落（地形の底）に薄く広く配置する領域。
/// プレイヤーが領域内に入ったら落下扱いにし、最後に接地していた位置へソフトリスポーンさせる（HPは減らさない）。
///
/// 検出は OnTriggerEnter ではなく、起動時にキャッシュした AABB への bounds.Contains ポーリングで行う。
/// プレイヤーは kinematic Rigidbody を transform で直接動かす KCC で、かつ Physics.autoSyncTransforms=false のため
/// 静的トリガーとの OnTriggerEnter が安定して発火しない。プレイヤーの transform 位置を直接見るこの方式なら確実。
/// 領域は静的（動かさない）前提なので、Collider は起動時に AABB を取得したら無効化し、物理ブロードフェーズから外す
/// （巨大トリガーを物理に残すコストを避ける）。
/// 依存: Collider(同 GameObject), Player.FallRespawn
/// </summary>
[RequireComponent(typeof(Collider))]
public class DeathZone : MonoBehaviour
{
    private Bounds m_bounds;
    private Player m_player;
    private bool m_inside;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Start()
    {
        var col = GetComponent<Collider>();
        if (col == null) { ChannelLogger.LogGuardReturn("Player", "DeathZone に Collider が無い"); return; }

        // 静的領域の AABB を1回だけ取得してキャッシュ。autoSyncTransforms=false 対策に取得直前で同期。
        Physics.SyncTransforms();
        m_bounds = col.bounds;

        // 以降は bounds.Contains だけで判定するので、巨大コライダーを物理から外す（ブロードフェーズ負荷を回避）。
        col.enabled = false;
    }

    void FixedUpdate()
    {
        if (m_player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go == null) return;
            m_player = go.GetComponent<Player>();
            if (m_player == null) return;
        }

        // 領域に入った瞬間（外→内の遷移）だけ発火。FallRespawn 自身も多重起動ガードを持つ。
        bool inside = m_bounds.Contains(m_player.transform.position);
        if (inside && !m_inside)
            m_player.FallRespawn();
        m_inside = inside;
    }
}

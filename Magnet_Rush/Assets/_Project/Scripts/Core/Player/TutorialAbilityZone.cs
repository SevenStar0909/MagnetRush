using UnityEngine;

/// <summary>
/// チュートリアル用の機能ロック/解除ゾーン。プレイヤーが領域に入った瞬間に発動する。
/// 「入口で全ロック → 各エリアの入口で1機能ずつ解除」という置くだけのチュートリアル構成を作れる。
///
/// 検出は OnTriggerEnter ではなく、起動時にキャッシュした AABB への bounds.Contains ポーリングで行う
/// （プレイヤーは kinematic + transform 直接移動のため静的トリガーが安定発火しない。DeathZone と同方式）。
/// 依存: Collider(同 GameObject), Player.Current, PlayerAbilityLocker
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialAbilityZone : MonoBehaviour
{
    /// <summary>ゾーンに入ったときに行う操作。InspectorName でドロップダウンには日本語で表示される。</summary>
    public enum ZoneAction
    {
        /// <summary>全機能をロックする（移動とカメラは対象外。封じたい場合は Lock で個別指定する）。</summary>
        [InspectorName("全ロック（移動とカメラ以外を全部使えなくする）")]
        LockAll,

        /// <summary>指定した1機能のロックを解除する。</summary>
        [InspectorName("1機能を解除（アビリティで選んだものだけ）")]
        Unlock,

        /// <summary>指定した1機能をロックする。</summary>
        [InspectorName("1機能をロック（アビリティで選んだものだけ）")]
        Lock,

        /// <summary>全機能のロックを解除する。</summary>
        [InspectorName("全解除（全部の機能を使えるようにする）")]
        UnlockAll,
    }

    [Tooltip("プレイヤーがこのゾーンに入ったときに何をするか")]
    [SerializeField] private ZoneAction m_action = ZoneAction.LockAll;

    [Tooltip("Unlock / Lock のときに対象にする機能（LockAll / UnlockAll では使わない）")]
    [SerializeField] private PlayerAbilityType m_ability = PlayerAbilityType.Jump;

    [Tooltip("一度発動したら以降は反応しなくする（チェックを外すと出入りのたびに発動）")]
    [SerializeField] private bool m_oneShot = true;

    private Bounds m_bounds;
    private bool m_inside;
    private bool m_fired;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Start()
    {
        var col = GetComponent<Collider>();
        if (col == null) { ChannelLogger.LogGuardReturn("Player", "TutorialAbilityZone に Collider が無い"); return; }

        // 静的領域の AABB を1回だけ取得してキャッシュ。autoSyncTransforms=false 対策に取得直前で同期。
        Physics.SyncTransforms();
        m_bounds = col.bounds;

        // 以降は bounds.Contains だけで判定するので、コライダーを物理から外す（DeathZone と同じ理由）。
        col.enabled = false;
    }

    void FixedUpdate()
    {
        if (m_fired && m_oneShot) return;

        var player = Player.Current;
        if (player == null) return;

        // 外→内の遷移の瞬間だけ発動
        bool inside = m_bounds.Contains(player.transform.position);
        if (inside && !m_inside) Fire(player);
        m_inside = inside;
    }

    private void Fire(Player player)
    {
        var locker = player.abilityLocker;
        if (locker == null)
        {
            ChannelLogger.LogGuardReturn("Player", "PlayerAbilityLocker が Player に付いていない — ゾーン発動をスキップ");
            return;
        }

        switch (m_action)
        {
            case ZoneAction.LockAll: locker.LockAll(); break;
            case ZoneAction.Unlock: locker.SetLocked(m_ability, false); break;
            case ZoneAction.Lock: locker.SetLocked(m_ability, true); break;
            case ZoneAction.UnlockAll: locker.UnlockAll(); break;
        }

        m_fired = true;
        ChannelLogger.Log("Player", $"チュートリアルゾーン発動: {m_action} {(m_action == ZoneAction.Unlock || m_action == ZoneAction.Lock ? m_ability.ToString() : "")}");
    }

    // シーンビューで領域と種類が一目でわかるように描画（ロック系=赤 / 解除系=緑）
    void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        bool isLock = m_action == ZoneAction.LockAll || m_action == ZoneAction.Lock;
        Gizmos.color = isLock ? new Color(1f, 0.3f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.3f, 0.3f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = isLock ? Color.red : Color.green;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}

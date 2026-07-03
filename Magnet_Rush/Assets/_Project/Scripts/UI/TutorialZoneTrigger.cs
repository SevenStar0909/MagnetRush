using UnityEngine;

/// <summary>
/// チュートリアルゾーンの侵入判定。侵入したら SimpleTutorial にゾーン番号を通知する。
/// 検出は OnTriggerEnter ではなく、起動時にキャッシュした AABB への bounds.Contains ポーリングで行う（DeathZone と同方式）。
/// プレイヤーは kinematic Rigidbody を transform で直接動かす KCC で、かつ Physics.autoSyncTransforms=false のため
/// 静的トリガーとの OnTriggerEnter が安定して発火せず、取り逃すとチュートリアル進行が永久に止まる。
/// 領域は静的（動かさない）前提なので、Collider は起動時に AABB を取得したら無効化し、物理ブロードフェーズから外す。
/// 依存: Collider(同 GameObject), SimpleTutorial.OnPlayerEnterZone
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialZoneTrigger : MonoBehaviour
{
    [Header("チュートリアルゾーン設定")]
    [Tooltip("何番目のゾーン（図）か (1〜5)")]
    [SerializeField] private int m_zoneNumber = 1;

    [Tooltip("プレイヤーオブジェクトに設定されているタグ名")]
    [SerializeField] private string m_playerTag = "Player";

    private SimpleTutorial m_tutorialManager;
    private Bounds m_bounds;
    private bool m_hasBounds;
    private Transform m_playerRoot;
    private bool m_inside;

    void Start()
    {
        // シーン全体からチュートリアル管理スクリプトを自動で探して接続
        m_tutorialManager = Object.FindFirstObjectByType<SimpleTutorial>();

        var col = GetComponent<Collider>();
        if (col == null) { ChannelLogger.LogGuardReturn("Game", $"ゾーン{m_zoneNumber} に Collider が無いため侵入判定不可"); return; }

        // 静的領域の AABB を1回だけ取得してキャッシュ。autoSyncTransforms=false 対策に取得直前で同期
        Physics.SyncTransforms();
        m_bounds = col.bounds;
        m_hasBounds = true;

        // 以降は bounds.Contains だけで判定するので、コライダーを物理から外す
        col.enabled = false;
    }

    void FixedUpdate()
    {
        if (!m_hasBounds) return;

        if (m_playerRoot == null)
        {
            var tagged = GameObject.FindWithTag(m_playerTag);
            if (tagged == null) return;
            // Player タグは root と Hurtbox(子) の両方に付いているため root に寄せる
            m_playerRoot = tagged.transform.root;
        }

        // 領域に入った瞬間（外→内の遷移）だけ通知する
        bool inside = m_bounds.Contains(m_playerRoot.position);
        if (inside && !m_inside)
        {
            Debug.Log($"[TutorialZone] ゾーン【 {m_zoneNumber} 】にプレイヤーが侵入しました。");
            if (m_tutorialManager != null)
                m_tutorialManager.OnPlayerEnterZone(m_zoneNumber);
        }
        m_inside = inside;
    }
}

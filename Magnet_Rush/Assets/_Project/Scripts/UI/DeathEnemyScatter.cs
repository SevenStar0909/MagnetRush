using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーが死亡した瞬間、周囲のザコ敵を画面の外側へ退かせる演出。
/// 死亡カメラがプレイヤーへ寄って中央に写すとき、手前のザコが死を隠さないように画面の外へ滑らせる。
/// ザコの自走AI・移動・攻撃・NavMesh復帰をまとめて止めてから、こちらでカメラ視線の外へ動かす。
/// ボスは演出の主役なので退かさない。
/// 依存: Player(static Current/OnPlayerReady), Health.OnDie, EnemyWalkBase / EnemyAirBase
/// </summary>
public class DeathEnemyScatter : MonoBehaviour
{
    [Header("スポーン地点へ戻す")]
    [Tooltip("プレイヤーからこの距離内にいるザコだけ退かす(m)")]
    [SerializeField] private float m_retreatRadius = 14f;
    [Tooltip("死亡してから敵が動き出すまでの待ち(秒・実時間)。死をひと拍見せてから退かせる。0で即時")]
    [SerializeField] private float m_retreatDelay = 0.6f;
    [Tooltip("スポーン地点へ戻りきるまでの時間(秒・実時間)。長いほどゆっくり退く。死亡演出のスロー中・暗転中でも進むよう実時間で動かす")]
    [SerializeField] private float m_retreatDuration = 3.2f;
    [Tooltip("戻りながら進行方向（スポーン地点側）を向く。後ろ向きにズレて見えないように")]
    [SerializeField] private bool m_faceTravelDirection = true;

    private Health m_health;
    private Vector3 m_deathCenter;
    private bool m_active;
    private float m_blend;
    private float m_delayTimer;

    private struct RetreatItem
    {
        public Transform target;
        public Vector3 startPosition;
        public Vector3 spawnPosition;
    }

    private readonly List<RetreatItem> m_items = new();

    private void OnEnable()
    {
        Player.OnPlayerReady += Bind;
        if (Player.Current != null) Bind(Player.Current);
    }

    private void OnDisable()
    {
        Player.OnPlayerReady -= Bind;
        if (m_health != null) m_health.OnDie -= HandleDie;
    }

    private void Bind(Player player)
    {
        if (m_health != null) m_health.OnDie -= HandleDie;

        if (player == null) { ChannelLogger.LogGuardReturn("Game", "敵退避: Player が null"); return; }

        m_health = player.GetComponent<Health>();
        if (m_health == null) { ChannelLogger.LogGuardReturn("Game", "敵退避: Player に Health が無い"); return; }

        m_health.OnDie += HandleDie;
    }

    // 死亡した瞬間に範囲内のザコを集めてスポーン地点への後退を開始する。
    private void HandleDie()
    {
        if (m_active) { ChannelLogger.LogGuardReturn("Game", "敵退避: 既に退避中"); return; }

        m_deathCenter = Player.Current != null ? Player.Current.transform.position : transform.position;
        CollectEnemies(m_deathCenter);

        if (m_items.Count == 0) { ChannelLogger.LogGuardReturn("Game", "敵退避: 退かす敵がいない"); return; }

        m_blend = 0f;
        m_delayTimer = 0f;
        m_active = true;
    }

    private void CollectEnemies(Vector3 center)
    {
        m_items.Clear();
        float radiusSqr = m_retreatRadius * m_retreatRadius;

        CollectFromType<EnemyWalkBase>(center, radiusSqr, e => e.SpawnPosition);
        CollectFromType<EnemyAirBase>(center, radiusSqr, e => e.SpawnPosition);
    }

    private void CollectFromType<T>(Vector3 center, float radiusSqr, System.Func<T, Vector3> spawnOf)
        where T : MonoBehaviour
    {
        T[] found = FindObjectsByType<T>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            T enemy = found[i];
            if (enemy == null) continue;

            Transform root = enemy.transform;
            if ((root.position - center).sqrMagnitude > radiusSqr) continue;

            Vector3 spawn = spawnOf(enemy);
            FreezeBehaviours(root);
            m_items.Add(new RetreatItem
            {
                target = root,
                startPosition = root.position,
                spawnPosition = spawn
            });
        }
    }

    // ザコの自走・AI・攻撃・NavMesh復帰をまとめて止める。これをしないとAIがプレイヤーへ戻ろうとし、
    // NavMeshAgentが元位置へワープして退避演出と競合する。
    // RETRY/SELECT で必ずシーンが再読込されるので、元に戻す処理は不要。
    private void FreezeBehaviours(Transform root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this) continue;
            behaviour.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!m_active) return;

        // 死亡してからひと拍待ってから動かす。死んだ瞬間に即・速く退くとスローと噛み合わず不自然なため。
        if (m_delayTimer < m_retreatDelay)
        {
            m_delayTimer += Time.unscaledDeltaTime;
            return;
        }

        float dur = Mathf.Max(0.01f, m_retreatDuration);
        m_blend = Mathf.MoveTowards(m_blend, 1f, Time.unscaledDeltaTime / dur);
        float k = Mathf.SmoothStep(0f, 1f, m_blend);

        for (int i = 0; i < m_items.Count; i++)
        {
            RetreatItem item = m_items[i];
            if (item.target == null) continue;

            // 開始位置からスポーン地点へ滑らかに後退させる（イーズで最後は減速して馴染む）。
            item.target.position = Vector3.Lerp(item.startPosition, item.spawnPosition, k);

            if (m_faceTravelDirection)
            {
                Vector3 travel = item.spawnPosition - item.startPosition;
                travel.y = 0f;
                if (travel.sqrMagnitude > 0.01f)
                {
                    Quaternion look = Quaternion.LookRotation(travel.normalized, Vector3.up);
                    item.target.rotation = Quaternion.Slerp(item.target.rotation, look, 10f * Time.unscaledDeltaTime);
                }
            }
        }

        if (m_blend >= 1f)
            m_active = false;
    }
}

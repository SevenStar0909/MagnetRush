using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ボスの右手 Magnetizable が AttackStance / AttackMotion 中に磁化された時、
/// ボスを中心に球範囲で PhysicsObject を検出し、対象 Magnetizable に手の逆極を付与する。
/// 同時に Rigidbody.linearDamping を一時的に上書きしてオーバーシュートを抑える。
/// 付与した対象は Dictionary（key=Magnetizable, value=元の linearDamping）で記録し、
/// 手の極が None になる or 攻撃姿勢から抜けた瞬間に磁極・damping ともに元に戻す。
/// 吸引・接触・物理応答は全て既存 MagnetManager / MagneticMover に委譲する。
/// 依存: 右手 Magnetizable, EnemyBossAI, EnemyBossBase, EnemyBossSettings
/// </summary>
public class BossHandMagnetCaster : MonoBehaviour
{
    [Header("References")]
    [Tooltip("極変化を監視する右手の Magnetizable")]
    [SerializeField] private Magnetizable m_handMagnetizable;

    [Tooltip("State 参照用。Awake で GetComponentInParent で取得もできるがプレハブで明示アサインする想定")]
    [SerializeField] private EnemyBossAI m_bossAI;

    [Tooltip("範囲キャストの中心座標（ボスのピボット）取得用")]
    [SerializeField] private EnemyBossBase m_boss;

    [Header("Debug")]
    [SerializeField] private bool m_logCast = true;

    [Header("Visualizer (Game View)")]
    [Tooltip("Cast 中の色（不透明寄り）")]
    [SerializeField] private Color m_visualColorCasting = new Color(1f, 0.92f, 0.016f, 0.85f);

    [Tooltip("非 Cast 中の色（薄い）")]
    [SerializeField] private Color m_visualColorIdle = new Color(1f, 0.92f, 0.016f, 0.2f);

    [Tooltip("LineRenderer の太さ")]
    [SerializeField] private float m_visualLineWidth = 0.05f;

    // key=Magnetizable, value=元の Rigidbody.linearDamping。ClearAffected で復元する
    private readonly Dictionary<Magnetizable, float> m_affected = new Dictionary<Magnetizable, float>();
    private static readonly Collider[] s_overlapBuffer = new Collider[64];
    private EnemyBossSettings m_settings;

    private Transform m_visualContainer;
    private readonly List<LineRenderer> m_visualLines = new List<LineRenderer>();
    private static Material s_lineMaterial;

    private void Awake()
    {
        if (m_handMagnetizable == null) m_handMagnetizable = GetComponent<Magnetizable>();
        if (m_boss != null) m_settings = m_boss.StatusData;
        BuildVisualizer();
    }

    private void OnEnable()
    {
        if (m_handMagnetizable != null)
            m_handMagnetizable.OnPoleChanged += HandlePoleChanged;
    }

    private void OnDisable()
    {
        if (m_handMagnetizable != null)
            m_handMagnetizable.OnPoleChanged -= HandlePoleChanged;

        ClearAffected();
    }

    private void OnDestroy()
    {
        DestroyVisualizer();
    }

    private void LateUpdate()
    {
        // ボスが回転してもドームをワールド軸固定（ローテーションだけ打ち消す）
        if (m_visualContainer != null)
            m_visualContainer.rotation = Quaternion.identity;
    }

    private void Update()
    {
        // State 監視はここでだけ。OnPoleChanged は Magnetizable 側から push される
        if (m_affected.Count == 0) return;
        if (IsBossInCastableState()) return;

        // 攻撃姿勢から離脱した瞬間に付与した磁極を全部クリア
        ClearAffected();
    }

    private void HandlePoleChanged(MagneticPole newPole)
    {
        // 仕様: 極が消えたら付与済みも全部クリア
        if (newPole == MagneticPole.None)
        {
            ClearAffected();
            return;
        }

        if (!IsBossInCastableState())
        {
            ChannelLogger.LogGuardReturn("EnemyBossA", "AttackStance/AttackMotion 以外なのでキャストしない");
            return;
        }

        Cast(newPole);
    }

    private bool IsBossInCastableState()
    {
        if (m_bossAI == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "m_bossAI 未アサインのため state 判定不能"); return false; }

        var s = m_bossAI.State;
        return s == EnemyBossAI.BossState.AttackStance || s == EnemyBossAI.BossState.AttackMotion;
    }

    private void Cast(MagneticPole handPole)
    {
        if (m_boss == null || m_settings == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "Boss / Settings 未取得でキャスト不可"); return; }

        int layerMask = 1 << PhysicsLayers.PhysicsObject;
        Vector3 center = m_boss.transform.position;
        float radius = m_settings.magnetCastRadius;

        int count = Physics.OverlapSphereNonAlloc(center, radius, s_overlapBuffer, layerMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = s_overlapBuffer[i];
            if (col == null) continue;

            // ドーム化: ボスのピボット Y より下にある物体は対象外（床下や崖下を除外）
            if (col.bounds.center.y < center.y) continue;

            // 手自身（Hitbox の Magnetizable）は除外
            Magnetizable mag = col.GetComponentInParent<Magnetizable>();
            if (mag == null) continue;
            if (mag == m_handMagnetizable) continue;

            // 仕様: 既存極を尊重する。極なし物体は何もしない、磁化済み物体は同極=反発/異極=吸引を MagnetManager に委ねる
            MagneticPole existing = mag.Pole;
            if (existing == MagneticPole.None) continue;

            // 既に登録済みなら damping だけ同極/異極で更新、元値の保存は初回のみ
            bool isSamePole = existing == handPole;
            float overrideDamping = isSamePole ? m_settings.magnetCastDampingSamePole : m_settings.magnetCastDamping;

            var rb = mag.GetComponent<Rigidbody>();
            if (!m_affected.ContainsKey(mag))
            {
                float originalDamping = rb != null ? rb.linearDamping : 0f;
                m_affected[mag] = originalDamping;
            }
            if (rb != null) rb.linearDamping = overrideDamping;
        }

        SetVisualColor(m_visualColorCasting);

        if (m_logCast)
            ChannelLogger.Log("EnemyBossA", $"[BossHandMagnetCaster] cast hand={handPole} radius={radius} hits={count} affected={m_affected.Count}");
    }

    private void ClearAffected()
    {
        if (m_affected.Count == 0)
        {
            SetVisualColor(m_visualColorIdle);
            return;
        }
        foreach (var kvp in m_affected)
        {
            var mag = kvp.Key;
            if (mag == null) continue;

            // 元の linearDamping を復元する。仕様: 既存極を尊重するので極は触らない
            var rb = mag.GetComponent<Rigidbody>();
            if (rb != null) rb.linearDamping = kvp.Value;
        }
        m_affected.Clear();

        SetVisualColor(m_visualColorIdle);

        if (m_logCast)
            ChannelLogger.Log("EnemyBossA", "[BossHandMagnetCaster] cleared affected");
    }

    private void BuildVisualizer()
    {
        if (m_boss == null || m_settings == null) return;
        if (m_visualContainer != null) return;

        var go = new GameObject("BossHandCastVisualizer");
        m_visualContainer = go.transform;
        m_visualContainer.SetParent(m_boss.transform, worldPositionStays: false);
        m_visualContainer.localPosition = Vector3.zero;
        m_visualContainer.localRotation = Quaternion.identity;

        float radius = m_settings.magnetCastRadius;

        float[] latitudes = { 0f, 30f, 60f, 89f };
        foreach (float lat in latitudes)
        {
            float rad = lat * Mathf.Deg2Rad;
            float h = Mathf.Sin(rad) * radius;
            float r = Mathf.Cos(rad) * radius;
            CreateVisualRing(Vector3.up * h, r);
        }

        for (int i = 0; i < 4; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            CreateVisualMeridianHalf(dir, radius);
        }

        SetVisualColor(m_visualColorIdle);
    }

    private void DestroyVisualizer()
    {
        if (m_visualContainer == null) return;
        if (Application.isPlaying) Destroy(m_visualContainer.gameObject);
        else DestroyImmediate(m_visualContainer.gameObject);
        m_visualContainer = null;
        m_visualLines.Clear();
    }

    private void CreateVisualRing(Vector3 localCenter, float radius)
    {
        var go = new GameObject("Ring");
        go.transform.SetParent(m_visualContainer);
        go.transform.localPosition = localCenter;
        go.transform.localRotation = Quaternion.identity;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = m_visualLineWidth;
        SetupLineMaterial(lr);

        const int segments = 48;
        lr.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }

        m_visualLines.Add(lr);
    }

    private void CreateVisualMeridianHalf(Vector3 dir, float radius)
    {
        var go = new GameObject("Meridian");
        go.transform.SetParent(m_visualContainer);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.widthMultiplier = m_visualLineWidth;
        SetupLineMaterial(lr);

        const int segments = 24;
        lr.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float a = (float)i / segments * Mathf.PI / 2f;
            Vector3 pos = dir * (Mathf.Cos(a) * radius) + Vector3.up * (Mathf.Sin(a) * radius);
            lr.SetPosition(i, pos);
        }

        m_visualLines.Add(lr);
    }

    private static void SetupLineMaterial(LineRenderer lr)
    {
        if (s_lineMaterial == null)
            s_lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lr.material = s_lineMaterial;
    }

    private void SetVisualColor(Color color)
    {
        for (int i = 0; i < m_visualLines.Count; i++)
        {
            var lr = m_visualLines[i];
            if (lr == null) continue;
            lr.startColor = color;
            lr.endColor = color;
        }
    }

#if UNITY_EDITOR
    // Scene View 専用。キャスト範囲（ボス中心の半径 magnetCastRadius、ドーム上半分）を黄色で描画する
    private void OnDrawGizmos()
    {
        if (m_boss == null) return;
        var settings = m_settings != null ? m_settings : m_boss.StatusData;
        if (settings == null) return;

        Vector3 center = m_boss.transform.position;
        float radius = settings.magnetCastRadius;

        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.85f);
        DrawDome(center, radius);
    }

    private static void DrawDome(Vector3 center, float radius)
    {
        const int ringSegments = 48;
        const int meridianSegments = 24;

        float[] latitudes = { 0f, 30f, 60f, 89f };
        foreach (float lat in latitudes)
        {
            float rad = lat * Mathf.Deg2Rad;
            float h = Mathf.Sin(rad) * radius;
            float r = Mathf.Cos(rad) * radius;
            DrawWireCircle(center + Vector3.up * h, Vector3.up, r, ringSegments);
        }

        for (int i = 0; i < 4; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            DrawWireHalfMeridian(center, dir, radius, meridianSegments);
        }
    }

    private static void DrawWireCircle(Vector3 center, Vector3 normal, float radius, int segments)
    {
        Vector3 up = normal.normalized;
        Vector3 forward = Mathf.Abs(Vector3.Dot(up, Vector3.up)) < 0.99f
            ? Vector3.Cross(up, Vector3.up).normalized
            : Vector3.Cross(up, Vector3.forward).normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;

        Vector3 prev = center + forward * radius;
        for (int i = 1; i <= segments; i++)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            Vector3 next = center + forward * (Mathf.Cos(a) * radius) + right * (Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private static void DrawWireHalfMeridian(Vector3 center, Vector3 dir, float radius, int segments)
    {
        Vector3 prev = center + dir * radius;
        for (int i = 1; i <= segments; i++)
        {
            float a = (float)i / segments * Mathf.PI / 2f;
            Vector3 next = center + dir * (Mathf.Cos(a) * radius) + Vector3.up * (Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}

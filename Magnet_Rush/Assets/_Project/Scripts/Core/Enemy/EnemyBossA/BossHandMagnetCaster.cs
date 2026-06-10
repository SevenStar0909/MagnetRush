using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ボスが AttackStance / AttackMotion に入った瞬間、ボス中心の上半球ドーム範囲内
/// PhysicsObject 全員に同一の N/S 極をランダム付与する（手は無極のまま）。
/// プレイヤーはドームの色（=付与した極）を見て、対極の弾を手に当てて手を磁化させ、
/// オブジェクト群と手が異極になることで MagnetManager の吸引が成立し全オブジェクトが手に飛ぶ。
/// State が AttackStance/AttackMotion を抜けた瞬間に付与した極を全てクリアする。
/// 範囲ビジュアルは LineRenderer の上半球ドームで自描画する（N=赤 / S=青）。
/// 依存: 右手 Magnetizable, EnemyBossAI, EnemyBossBase
/// </summary>
public class BossHandMagnetCaster : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ドームと範囲キャストの中心となる右手 Magnetizable")]
    [SerializeField] private Magnetizable m_handMagnetizable;

    [Tooltip("State 監視用。AttackStance / AttackMotion への突入を検知する")]
    [SerializeField] private EnemyBossAI m_bossAI;

    [Tooltip("ボス本体（StatusData の magnetCastRadius を参照するため）")]
    [SerializeField] private EnemyBossBase m_boss;

    [Header("Visualizer")]
    [Tooltip("N極時のリング色")]
    [SerializeField] private Color m_colorN = new Color(1f, 0.2f, 0.2f, 0.8f);

    [Tooltip("S極時のリング色")]
    [SerializeField] private Color m_colorS = new Color(0.2f, 0.4f, 1f, 0.8f);

    [Tooltip("LineRendererの太さ")]
    [SerializeField] private float m_lineWidth = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool m_logCast = true;

    private readonly List<Magnetizable> m_affected = new List<Magnetizable>();
    private static readonly Collider[] s_overlapBuffer = new Collider[64];
    private EnemyBossSettings m_settings;
    private bool m_wasInCastableState;

    private GameObject m_visualizerGO;
    private readonly List<LineRenderer> m_visualLines = new List<LineRenderer>();
    private static Material s_lineMaterial;

    private void Awake()
    {
        if (m_handMagnetizable == null) m_handMagnetizable = GetComponent<Magnetizable>();
        if (m_boss != null) m_settings = m_boss.StatusData;
        BuildVisualizer();
        HideVisualizer();
    }

    private void OnDisable()
    {
        ClearAffected();
        HideVisualizer();
    }

    private void OnDestroy()
    {
        if (m_visualizerGO != null)
        {
            if (Application.isPlaying) Destroy(m_visualizerGO);
            else DestroyImmediate(m_visualizerGO);
        }
    }

    private void Update()
    {
        bool inState = IsBossInCastableState();

        if (inState && !m_wasInCastableState)
        {
            MagneticPole pole = Random.value < 0.5f ? MagneticPole.N : MagneticPole.S;
            Cast(pole);
            ShowVisualizer(pole);
        }
        else if (!inState && m_wasInCastableState)
        {
            ClearAffected();
            HideVisualizer();
        }

        m_wasInCastableState = inState;
    }

    private void LateUpdate()
    {
        if (m_visualizerGO == null || !m_visualizerGO.activeSelf) return;
        UpdateVisualizerTransform();
    }

    /// <summary>
    /// ドームを「ボスのXZ位置 + 真下の地面Y」に毎フレーム再配置する。
    /// 底辺がそのまま地面に張り付くようローカル原点に半球を作ってあるので、ワールド位置だけ動かせば良い。
    /// </summary>
    private void UpdateVisualizerTransform()
    {
        if (m_boss == null) return;

        Vector3 bossPos = m_boss.transform.position;
        float groundY = bossPos.y;
        int groundMask = PhysicsLayers.Bit(PhysicsLayers.Default)
            | PhysicsLayers.Bit(PhysicsLayers.Ground) | PhysicsLayers.Bit(PhysicsLayers.Wall);
        if (Physics.Raycast(bossPos + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 100f, groundMask, QueryTriggerInteraction.Ignore))
            groundY = hit.point.y;

        m_visualizerGO.transform.position = new Vector3(bossPos.x, groundY, bossPos.z);
        m_visualizerGO.transform.rotation = Quaternion.identity;
    }

    private bool IsBossInCastableState()
    {
        if (m_bossAI == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "m_bossAI 未アサインのため state 判定不能"); return false; }

        var s = m_bossAI.State;
        return s == EnemyBossAI.BossState.AttackStance || s == EnemyBossAI.BossState.AttackMotion;
    }

    private void Cast(MagneticPole pole)
    {
        if (m_boss == null || m_settings == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "Boss / Settings 未取得でキャスト不可"); return; }
        if (m_handMagnetizable == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "m_handMagnetizable 未アサイン"); return; }

        int layerMask = 1 << PhysicsLayers.PhysicsObject;
        Vector3 center = m_boss.transform.position;
        float radius = m_settings.magnetCastRadius;

        int count = Physics.OverlapSphereNonAlloc(center, radius, s_overlapBuffer, layerMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = s_overlapBuffer[i];
            if (col == null) continue;

            Magnetizable mag = col.GetComponentInParent<Magnetizable>();
            if (mag == null) continue;
            // 手は無極のままにする（プレイヤーが対極弾を当てて磁化させる）
            if (mag == m_handMagnetizable) continue;
            if (m_affected.Contains(mag)) continue;

            mag.SetPole(pole);
            // ドーム内オブジェクト同士の同極反発を抑制（MagnetManager.ProcessPair でフラグを見る）
            mag.RepulsionDisabled = true;
            m_affected.Add(mag);
        }

        if (m_logCast)
            ChannelLogger.Log("EnemyBossA", $"[BossHandMagnetCaster] cast pole={pole} radius={radius} affected={m_affected.Count}");
    }

    private void ClearAffected()
    {
        if (m_affected.Count == 0) return;

        for (int i = 0; i < m_affected.Count; i++)
        {
            var mag = m_affected[i];
            if (mag == null) continue;
            mag.RepulsionDisabled = false;
            mag.Deactivate();
        }
        m_affected.Clear();

        if (m_logCast)
            ChannelLogger.Log("EnemyBossA", "[BossHandMagnetCaster] cleared affected");
    }

    private void BuildVisualizer()
    {
        if (m_handMagnetizable == null) return;
        if (m_settings == null) return;
        if (m_visualizerGO != null) return;

        // ドームは地面追従させるため、親を持たない独立GOとして配置する。
        // LateUpdate で「手のXZ + 真下地面Y」に再配置するので親子追従は不要
        m_visualizerGO = new GameObject("BossHandCastVisualizer");
        m_visualizerGO.transform.position = Vector3.zero;
        m_visualizerGO.transform.rotation = Quaternion.identity;

        float radius = m_settings.magnetCastRadius;

        // 緯線: 0(底辺の赤道)、30、60、89度(ほぼ天頂)。上半球のみ
        float[] latitudes = { 0f, 30f, 60f, 89f };
        foreach (float lat in latitudes)
        {
            float rad = lat * Mathf.Deg2Rad;
            float h = Mathf.Sin(rad) * radius;
            float r = Mathf.Cos(rad) * radius;
            CreateRing(Vector3.up * h, r);
        }

        // 経線: 4方向に半周（地平→天頂の四半円）
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            CreateMeridianHalf(dir, radius);
        }
    }

    private void ShowVisualizer(MagneticPole pole)
    {
        if (m_visualizerGO == null) return;
        m_visualizerGO.SetActive(true);
        UpdateVisualizerTransform();
        Color c = pole == MagneticPole.N ? m_colorN : m_colorS;
        for (int i = 0; i < m_visualLines.Count; i++)
        {
            var lr = m_visualLines[i];
            if (lr == null) continue;
            lr.startColor = c;
            lr.endColor = c;
        }
    }

    private void HideVisualizer()
    {
        if (m_visualizerGO == null) return;
        m_visualizerGO.SetActive(false);
    }

    private void CreateRing(Vector3 localCenter, float radius)
    {
        var go = new GameObject("Ring");
        go.transform.SetParent(m_visualizerGO.transform);
        go.transform.localPosition = localCenter;
        go.transform.localRotation = Quaternion.identity;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = m_lineWidth;
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

    private void CreateMeridianHalf(Vector3 dir, float radius)
    {
        var go = new GameObject("Meridian");
        go.transform.SetParent(m_visualizerGO.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.widthMultiplier = m_lineWidth;
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
}

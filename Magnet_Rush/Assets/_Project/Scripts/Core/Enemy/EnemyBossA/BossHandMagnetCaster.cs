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
    private const int RingSegments = 48;
    private const int MeridianSegments = 24;
    private static readonly float[] s_latitudes = { 0f, 30f, 60f, 89f };

    [Header("References")]
    [Tooltip("ドームと範囲キャストの中心となる右手 Magnetizable")]
    [SerializeField] private Magnetizable m_handMagnetizable;

    [Tooltip("State 監視用。AttackStance / AttackMotion への突入を検知する")]
    [SerializeField] private EnemyBossAI m_bossAI;

    [Tooltip("ボス本体（StatusData の magnetCastRadius を参照するため）")]
    [SerializeField] private EnemyBossBase m_boss;

    [Header("Visualizer")]
    [Tooltip("N極時のリング色")]
    [SerializeField] private Color m_colorN = new Color(1f, 0.5f, 0.5f, 0.22f);

    [Tooltip("S極時のリング色")]
    [SerializeField] private Color m_colorS = new Color(0.5f, 0.65f, 1f, 0.22f);

    [Tooltip("LineRendererの太さ")]
    [SerializeField] private float m_lineWidth = 0.035f;

    [Header("Charge Preview")]
    [SerializeField] private BulletSettings m_referenceBulletSettings;
    [SerializeField] private GameObject m_chargeEffectN;
    [SerializeField] private GameObject m_chargeEffectS;
    [SerializeField, Min(0f)] private float m_chargeDuration = 1.1f;
    [SerializeField, Min(0f)] private float m_visualizerStartScale = 0.05f;
    [SerializeField, Min(0f)] private float m_effectStartScale = 0.05f;
    [Tooltip("ONならエフェクト終端サイズを magnetCastRadius に合わせる")]
    [SerializeField] private bool m_matchEffectEndScaleToMagnetRadius = true;
    [Tooltip("magnetCastRadius に合わせる時の倍率")]
    [SerializeField, Min(0f)] private float m_effectRadiusMultiplier = 1.3f;
    [Tooltip("Match Effect End Scale To Magnet Radius がOFFの時に使う手動終端サイズ")]
    [SerializeField, Min(0f)] private float m_effectEndScale = 20f;
    [SerializeField] private float m_effectGroundOffset = 0.05f;
    [Tooltip("ONなら広がりきったタイミングで予兆エフェクトを消す")]
    [SerializeField] private bool m_destroyChargeEffectOnComplete = true;
    [SerializeField] private bool m_delayMagnetUntilChargeComplete = true;
    [SerializeField] private bool m_tintChargeEffect = true;
    [SerializeField] private AnimationCurve m_chargeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    [SerializeField] private bool m_logCast = true;

    private readonly List<Magnetizable> m_affected = new List<Magnetizable>();
    private static readonly Collider[] s_overlapBuffer = new Collider[64];
    private EnemyBossSettings m_settings;
    private bool m_wasInCastableState;

    private GameObject m_visualizerGO;
    private GameObject m_chargeEffectInstance;
    private readonly List<LineRenderer> m_visualLines = new List<LineRenderer>();
    private static Material s_lineMaterial;
    private float m_visualizerRadius = -1f;
    private MagneticPole m_pendingPole = MagneticPole.None;
    private MagneticPole m_visiblePole = MagneticPole.None;
    private float m_chargeElapsed;
    private bool m_isCharging;
    private bool m_hasAppliedCharge;
    private bool m_hasCompletedChargeVisual;

    private void Awake()
    {
        if (m_handMagnetizable == null) m_handMagnetizable = GetComponent<Magnetizable>();
        if (m_boss != null) m_settings = m_boss.StatusData;
        BuildVisualizer();
        HideVisualizer();
    }

    private void OnDisable()
    {
        StopChargePreview();
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

        DestroyChargeEffect();
    }

    private void Update()
    {
        bool inState = IsBossInCastableState();

        if (inState && !m_wasInCastableState)
        {
            MagneticPole pole = Random.value < 0.5f ? MagneticPole.N : MagneticPole.S;
            BeginChargePreview(pole);
        }

        if (inState && m_isCharging)
        {
            UpdateChargePreview();
            ApplyLivePreviewSettings();
        }
        else if (!inState && m_wasInCastableState)
        {
            StopChargePreview();
            ClearAffected();
            HideVisualizer();
        }

        m_wasInCastableState = inState;
    }

    private void LateUpdate()
    {
        if (m_visualizerGO != null && m_visualizerGO.activeSelf)
            UpdateVisualizerTransform();

        if (m_chargeEffectInstance != null)
            m_chargeEffectInstance.transform.position = GetGroundCenterPosition() + Vector3.up * m_effectGroundOffset;
    }

    /// <summary>
    /// ドームを「ボスのXZ位置 + 真下の地面Y」に毎フレーム再配置する。
    /// 底辺がそのまま地面に張り付くようローカル原点に半球を作ってあるので、ワールド位置だけ動かせば良い。
    /// </summary>
    private void UpdateVisualizerTransform()
    {
        if (m_visualizerGO == null) return;

        m_visualizerGO.transform.position = GetGroundCenterPosition();
        m_visualizerGO.transform.rotation = Quaternion.identity;
    }

    private Vector3 GetGroundCenterPosition()
    {
        if (m_boss == null) return transform.position;

        Vector3 bossPos = m_boss.transform.position;
        float groundY = bossPos.y;
        int groundMask = PhysicsLayers.Bit(PhysicsLayers.Default)
            | PhysicsLayers.Bit(PhysicsLayers.Ground) | PhysicsLayers.Bit(PhysicsLayers.Wall);
        if (Physics.Raycast(bossPos + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 100f, groundMask, QueryTriggerInteraction.Ignore))
            groundY = hit.point.y;

        return new Vector3(bossPos.x, groundY, bossPos.z);
    }

    private bool IsBossInCastableState()
    {
        if (m_bossAI == null)
        { ChannelLogger.LogGuardReturn("EnemyBossA", "m_bossAI 未アサインのため state 判定不能"); return false; }

        // 状態集合は EnemyBossAI.IsArmAttackState と共有（磁力リセットとドームキャストの対象ズレ防止）
        return EnemyBossAI.IsArmAttackState(m_bossAI.State);
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

    private void BeginChargePreview(MagneticPole pole)
    {
        m_pendingPole = pole;
        m_chargeElapsed = 0f;
        m_isCharging = true;
        m_hasAppliedCharge = false;
        m_hasCompletedChargeVisual = false;

        ShowVisualizer(pole);
        SpawnChargeEffect(pole);
        SetChargeProgress(0f);

        if (!m_delayMagnetUntilChargeComplete)
            ApplyPendingCast();

        if (m_chargeDuration <= 0f)
        {
            SetChargeProgress(1f);
            CompleteChargePreview();
        }
    }

    private void UpdateChargePreview()
    {
        if (m_chargeDuration > 0f)
            m_chargeElapsed += Time.deltaTime;

        float progress = m_chargeDuration <= 0f ? 1f : Mathf.Clamp01(m_chargeElapsed / m_chargeDuration);
        SetChargeProgress(progress);

        if (progress >= 1f)
            CompleteChargePreview();
    }

    private void CompleteChargePreview()
    {
        if (!m_hasAppliedCharge)
            ApplyPendingCast();

        if (m_hasCompletedChargeVisual) return;
        m_hasCompletedChargeVisual = true;

        if (m_destroyChargeEffectOnComplete)
            DestroyChargeEffect();
    }

    private void ApplyPendingCast()
    {
        if (m_hasAppliedCharge || m_pendingPole == MagneticPole.None) return;

        Cast(m_pendingPole);
        m_hasAppliedCharge = true;
    }

    private void StopChargePreview()
    {
        m_isCharging = false;
        m_hasAppliedCharge = false;
        m_hasCompletedChargeVisual = false;
        m_pendingPole = MagneticPole.None;
        m_chargeElapsed = 0f;
        DestroyChargeEffect();
    }

    private void SetChargeProgress(float progress)
    {
        float curved = m_chargeCurve != null ? m_chargeCurve.Evaluate(progress) : progress;
        curved = Mathf.Clamp01(curved);

        if (m_visualizerGO != null)
        {
            float visualScale = Mathf.Lerp(m_visualizerStartScale, 1f, curved);
            m_visualizerGO.transform.localScale = Vector3.one * Mathf.Max(0f, visualScale);
        }

        if (m_chargeEffectInstance != null)
        {
            float effectScale = Mathf.Lerp(m_effectStartScale, GetEffectEndScale(), curved);
            m_chargeEffectInstance.transform.localScale = Vector3.one * Mathf.Max(0f, effectScale);
        }
    }

    private void SpawnChargeEffect(MagneticPole pole)
    {
        DestroyChargeEffect();

        GameObject prefab = GetChargeEffectPrefab(pole);
        if (prefab == null) return;

        m_chargeEffectInstance = Instantiate(prefab, GetGroundCenterPosition() + Vector3.up * m_effectGroundOffset, Quaternion.identity);
        m_chargeEffectInstance.SetActive(true);

        if (m_tintChargeEffect)
            TintChargeEffect(m_chargeEffectInstance, GetPoleColor(pole));
    }

    private GameObject GetChargeEffectPrefab(MagneticPole pole)
    {
        if (pole == MagneticPole.S)
            return m_chargeEffectS != null ? m_chargeEffectS : m_referenceBulletSettings != null ? m_referenceBulletSettings.impactEffect_S : null;

        return m_chargeEffectN != null ? m_chargeEffectN : m_referenceBulletSettings != null ? m_referenceBulletSettings.impactEffect_N : null;
    }

    private float GetEffectEndScale()
    {
        if (m_matchEffectEndScaleToMagnetRadius && m_settings != null)
            return Mathf.Max(0f, m_settings.magnetCastRadius * m_effectRadiusMultiplier);

        if (m_effectEndScale > 0f) return m_effectEndScale;
        return m_referenceBulletSettings != null ? Mathf.Max(0f, m_referenceBulletSettings.impactEffectScale) : 1f;
    }

    private Color GetPoleColor(MagneticPole pole)
    {
        return pole == MagneticPole.N ? m_colorN : m_colorS;
    }

    private void TintChargeEffect(GameObject root, Color color)
    {
        var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            var main = particleSystems[i].main;
            main.startColor = color;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var materials = renderers[i].materials;
            for (int j = 0; j < materials.Length; j++)
            {
                var material = materials[j];
                if (material == null) continue;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", color);
                if (material.HasProperty("_TintColor"))
                    material.SetColor("_TintColor", color);
            }
        }
    }

    private void DestroyChargeEffect()
    {
        if (m_chargeEffectInstance == null) return;

        if (Application.isPlaying) Destroy(m_chargeEffectInstance);
        else DestroyImmediate(m_chargeEffectInstance);

        m_chargeEffectInstance = null;
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

        float radius = Mathf.Max(0f, m_settings.magnetCastRadius);
        m_visualizerRadius = radius;

        // 緯線: 0(底辺の赤道)、30、60、89度(ほぼ天頂)。上半球のみ
        foreach (float lat in s_latitudes)
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

    private void RefreshVisualizerGeometry()
    {
        if (m_visualizerGO == null || m_settings == null) return;

        float radius = Mathf.Max(0f, m_settings.magnetCastRadius);
        if (Mathf.Approximately(m_visualizerRadius, radius)) return;

        m_visualizerRadius = radius;

        int lineIndex = 0;
        foreach (float lat in s_latitudes)
        {
            if (lineIndex >= m_visualLines.Count) return;

            float rad = lat * Mathf.Deg2Rad;
            float h = Mathf.Sin(rad) * radius;
            float r = Mathf.Cos(rad) * radius;
            SetRingGeometry(m_visualLines[lineIndex], Vector3.up * h, r);
            lineIndex++;
        }

        for (int i = 0; i < 4; i++)
        {
            if (lineIndex >= m_visualLines.Count) return;

            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            SetMeridianGeometry(m_visualLines[lineIndex], dir, radius);
            lineIndex++;
        }
    }

    private void ShowVisualizer(MagneticPole pole)
    {
        if (m_visualizerGO == null) return;
        m_visiblePole = pole;
        m_visualizerGO.SetActive(true);
        UpdateVisualizerTransform();
        ApplyVisualizerStyle(pole);
    }

    private void ApplyLivePreviewSettings()
    {
        RefreshVisualizerGeometry();

        if (m_visualizerGO != null && m_visualizerGO.activeSelf && m_visiblePole != MagneticPole.None)
            ApplyVisualizerStyle(m_visiblePole);

        if (m_chargeEffectInstance != null && m_tintChargeEffect && m_pendingPole != MagneticPole.None)
            TintChargeEffect(m_chargeEffectInstance, GetPoleColor(m_pendingPole));
    }

    private void ApplyVisualizerStyle(MagneticPole pole)
    {
        Color c = GetPoleColor(pole);
        for (int i = 0; i < m_visualLines.Count; i++)
        {
            var lr = m_visualLines[i];
            if (lr == null) continue;
            lr.widthMultiplier = m_lineWidth;
            lr.startColor = c;
            lr.endColor = c;
        }
    }

    private void HideVisualizer()
    {
        if (m_visualizerGO == null) return;
        m_visiblePole = MagneticPole.None;
        m_visualizerGO.transform.localScale = Vector3.one;
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

        SetRingGeometry(lr, localCenter, radius);

        m_visualLines.Add(lr);
    }

    private void SetRingGeometry(LineRenderer lr, Vector3 localCenter, float radius)
    {
        if (lr == null) return;

        lr.transform.localPosition = localCenter;
        lr.positionCount = RingSegments;
        for (int i = 0; i < RingSegments; i++)
        {
            float a = (float)i / RingSegments * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
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

        SetMeridianGeometry(lr, dir, radius);

        m_visualLines.Add(lr);
    }

    private void SetMeridianGeometry(LineRenderer lr, Vector3 dir, float radius)
    {
        if (lr == null) return;

        lr.transform.localPosition = Vector3.zero;
        lr.positionCount = MeridianSegments + 1;
        for (int i = 0; i <= MeridianSegments; i++)
        {
            float a = (float)i / MeridianSegments * Mathf.PI / 2f;
            Vector3 pos = dir * (Mathf.Cos(a) * radius) + Vector3.up * (Mathf.Sin(a) * radius);
            lr.SetPosition(i, pos);
        }
    }

    private static void SetupLineMaterial(LineRenderer lr)
    {
        if (s_lineMaterial == null)
            s_lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lr.material = s_lineMaterial;
    }
}

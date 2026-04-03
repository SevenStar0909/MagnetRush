using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 磁力場コンポーネント。形状ベースの方向計算、減衰、ダメージ蓄積、ライフタイムを管理する。
/// 弾のGOにAddComponentされ、弾と一体のライフサイクルで動作する。
/// トリガーベースで範囲内のEntityを検知する。
/// </summary>
public class MagnetField : MonoBehaviour, IMagnetField
{
    [FormerlySerializedAs("settings")]
    [SerializeField] private MagnetFieldSettings m_settings;

    private MagneticPole m_pole;
    private float m_remainingLifetime;
    private float m_storedDamage;
    private bool m_initialized;
    private bool m_expired;
    private SphereCollider m_triggerCollider;

    // トリガー検知用キャッシュ（GravityFieldパターン）
    private readonly Dictionary<Collider, Entity> m_entityCache = new();
    private readonly List<Entity> m_entitiesInRange = new();

    // --- IMagnetField ---
    public MagneticPole Pole => m_pole;
    public int Priority => 0;
    public Vector3 Center => transform.position;
    public bool IsDestroyed => this == null;

    // --- Events ---
    public event Action OnFieldExpired;
    public float StoredDamage => m_storedDamage;
    public float InnerRadius => m_settings != null ? m_settings.innerRadius : 3f;
    public float OuterRadius => m_settings != null ? m_settings.EffectiveOuterRadius : 8f;

    // --- 形状プロパティ ---
    public FieldShape Shape => m_settings != null ? m_settings.shape : FieldShape.Sphere;
    public Vector3 Size => m_settings != null ? Vector3.Scale(m_settings.size, transform.lossyScale) : Vector3.one;
    public float CylinderHeight => m_settings != null ? m_settings.cylinderHeight * transform.lossyScale.y : 4f;
    public float CylinderRadius => m_settings != null ? m_settings.cylinderRadius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : 1f;
    public Vector3 Top => Center + transform.up * (CylinderHeight * 0.5f);
    public Vector3 Bottom => Center - transform.up * (CylinderHeight * 0.5f);

    /// <summary>範囲内のEntity一覧。MagnetManagerがフィールド割り当てに使用する。</summary>
    public List<Entity> GetEntitiesInRange() => m_entitiesInRange;

    /// <summary>
    /// フィールドを初期化する。MagnetBullet.StickToSurface等から呼ぶ。
    /// </summary>
    public void Initialize(MagneticPole fieldPole, MagnetFieldSettings fieldSettings)
    {
        m_pole = fieldPole;
        m_settings = fieldSettings;
        m_remainingLifetime = m_settings.lifetime;
        SetupTriggerCollider();
        m_initialized = true;

        // sibling の Magnetizable にフィールド参照を登録
        var mag = GetComponent<Magnetizable>();
        if (mag != null) mag.SetField(this);
    }

    private GameObject m_triggerGO;

    private void SetupTriggerCollider()
    {
        // 専用の子GOにトリガーを配置し MagnetField レイヤーに設定
        // 親GOのレイヤーを変えずに、Bullet レイヤーとの衝突を Layer Matrix で遮断できる
        m_triggerGO = new GameObject("MagnetFieldTrigger");
        m_triggerGO.transform.SetParent(transform, false);
        m_triggerGO.layer = PhysicsLayers.MagnetField;

        m_triggerCollider = m_triggerGO.AddComponent<SphereCollider>();
        m_triggerCollider.isTrigger = true;
        m_triggerCollider.radius = CalcTriggerRadius();

        // Rigidbody が必要（トリガーイベント発火のため）。kinematic で物理影響なし
        var rb = m_triggerGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // ブリッジ: 子GOのトリガーイベントを親MagnetFieldに転送
        var bridge = m_triggerGO.AddComponent<MagnetFieldTriggerBridge>();
        bridge.Initialize(this);
    }

    private float CalcTriggerRadius()
    {
        if (m_settings == null) return 8f;

        return Shape switch
        {
            FieldShape.Box => m_settings.size.magnitude * 0.5f + m_settings.EffectiveOuterRadius,
            FieldShape.Cylinder => Mathf.Max(m_settings.cylinderHeight * 0.5f, m_settings.cylinderRadius) + m_settings.EffectiveOuterRadius,
            _ => m_settings.EffectiveOuterRadius
        };
    }

    /// <summary>
    /// 形状の最近接面から point への方向ベクトル（正規化済み）。
    /// </summary>
    public Vector3 GetFieldDirection(Vector3 point)
    {
        Vector3 dir = Shape switch
        {
            FieldShape.Box => (point - BoundsHelper.NearestPointOnBox(Center, Size, transform.rotation, point)).normalized,
            FieldShape.Cylinder => (point - BoundsHelper.NearestPointOnFiniteLine(Top, Bottom, point)).normalized,
            _ => (point - Center).normalized
        };

        return dir == Vector3.zero ? Vector3.up : dir;
    }

    /// <summary>
    /// point での磁力強度（0〜1）。表面からの距離でinner/outer減衰。
    /// </summary>
    public float GetStrengthAt(Vector3 point)
    {
        if (m_settings == null) return 0f;

        Vector3 nearestSurface = Shape switch
        {
            FieldShape.Box => BoundsHelper.NearestPointOnBox(Center, Size, transform.rotation, point),
            FieldShape.Cylinder => BoundsHelper.NearestPointOnFiniteLine(Top, Bottom, point),
            _ => Center
        };

        float dist = Vector3.Distance(point, nearestSurface);
        if (dist <= m_settings.innerRadius) return 1f;
        if (dist >= m_settings.EffectiveOuterRadius) return 0f;

        return 1f - (dist - m_settings.innerRadius) / (m_settings.EffectiveOuterRadius - m_settings.innerRadius);
    }

    /// <summary>
    /// ダメージを蓄積する。maxStoredDamageでクランプ。
    /// </summary>
    public void AccumulateDamage(float amount)
    {
        if (m_settings == null || !m_settings.accumulateDamage) return;
        m_storedDamage = Mathf.Min(m_storedDamage + amount, m_settings.maxStoredDamage);
    }

    /// <summary>ブリッジから呼ばれるトリガーStay。</summary>
    public void HandleTriggerStay(Collider other)
    {
        if (!m_initialized) return;

        if (!m_entityCache.TryGetValue(other, out var entity))
        {
            entity = other.GetComponent<Entity>();
            m_entityCache[other] = entity;
        }

        if (entity != null && !m_entitiesInRange.Contains(entity))
            m_entitiesInRange.Add(entity);
    }

    /// <summary>ブリッジから呼ばれるトリガーExit。</summary>
    public void HandleTriggerExit(Collider other)
    {
        if (!m_initialized) return;

        if (m_entityCache.TryGetValue(other, out var entity))
        {
            if (entity != null)
                m_entitiesInRange.Remove(entity);
            m_entityCache.Remove(other);
        }

    }

    /// <summary>ブリッジから呼ばれるトリガーEnter。</summary>
    public void HandleTriggerEnter(Collider other)
    {
        if (!m_initialized) return;
    }

    void OnEnable()
    {
        if (MagnetManager.Instance != null)
            MagnetManager.Instance.RegisterField(this);
    }

    void OnDisable()
    {
        if (MagnetManager.Instance != null)
            MagnetManager.Instance.UnregisterField(this);

        if (m_triggerGO != null)
            Destroy(m_triggerGO);

        m_entitiesInRange.Clear();
        m_entityCache.Clear();
    }

    void Update()
    {
        if (m_settings == null) return;

        // lifetime=0 は永続（タイマー不動）
        if (m_settings.lifetime <= 0f) return;

        m_remainingLifetime -= Time.deltaTime;
        if (m_remainingLifetime <= 0f)
            ForceExpire();
    }

    /// <summary>
    /// フィールドを強制期限切れにする。OnFieldExpired を発火してから自身を破棄する。
    /// リロード時や外部からの明示的な停止に使用。
    /// </summary>
    public void ForceExpire()
    {
        if (m_expired) return;
        m_expired = true;
        OnFieldExpired?.Invoke();
        Destroy(this);
    }
}

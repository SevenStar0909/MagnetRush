using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 磁力場コンポーネント。形状ベースの方向計算、減衰、ダメージ蓄積、ライフタイムを管理する。
/// 弾のGOにAddComponentされ、弾と一体のライフサイクルで動作する。
/// トリガーベースで範囲内のEntityを検知する。
/// </summary>
public class MagnetField : MonoBehaviour, IMagnetField
{
    [SerializeField] private MagnetFieldSettings settings;

    private MagneticPole pole;
    private float remainingLifetime;
    private float storedDamage;
    private bool m_initialized;
    private SphereCollider m_triggerCollider;

    // トリガー検知用キャッシュ（GravityFieldパターン）
    private readonly Dictionary<Collider, Entity> m_entityCache = new();
    private readonly List<Entity> m_entitiesInRange = new();

    // --- IMagnetField ---
    public MagneticPole Pole => pole;
    public int Priority => 0;
    public Vector3 Center => transform.position;
    public bool IsDestroyed => this == null;

    // --- Events ---
    public event Action OnFieldExpired;
    public event Action<Magnetizable> OnObjectEnter;
    public event Action<Magnetizable> OnObjectExit;

    // --- Public API ---
    public MagnetFieldSettings Settings => settings;
    public float StoredDamage => storedDamage;
    public float OuterRadius => settings != null ? settings.outerRadius : 8f;

    // --- 形状プロパティ ---
    public FieldShape Shape => settings != null ? settings.shape : FieldShape.Sphere;
    public Vector3 Size => settings != null ? Vector3.Scale(settings.size, transform.lossyScale) : Vector3.one;
    public float CylinderHeight => settings != null ? settings.cylinderHeight * transform.lossyScale.y : 4f;
    public float CylinderRadius => settings != null ? settings.cylinderRadius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : 1f;
    public Vector3 Top => Center + transform.up * (CylinderHeight * 0.5f);
    public Vector3 Bottom => Center - transform.up * (CylinderHeight * 0.5f);

    /// <summary>範囲内のEntity一覧。MagnetManagerがフィールド割り当てに使用する。</summary>
    public List<Entity> GetEntitiesInRange() => m_entitiesInRange;

    /// <summary>
    /// フィールドを初期化する。MagnetBullet.StickToSurface等から呼ぶ。
    /// </summary>
    public void Initialize(MagneticPole fieldPole, MagnetFieldSettings fieldSettings)
    {
        pole = fieldPole;
        settings = fieldSettings;
        remainingLifetime = settings.lifetime;
        SetupTriggerCollider();
        m_initialized = true;
    }

    private GameObject m_triggerGO;

    private void SetupTriggerCollider()
    {
        // 専用の子GOにトリガーを配置し MagnetField レイヤーに設定
        // 親GOのレイヤーを変えずに、Bullet レイヤーとの衝突を Layer Matrix で遮断できる
        m_triggerGO = new GameObject("MagnetFieldTrigger");
        m_triggerGO.transform.SetParent(transform, false);
        m_triggerGO.layer = LayerMask.NameToLayer("MagnetField");

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
        if (settings == null) return 8f;

        return Shape switch
        {
            FieldShape.Box => settings.size.magnitude * 0.5f + settings.outerRadius,
            FieldShape.Cylinder => Mathf.Max(settings.cylinderHeight * 0.5f, settings.cylinderRadius) + settings.outerRadius,
            _ => settings.outerRadius
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
        if (settings == null) return 0f;

        Vector3 nearestSurface = Shape switch
        {
            FieldShape.Box => BoundsHelper.NearestPointOnBox(Center, Size, transform.rotation, point),
            FieldShape.Cylinder => BoundsHelper.NearestPointOnFiniteLine(Top, Bottom, point),
            _ => Center
        };

        float dist = Vector3.Distance(point, nearestSurface);
        if (dist <= settings.innerRadius) return 1f;
        if (dist >= settings.outerRadius) return 0f;

        return 1f - (dist - settings.innerRadius) / (settings.outerRadius - settings.innerRadius);
    }

    /// <summary>
    /// ダメージを蓄積する。maxStoredDamageでクランプ。
    /// </summary>
    public void AccumulateDamage(float amount)
    {
        if (settings == null || !settings.accumulateDamage) return;
        storedDamage = Mathf.Min(storedDamage + amount, settings.maxStoredDamage);
    }

    /// <summary>MagnetManagerから呼ばれるEnter通知。</summary>
    public void NotifyObjectEnter(Magnetizable m) => OnObjectEnter?.Invoke(m);

    /// <summary>MagnetManagerから呼ばれるExit通知。</summary>
    public void NotifyObjectExit(Magnetizable m) => OnObjectExit?.Invoke(m);

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

        var mag = other.GetComponent<Magnetizable>();
        if (mag != null)
            OnObjectExit?.Invoke(mag);
    }

    /// <summary>ブリッジから呼ばれるトリガーEnter。</summary>
    public void HandleTriggerEnter(Collider other)
    {
        if (!m_initialized) return;

        var mag = other.GetComponent<Magnetizable>();
        if (mag != null)
            OnObjectEnter?.Invoke(mag);
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
        if (settings == null) return;

        // lifetime=0 は永続（タイマー不動）
        if (settings.lifetime <= 0f) return;

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
            ForceExpire();
    }

    /// <summary>
    /// フィールドを強制期限切れにする。OnFieldExpired を発火してから自身を破棄する。
    /// リロード時や外部からの明示的な停止に使用。
    /// </summary>
    public void ForceExpire()
    {
        OnFieldExpired?.Invoke();
        Destroy(this);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (settings == null) return;

        var color = pole == MagneticPole.S
            ? new Color(0.2f, 0.4f, 1f, 0.5f)
            : new Color(1f, 0.2f, 0.2f, 0.5f);

        Gizmos.color = color;

        switch (Shape)
        {
            case FieldShape.Box:
                var matrix = Matrix4x4.TRS(Center, transform.rotation, Vector3.one);
                Gizmos.matrix = matrix;
                Gizmos.DrawWireCube(Vector3.zero, Size);
                color.a = 0.2f;
                Gizmos.color = color;
                var outerSize = Size + Vector3.one * settings.outerRadius * 2f;
                Gizmos.DrawWireCube(Vector3.zero, outerSize);
                Gizmos.matrix = Matrix4x4.identity;
                break;

            case FieldShape.Cylinder:
                DrawWireCircle(Top, transform.up, CylinderRadius);
                DrawWireCircle(Bottom, transform.up, CylinderRadius);
                Gizmos.DrawLine(Top + transform.right * CylinderRadius, Bottom + transform.right * CylinderRadius);
                Gizmos.DrawLine(Top - transform.right * CylinderRadius, Bottom - transform.right * CylinderRadius);
                Gizmos.DrawLine(Top + transform.forward * CylinderRadius, Bottom + transform.forward * CylinderRadius);
                Gizmos.DrawLine(Top - transform.forward * CylinderRadius, Bottom - transform.forward * CylinderRadius);
                color.a = 0.2f;
                Gizmos.color = color;
                DrawWireCircle(Top, transform.up, CylinderRadius + settings.outerRadius);
                DrawWireCircle(Bottom, transform.up, CylinderRadius + settings.outerRadius);
                break;

            default:
                Gizmos.DrawWireSphere(Center, settings.innerRadius);
                color.a = 0.2f;
                Gizmos.color = color;
                Gizmos.DrawWireSphere(Center, settings.outerRadius);
                break;
        }
    }

    void DrawWireCircle(Vector3 center, Vector3 normal, float radius, int segments = 32)
    {
        var rot = Quaternion.LookRotation(normal);
        var prev = center + rot * (Vector3.up * radius);

        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            var next = center + rot * (new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
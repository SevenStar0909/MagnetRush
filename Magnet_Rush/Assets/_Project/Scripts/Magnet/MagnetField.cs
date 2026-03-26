using System;
using UnityEngine;

/// <summary>
/// 磁力場コンポーネント。形状ベースの方向計算、減衰、ダメージ蓄積、ライフタイムを管理する。
/// 弾のGOにAddComponentされ、弾と一体のライフサイクルで動作する。
/// </summary>
public class MagnetField : MonoBehaviour, IMagnetField
{
    [SerializeField] private MagnetFieldSettings settings;

    private MagneticPole pole;
    private float remainingLifetime;
    private float storedDamage;

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

    /// <summary>
    /// フィールドを初期化する。MagnetBullet.StickToSurface等から呼ぶ。
    /// </summary>
    public void Initialize(MagneticPole fieldPole, MagnetFieldSettings fieldSettings)
    {
        pole = fieldPole;
        settings = fieldSettings;
        remainingLifetime = settings.lifetime;
    }

    /// <summary>
    /// 形状の最近接面から point への方向ベクトル（正規化済み）。
    /// Sphere: 中心 → point の方向。
    /// </summary>
    public Vector3 GetFieldDirection(Vector3 point)
    {
        // プロトではSphereのみ実装
        Vector3 dir = (point - Center).normalized;
        return dir == Vector3.zero ? Vector3.up : dir;
    }

    /// <summary>
    /// point での磁力強度（0〜1）。inner内=1、inner〜outer=lerp、outer外=0。
    /// </summary>
    public float GetStrengthAt(Vector3 point)
    {
        if (settings == null) return 0f;

        float distance = Vector3.Distance(point, Center);
        if (distance <= settings.innerRadius) return 1f;
        if (distance >= settings.outerRadius) return 0f;

        // inner〜outer で線形補間
        return 1f - (distance - settings.innerRadius) / (settings.outerRadius - settings.innerRadius);
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

    void Update()
    {
        if (settings == null) return;

        // lifetime=0 は永続（タイマー不動）
        if (settings.lifetime <= 0f) return;

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            OnFieldExpired?.Invoke();
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (settings == null) return;

        var color = pole == MagneticPole.S
            ? new Color(1f, 0.2f, 0.2f, 0.5f)
            : new Color(0.2f, 0.4f, 1f, 0.5f);

        // inner radius（実線）
        Gizmos.color = color;
        Gizmos.DrawWireSphere(Center, settings.innerRadius);

        // outer radius（半透明）
        color.a = 0.2f;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(Center, settings.outerRadius);
    }
#endif
}
using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 磁力弾。着弾時にパターン1（壁にくっつく）またはパターン2（弾消去＋磁化）を実行する。
/// 飛行中はMagnetFieldの影響で弾道が曲がる。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class MagnetBullet : MonoBehaviour
{
    [FormerlySerializedAs("settings")]
    [SerializeField] private BulletSettings m_settings;

    public MagneticPole Pole { get; private set; }
    public bool IsStuck { get; private set; }
    public bool IsSelfFire { get; private set; }

    /// <summary>弾が何かに着弾した時に発火するコールバック。</summary>
    public event Action OnImpact;

    private Rigidbody m_rb;
    private float m_timer;
    private bool m_registered;
    // 生成したエフェクトのインスタンスを保持する変数
    private GameObject m_nEffectInstance;
    private GameObject m_sEffectInstance;

    void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
    }

    public void Initialize(MagneticPole pole, Vector3 direction, bool isSelfFire = false)
    {
        Pole = pole;
        IsSelfFire = isSelfFire;
        m_rb.isKinematic = false;
        m_rb.useGravity = false;
        m_rb.linearVelocity = direction.normalized * m_settings.bulletSpeed;
        m_timer = m_settings.lifetime;

        // ビジュアル切替（S=赤、N=青）
        var renderer = GetComponent<MeshRenderer>();
        if (renderer != null && m_settings != null)
        {
            Material mat = pole == MagneticPole.S ? m_settings.sMaterial : m_settings.nMaterial;
            if (mat != null) renderer.material = mat;
        }

        // 発射時にエフェクトを初期化して描画
        InitializeEffects(pole);

        // BulletManager登録
        if (BulletManager.Instance != null)
        {
            BulletManager.Instance.Register(this);
            m_registered = true;
        }
    }

    private void InitializeEffects(MagneticPole pole)
    {
        if (m_settings == null) return;
        GameObject prefab = pole == MagneticPole.S ? m_settings.fireEffect_S : m_settings.fireEffect_N;
        if (prefab == null) return;

        var instance = Instantiate(prefab, transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one * m_settings.fireEffectScale;
        instance.SetActive(true);

        if (pole == MagneticPole.N)
            m_nEffectInstance = instance;
        else
            m_sEffectInstance = instance;
    }

    void Update()
    {
        if (IsStuck) return;

        // timeScaleの影響を受けない（エイム中に寿命が短くならない）
        m_timer -= Time.unscaledDeltaTime;
        if (m_timer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    void FixedUpdate()
    {
        if (IsStuck || m_rb == null || m_settings == null) return;
        if (MagnetManager.Instance == null) return;

        var fields = MagnetManager.Instance.GetActiveFields();
        for (int i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (field == null) continue;

            float strength = field.GetStrengthAt(transform.position);
            if (strength <= 0f) continue;

            // 極性判定: 異極=吸引、同極=反発
            bool attract = Pole != field.Pole && field.Pole != MagneticPole.None && Pole != MagneticPole.None;
            Vector3 toCenter = (field.Center - transform.position).normalized;
            float pull = strength * m_settings.fieldAttractionFactor;

            m_rb.linearVelocity += (attract ? toCenter : -toCenter) * pull * Time.fixedDeltaTime;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsStuck) return;

        // トリガーコライダー（MagnetField の範囲検知用等）は無視。物理コライダーのみ反応
        if (other.isTrigger) return;

        // 他の弾 — MagnetFieldを持つ弾にはダメージ蓄積
        if (other.CompareTag(GameTags.MagnetBullet))
        {
            var otherField = other.GetComponent<MagnetField>();
            if (otherField != null && m_settings != null)
            {
                bool isOpposite = Pole != otherField.Pole && Pole != MagneticPole.None && otherField.Pole != MagneticPole.None;
                if (isOpposite)
                {
                    otherField.AccumulateDamage(m_settings.bulletDamage);
                    OnImpact?.Invoke();
                    Destroy(gameObject);
                }
            }
            return;
        }

        // 通常弾はプレイヤーを無視。SelfFire弾のみプレイヤーに当たる
        if (other.CompareTag(GameTags.Player) && !IsSelfFire) return;

        // 対象に Magnetizable があるか → パターン分岐
        var targetMag = other.GetComponent<Magnetizable>();

        if (targetMag != null)
        {
            MagnetizeTarget(other, targetMag);
        }
        else
        {
            Debug.Log($"[Bullet] → Pattern1: StickToSurface({other.name})");
            StickToSurface(other);
        }
    }

    /// <summary>
    /// パターン2: 弾消滅 + 対象オブジェクトを磁化。
    /// 対象から磁力オーラが出る。残弾は回復する。
    /// </summary>
    /// <summary>
    /// パターン2: 弾消滅 + 対象オブジェクトを磁化。
    /// 対象から磁力オーラが出る。残弾は回復する。
    /// 対象が OnFieldExpired を聞いて自分で磁化解除する。
    /// </summary>
    private void MagnetizeTarget(Collider target, Magnetizable targetMag)
    {
        targetMag.SetPole(Pole);

        if (target.GetComponent<MagnetField>() == null && m_settings != null && m_settings.bulletFieldSettings != null)
        {
            var field = target.gameObject.AddComponent<MagnetField>();
            field.Initialize(Pole, m_settings.bulletFieldSettings);

            if (MagnetManager.Instance != null)
                MagnetManager.Instance.RegisterField(field);

            var visualizer = target.gameObject.AddComponent<MagnetFieldVisualizer>();
            visualizer.Show(Pole, m_settings.bulletFieldSettings);

            // 着弾エフェクトを対象に生成
            var effectInstance = SpawnImpactEffect(target.transform);

            // フィールド期限切れ → 対象の磁化解除 + Visualizer + エフェクト除去
            field.OnFieldExpired += () =>
            {
                if (targetMag != null) targetMag.Deactivate();
                if (visualizer != null) Destroy(visualizer);
                if (effectInstance != null) Destroy(effectInstance);
            };
        }

        OnImpact?.Invoke();
        Destroy(gameObject);
    }

    /// <summary>
    /// パターン1: 弾がくっつき、弾自身が磁力源。壁/天井/タレット用。
    /// フィールド期限切れで弾ごと消える。
    /// </summary>
    private void StickToSurface(Collider surface)
    {
        IsStuck = true;
        m_rb.linearVelocity = Vector3.zero;
        m_rb.isKinematic = true;
        transform.SetParent(surface.transform);

        var mag = GetComponent<Magnetizable>();
        if (mag != null)
        {
            mag.SetPole(Pole);
            mag.mass = Mathf.Infinity;
        }

        if (m_settings != null && m_settings.bulletFieldSettings != null)
        {
            var field = gameObject.AddComponent<MagnetField>();
            field.Initialize(Pole, m_settings.bulletFieldSettings);

            if (MagnetManager.Instance != null)
                MagnetManager.Instance.RegisterField(field);

            var visualizer = gameObject.AddComponent<MagnetFieldVisualizer>();
            visualizer.Show(Pole, m_settings.bulletFieldSettings);

            // 着弾エフェクトを弾自身に生成（弾の子なのでフィールド期限切れで一緒に消える）
            SpawnImpactEffect(transform);

            // フィールド期限切れ → 弾ごと消える
            field.OnFieldExpired += () => Destroy(gameObject);
        }

        OnImpact?.Invoke();
    }

    /// <summary>
    /// 着弾エフェクトを対象の子として生成する。
    /// </summary>
    private GameObject SpawnImpactEffect(Transform parent)
    {
        if (m_settings == null) return null;
        GameObject prefab = Pole == MagneticPole.S ? m_settings.impactEffect_S : m_settings.impactEffect_N;
        if (prefab == null) return null;

        var instance = Instantiate(prefab, parent);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one * m_settings.impactEffectScale;
        return instance;
    }

    void OnDestroy()
    {
        // BulletManager解除（二重呼出ガード）
        if (m_registered && BulletManager.Instance != null)
        {
            BulletManager.Instance.Unregister(this);
            m_registered = false;
        }
    }
}

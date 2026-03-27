using System;
using UnityEngine;

/// <summary>
/// 磁力弾。着弾時にパターン①（壁にくっつく）またはパターン②（弾消去＋磁化）を実行する。
/// 飛行中はMagnetFieldの影響で弾道が曲がる。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class MagnetBullet : MonoBehaviour
{
    [SerializeField] private BulletSettings settings;

    public MagneticPole Pole { get; private set; }
    public bool IsStuck { get; private set; }

    /// <summary>弾が何かに着弾した時に発火するコールバック。</summary>
    public event Action OnImpact;

    private Rigidbody rb;
    private float timer;
    private bool registered;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Initialize(MagneticPole pole, Vector3 direction)
    {
        Pole = pole;
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.linearVelocity = direction.normalized * settings.bulletSpeed;
        timer = settings.lifetime;

        // ビジュアル切替（S=赤、N=青）
        var renderer = GetComponent<MeshRenderer>();
        if (renderer != null && settings != null)
        {
            Material mat = pole == MagneticPole.S ? settings.sMaterial : settings.nMaterial;
            if (mat != null) renderer.material = mat;
        }

        // BulletManager登録
        if (BulletManager.Instance != null)
        {
            BulletManager.Instance.Register(this);
            registered = true;
        }
    }

    void Update()
    {
        if (IsStuck) return;

        // timeScaleの影響を受けない（エイム中に寿命が短くならない）
        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    void FixedUpdate()
    {
        if (IsStuck || rb == null || settings == null) return;
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
            float pull = strength * settings.fieldAttractionFactor;

            rb.linearVelocity += (attract ? toCenter : -toCenter) * pull * Time.fixedDeltaTime;
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
            if (otherField != null && settings != null)
            {
                bool isOpposite = Pole != otherField.Pole && Pole != MagneticPole.None && otherField.Pole != MagneticPole.None;
                if (isOpposite)
                {
                    otherField.AccumulateDamage(settings.bulletDamage);
                    OnImpact?.Invoke();
                    Destroy(gameObject);
                }
            }
            return;
        }

        // プレイヤー自身は無視
        if (other.CompareTag(GameTags.Player)) return;

        // 対象に Magnetizable があるか → パターン分岐
        var targetMag = other.GetComponent<Magnetizable>();

        if (targetMag != null)
        {
            // パターン2: 弾が消え、オブジェクト自体が磁力源になる
            MagnetizeTarget(other, targetMag);
        }
        else
        {
            // パターン1: 弾がくっつき、弾が磁力源になる
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

        if (target.GetComponent<MagnetField>() == null && settings != null && settings.bulletFieldSettings != null)
        {
            var field = target.gameObject.AddComponent<MagnetField>();
            field.Initialize(Pole, settings.bulletFieldSettings);

            if (MagnetManager.Instance != null)
                MagnetManager.Instance.RegisterField(field);

            var visualizer = target.gameObject.AddComponent<MagnetFieldVisualizer>();
            visualizer.Show(Pole, settings.bulletFieldSettings.outerRadius);

            // フィールド期限切れ → 対象の磁化解除 + Visualizer 除去
            field.OnFieldExpired += () =>
            {
                targetMag.Deactivate();
                if (visualizer != null) Destroy(visualizer);
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
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        transform.SetParent(surface.transform);

        var mag = GetComponent<Magnetizable>();
        if (mag != null)
        {
            mag.SetPole(Pole);
            mag.mass = Mathf.Infinity;
        }

        if (settings != null && settings.bulletFieldSettings != null)
        {
            var field = gameObject.AddComponent<MagnetField>();
            field.Initialize(Pole, settings.bulletFieldSettings);

            if (MagnetManager.Instance != null)
                MagnetManager.Instance.RegisterField(field);

            var visualizer = gameObject.AddComponent<MagnetFieldVisualizer>();
            visualizer.Show(Pole, settings.bulletFieldSettings.outerRadius);

            // フィールド期限切れ → 弾ごと消える
            field.OnFieldExpired += () => Destroy(gameObject);
        }

        OnImpact?.Invoke();
    }

    void OnDestroy()
    {
        // BulletManager解除（二重呼出ガード）
        if (registered && BulletManager.Instance != null)
        {
            BulletManager.Instance.Unregister(this);
            registered = false;
        }
    }
}

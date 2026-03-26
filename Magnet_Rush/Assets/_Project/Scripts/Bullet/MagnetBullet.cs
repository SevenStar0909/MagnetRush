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

        // 他の弾 — MagnetFieldを持つ弾にはダメージ蓄積
        if (other.CompareTag(GameTags.MagnetBullet))
        {
            var otherField = other.GetComponent<MagnetField>();
            if (otherField != null && settings != null)
            {
                // 異極弾がフィールドに着弾 → ダメージ蓄積
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

        // パターン1: 壁/タレット/その他の静的オブジェクトにくっつく
        if (other.CompareTag(GameTags.Wall) || other.CompareTag(GameTags.Turret)
            || other.CompareTag(GameTags.Untagged))
        {
            StickToSurface(other);
        }
        // パターン2: 敵/敵武器/プレイヤーに当たると弾消去＋対象を磁化
        else if (other.CompareTag(GameTags.Enemy) || other.CompareTag(GameTags.EnemyWeapon)
                 || other.CompareTag(GameTags.Player))
        {
            // フォールバック: 技術的に2が困難な場合は1の動作
            if (settings != null && settings.useFallbackMode)
            {
                StickToSurface(other);
                return;
            }

            var magnetizable = other.GetComponent<Magnetizable>();
            if (magnetizable != null)
            {
                magnetizable.SetPole(Pole);
            }

            OnImpact?.Invoke();
            Destroy(gameObject);
        }
    }

    private void StickToSurface(Collider surface)
    {
        IsStuck = true;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        transform.SetParent(surface.transform);

        // 弾自身のMagnetizableを有効化（磁力源として機能させる）
        var mag = GetComponent<Magnetizable>();
        if (mag != null)
        {
            mag.SetPole(Pole);
            mag.mass = Mathf.Infinity; // 壁に固定 = 無限質量
        }

        // MagnetField を生成（MagnetFieldVisualizerの代替）
        CreateMagnetField();

        OnImpact?.Invoke();
    }

    /// <summary>
    /// 弾のGOにMagnetFieldを追加して磁力場を生成する。
    /// </summary>
    private void CreateMagnetField()
    {
        if (settings == null || settings.bulletFieldSettings == null) return;

        var field = gameObject.AddComponent<MagnetField>();
        field.Initialize(Pole, settings.bulletFieldSettings);

        // MagnetManagerに登録
        if (MagnetManager.Instance != null)
            MagnetManager.Instance.RegisterField(field);
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

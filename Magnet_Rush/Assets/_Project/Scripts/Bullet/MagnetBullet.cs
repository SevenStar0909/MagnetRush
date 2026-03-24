using System;
using UnityEngine;
using MagnetRush.Common;

/// <summary>
/// 磁力弾。着弾時にパターン①（壁にくっつく）またはパターン②（弾消去＋磁化）を実行する。
/// 可視化はMagnetFieldVisualizerに委譲（SRP）。
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

    void OnTriggerEnter(Collider other)
    {
        if (IsStuck) return;

        // 他の弾は無視
        if (other.CompareTag(GameTags.MagnetBullet)) return;

        // パターン①: 壁/タレット/その他の静的オブジェクトにくっつく
        if (other.CompareTag(GameTags.Wall) || other.CompareTag(GameTags.Turret)
            || other.CompareTag(GameTags.Untagged))
        {
            StickToSurface(other);
        }
        // パターン②: 敵/敵武器/プレイヤーに当たると弾消去＋対象を磁化
        else if (other.CompareTag(GameTags.Enemy) || other.CompareTag(GameTags.EnemyWeapon)
                 || other.CompareTag(GameTags.Player))
        {
            // フォールバック: 技術的に②が困難な場合は①動作
            if (settings != null && settings.useFallbackMode)
            {
                StickToSurface(other);
                return;
            }

            var magnetizable = other.GetComponent<Magnetizable>();
            if (magnetizable != null)
            {
                magnetizable.SetPole(Pole);
                // パターン②: 対象に可視化を追加（MagnetFieldVisualizerに委譲）
                ShowFieldVisualization(other.gameObject);
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
        if (mag != null) mag.SetPole(Pole);

        // パターン①: 磁力範囲を可視化（MagnetFieldVisualizerに委譲）
        ShowFieldVisualization(gameObject);

        OnImpact?.Invoke();
    }

    /// <summary>
    /// 対象にMagnetFieldVisualizerを追加して磁力範囲を可視化する。
    /// </summary>
    private void ShowFieldVisualization(GameObject target)
    {
        float range = MagnetManager.Instance != null ? MagnetManager.Instance.GetMagnetRange() : 5f;
        var visualizer = target.AddComponent<MagnetFieldVisualizer>();
        visualizer.Show(Pole, range);
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

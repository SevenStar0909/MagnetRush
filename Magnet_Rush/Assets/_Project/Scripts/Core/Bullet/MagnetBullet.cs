using System;
using UnityEngine;
/// <summary>
/// 磁力弾。着弾時にパターン1（壁にくっつく）またはパターン2（弾消去＋磁化）を実行する。
/// 飛行中はMagnetFieldの影響で弾道が曲がる。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class MagnetBullet : MonoBehaviour, IBulletProximity
{
    [SerializeField] private BulletSettings m_settings;

    [SerializeField]
    [Tooltip("ヒット解決上の所属グループ。プレイヤーの弾なので Player")]
    private HitGroup m_hitGroup = HitGroup.Player;

    /// <summary>所属グループ。被弾側との比較で自傷・同士討ちを弾く（強制は段階2c以降）。</summary>
    public HitGroup HitGroup => m_hitGroup;

    public MagneticPole Pole { get; private set; }
    public bool IsStuck { get; private set; }

    /// <summary>弾が何かに着弾した時に発火するコールバック。</summary>
    public event Action OnImpact;

    private Rigidbody m_rb;
    private SphereCollider m_sphereCollider;
    private float m_timer;
    private bool m_registered;
    // 自前速度。Kinematic Rigidbody なので物理エンジンの velocity は使わず、自分で位置を進める
    private Vector3 m_velocity;

    // 衝突対象レイヤーマスク（PlayerBullet が当たるべき相手）。初期化は遅延（PhysicsLayers が確定後）
    private static int s_collisionMask;
    private static bool s_maskInitialized;

    private static int GetCollisionMask()
    {
        if (!s_maskInitialized)
        {
            // EntityBody (Pushbox 用) は除外。発射時に Player Pushbox に重なって自身を磁化する事故防止。
            // Boss/Enemy も被弾は Hurtbox(=Enemy layer) で受けるので Pushbox は対象外で問題ない。
            // EnemyBullet はタレット弾/ミサイルを磁化対象として含める。
            s_collisionMask = (1 << PhysicsLayers.Ground)
                | (1 << PhysicsLayers.Wall)
                | (1 << PhysicsLayers.Enemy)
                | (1 << PhysicsLayers.EnemyBullet)
                | (1 << PhysicsLayers.PhysicsObject);
            s_maskInitialized = true;
        }
        return s_collisionMask;
    }

    void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_sphereCollider = GetComponent<SphereCollider>();
    }

    public void Initialize(MagneticPole pole, Vector3 direction)
    {
        Pole = pole;
        // 物理エンジンの移動と CCD を捨てる。Kinematic にして自前で位置を進める
        m_rb.isKinematic = true;
        m_rb.useGravity = false;
        m_velocity = direction.normalized * m_settings.bulletSpeed;
        m_timer = m_settings.lifetime;

        // ビジュアル切替（S=赤、N=青）
        var renderer = GetComponentInChildren<MeshRenderer>();
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

        // MagnetManager弾近接検出に登録
        if (MagnetManager.Instance != null)
            MagnetManager.Instance.RegisterBullet(this);
    }

    private void InitializeEffects(MagneticPole pole)
    {
        if (m_settings == null) { ChannelLogger.LogGuardReturn("Bullet", "BulletSettings未設定"); return; }
        GameObject prefab = pole == MagneticPole.S ? m_settings.fireEffect_S : m_settings.fireEffect_N;
        if (prefab == null) { ChannelLogger.LogGuardReturn("Bullet", "発射エフェクトプレハブ未設定"); return; }

        var instance = Instantiate(prefab, transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = WorldScale(m_settings.fireEffectScale, transform);
        instance.SetActive(true);
    }

    void Update()
    {
        if (IsStuck) { ChannelLogger.LogGuardReturn("Bullet", "着弾済み(Update)"); return; }

        // timeScaleの影響を受けない（エイム中に寿命が短くならない）
        m_timer -= Time.unscaledDeltaTime;
        if (m_timer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    void FixedUpdate()
    {
        if (IsStuck || m_rb == null || m_settings == null) { ChannelLogger.LogGuardReturn("Bullet", "着弾済みまたはRigidbody/Settings未設定"); return; }
        if (MagnetManager.Instance == null) { ChannelLogger.LogGuardReturn("Bullet", "MagnetManager未初期化"); return; }

        // 磁場で速度を曲げる。Kinematic だが velocity は自前変数なのでそのまま加算。
        // 磁場が重なった所では、一番強い1個はフル、残りは重なり係数で減衰して足す（重ねても曲がりすぎない）
        var fields = MagnetManager.Instance.GetActiveFields();
        var magnetSettings = MagnetManager.Instance.Settings;
        float overlapWeight = magnetSettings != null ? magnetSettings.overlapForceWeight : 1f;

        int strongestIndex = -1;
        float strongestPull = 0f;
        Vector3 strongestDelta = Vector3.zero;
        Vector3 weightedRestDelta = Vector3.zero;

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
            Vector3 delta = (attract ? toCenter : -toCenter) * pull * Time.fixedDeltaTime;

            if (pull > strongestPull)
            {
                // これまでの最強は「残り」へ回して係数を掛ける
                if (strongestIndex >= 0) weightedRestDelta += strongestDelta * overlapWeight;
                strongestPull = pull;
                strongestDelta = delta;
                strongestIndex = i;
            }
            else
            {
                weightedRestDelta += delta * overlapWeight;
            }
        }

        if (strongestIndex >= 0)
            m_velocity += strongestDelta + weightedRestDelta;

        // 自前 SphereCast による衝突検出。物理エンジンの CCD に頼らないので
        // 厚み0の MeshCollider でも確実に検知できる
        float radius = m_sphereCollider != null ? m_sphereCollider.radius : 0.03f;
        float speed = m_velocity.magnitude;
        if (speed <= 0.0001f) return;

        Vector3 dir = m_velocity / speed;
        float dist = speed * Time.fixedDeltaTime;

        if (Physics.SphereCast(transform.position, radius, dir, out RaycastHit hit, dist, GetCollisionMask(), QueryTriggerInteraction.Collide))
        {
            // ヒット位置の少し手前に止める（めり込み防止）
            m_rb.MovePosition(hit.point - dir * radius);
            ResolveHit(hit.collider, hit.normal);
        }
        else
        {
            m_rb.MovePosition(transform.position + m_velocity * Time.fixedDeltaTime);
        }
    }

    /// <summary>SphereCast でヒットした collider に対する処理。</summary>
    private void ResolveHit(Collider other, Vector3 surfaceNormal)
    {
        if (IsStuck) return;

        var targetMag = other.GetComponentInParent<Magnetizable>();
        if (targetMag != null)
            MagnetizeTarget(other, targetMag);
        else
            StickToSurface(other, surfaceNormal);
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

        // MagnetField/Visualizer/VFX はすべて着弾Collider（ボーン/Hurtbox子等）側に出す。
        // フィールド中心がボーンに追従し、VFXと範囲が一致する。
        var hostGO = target.gameObject;
        if (hostGO.GetComponent<MagnetField>() == null && m_settings != null && m_settings.bulletFieldSettings != null)
        {
            var field = hostGO.AddComponent<MagnetField>();
            field.Initialize(Pole, m_settings.bulletFieldSettings);

            if (MagnetManager.Instance != null)
                MagnetManager.Instance.RegisterField(field);

            var visualizer = hostGO.AddComponent<MagnetFieldVisualizer>();
            visualizer.Show(Pole, m_settings.bulletFieldSettings);

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
    private void StickToSurface(Collider surface, Vector3 surfaceNormal)
    {
        IsStuck = true;
        m_velocity = Vector3.zero;
        m_rb.isKinematic = true;

        // CCD で薄い MeshCollider に高速ヒットすると、衝突検出時の transform.position が
        // 既に surface を貫通した位置になっていることがある。bounds の最近点に補正して
        // 「表側」に貼り付けないと、裏面のみ描画される平面の裏に潜って見えなくなる。
        Vector3 corrected = surface.bounds.ClosestPoint(transform.position);
        if (corrected != transform.position)
            transform.position = corrected;

        // 弾の up を着弾面の法線に揃える。VFX/MagnetFieldVisualizer (Cylinder等) が
        // 着弾面に対し常に垂直になり、斜め発射時に磁場が斜め描画される問題を解消
        if (surfaceNormal.sqrMagnitude > 0.0001f)
            transform.up = surfaceNormal;

        // 親 lossyScale が非均一だと子 VFX/Visualizer が楕円化する（Plane の (2.4,1.2,2.1) 等）。
        // MagnetField.CylinderHeight も lossyScale.y を掛けて算出するため磁場の形も歪む。
        // 静的地面は動かない前提で、非均一スケール親への SetParent はスキップする。
        Vector3 ps = surface.transform.lossyScale;
        const float scaleEpsilon = 0.001f;
        bool uniformScale = Mathf.Abs(ps.x - ps.y) < scaleEpsilon && Mathf.Abs(ps.y - ps.z) < scaleEpsilon;
        if (uniformScale)
            transform.SetParent(surface.transform, true);

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
        instance.transform.localScale = WorldScale(m_settings.impactEffectScale, parent);
        return instance;
    }

    /// <summary>
    /// 親のスケールに関係なく一定のワールドサイズになるlocalScaleを返す。
    /// </summary>
    private static Vector3 WorldScale(float size, Transform parent)
    {
        Vector3 l = parent.lossyScale;
        return new Vector3(
            Mathf.Abs(l.x) > 0.001f ? size / l.x : size,
            Mathf.Abs(l.y) > 0.001f ? size / l.y : size,
            Mathf.Abs(l.z) > 0.001f ? size / l.z : size
        );
    }

    public MagnetField GetField() => GetComponent<MagnetField>();

    void OnDestroy()
    {
        // BulletManager解除（二重呼出ガード）
        if (m_registered && BulletManager.Instance != null)
        {
            BulletManager.Instance.Unregister(this);
            m_registered = false;
        }

        // MagnetManager弾近接検出から解除
        if (MagnetManager.Instance != null)
            MagnetManager.Instance.UnregisterBullet(this);
    }
}

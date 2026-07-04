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
    private readonly Collider[] m_hitBuffer = new Collider[8];
    private readonly Collider[] m_nearMissBuffer = new Collider[32];

    // 衝突対象レイヤーマスク（PlayerBullet が当たるべき相手）。初期化は遅延（PhysicsLayers が確定後）
    private static int s_collisionMask;
    private static bool s_maskInitialized;
    private static int s_nearMissAssistMask;
    private static bool s_nearMissAssistMaskInitialized;

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

    private static int GetNearMissAssistMask()
    {
        if (!s_nearMissAssistMaskInitialized)
        {
            // 補正は敵・磁化可能物理オブジェクトだけに限定する。
            // Ground/Wall まで吸うと、壁際を通った弾が意図せず環境へ着弾するため含めない。
            s_nearMissAssistMask = PhysicsLayers.Bit(PhysicsLayers.Enemy)
                | PhysicsLayers.Bit(PhysicsLayers.PhysicsObject);
            s_nearMissAssistMaskInitialized = true;
        }
        return s_nearMissAssistMask;
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
        float radius = GetCollisionRadius();
        float speed = m_velocity.magnitude;
        if (speed <= 0.0001f) return;

        Vector3 dir = m_velocity / speed;
        float dist = speed * Time.fixedDeltaTime;

        Vector3 start = transform.position;
        Vector3 end = start + m_velocity * Time.fixedDeltaTime;

        if (TryFindImmediateHit(radius, out Collider overlapped))
        {
            ResolveHit(overlapped);
        }
        else
        {
            bool hasHit = Physics.SphereCast(start, radius, dir, out RaycastHit hit, dist, GetCollisionMask(), QueryTriggerInteraction.Collide);
            bool hasAssistedHit = TryFindNearMissHit(start, end, radius, dir, out Collider assistedHit, out Vector3 assistedPosition, out float assistedDistance);

            if (hasAssistedHit && (!hasHit || assistedDistance < hit.distance))
            {
                transform.position = assistedPosition;
                ResolveHit(assistedHit);
            }
            else if (hasHit)
            {
                // 着弾で停止するので MovePosition の1フレーム遅延を避け、表面の手前へ直接スナップ。
                // この位置がそのまま磁場の中心（＝着弾点）になる。
                transform.position = hit.point - dir * radius;
                ResolveHit(hit.collider);
            }
            else
            {
                m_rb.MovePosition(end);
            }
        }
    }

    private float GetCollisionRadius()
    {
        float prefabRadius = m_sphereCollider != null
            ? m_sphereCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z)
            : 0.03f;
        float settingsRadius = m_settings != null ? m_settings.collisionRadius : 0f;
        return Mathf.Max(prefabRadius, settingsRadius);
    }

    private bool TryFindImmediateHit(float radius, out Collider hit)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, m_hitBuffer, GetCollisionMask(), QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider col = m_hitBuffer[i];
            if (col == null)
                continue;

            if (col.transform.root == transform.root)
                continue;

            hit = col;
            return true;
        }

        hit = null;
        return false;
    }

    private bool TryFindNearMissHit(Vector3 start, Vector3 end, float collisionRadius, Vector3 dir, out Collider hit, out Vector3 hitPosition, out float hitDistance)
    {
        hit = null;
        hitPosition = Vector3.zero;
        hitDistance = 0f;

        float assistRadius = m_settings != null ? Mathf.Max(0f, m_settings.nearMissAssistRadius) : 0f;
        if (assistRadius <= collisionRadius + 0.001f)
            return false;

        int count = Physics.OverlapCapsuleNonAlloc(start, end, assistRadius, m_nearMissBuffer, GetNearMissAssistMask(), QueryTriggerInteraction.Collide);
        float bestSqrDistance = float.PositiveInfinity;
        float bestDistance = float.PositiveInfinity;
        Vector3 bestPosition = start;
        Collider bestCollider = null;

        for (int i = 0; i < count; i++)
        {
            Collider col = m_nearMissBuffer[i];
            if (col == null)
                continue;

            if (col.transform.root == transform.root)
                continue;

            if (col.GetComponentInParent<Magnetizable>() == null)
                continue;

            if (!TryGetClosestApproach(col, start, end, dir, out Vector3 pathPoint, out float distanceAlong, out float sqrDistance))
                continue;

            if (sqrDistance < bestSqrDistance || (Mathf.Approximately(sqrDistance, bestSqrDistance) && distanceAlong < bestDistance))
            {
                bestSqrDistance = sqrDistance;
                bestDistance = distanceAlong;
                bestPosition = pathPoint;
                bestCollider = col;
            }
        }

        if (bestCollider == null)
            return false;

        hit = bestCollider;
        hitPosition = bestPosition;
        hitDistance = bestDistance;
        return true;
    }

    private static bool TryGetClosestApproach(Collider col, Vector3 start, Vector3 end, Vector3 dir, out Vector3 bestPathPoint, out float bestDistanceAlong, out float bestSqrDistance)
    {
        Vector3 localBestPathPoint = start;
        float localBestDistanceAlong = 0f;
        float localBestSqrDistance = float.PositiveInfinity;

        Vector3 segment = end - start;
        float segmentLength = segment.magnitude;
        if (segmentLength <= 0.0001f)
        {
            bestPathPoint = localBestPathPoint;
            bestDistanceAlong = localBestDistanceAlong;
            bestSqrDistance = localBestSqrDistance;
            return false;
        }

        TestPathPoint(start);
        TestPathPoint(Vector3.Lerp(start, end, 0.25f));
        TestPathPoint(Vector3.Lerp(start, end, 0.5f));
        TestPathPoint(Vector3.Lerp(start, end, 0.75f));
        TestPathPoint(end);
        TestPathPoint(ClosestPointOnSegment(start, end, col.bounds.center));

        bestPathPoint = localBestPathPoint;
        bestDistanceAlong = localBestDistanceAlong;
        bestSqrDistance = localBestSqrDistance;
        return !float.IsPositiveInfinity(localBestSqrDistance);

        void TestPathPoint(Vector3 pathPoint)
        {
            Vector3 closest = col.ClosestPoint(pathPoint);
            float sqrDistance = (closest - pathPoint).sqrMagnitude;
            float distanceAlong = Mathf.Clamp(Vector3.Dot(pathPoint - start, dir), 0f, segmentLength);

            if (sqrDistance < localBestSqrDistance || (Mathf.Approximately(sqrDistance, localBestSqrDistance) && distanceAlong < localBestDistanceAlong))
            {
                localBestSqrDistance = sqrDistance;
                localBestDistanceAlong = distanceAlong;
                localBestPathPoint = pathPoint;
            }
        }
    }

    private static Vector3 ClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
    {
        Vector3 segment = end - start;
        float sqrLength = segment.sqrMagnitude;
        if (sqrLength <= 0.0001f)
            return start;

        float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / sqrLength);
        return start + segment * t;
    }

    /// <summary>SphereCast でヒットした collider に対する処理。</summary>
    private void ResolveHit(Collider other)
    {
        if (IsStuck) return;

        // 弾が何かに着弾した音（着弾点の3D位置で再生＝離れていると小さく聞こえる）
        Sound.PlayAt(SoundData.CueSheet.SE, SoundData.SE.HitObject, transform.position);

        var targetMag = other.GetComponentInParent<Magnetizable>();
        if (targetMag != null)
            MagnetizeTarget(other, targetMag);
        else
            StickToSurface(other);
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
        // 既に同じ敵へ磁力が付いている場合、古いドーム/着弾VFXを残さず上書きする。
        targetMag.DeactivateWithFields();
        targetMag.SetPole(Pole);

        if (m_settings != null && m_settings.bulletFieldSettings != null)
        {
            // 磁場・Visualizer・着弾VFX は着弾Colliderの「実際の判定中心」に出す。
            // Hurtbox は box.center で見た目側へ大きくオフセットされている場合があり
            // （ボス右手は約12〜18m離れている）、対象GO原点に出すと手から離れた位置（肩付近）に
            // 磁場・オーラが出てしまう。判定中心にアンカーを作り、Colliderの子にしてボーンに追従させる。
            var anchor = new GameObject("MagnetFieldAnchor");
            anchor.transform.SetParent(target.transform, false);
            anchor.transform.position = ColliderCenter(target);

            var field = anchor.AddComponent<MagnetField>();
            field.Initialize(Pole, m_settings.bulletFieldSettings);

            if (MagnetManager.Instance != null)
                MagnetManager.Instance.RegisterField(field);

            var visualizer = anchor.AddComponent<MagnetFieldVisualizer>();
            visualizer.Show(Pole, m_settings.bulletFieldSettings);

            SpawnImpactEffect(anchor.transform);

            // フィールド期限切れ → 対象の磁化解除 + アンカー（Visualizer/VFX を内包）破棄
            field.OnFieldExpired += () =>
            {
                if (targetMag != null) targetMag.Deactivate();
                if (anchor != null) Destroy(anchor);
            };
        }

        OnImpact?.Invoke();
        Destroy(gameObject);
    }

    /// <summary>Colliderのローカル中心(box.center等)をワールド座標で返す。GO原点ではなく実際の判定中心。</summary>
    private static Vector3 ColliderCenter(Collider col)
    {
        switch (col)
        {
            case BoxCollider b: return col.transform.TransformPoint(b.center);
            case SphereCollider s: return col.transform.TransformPoint(s.center);
            case CapsuleCollider c: return col.transform.TransformPoint(c.center);
            default: return col.bounds.center;
        }
    }

    /// <summary>
    /// パターン1: 弾がくっつき、弾自身が磁力源。壁/天井/タレット用。
    /// フィールド期限切れで弾ごと消える。
    /// </summary>
    private void StickToSurface(Collider surface)
    {
        IsStuck = true;
        m_velocity = Vector3.zero;
        m_rb.isKinematic = true;

        // 着弾点（FixedUpdate で transform.position に確定済み）を中心に正球形の磁場を出す。
        // 着弾オブジェクト（壁/地面/メッシュコライダー）へ parent すると、そのスケール・回転を
        // 磁場の球コライダーとビジュアライザが引き継いで楕円化・サイズ変化していた。
        // 環境は静的前提なので parent せず、回転・スケールを正規化したルートに固定する。
        transform.SetParent(null, true);
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

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

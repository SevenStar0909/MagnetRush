using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Magnetizable))]
public class EnemyMissile : MonoBehaviour, IMagneticResponse
{
    [Header("Movement")]
    [SerializeField] private float m_maxSpeed = 18f;    // 最高速度
    [SerializeField] private float m_acceleration = 40f; // 加速度(m/s²)
    [SerializeField] private float m_turnRate = 10f;    // 旋回速度
    [SerializeField] private float m_seekDelay = 0.2f;  // 発射後すぐは誘導しない時間
    [SerializeField] private float m_arrivalDistance = 0.6f; // ターゲットに近づきすぎないようにする距離

    [Header("Targeting")]
    [SerializeField] private Vector3 m_targetOffset;    // ターゲットのどこを狙うかのオフセット
    [SerializeField] private float m_targetRefreshInterval = 0.1f; // ターゲットの再検索間隔

    [Header("Arc Flight")]
    [SerializeField] private float m_arcWaypointReachDistance = 1.2f;

    [Header("Visual Orientation")]
    [Tooltip("ミサイルの先端が向いているローカル方向")]
    [SerializeField] private Vector3 m_tipLocalDirection = Vector3.forward;

    [Tooltip("磁力で撃ち返されている時、先端をターゲットへ向ける速度")]
    [SerializeField] private float m_magnetFaceTurnRate = 30f;

    [Header("References")]
    [SerializeField] private Transform m_player;    // プレイヤーへの参照（Inspectorで設定、もしくは起動時に自動検索）

    [Header("Combat")]
    [SerializeField] private int m_damage = 1;

    [SerializeField]
    [Tooltip("磁力で誘導してボス本体に当てたとき、ボスのスタンゲージが何％溜まるか。仕様＝30")]
    [Range(0, 100)]
    private int m_stunGaugePercent = 30;

    /// <summary>
    /// 誘導してボス本体に当てた時に与えるスタン値の蓄積率（％）。仕様＝30%/発。
    /// 磁化中（プレイヤーが磁力で誘導した状態）に当たった時だけ有効。磁化せずに当たった場合は 0（ただ爆発するだけ）。
    /// </summary>
    public int StunGaugePercent => (m_selfMagnetizable != null && m_selfMagnetizable.IsActive) ? m_stunGaugePercent : 0;

    [SerializeField]
    [Tooltip("発射してからこの秒数は発射元（ボス）と当たらない（自爆防止）。経過後はボスにも当たる＝磁力で撃ち返せる")]
    private float m_collisionRestoreDelay = 3f;

    [SerializeField]
    [Tooltip("ヒット解決上の所属グループ。誰にでも当たる物理ハザードなので Physics（Player/Enemy 両方にダメージが通る）")]
    private HitGroup m_hitGroup = HitGroup.Physics;

    /// <summary>所属グループ。被弾側と HitGroup が異なるときだけダメージを通す。Physics は Player/Enemy 両方に通る。</summary>
    public HitGroup HitGroup => m_hitGroup;
    [SerializeField] private float m_lifetime = 6f;

    [Header("ExplosionEffects")]
    [SerializeField] private GameObject m_explosionEffect; // P_MS_ExplosionPS
    [SerializeField] private float m_explosionEffectLifetime = 3f;

    private Rigidbody m_rb;
    private Magnetizable m_selfMagnetizable;
    private Magnetizable m_currentTarget;
    private float m_timer;
    private float m_seekTimer;
    private float m_refreshTimer;
    private bool m_initialized;
    private Collider m_collider;
    private bool m_exploded;
    private Vector3 m_lastMagnetSourcePosition;
    private bool m_hasMagnetSourcePosition;
    private bool m_arcFlightActive;
    private bool m_arcWaypointReady;
    private Vector3 m_arcStartPosition;
    private Vector3 m_arcDirection;
    private Vector3 m_arcWaypoint;
    private float m_arcHeight;
    private float m_arcSpreadDistance;
    private float m_arcLaneOffset;

    private readonly List<Collider> m_ignoredColliders = new List<Collider>();
    private bool m_collisionRestored = true;
    private float m_restoreTimer;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_selfMagnetizable = GetComponent<Magnetizable>();
        m_collider = GetComponent<Collider>();

        if (m_explosionEffect == null)
            m_explosionEffect = Resources.Load<GameObject>("P_MS_ExplosionPS");

        if (m_player == null)
        {
            GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
            if (playerObj != null)
                m_player = playerObj.transform;
        }
    }

    private void OnEnable()
    {
        if (m_selfMagnetizable != null)
            m_selfMagnetizable.OnMagnetContact += HandleMagnetContact;
    }

    private void OnDisable()
    {
        if (m_selfMagnetizable != null)
            m_selfMagnetizable.OnMagnetContact -= HandleMagnetContact;
    }

    private void Start()
    {
        // 外部からInitializeされない限り、起動時に自動初期化
        // テスト用
        if (!m_initialized)
            Initialize(m_player, transform.forward);
    }

    public void Initialize(Transform target, Vector3 initialDirection)
    {
        if (target != null)
            m_player = target;

        if (m_player == null)
        {
            GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
            if (playerObj != null)
                m_player = playerObj.transform;
        }

        m_timer = m_lifetime;
        m_seekTimer = m_seekDelay;
        m_refreshTimer = 0f;
        m_initialized = true;
        ClearArcFlight();

        Vector3 dir = initialDirection.sqrMagnitude > 0f ? initialDirection.normalized : transform.forward;
        m_rb.linearVelocity = dir * Mathf.Max(0.1f, m_maxSpeed * 0.6f);
        transform.rotation = BuildTipRotation(dir);
    }

    /// <summary>
    /// seekDelay を上書きして初期化する。アーク弾（上げてから狙う）用に、ホーミング開始までの上昇時間を延ばす。
    /// </summary>
    /// <param name="seekDelayOverride">0以上で上書き。負値なら既定の m_seekDelay を使う</param>
    public void Initialize(Transform target, Vector3 initialDirection, float seekDelayOverride)
    {
        Initialize(target, initialDirection);
        if (seekDelayOverride >= 0f)
            m_seekTimer = seekDelayOverride;
    }

    public void InitializeArc(
        Transform target,
        Vector3 launchDirection,
        Vector3 formationDirection,
        float launchDelay,
        float arcHeight,
        float arcSpreadDistance,
        float arcLaneOffset)
    {
        Initialize(target, launchDirection);
        if (launchDelay >= 0f)
            m_seekTimer = launchDelay;

        ConfigureArcFlight(formationDirection, arcHeight, arcSpreadDistance, arcLaneOffset);
    }

    public void SetPlayer(Transform player)
    {
        m_player = player;
    }

    private void Update()
    {
        if (!m_initialized) { ChannelLogger.LogGuardReturn("Enemy", "Missile未初期化"); return; }

        RestoreIgnoredCollisionsIfCleared();

        m_timer -= Time.deltaTime;
        if (m_timer <= 0f)
            Destroy(gameObject);
    }

    // 発射直後は発射元（ボス）との衝突を無効にしているが、ボスから十分離れたら再有効化する。
    // これで「ボスのミサイルを磁力で誘導してボスに当てる」（仕様）が物理衝突でも成立し、スタン値が溜まる。
    private void RestoreIgnoredCollisionsIfCleared()
    {
        if (m_collisionRestored) return;
        if (m_collider == null) { m_collisionRestored = true; return; }

        // 発射してから m_collisionRestoreDelay 秒の間は発射元（ボス）と当たらない＝自爆しない。
        // 経過したらボスとの衝突を戻すので、磁力で撃ち返したミサイルがボスに当たって +30% が入る。
        m_restoreTimer -= Time.deltaTime;
        if (m_restoreTimer > 0f) return;

        for (int i = 0; i < m_ignoredColliders.Count; i++)
        {
            var c = m_ignoredColliders[i];
            if (c != null)
                Physics.IgnoreCollision(m_collider, c, false);
        }
        m_collisionRestored = true;
    }

    private void FixedUpdate()
    {
        if (!m_initialized || m_rb == null) { ChannelLogger.LogGuardReturn("Enemy", "Missile未初期化またはRigidbodyなし"); return; }

        if (m_seekTimer > 0f)
        {
            m_seekTimer -= Time.fixedDeltaTime;
            return;
        }

        UpdateMagnetTarget();

        Vector3 desiredVelocity = ResolveDesiredVelocity();
        Vector3 steering = desiredVelocity - m_rb.linearVelocity;
        steering = Vector3.ClampMagnitude(steering, m_acceleration * Time.fixedDeltaTime);

        Vector3 nextVelocity = m_rb.linearVelocity + steering;
        m_rb.linearVelocity = Vector3.ClampMagnitude(nextVelocity, m_maxSpeed);

        UpdateRotation(Time.fixedDeltaTime);
    }

    public bool IsResponseActive => m_selfMagnetizable != null
        && m_selfMagnetizable.IsActive
        && m_rb != null
        && !m_exploded;

    public void OnMagnetForce(Vector3 force, Vector3 sourcePosition)
    {
        if (m_rb == null || m_exploded)
            return;

        ClearArcFlight();
        m_hasMagnetSourcePosition = true;
        m_lastMagnetSourcePosition = sourcePosition;

        m_rb.linearVelocity += force * Time.fixedDeltaTime;
        m_rb.angularVelocity = Vector3.zero;
    }

    public void OnMagnetContact(Magnetizable self, Magnetizable other)
    {
        // 爆発処理は既存の Magnetizable.OnMagnetContact 購読経路で行う。
    }

    private void UpdateRotation(float dt)
    {
        Vector3 lookDirection = ResolveLookDirection();
        if (lookDirection.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRot = BuildTipRotation(lookDirection.normalized);
        float turnRate = IsResponseActive && HasMagnetLookTarget()
            ? m_magnetFaceTurnRate
            : m_turnRate;

        Quaternion nextRotation = Quaternion.Slerp(
            m_rb.rotation,
            targetRot,
            Mathf.Clamp01(turnRate * dt)
        );

        m_rb.MoveRotation(nextRotation);
    }

    private Vector3 ResolveLookDirection()
    {
        if (IsResponseActive && TryGetMagnetLookTarget(out Vector3 targetPosition))
        {
            Vector3 toMagnetTarget = targetPosition - transform.position;
            if (toMagnetTarget.sqrMagnitude > 0.001f)
                return toMagnetTarget;
        }

        return m_rb != null ? m_rb.linearVelocity : transform.forward;
    }

    private bool HasMagnetLookTarget()
    {
        return m_currentTarget != null || m_hasMagnetSourcePosition;
    }

    private bool TryGetMagnetLookTarget(out Vector3 targetPosition)
    {
        if (m_currentTarget != null)
        {
            targetPosition = m_currentTarget.Position;
            return true;
        }

        if (m_hasMagnetSourcePosition)
        {
            targetPosition = m_lastMagnetSourcePosition;
            return true;
        }

        targetPosition = Vector3.zero;
        return false;
    }

    private Quaternion BuildTipRotation(Vector3 lookDirection)
    {
        Vector3 tipDirection = m_tipLocalDirection.sqrMagnitude > 0.0001f
            ? m_tipLocalDirection.normalized
            : Vector3.forward;

        Vector3 up = Mathf.Abs(Vector3.Dot(lookDirection.normalized, Vector3.up)) > 0.98f
            ? transform.up
            : Vector3.up;

        Quaternion baseRotation = Quaternion.LookRotation(lookDirection.normalized, up);
        Quaternion tipOffset = Quaternion.FromToRotation(tipDirection, Vector3.forward);
        return baseRotation * tipOffset;
    }

    private void UpdateMagnetTarget()
    {
        if (m_selfMagnetizable == null || !m_selfMagnetizable.IsActive)
        {
            m_currentTarget = null;
            m_hasMagnetSourcePosition = false;
            return;
        }

        m_refreshTimer -= Time.fixedDeltaTime;
        if (m_refreshTimer > 0f)
            return;

        m_refreshTimer = Mathf.Max(0.02f, m_targetRefreshInterval);
        m_currentTarget = FindNearestOppositeMagnetizable();
    }

    private Vector3 ResolveDesiredVelocity()
    {
        Vector3 forward = transform.forward;
        Transform target = ResolveTargetTransform();
        if (target == null)
            return forward * m_maxSpeed;

        if (TryResolveArcDesiredVelocity(out Vector3 arcVelocity))
            return arcVelocity;

        Vector3 targetPos = target.position + m_targetOffset;
        Vector3 toTarget = targetPos - transform.position;

        if (toTarget.sqrMagnitude < m_arrivalDistance * m_arrivalDistance)
            return forward * m_maxSpeed;

        return toTarget.normalized * m_maxSpeed;
    }

    private void ConfigureArcFlight(Vector3 formationDirection, float arcHeight, float arcSpreadDistance, float arcLaneOffset)
    {
        Vector3 dir = formationDirection.sqrMagnitude > 0.0001f ? formationDirection.normalized : transform.forward;
        m_arcDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.up;
        m_arcStartPosition = transform.position;
        m_arcHeight = Mathf.Max(0f, arcHeight);
        m_arcSpreadDistance = Mathf.Max(0f, arcSpreadDistance);
        m_arcLaneOffset = arcLaneOffset;
        m_arcWaypointReady = false;
        m_arcFlightActive = m_arcHeight > 0f
            || m_arcSpreadDistance > 0f
            || Mathf.Abs(m_arcLaneOffset) > 0.001f;
    }

    private void ClearArcFlight()
    {
        m_arcFlightActive = false;
        m_arcWaypointReady = false;
        m_arcStartPosition = Vector3.zero;
        m_arcDirection = Vector3.zero;
        m_arcWaypoint = Vector3.zero;
        m_arcHeight = 0f;
        m_arcSpreadDistance = 0f;
        m_arcLaneOffset = 0f;
    }

    private bool TryResolveArcDesiredVelocity(out Vector3 desiredVelocity)
    {
        desiredVelocity = Vector3.zero;

        if (!m_arcFlightActive)
            return false;

        if (IsResponseActive)
        {
            ClearArcFlight();
            return false;
        }

        if (!m_arcWaypointReady)
            BuildArcWaypoint();

        Vector3 toWaypoint = m_arcWaypoint - transform.position;
        float reachDistance = Mathf.Max(0.05f, m_arcWaypointReachDistance);
        if (toWaypoint.sqrMagnitude <= reachDistance * reachDistance || HasPassedArcWaypoint())
        {
            ClearArcFlight();
            return false;
        }

        desiredVelocity = toWaypoint.normalized * m_maxSpeed;
        return true;
    }

    private void BuildArcWaypoint()
    {
        Vector3 targetPos = ResolveArcTargetPosition();
        Vector3 toTargetFlat = Vector3.ProjectOnPlane(targetPos - m_arcStartPosition, Vector3.up);
        Vector3 targetDir = ResolveFlatDirection(toTargetFlat, transform.forward);
        Vector3 formationFlat = Vector3.ProjectOnPlane(m_arcDirection, Vector3.up);
        Vector3 spread = formationFlat.sqrMagnitude > 0.0001f
            ? formationFlat.normalized * m_arcSpreadDistance
            : Vector3.zero;
        Vector3 laneDir = Vector3.Cross(Vector3.up, targetDir);
        if (laneDir.sqrMagnitude <= 0.0001f)
            laneDir = Vector3.right;

        float targetDistance = toTargetFlat.magnitude;
        float forwardDistance = Mathf.Max(m_arcSpreadDistance, targetDistance * 0.45f);

        m_arcWaypoint = m_arcStartPosition
            + targetDir * forwardDistance
            + spread
            + laneDir.normalized * m_arcLaneOffset;
        m_arcWaypoint.y = Mathf.Max(m_arcStartPosition.y, targetPos.y) + m_arcHeight;
        m_arcWaypointReady = true;
    }

    private Vector3 ResolveArcTargetPosition()
    {
        Transform target = ResolveTargetTransform();
        if (target != null)
            return target.position + m_targetOffset;

        float distance = Mathf.Max(1f, m_arcSpreadDistance);
        return m_arcStartPosition + m_arcDirection * distance;
    }

    private Vector3 ResolveFlatDirection(Vector3 candidate, Vector3 fallback)
    {
        Vector3 flat = Vector3.ProjectOnPlane(candidate, Vector3.up);
        if (flat.sqrMagnitude > 0.0001f)
            return flat.normalized;

        flat = Vector3.ProjectOnPlane(fallback, Vector3.up);
        if (flat.sqrMagnitude > 0.0001f)
            return flat.normalized;

        return Vector3.forward;
    }

    private bool HasPassedArcWaypoint()
    {
        Vector3 startToWaypoint = m_arcWaypoint - m_arcStartPosition;
        if (startToWaypoint.sqrMagnitude <= 0.0001f)
            return false;

        Vector3 startToCurrent = transform.position - m_arcStartPosition;
        return Vector3.Dot(startToCurrent, startToWaypoint) > startToWaypoint.sqrMagnitude;
    }

    private Transform ResolveTargetTransform()
    {
        return m_currentTarget != null ? m_currentTarget.transform : m_player;
    }

    private Magnetizable FindNearestOppositeMagnetizable()
    {
        if (m_selfMagnetizable == null)
            return null;

        MagneticPole selfPole = m_selfMagnetizable.Pole;
        if (selfPole == MagneticPole.None)
            return null;

        // 全シーン走査(FindObjectsByType)は配列確保のGCゴミと全探索コストが重い。
        // MagnetManager がキャッシュ済みの登録一覧を読む(結果は同一・ゴミゼロ)。
        if (MagnetManager.Instance == null)
            return null;
        List<Magnetizable> all = MagnetManager.Instance.GetActiveMagnetizables();

        Magnetizable nearest = null;
        float detectionRange = ResolveDetectionRange();
        float nearestSqr = detectionRange * detectionRange;
        Vector3 origin = transform.position;

        for (int i = 0; i < all.Count; i++)
        {
            Magnetizable candidate = all[i];
            if (candidate == null) continue;
            if (candidate == m_selfMagnetizable) continue;
            if (!candidate.IsActive) continue;
            if (candidate.Pole == MagneticPole.None) continue;
            if (candidate.Pole == selfPole) continue;

            Vector3 delta = candidate.transform.position - origin;
            float sqr = delta.sqrMagnitude;

            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private float ResolveDetectionRange()
    {
        if (MagnetManager.Instance != null && MagnetManager.Instance.Settings != null)
            return MagnetManager.Instance.Settings.magnetRange;

        return 10f;
    }

    private void SpawnExplosionEffect(Vector3 position)
    {
        if (m_explosionEffect == null)
            return;

        GameObject effectInstance = Instantiate(m_explosionEffect, position, Quaternion.identity);
        if (m_explosionEffectLifetime > 0f)
            Destroy(effectInstance, m_explosionEffectLifetime);
    }

    // PhysicsObject レイヤーの物理ハザードとして、相手の Pushbox(EntityBody)/地面/壁/他物理オブジェクトと
    // OnCollisionEnter で衝突する（Matrix で一元管理。原則1）。トリガー(MagnetField 等)では発火しないので誤爆しない。
    // 相手の HitGroup が自分(Physics)と異なるときだけダメージを通す（Player/Enemy 両方に通る。物理同士は弾く。原則3）。
    private void OnCollisionEnter(Collision collision)
    {
        if (m_exploded) { ChannelLogger.LogGuardReturn("Enemy", "Missile既に爆発済み"); return; }

        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        ResolveHitAndExplode(collision.collider != null ? collision.collider.gameObject : null, point);
    }

    // 磁化されると異極の磁化オブジェクト同士が FixedJoint で固定され、Joint は既定で enableCollision=false の
    // ため OnCollisionEnter が発火しない。磁石に保持されて壁際で止まる弾も自由落下しなくなる。
    // よって磁石の接触イベントでも爆発させる。これで磁化中でも磁化物体への接触で確実に爆発する。
    private void HandleMagnetContact(Magnetizable other)
    {
        if (m_exploded) { ChannelLogger.LogGuardReturn("Enemy", "Missile既に爆発済み"); return; }

        Vector3 point = other != null ? Vector3.Lerp(transform.position, other.transform.position, 0.5f) : transform.position;
        if (ShouldExplodeWithMissile(other))
        {
            Explode(point);
            return;
        }

        ResolveHitAndExplode(other != null ? other.gameObject : null, point);
    }

    private bool ShouldExplodeWithMissile(Magnetizable other)
    {
        if (other == null || m_selfMagnetizable == null)
            return false;

        EnemyMissile otherMissile = other.GetComponentInParent<EnemyMissile>();
        if (otherMissile == null || otherMissile == this)
            return false;

        MagneticPole selfPole = m_selfMagnetizable.Pole;
        MagneticPole otherPole = other.Pole;
        return selfPole != MagneticPole.None
            && otherPole != MagneticPole.None
            && selfPole != otherPole;
    }

    // OnCollisionEnter（物理衝突）と HandleMagnetContact（磁石接触）の共通経路。
    // 相手から IHittable を取り、HitGroup が自分(Physics)と異なるときだけダメージを通してから爆発する。
    private void ResolveHitAndExplode(GameObject other, Vector3 point)
    {
        if (m_exploded) return;

        if (other != null && other.GetComponentInParent<EnemyMissile>() != null)
            return;

        if (other != null)
        {
            var hittable = other.GetComponentInParent<IHittable>();
            if (hittable != null && hittable.HitGroup != m_hitGroup)
            {
                hittable.OnHit(new HitData
                {
                    damage = m_damage,
                    hitPoint = point,
                    knockbackDir = m_rb != null ? m_rb.linearVelocity.normalized : transform.forward,
                    source = gameObject
                });
            }
        }

        Explode(point);
    }

    /// <summary>爆発エフェクトを出して自身を破棄する。</summary>
    private void Explode(Vector3 point)
    {
        m_exploded = true;
        SpawnExplosionEffect(point);
        Destroy(gameObject);
    }

    /// <summary>
    /// 発射元（ボス）との衝突無効時間を上書きして <see cref="IgnoreCollisionsWith(GameObject)"/> を呼ぶ。
    /// ボス側 Inspector から発射ごとに猶予秒数を渡せるようにするためのオーバーロード。
    /// </summary>
    /// <param name="source">無視したい相手（発射元のルート GameObject）</param>
    /// <param name="restoreDelay">0以上でこの秒数だけ衝突無効。負値なら既定の m_collisionRestoreDelay を使う</param>
    public void IgnoreCollisionsWith(GameObject source, float restoreDelay)
    {
        if (restoreDelay >= 0f)
            m_collisionRestoreDelay = restoreDelay;
        IgnoreCollisionsWith(source);
    }

    /// <summary>
    /// 発射元（ボス）等のコライダーとの衝突を無効化する。spawn 直後の自己衝突・自傷を防ぐ。
    /// </summary>
    /// <param name="source">無視したい相手（発射元のルート GameObject）</param>
    public void IgnoreCollisionsWith(GameObject source)
    {
        if (source == null) { ChannelLogger.LogGuardReturn("Enemy", "Missile: ignore source なし"); return; }
        if (m_collider == null) m_collider = GetComponent<Collider>();
        if (m_collider == null) { ChannelLogger.LogGuardReturn("Enemy", "Missile: 自身のColliderなし"); return; }

        m_ignoredColliders.Clear();
        m_collisionRestored = false;
        m_restoreTimer = m_collisionRestoreDelay;
        foreach (var c in source.GetComponentsInChildren<Collider>(true))
        {
            if (c != null)
            {
                Physics.IgnoreCollision(m_collider, c, true);
                m_ignoredColliders.Add(c);
            }
        }

        IgnoreOtherMissiles();
    }

    // 1波4発が同時発射 → 密集や誘導での収束でミサイル同士がぶつかり、無条件 Explode で自爆する。
    // ミサイル同士は攻撃対象ではないため、生成済みのミサイルとは破棄まで衝突させない。
    private void IgnoreOtherMissiles()
    {
        if (m_collider == null) return;

        EnemyMissile[] others = FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
        for (int i = 0; i < others.Length; i++)
        {
            EnemyMissile other = others[i];
            if (other == null || other == this) continue;

            Collider c = other.m_collider != null ? other.m_collider : other.GetComponent<Collider>();
            if (c == null || m_ignoredColliders.Contains(c)) continue;

            Physics.IgnoreCollision(m_collider, c, true);
        }
    }
}

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Magnetizable))]
public class EnemyMissile : MonoBehaviour
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

    [Header("References")]
    [SerializeField] private Transform m_player;    // プレイヤーへの参照（Inspectorで設定、もしくは起動時に自動検索）

    [Header("Combat")]
    [SerializeField] private int m_damage = 1;

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

        Vector3 dir = initialDirection.sqrMagnitude > 0f ? initialDirection.normalized : transform.forward;
        m_rb.linearVelocity = dir * Mathf.Max(0.1f, m_maxSpeed * 0.6f);
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
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

    public void SetPlayer(Transform player)
    {
        m_player = player;
    }

    private void Update()
    {
        if (!m_initialized) { ChannelLogger.LogGuardReturn("Enemy", "Missile未初期化"); return; }

        m_timer -= Time.deltaTime;
        if (m_timer <= 0f)
            Destroy(gameObject);
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

        if (m_rb.linearVelocity.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(m_rb.linearVelocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, m_turnRate * Time.fixedDeltaTime);
        }
    }

    private void UpdateMagnetTarget()
    {
        if (m_selfMagnetizable == null || !m_selfMagnetizable.IsActive)
        {
            m_currentTarget = null;
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

        Vector3 targetPos = target.position + m_targetOffset;
        Vector3 toTarget = targetPos - transform.position;

        if (toTarget.sqrMagnitude < m_arrivalDistance * m_arrivalDistance)
            return forward * m_maxSpeed;

        return toTarget.normalized * m_maxSpeed;
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

        Magnetizable[] all = FindObjectsByType<Magnetizable>(FindObjectsSortMode.None);

        Magnetizable nearest = null;
        float detectionRange = ResolveDetectionRange();
        float nearestSqr = detectionRange * detectionRange;
        Vector3 origin = transform.position;

        for (int i = 0; i < all.Length; i++)
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

        // ミサイル同士が接触したら両方爆発する（発射点は左右で離れているので spawn 時の相殺は起きない）。
        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

        var hittable = collision.collider.GetComponentInParent<IHittable>();
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

        // どんな接触でも爆発する（通常のヒット処理）
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
    /// 発射元（ボス）等のコライダーとの衝突を無効化する。spawn 直後の自己衝突・自傷を防ぐ。
    /// </summary>
    /// <param name="source">無視したい相手（発射元のルート GameObject）</param>
    public void IgnoreCollisionsWith(GameObject source)
    {
        if (source == null) { ChannelLogger.LogGuardReturn("Enemy", "Missile: ignore source なし"); return; }
        if (m_collider == null) m_collider = GetComponent<Collider>();
        if (m_collider == null) { ChannelLogger.LogGuardReturn("Enemy", "Missile: 自身のColliderなし"); return; }

        foreach (var c in source.GetComponentsInChildren<Collider>(true))
        {
            if (c != null)
                Physics.IgnoreCollision(m_collider, c, true);
        }
    }
}
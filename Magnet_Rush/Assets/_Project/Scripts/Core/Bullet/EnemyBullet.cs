using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class EnemyBullet : MonoBehaviour
{
    [Header("Bullet")]
    [SerializeField] private float m_speed = 20f;
    [SerializeField] private float m_lifetime = 4f;
    [SerializeField] private int m_damage = 1;

    [SerializeField]
    [Tooltip("ヒット解決上の所属グループ。誰にでも当たる物理ハザードなので Physics（Player/Enemy 両方にダメージが通る）")]
    private HitGroup m_hitGroup = HitGroup.Physics;

    /// <summary>所属グループ。被弾側と HitGroup が異なるときだけダメージを通す。Physics は Player/Enemy 両方に通る。</summary>
    public HitGroup HitGroup => m_hitGroup;

    private Rigidbody m_rb;
    private Collider m_collider;
    private float m_timer;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_collider = GetComponent<Collider>();
        m_rb.useGravity = false;
        m_timer = m_lifetime;
    }

    public void Initialize(Vector3 direction)
    {
        if (m_rb == null) { ChannelLogger.LogGuardReturn("Enemy", "Rigidbody未取得"); return; }
        m_rb.linearVelocity = direction.normalized * m_speed;
    }

    private void Update()
    {
        m_timer -= Time.deltaTime;
        if (m_timer <= 0f)
            Destroy(gameObject);
    }

    /// <summary>
    /// 発射元（タレット）のコライダーとの衝突を無効化する。spawn 直後に自分の Pushbox(EntityBody) と
    /// 即衝突して自爆・自傷するのを防ぐ。タレットからは撃ち返さないので復帰はせず、弾の寿命中ずっと無効のまま。
    /// </summary>
    /// <param name="source">無視する発射元（タレットのルート GameObject）</param>
    public void IgnoreCollisionsWith(GameObject source)
    {
        if (source == null) { ChannelLogger.LogGuardReturn("Enemy", "弾: ignore source なし"); return; }
        if (m_collider == null) m_collider = GetComponent<Collider>();
        if (m_collider == null) { ChannelLogger.LogGuardReturn("Enemy", "弾: 自身のColliderなし"); return; }

        foreach (var c in source.GetComponentsInChildren<Collider>(true))
        {
            if (c != null)
                Physics.IgnoreCollision(m_collider, c, true);
        }
    }

    // PhysicsObject レイヤーの物理ハザードとして、相手の Pushbox(EntityBody)/地面/壁と OnCollisionEnter で
    // 衝突する（当たる相手は Matrix で一元管理。原則1）。トリガー(MagnetField 等)では発火しないので誤爆しない。
    // 相手の HitGroup が自分(Physics)と異なるときだけダメージを通す（Player/Enemy 両方に通る。物理同士は弾く。原則3）。
    private void OnCollisionEnter(Collision collision)
    {
        // Pushbox/Hurtbox の子コライダー直撃でも親 Hitbox の IHittable に到達する
        var hittable = collision.collider.GetComponentInParent<IHittable>();
        if (hittable != null && hittable.HitGroup != m_hitGroup)
        {
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            hittable.OnHit(new HitData
            {
                damage = m_damage,
                hitPoint = point,
                knockbackDir = m_rb != null ? m_rb.linearVelocity.normalized : transform.forward,
                source = gameObject
            });
        }

        Destroy(gameObject);
    }
}

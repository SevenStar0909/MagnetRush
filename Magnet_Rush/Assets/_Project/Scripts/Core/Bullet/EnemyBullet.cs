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

    private Rigidbody m_rb;
    private float m_timer;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
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

    // Layer Matrix で「当たる相手」を一元管理（原則1）。PlayerBullet × EnemyBullet は OFF。
    // MagnetBullet は SphereCast で EnemyBullet レイヤを直接拾うので Matrix OFF でも磁化検知は機能する。
    // コリジョンコールバック内で相手の型/タグ判定はしない（原則4）。
    private void OnTriggerEnter(Collider other)
    {
        // Hurtbox 子コライダー直撃でも親 Hitbox の IHittable に到達する
        var hittable = other.GetComponentInParent<IHittable>();
        if (hittable != null)
        {
            hittable.OnHit(new HitData
            {
                damage = m_damage,
                hitPoint = other.ClosestPoint(transform.position),
                knockbackDir = m_rb.linearVelocity.normalized,
                source = gameObject
            });
        }

        Destroy(gameObject);
    }
}

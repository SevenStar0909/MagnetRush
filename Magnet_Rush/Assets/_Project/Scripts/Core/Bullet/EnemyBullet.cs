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

    private void OnTriggerEnter(Collider other)
    {
        // 磁化時に自身に AddComponent された MagnetField の子トリガー (MagnetFieldTrigger) との自己発火は無視
        if (other.transform.IsChildOf(transform))
        {
            ChannelLogger.LogGuardReturn("Enemy", "自身の子コライダーとの衝突は無視");
            return;
        }

        // PlayerBullet (MagnetBullet) で被弾した時はタレット弾側で磁化されるため自滅しない
        // （MagnetBullet 側で SetPole / MagnetField 付与 / 自身の Destroy を行う）
        if (other.GetComponentInParent<MagnetBullet>() != null)
        {
            ChannelLogger.LogGuardReturn("Enemy", "MagnetBullet衝突は磁化委譲のため自滅スキップ");
            return;
        }

        // Matrixが「当たるべき相手」だけを通す。Hurtbox 子コライダー直撃でも親 Hitbox の IHittable に到達する
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

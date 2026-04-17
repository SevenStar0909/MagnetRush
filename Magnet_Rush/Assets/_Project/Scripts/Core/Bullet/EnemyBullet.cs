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
        // Matrixが「当たるべき相手」だけを通す
        var hittable = other.GetComponent<IHittable>();
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

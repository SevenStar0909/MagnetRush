using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class EnemyTurretBullet : MonoBehaviour
{
    [Header("Bullet")]
    [SerializeField] private float m_speed = 20f;
    [SerializeField] private float m_lifetime = 4f;
    [SerializeField] private int m_damage = 1;

    private Rigidbody m_rb;
    private GameObject m_owner;
    private float m_timer;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_rb.useGravity = false;
        m_timer = m_lifetime;
    }

    public void Initialize(Vector3 direction, GameObject owner)
    {
        m_owner = owner;
        if (m_rb == null) return;

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
        HandleHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider other)
    {
        if (other == null) return;
        if (m_owner != null && other.transform.root.gameObject == m_owner) return;

        Health health = other.GetComponentInParent<Health>();
        if (health != null)
        {
            health.Damage(m_damage);
            Debug.Log($"[TurretBullet] {other.name}({other.transform.root.name})にヒット HP={health.CurrentHealth}/{health.MaxHealth}");
        }
        else
        {
            Debug.Log($"[TurretBullet] {other.name}({other.transform.root.name})に衝突（Health無し）");
        }

        Destroy(gameObject);
    }
}

using UnityEngine;

public enum MagneticPole
{
    None,
    S,
    N
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class MagnetBullet : MonoBehaviour
{
    [SerializeField] private BulletSettings settings;

    public MagneticPole Pole { get; private set; }

    private Rigidbody rb;
    private float timer;

    public void Initialize(MagneticPole pole, Vector3 direction)
    {
        Pole = pole;
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearVelocity = direction.normalized * settings.bulletSpeed;
        timer = settings.lifetime;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Wall"))
        {
            // パターン1: 壁にくっつく（骨組みのみ）
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
            Debug.Log($"Bullet hit Wall: {Pole}");
        }
        else if (other.CompareTag("Enemy"))
        {
            // パターン2: 弾消去（骨組みのみ）
            Debug.Log($"Bullet hit Enemy: {Pole}");
        }
    }
}

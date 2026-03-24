using UnityEditor.Search;
using UnityEngine;

public enum MagneticPole
{
    None,
    S,
    N
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Renderer))]
public class MagnetBullet : MonoBehaviour
{
    [SerializeField] private BulletSettings settings;

    [SerializeField] private GameObject magneticFieldArea;

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
        ApplyMaterial();

        // 飛んでる最中は磁力範囲を非表示にする
        if(magneticFieldArea != null )
        {
            magneticFieldArea.SetActive(false);
        }
    }

    private void ApplyMaterial()
    {
        Renderer renderer = GetComponent<Renderer>();

        if (Pole == MagneticPole.S)
        {
            renderer.material = settings.sMaterial;
        }
        else if (Pole == MagneticPole.N)
        {
            renderer.material = settings.nMaterial;
        }
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

            // 親子関係にする
            transform.SetParent(other.transform);

            ShowMagneticField();

            Debug.Log($"Bullet hit Wall: {Pole}");
            Debug.Log($"Bullet stucj Wall. Wall Object: {other.name}");
        }
        else if (other.CompareTag("Enemy"))
        {
            // パターン2: 弾消去（骨組みのみ）
            Debug.Log($"Bullet hit Enemy: {Pole}");
        }
    }

    private void ShowMagneticField()
    {
        if (magneticFieldArea == null) return;

        // オブジェクトを表示
        magneticFieldArea.SetActive(true);

        // 色（マテリアル）をS極・N極に合わせる
        Renderer fieldRenderer = magneticFieldArea.GetComponent<Renderer>();
        if (fieldRenderer != null)
        {
            if (Pole == MagneticPole.S) fieldRenderer.material = settings.sFieldMaterial;
            else if (Pole == MagneticPole.N) fieldRenderer.material = settings.nFieldMaterial;
        }
    }

    // リストからも削除する
    private void OnDestroy()
    {
        if (BulletManager.Instance != null)
        {
            BulletManager.Instance.UnregisterBullet(this);
        }
    }
    
}

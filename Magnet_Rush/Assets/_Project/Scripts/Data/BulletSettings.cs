using UnityEngine;

[CreateAssetMenu(fileName = "BulletSettings", menuName = "MagnetRush/BulletSettings")]
public class BulletSettings : ScriptableObject
{
    [Header("弾")]
    public float bulletSpeed = 30f;
    public float lifetime = 5f;
    public int maxBullets = 4;
    public GameObject bulletPrefab;

    [Header("射撃")]
    public float raycastDistance = 200f;
    public float defaultMagnetRange = 5f;

    [Header("フォールバック")]
    [Tooltip("ONの場合、敵に当たっても弾を消さず壁と同じくくっつく")]
    public bool useFallbackMode = false;

    [Header("弾マテリアル")]
    public Material sMaterial;
    public Material nMaterial;
}

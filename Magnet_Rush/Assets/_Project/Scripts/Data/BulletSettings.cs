using UnityEngine;

[CreateAssetMenu(fileName = "BulletSettings", menuName = "MagnetRush/BulletSettings")]
public class BulletSettings : ScriptableObject
{
    [Header("Bullet")]
    public float bulletSpeed = 30f;
    public float lifetime = 5f;
    public int maxBullets = 4;
}

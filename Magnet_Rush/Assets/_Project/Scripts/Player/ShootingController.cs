using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// RT入力で磁力弾を画面中央方向に発射する。
/// </summary>
public class ShootingController : MonoBehaviour
{
    [FormerlySerializedAs("bulletSettings")]
    [SerializeField] private BulletSettings m_bulletSettings;
    [FormerlySerializedAs("playerSettings")]
    [SerializeField] private PlayerSettings m_playerSettings;
    [FormerlySerializedAs("firePoint")]
    [SerializeField] private Transform m_firePoint;

    private PlayerInputHandler m_input;
    private PolarityController m_polarityController;
    private AimController m_aimController;
    private PlayerEvents m_events;
    private Camera m_mainCamera;

    void Awake()
    {
        m_input = GetComponent<PlayerInputHandler>();
        m_polarityController = GetComponent<PolarityController>();
        m_aimController = GetComponent<AimController>();
        m_events = GetComponent<PlayerEvents>();
    }

    void Start()
    {
        m_mainCamera = Camera.main;
    }

    void Update()
    {
        // リロード（X）
        if (m_input.ConsumeReload() && BulletManager.Instance != null)
        {
            BulletManager.Instance.ClearAll();
            m_events?.FireReload();
        }

        // 射撃（RT）
        if (m_input.ConsumeFire())
        {
            if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null) return;
            if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot()) return;
            Fire();
        }
    }

    private void Fire()
    {
        if (m_mainCamera == null) return;

        // 画面中央からレイキャスト→着弾点を算出（Playerレイヤー除外）
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        Ray ray = m_mainCamera.ScreenPointToRay(screenCenter);

        int layerMask = ~(1 << gameObject.layer); // 自分のレイヤーを除外
        float maxDist = m_bulletSettings != null ? m_bulletSettings.raycastDistance : 200f;
        Vector3 targetPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, maxDist, layerMask))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.GetPoint(maxDist);
        }

        // 発射位置から着弾点への方向
        float height = m_playerSettings != null ? m_playerSettings.firePointHeight : 1.2f;
        Vector3 spawnPos = m_firePoint != null ? m_firePoint.position : transform.position + Vector3.up * height;
        Vector3 direction = (targetPoint - spawnPos).normalized;

        // 弾を生成・初期化
        GameObject bulletObj = Instantiate(m_bulletSettings.bulletPrefab, spawnPos, Quaternion.LookRotation(direction));
        var bullet = bulletObj.GetComponent<MagnetBullet>();
        if (bullet != null)
        {
            MagneticPole pole = m_polarityController != null ? m_polarityController.CurrentPole : MagneticPole.S;
            bullet.Initialize(pole, direction);
        }

        m_events?.FireShoot();

        // 弾の着弾時にスロー解除するコールバックを設定
        if (bullet != null && m_aimController != null)
        {
            var aim = m_aimController;
            bullet.OnImpact += () => aim.StopAim();
        }
    }
}

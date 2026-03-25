using UnityEngine;
using MagnetRush.Common;

namespace MagnetRush.Player
{
    /// <summary>
    /// RT入力で磁力弾を画面中央方向に発射する。
    /// </summary>
    public class ShootingController : MonoBehaviour
    {
        [SerializeField] private BulletSettings bulletSettings;
        [SerializeField] private Transform firePoint;

        private PlayerInputHandler input;
        private PolarityController polarityController;
        private AimController aimController;
        private PlayerEvents events;
        private Camera mainCamera;

        void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            polarityController = GetComponent<PolarityController>();
            aimController = GetComponent<AimController>();
            events = GetComponent<PlayerEvents>();
        }

        void Start()
        {
            mainCamera = Camera.main;
        }

        void Update()
        {
            // リロード（X）
            if (input.ConsumeReload() && BulletManager.Instance != null)
            {
                BulletManager.Instance.ClearAll();
                events?.FireReload();
            }

            // 射撃（RT）
            if (input.ConsumeFire())
            {
                if (bulletSettings == null || bulletSettings.bulletPrefab == null) return;
                if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot()) return;
                Fire();
            }
        }

        private void Fire()
        {
            if (mainCamera == null) return;

            // 画面中央からレイキャスト→着弾点を算出（Playerレイヤー除外）
            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            Ray ray = mainCamera.ScreenPointToRay(screenCenter);

            int layerMask = ~(1 << gameObject.layer); // 自分のレイヤーを除外
            Vector3 targetPoint;
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, layerMask))
            {
                targetPoint = hit.point;
            }
            else
            {
                targetPoint = ray.GetPoint(200f);
            }

            // 発射位置から着弾点への方向
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.2f;
            Vector3 direction = (targetPoint - spawnPos).normalized;

            // 弾を生成・初期化
            GameObject bulletObj = Instantiate(bulletSettings.bulletPrefab, spawnPos, Quaternion.LookRotation(direction));
            var bullet = bulletObj.GetComponent<MagnetBullet>();
            if (bullet != null)
            {
                MagneticPole pole = polarityController != null ? polarityController.CurrentPole : MagneticPole.S;
                bullet.Initialize(pole, direction);
            }

            events?.FireShoot();

            // 弾の着弾時にスロー解除するコールバックを設定
            if (bullet != null && aimController != null)
            {
                var aim = aimController;
                bullet.OnImpact += () => aim.StopAim();
            }
        }
    }
}

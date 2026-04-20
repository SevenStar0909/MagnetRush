using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// RT入力で磁力弾を画面中央方向に発射する。A/Fで自身に磁力を付与する。
/// </summary>
[RequireComponent(typeof(Player))]
public class ShootingController : MonoBehaviour
{
    [FormerlySerializedAs("bulletSettings")]
    [SerializeField] private BulletSettings m_bulletSettings;
    [FormerlySerializedAs("firePoint")]
    [SerializeField] private Transform m_firePoint;
    [SerializeField] private float m_selfFireHeightOffset = 1.0f;

    private PlayerSettings m_playerSettings;
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
        m_playerSettings = GetComponent<Player>().Settings;
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
            if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null) { ChannelLogger.LogGuardReturn("Player", "BulletSettings未設定"); return; }
            if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot()) { ChannelLogger.LogGuardReturn("Player", "BulletManager未初期化 or 射撃不可"); return; }
            Fire();
        }

        // セルフファイア（A / F）— 自分に磁力を付与
        if (m_input.ConsumeSelfFire())
        {
            if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null) { ChannelLogger.LogGuardReturn("Player", "BulletSettings未設定(SelfFire)"); return; }
            if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot()) { ChannelLogger.LogGuardReturn("Player", "BulletManager未初期化 or 射撃不可(SelfFire)"); return; }
            SelfFire();
        }
    }

    private const float k_ForwardDotThreshold = 0.1f;

    private void Fire()
    {
        if (m_mainCamera == null) { ChannelLogger.LogGuardReturn("Player", "MainCameraなし"); return; }

        // 発射位置を先に確定
        float height = m_playerSettings != null ? m_playerSettings.firePointHeight : 1.2f;
        Vector3 spawnPos = m_firePoint != null ? m_firePoint.position : transform.position + Vector3.up * height;

        // 画面中央からカメラレイ取得
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        Ray ray = m_mainCamera.ScreenPointToRay(screenCenter);
        Vector3 camForward = m_mainCamera.transform.forward;

        // MaskShootingRaycastでTrigger系は既に除外済み
        int layerMask = PhysicsLayers.MaskShootingRaycast;
        float maxDist = m_bulletSettings != null ? m_bulletSettings.raycastDistance : 200f;

        Vector3 targetPoint = CalculateTargetPoint(ray, camForward, spawnPos, layerMask, maxDist);

        // デバッグ表示（CalculateTargetPointの結果を可視化）
        float debugDuration = 3.0f; // 線を表示し続ける時間（秒）
        Debug.DrawLine(ray.origin, targetPoint, Color.cyan, debugDuration); // カメラからの視線
        Debug.DrawLine(spawnPos, targetPoint, Color.yellow, debugDuration); // 実際の弾道

        Vector3 direction = (targetPoint - spawnPos).normalized;

        // 弾を生成・初期化
        GameObject bulletObj = Instantiate(m_bulletSettings.bulletPrefab, spawnPos, Quaternion.LookRotation(direction));
        var bullet = bulletObj.GetComponent<MagnetBullet>();
        if (bullet != null)
        {
            MagneticPole pole = m_polarityController != null ? m_polarityController.CurrentPole : MagneticPole.S;
            bullet.Initialize(pole, direction);

            // 【変更点2】生成した弾をBulletManagerに登録する
            BulletManager.Instance.Register(bullet);
        }

        m_events?.FireShoot();

        // 弾の着弾時にスロー解除するコールバックを設定
        if (bullet != null && m_aimController != null)
        {
            var aim = m_aimController;
            bullet.OnImpact += () => aim.StopAim();
        }
    }

    /// <summary>
    /// 平面交差法でターゲット座標を算出する。
    /// レイキャストhitが発射位置より前方ならそのまま使い、
    /// 後方 or hitなしなら発射位置高さの水平面との交点を使う。
    /// 真下向き（交点なし）ではcamera.forwardにフォールバック。
    /// </summary>
    private Vector3 CalculateTargetPoint(Ray ray, Vector3 camForward, Vector3 spawnPos, int layerMask, float maxDist)
    {
        // レイキャストでhit探索
        if (Physics.Raycast(ray, out RaycastHit hit, maxDist, layerMask))
        {
            // hitが発射位置より前方なら採用
            if (Vector3.Dot(camForward, hit.point - spawnPos) > 0f)
                return hit.point;
        }

        // hitなし or 後方 → 平面交差法
        Plane firePlane = new Plane(Vector3.up, spawnPos);
        if (firePlane.Raycast(ray, out float enter) && enter > 0f)
        {
            Vector3 intersection = ray.GetPoint(enter);
            Vector3 toIntersection = (intersection - spawnPos).normalized;

            // 交点が前方かつ十分な角度があるか
            if (Vector3.Dot(camForward, toIntersection) > k_ForwardDotThreshold)
                return intersection;
        }

        // 真下向き → camera.forwardフォールバック
        return spawnPos + camForward * maxDist;
    }

    /// <summary>
    /// コリジョンを経由せず、直接プレイヤーを磁化する。
    /// MagnetField+Visualizer+エフェクトを生成して演出を維持。
    /// </summary>
    private void SelfFire()
    {
        MagneticPole pole = m_polarityController != null ? m_polarityController.CurrentPole : MagneticPole.S;

        // プレイヤーを磁化
        var magnetizable = GetComponent<Magnetizable>();
        if (magnetizable != null)
            magnetizable.SetPole(pole);

        // MagnetField + Visualizer を直接生成
        var fieldSettings = m_bulletSettings != null ? m_bulletSettings.bulletFieldSettings : null;
        if (fieldSettings != null)
        {
            var existing = GetComponent<MagnetField>();
            if (existing == null)
            {
                var field = gameObject.AddComponent<MagnetField>();
                field.Initialize(pole, fieldSettings);

                if (MagnetManager.Instance != null)
                    MagnetManager.Instance.RegisterField(field);

                var visualizer = gameObject.AddComponent<MagnetFieldVisualizer>();
                visualizer.Show(pole, fieldSettings);

                // 着弾エフェクト
                GameObject effectPrefab = pole == MagneticPole.S ? m_bulletSettings.impactEffect_S : m_bulletSettings.impactEffect_N;
                GameObject effectInstance = null;
                if (effectPrefab != null)
                {
                    effectInstance = Instantiate(effectPrefab, transform);
                    effectInstance.transform.localPosition = Vector3.zero;
                }

                field.OnFieldExpired += () =>
                {
                    if (magnetizable != null) magnetizable.Deactivate();
                    if (visualizer != null) Destroy(visualizer);
                    if (effectInstance != null) Destroy(effectInstance);
                };
            }
        }

        // 弾数カウント（自己射撃も弾を消費）
        if (BulletManager.Instance != null)
            BulletManager.Instance.IncrementShotCount();

        m_events?.FireSelfShoot();
    }
}

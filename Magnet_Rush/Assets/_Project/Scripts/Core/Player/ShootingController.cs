using UnityEngine;

/// <summary>
/// 射撃コンポーネント。RT で通常射撃、A/F でセルフファイア、X でリロード。
/// 依存: PlayerInputHandler, PlayerEvents, Magnetizable, PoleController, AimController, Player（PlayerSettings 参照用）
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
[RequireComponent(typeof(Magnetizable))]
[RequireComponent(typeof(PoleController))]
[RequireComponent(typeof(AimController))]
[RequireComponent(typeof(Player))]
public class ShootingController : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] private BulletSettings m_bulletSettings;
    [SerializeField] private Transform m_firePoint;

    private Camera m_mainCamera;
    private PlayerInputHandler m_input;
    private PlayerEvents m_events;
    private Magnetizable m_magnetizable;
    private PoleController m_pole;
    private AimController m_aim;
    private Player m_player;

    private const float k_ForwardDotThreshold = 0.1f;

    void Awake()
    {
        m_input = GetComponent<PlayerInputHandler>();
        m_events = GetComponent<PlayerEvents>();
        m_magnetizable = GetComponent<Magnetizable>();
        m_pole = GetComponent<PoleController>();
        m_aim = GetComponent<AimController>();
        m_player = GetComponent<Player>();
    }

    void Start()
    {
        m_mainCamera = Camera.main;
    }

    /// <summary>RT 入力があれば通常射撃。毎フレーム呼ぶ。</summary>
    public void Fire()
    {
        if (!m_input.IsFirePressed) return;
        if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null)
        { ChannelLogger.LogGuardReturn("Player", "BulletSettings未設定"); return; }
        if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot())
        { ChannelLogger.LogGuardReturn("Player", "BulletManager未初期化 or 射撃不可"); return; }
        if (m_mainCamera == null)
        { ChannelLogger.LogGuardReturn("Player", "MainCameraなし"); return; }

        m_input.ConsumeFire();

        float height = m_player.Settings != null ? m_player.Settings.firePointHeight : 1.2f;
        Vector3 spawnPos = m_firePoint != null ? m_firePoint.position : transform.position + Vector3.up * height;

        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        Ray ray = m_mainCamera.ScreenPointToRay(screenCenter);
        Vector3 camForward = m_mainCamera.transform.forward;

        int layerMask = PhysicsLayers.MaskShootingRaycast;
        float maxDist = m_bulletSettings.raycastDistance;

        Vector3 targetPoint = CalculateTargetPoint(ray, camForward, spawnPos, layerMask, maxDist);

        float debugDuration = 3.0f;
        Debug.DrawLine(ray.origin, targetPoint, Color.cyan, debugDuration);
        Debug.DrawLine(spawnPos, targetPoint, Color.yellow, debugDuration);

        Vector3 direction = (targetPoint - spawnPos).normalized;

        GameObject bulletObj = Instantiate(m_bulletSettings.bulletPrefab, spawnPos, Quaternion.LookRotation(direction));
        var bullet = bulletObj.GetComponent<MagnetBullet>();
        if (bullet != null)
        {
            bullet.Initialize(m_pole.CurrentPole, direction);
            BulletManager.Instance.Register(bullet);
            // 着弾時にエイム解除、自己 unsubscribe で累積・ダングリング参照を防ぐ
            void HandleImpact()
            {
                m_aim.StopAim();
                bullet.OnImpact -= HandleImpact;
            }
            bullet.OnImpact += HandleImpact;
        }

        m_events.FireShoot();
    }

    /// <summary>A / F 入力があればセルフファイア（自己磁化）。毎フレーム呼ぶ。</summary>
    public void SelfFire()
    {
        if (!m_input.IsSelfFirePressed) return;
        if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null)
        { ChannelLogger.LogGuardReturn("Player", "BulletSettings未設定(SelfFire)"); return; }
        if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot())
        { ChannelLogger.LogGuardReturn("Player", "BulletManager未初期化 or 射撃不可(SelfFire)"); return; }

        m_input.ConsumeSelfFire();

        m_magnetizable.SetPole(m_pole.CurrentPole);

        var fieldSettings = m_bulletSettings.bulletFieldSettings;
        if (fieldSettings != null)
        {
            var existing = GetComponent<MagnetField>();
            if (existing == null)
            {
                var field = gameObject.AddComponent<MagnetField>();
                field.Initialize(m_pole.CurrentPole, fieldSettings);

                if (MagnetManager.Instance != null)
                    MagnetManager.Instance.RegisterField(field);

                var visualizer = gameObject.AddComponent<MagnetFieldVisualizer>();
                visualizer.Show(m_pole.CurrentPole, fieldSettings);

                GameObject effectPrefab = m_pole.CurrentPole == MagneticPole.S
                    ? m_bulletSettings.impactEffect_S
                    : m_bulletSettings.impactEffect_N;
                GameObject effectInstance = null;
                if (effectPrefab != null)
                {
                    effectInstance = Instantiate(effectPrefab, transform);
                    effectInstance.transform.localPosition = Vector3.zero;
                }

                field.OnFieldExpired += () =>
                {
                    m_magnetizable.Deactivate();
                    if (visualizer != null) Destroy(visualizer);
                    if (effectInstance != null) Destroy(effectInstance);
                };
            }
        }

        if (BulletManager.Instance != null)
            BulletManager.Instance.IncrementShotCount();

        m_events.FireSelfShoot();
    }

    /// <summary>X 入力があればリロード（全弾クリア）。毎フレーム呼ぶ。</summary>
    public void Reload()
    {
        if (!m_input.IsReloadPressed) return;
        if (BulletManager.Instance == null) return;
        m_input.ConsumeReload();
        BulletManager.Instance.ClearAll();
        m_events.FireReload();
    }

    /// <summary>弾道計算。カメラレイ交差 → 平面交差 → 前方フォールバック。</summary>
    private Vector3 CalculateTargetPoint(Ray ray, Vector3 camForward, Vector3 spawnPos, int layerMask, float maxDist)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, maxDist, layerMask))
        {
            if (Vector3.Dot(camForward, hit.point - spawnPos) > 0f)
                return hit.point;
        }

        Plane firePlane = new Plane(Vector3.up, spawnPos);
        if (firePlane.Raycast(ray, out float enter) && enter > 0f)
        {
            Vector3 intersection = ray.GetPoint(enter);
            Vector3 toIntersection = (intersection - spawnPos).normalized;
            if (Vector3.Dot(camForward, toIntersection) > k_ForwardDotThreshold)
                return intersection;
        }

        return spawnPos + camForward * maxDist;
    }
}

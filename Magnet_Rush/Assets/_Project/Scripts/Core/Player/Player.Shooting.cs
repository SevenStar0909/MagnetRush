using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Player の射撃系（partial）。RT で通常射撃、A/F でセルフファイア、X でリロード。
/// SerializeField は Inspector で _Player.prefab に設定済み。
/// </summary>
public partial class Player
{
    [Header("Shooting")]
    [FormerlySerializedAs("bulletSettings")]
    [SerializeField] private BulletSettings m_bulletSettings;

    [FormerlySerializedAs("firePoint")]
    [SerializeField] private Transform m_firePoint;

    [SerializeField] private float m_selfFireHeightOffset = 1.0f;

    private Camera m_mainCamera;

    private const float k_ForwardDotThreshold = 0.1f;

    /// <summary>RT 入力があれば通常射撃。毎フレーム呼ぶ。</summary>
    public void Fire()
    {
        if (!input.ConsumeFire()) return;
        if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null)
        { ChannelLogger.LogGuardReturn("Player", "BulletSettings未設定"); return; }
        if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot())
        { ChannelLogger.LogGuardReturn("Player", "BulletManager未初期化 or 射撃不可"); return; }
        if (m_mainCamera == null)
        { ChannelLogger.LogGuardReturn("Player", "MainCameraなし"); return; }

        // 発射位置を先に確定
        float height = m_settings != null ? m_settings.firePointHeight : 1.2f;
        Vector3 spawnPos = m_firePoint != null ? m_firePoint.position : transform.position + Vector3.up * height;

        // 画面中央からカメラレイ取得
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
            bullet.Initialize(CurrentPole, direction);
            BulletManager.Instance.Register(bullet);
            bullet.OnImpact += StopAim;
        }

        events?.FireShoot();
    }

    /// <summary>A / F 入力があればセルフファイア（自己磁化）。毎フレーム呼ぶ。</summary>
    public void SelfFire()
    {
        if (!input.ConsumeSelfFire()) return;
        if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null)
        { ChannelLogger.LogGuardReturn("Player", "BulletSettings未設定(SelfFire)"); return; }
        if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot())
        { ChannelLogger.LogGuardReturn("Player", "BulletManager未初期化 or 射撃不可(SelfFire)"); return; }

        if (magnetizable != null)
            magnetizable.SetPole(CurrentPole);

        var fieldSettings = m_bulletSettings.bulletFieldSettings;
        if (fieldSettings != null)
        {
            var existing = GetComponent<MagnetField>();
            if (existing == null)
            {
                var field = gameObject.AddComponent<MagnetField>();
                field.Initialize(CurrentPole, fieldSettings);

                if (MagnetManager.Instance != null)
                    MagnetManager.Instance.RegisterField(field);

                var visualizer = gameObject.AddComponent<MagnetFieldVisualizer>();
                visualizer.Show(CurrentPole, fieldSettings);

                GameObject effectPrefab = CurrentPole == MagneticPole.S
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
                    if (magnetizable != null) magnetizable.Deactivate();
                    if (visualizer != null) Destroy(visualizer);
                    if (effectInstance != null) Destroy(effectInstance);
                };
            }
        }

        if (BulletManager.Instance != null)
            BulletManager.Instance.IncrementShotCount();

        events?.FireSelfShoot();
    }

    /// <summary>X 入力があればリロード（全弾クリア）。毎フレーム呼ぶ。</summary>
    public void Reload()
    {
        if (!input.ConsumeReload()) return;
        if (BulletManager.Instance == null) return;
        BulletManager.Instance.ClearAll();
        events?.FireReload();
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

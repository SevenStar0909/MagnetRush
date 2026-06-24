using UnityEngine;

/// <summary>
/// 射撃能力。RT で通常射撃、A/F でセルフファイア、X でリロード。
/// 基底: Ability（共通の依存 m_input / m_player / m_events / m_states は基底で取得済み）
/// 派生固有の追加依存: Magnetizable / PoleAbility / AimAbility（極性参照・着弾時エイム解除）
/// </summary>
[RequireComponent(typeof(Magnetizable))]
[RequireComponent(typeof(PoleAbility))]
[RequireComponent(typeof(AimAbility))]
public class ShootingAbility : Ability
{
    [Header("Shooting")]
    [SerializeField] private BulletSettings m_bulletSettings;
    [SerializeField] private Transform m_firePoint;

    [Header("Self Fire Effect")]
    [SerializeField] private Transform m_selfFireEffectAnchor;
    [SerializeField] private Vector3 m_selfFireEffectLocalOffset = Vector3.up;
    [SerializeField] private float m_selfFireEffectScaleMultiplier = 1f;

    private Camera m_mainCamera;
    private Magnetizable m_magnetizable;
    private PoleAbility m_pole;
    private AimAbility m_aim;
    private MagnetField m_selfFireField;
    private GameObject m_selfFireFieldHost;

    private const float k_ForwardDotThreshold = 0.1f;
    private const string k_SelfFireFieldHostName = "SelfMagnetFieldHost";

    protected override void Awake()
    {
        base.Awake();
        m_magnetizable = GetComponent<Magnetizable>();
        m_pole = GetComponent<PoleAbility>();
        m_aim = GetComponent<AimAbility>();
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

        // 自分(Player)・自弾(PlayerBullet)・Trigger系(MeleeHitbox/MagnetField)・IgnoreRaycast を除外した全部
        // TODO(段階2): 陣営除外(Player/PlayerBullet)は HitGroup ベースに寄せる
        int layerMask = ~(PhysicsLayers.Bit(PhysicsLayers.Player) | PhysicsLayers.Bit(PhysicsLayers.PlayerBullet)
            | PhysicsLayers.Bit(PhysicsLayers.MeleeHitbox) | PhysicsLayers.Bit(PhysicsLayers.MagnetField)
            | PhysicsLayers.Bit(PhysicsLayers.IgnoreRaycast));
        float maxDist = m_bulletSettings.raycastDistance;

        float aimAssistRadius = Mathf.Max(0f, m_bulletSettings.aimAssistRadius);
        Vector3 targetPoint = CalculateTargetPoint(ray, camForward, spawnPos, layerMask, maxDist, aimAssistRadius);

        float debugDuration = 3.0f;
        Debug.DrawLine(ray.origin, targetPoint, Color.cyan, debugDuration);
        Debug.DrawLine(spawnPos, targetPoint, Color.yellow, debugDuration);

        Vector3 direction = (targetPoint - spawnPos).normalized;

        // 撃った瞬間に体を弾の方向（水平成分のみ）へスナップ。Hipfire 時の見栄え用、エイム中も無害
        m_player.FaceHorizontalInstant(direction);

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
        Sound.Play(SoundData.CueSheet.SE, SoundData.SE.PlayerShot);
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
            if (m_selfFireField == null)
            {
                Transform effectParent = m_selfFireEffectAnchor != null ? m_selfFireEffectAnchor : transform;
                m_selfFireFieldHost = new GameObject(k_SelfFireFieldHostName);
                m_selfFireFieldHost.transform.SetParent(effectParent, false);
                m_selfFireFieldHost.transform.localPosition = m_selfFireEffectLocalOffset;
                m_selfFireFieldHost.transform.localRotation = Quaternion.identity;
                m_selfFireFieldHost.transform.localScale = Vector3.one;

                var field = m_selfFireFieldHost.AddComponent<MagnetField>();
                field.Initialize(m_pole.CurrentPole, fieldSettings);
                m_selfFireField = field;

                if (MagnetManager.Instance != null)
                    MagnetManager.Instance.RegisterField(field);

                var visualizer = m_selfFireFieldHost.AddComponent<MagnetFieldVisualizer>();
                visualizer.Show(m_pole.CurrentPole, fieldSettings);

                GameObject effectPrefab = m_pole.CurrentPole == MagneticPole.S
                    ? m_bulletSettings.impactEffect_S
                    : m_bulletSettings.impactEffect_N;
                GameObject effectInstance = null;
                if (effectPrefab != null)
                {
                    effectInstance = Instantiate(effectPrefab, m_selfFireFieldHost.transform);
                    effectInstance.transform.localPosition = Vector3.zero;
                    effectInstance.transform.localRotation = Quaternion.identity;
                    effectInstance.transform.localScale = WorldScale(
                        Mathf.Max(0f, m_bulletSettings.impactEffectScale * m_selfFireEffectScaleMultiplier),
                        m_selfFireFieldHost.transform);
                }

                field.OnFieldExpired += () =>
                {
                    m_magnetizable.Deactivate();
                    m_selfFireField = null;
                    if (m_selfFireFieldHost != null) Destroy(m_selfFireFieldHost);
                    else
                    {
                        if (visualizer != null) Destroy(visualizer);
                        if (effectInstance != null) Destroy(effectInstance);
                    }
                };
            }
        }

        if (BulletManager.Instance != null)
            BulletManager.Instance.IncrementShotCount();

        m_events.FireSelfShoot();
        Sound.Play(SoundData.CueSheet.SE, SoundData.SE.SelfShot);
    }

    /// <summary>X 入力があればリロード（全弾クリア）。毎フレーム呼ぶ。</summary>
    public void Reload()
    {
        if (!m_input.IsReloadPressed) return;
        if (BulletManager.Instance == null) return;
        m_input.ConsumeReload();
        BulletManager.Instance.ClearAll();
        m_events.FireReload();
        Sound.Play(SoundData.CueSheet.SE, SoundData.SE.Reload);
    }

    /// <summary>
    /// 弾道計算。spawnPos基準→カメラレイ交差→平面交差→前方フォールバック。
    /// spawnPos からのRaycastを優先する理由: TPS でカメラがプレイヤー後方上にあるため、
    /// カメラRayは下向き時にプレイヤー後方を指して弾が画面外の方向に飛ぶ問題を防ぐ。
    /// </summary>
    private Vector3 CalculateTargetPoint(Ray ray, Vector3 camForward, Vector3 spawnPos, int layerMask, float maxDist, float aimAssistRadius)
    {
        // カメラからレティクルへレイを飛ばす (TPSでの遠距離照準合わせ用)
        if (TryAimCast(ray.origin, ray.direction, maxDist, layerMask, aimAssistRadius, out RaycastHit hit))
        {
            if (Vector3.Dot(camForward, hit.point - spawnPos) > 0f)
                return hit.point;
        }

        // 弾の発射点からカメラ方向にRaycast (弾の進行方向と一致するので照準ズレなし)
        if (TryAimCast(spawnPos, camForward, maxDist, layerMask, aimAssistRadius, out RaycastHit bulletHit))
            return bulletHit.point;

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

    private bool TryAimCast(Vector3 origin, Vector3 direction, float maxDist, int layerMask, float radius, out RaycastHit hit)
    {
        if (radius > 0.001f && Physics.SphereCast(origin, radius, direction, out hit, maxDist, layerMask, QueryTriggerInteraction.Collide))
            return true;

        return Physics.Raycast(origin, direction, out hit, maxDist, layerMask, QueryTriggerInteraction.Collide);
    }

    private static Vector3 WorldScale(float size, Transform parent)
    {
        Vector3 l = parent.lossyScale;
        return new Vector3(
            Mathf.Abs(l.x) > 0.001f ? size / l.x : size,
            Mathf.Abs(l.y) > 0.001f ? size / l.y : size,
            Mathf.Abs(l.z) > 0.001f ? size / l.z : size
        );
    }
}

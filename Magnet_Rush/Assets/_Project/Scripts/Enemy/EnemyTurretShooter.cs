using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyTurretBase))]
[RequireComponent(typeof(EnemyTurretMagneticAim))]
[RequireComponent(typeof(Magnetizable))]
public class EnemyTurretShooter : MonoBehaviour
{
    [Header("References")]
    [Tooltip("磁力弾ではない、タレット専用の通常弾Prefab")]
    [SerializeField] private GameObject m_projectilePrefab;
    [Tooltip("未指定なら EnemyTurretBase.FirePoint を使用")]
    [SerializeField] private Transform m_firePointOverride;

    private EnemyTurretBase m_turretBase;
    private EnemyTurretMagneticAim m_aim;
    private Magnetizable m_selfMagnetizable;
    private EnemyTurretSettings m_data;

    private float m_shootTimer;

    private void Awake()
    {
        m_turretBase = GetComponent<EnemyTurretBase>();
        m_aim = GetComponent<EnemyTurretMagneticAim>();
        m_selfMagnetizable = GetComponent<Magnetizable>();
    }

    private void Start()
    {
        m_data = m_turretBase != null ? m_turretBase.StatusData : null;
    }

    private void Update()
    {
        m_shootTimer += Time.deltaTime;

        if (!CanShootNow())
            return;

        Fire();
        m_shootTimer = 0f;
    }

    private bool CanShootNow()
    {
        if (m_projectilePrefab == null) return false;
        if (m_data == null || m_aim == null) return false;
        if (!m_aim.HasAimTarget) return false;

        if (m_data.shootOnlyWhenMagnetized && (m_selfMagnetizable == null || !m_selfMagnetizable.IsActive))
            return false;

        float interval = Mathf.Max(0.05f, m_data.shootInterval);
        if (m_shootTimer < interval)
            return false;

        Transform muzzle = ResolveFirePoint();

        Vector3 muzzleForward = muzzle != null ? muzzle.forward : transform.forward;
        muzzleForward.y = 0f;
        if (muzzleForward.sqrMagnitude <= 0.0001f) return false;
        muzzleForward.Normalize();

        Vector3 aimDir = m_aim.CurrentAimDirection;
        aimDir.y = 0f;
        if (aimDir.sqrMagnitude <= 0.0001f) return false;
        aimDir.Normalize();

        float minDot = Mathf.Clamp(m_data.shootMinDot, -1f, 1f);
        return Vector3.Dot(muzzleForward, aimDir) >= minDot;
    }

    private void Fire()
    {
        Transform muzzle = ResolveFirePoint();
        Vector3 spawnPos = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.2f;

        Vector3 direction = m_aim.CurrentAimDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = muzzle != null ? muzzle.forward : transform.forward;
        direction.Normalize();

        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        GameObject projectileObj = Instantiate(m_projectilePrefab, spawnPos, rotation);

        EnemyTurretBullet turretBullet = projectileObj.GetComponent<EnemyTurretBullet>();
        if (turretBullet != null)
            turretBullet.Initialize(direction, gameObject);
    }

    private Transform ResolveFirePoint()
    {
        if (m_firePointOverride != null) return m_firePointOverride;
        if (m_turretBase != null && m_turretBase.FirePoint != null) return m_turretBase.FirePoint;
        return null;
    }
}

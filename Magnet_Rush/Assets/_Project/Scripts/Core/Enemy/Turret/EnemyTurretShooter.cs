using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyTurretBase))]
[RequireComponent(typeof(Magnetizable))]
public class EnemyTurretShooter : MonoBehaviour
{
    [Header("References")]
    [Tooltip("タレット用の通常弾Prefab")]
    [SerializeField] private GameObject m_projectilePrefab;
    [Tooltip("未指定ならEnemyTurretBase.FirePointを使用")]
    [SerializeField] private Transform m_firePointOverride;

    private EnemyTurretBase m_turretBase;
    private Magnetizable m_selfMagnetizable;
    private EnemyTurretSettings m_data;

    private float m_shootTimer;
    private int m_burstRemaining;
    private float m_burstTimer;

    private void Awake()
    {
        m_turretBase = GetComponent<EnemyTurretBase>();
        m_selfMagnetizable = GetComponent<Magnetizable>();
    }

    private void Start()
    {
        m_data = m_turretBase != null ? m_turretBase.StatusData : null;
        m_shootTimer = m_data != null ? m_data.shootInterval : 0f;
    }

    private void Update()
    {
        // バースト中
        if (m_burstRemaining > 0)
        {
            m_burstTimer -= Time.deltaTime;
            if (m_burstTimer <= 0f)
            {
                Fire();
                m_burstRemaining--;
                m_burstTimer = m_data != null ? m_data.burstInterval : 0.15f;
            }
            return;
        }

        m_shootTimer += Time.deltaTime;

        if (!CanShootNow())
            return;

        Fire();
        int burst = m_data != null ? m_data.burstCount : 1;
        m_burstRemaining = Mathf.Max(0, burst - 1);
        m_burstTimer = m_data != null ? m_data.burstInterval : 0.15f;
        m_shootTimer = 0f;
    }

    private bool CanShootNow()
    {
        if (m_projectilePrefab == null) return false;
        if (m_data == null) return false;
        if (m_turretBase == null || m_turretBase.Player == null) return false;

        if (m_data.shootOnlyWhenMagnetized && (m_selfMagnetizable == null || !m_selfMagnetizable.IsActive))
            return false;

        float interval = Mathf.Max(0.05f, m_data.shootInterval);
        if (m_shootTimer < interval)
            return false;

        // 砲身がプレイヤー方向を向いているか（砲身forwardとプレイヤー方向のdot）
        Transform muzzle = ResolveFirePoint();
        if (muzzle != null)
        {
            Vector3 toPlayer = (m_turretBase.Player.position - muzzle.position).normalized;
            // 砲身メッシュのバレルはlocal -Z方向
            float dot = Vector3.Dot(-muzzle.forward, toPlayer);
            if (dot < m_data.shootMinDot)
                return false;
        }

        return true;
    }

    private void Fire()
    {
        if (m_turretBase == null || m_turretBase.Player == null) { ChannelLogger.LogGuardReturn("Enemy", "タレット基底/プレイヤー参照なし"); return; }

        Transform muzzle = ResolveFirePoint();
        Vector3 spawnPos = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.2f;

        // 砲身方向に発射（磁化で砲身が逸れれば弾も逸れる）
        Vector3 direction = muzzle != null ? -muzzle.forward : (m_turretBase.Player.position - spawnPos).normalized;

        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        GameObject projectileObj = Instantiate(m_projectilePrefab, spawnPos, rotation);

        EnemyBullet enemyBullet = projectileObj.GetComponent<EnemyBullet>();
        if (enemyBullet != null)
        {
            // 物理ハザード化した弾は spawn 位置（砲口＝自分の Pushbox 内）で自分の EntityBody に即衝突しうる。
            // 発射元タレットのコライダーを無視させてから発射し、自爆・自傷を防ぐ。
            enemyBullet.IgnoreCollisionsWith(gameObject);
            enemyBullet.Initialize(direction);
        }
    }

    private Transform ResolveFirePoint()
    {
        if (m_firePointOverride != null) return m_firePointOverride;
        if (m_turretBase != null && m_turretBase.FirePoint != null) return m_turretBase.FirePoint;
        return null;
    }
}

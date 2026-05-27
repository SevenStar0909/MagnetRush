using UnityEngine;

using System;

/// <summary>
/// 空中敵の基底クラス。Entity を継承し、プレイヤー参照・HP・3D飛行移動を共通化する。
/// 位置移動は3D、回転はY軸のみで制御する。
/// </summary>
[DisallowMultipleComponent]
public class EnemyAirBase : Entity
{
    [Header("Data")]
    [SerializeField] private EnemyAirSettings m_statusData;

    [Header("References")]
    [SerializeField] private Transform m_player;

    [Header("Magnetic")]
    [SerializeField] private MagneticMover m_mover;

    [Header("Disappear Effect")]
    [SerializeField] private GameObject m_disappearEffectPrefab;
    [SerializeField] private float m_disappearEffectLifetime = 0.75f;

    private readonly Collider[] m_environmentContactBuffer = new Collider[8];
    private bool m_disappearEffectPlayed;

    public EnemyAirSettings StatusData => m_statusData;
    public Transform Player => m_player;
    public bool IsMagnetControlled => m_mover != null && m_mover.IsMagnetActive;
    public event Action<Collider> EnvironmentContact;

    protected override float Gravity => 0f;
    protected override float SnapForce => 0f;
    protected override float ExternalDrag => m_statusData != null ? m_statusData.externalDrag : base.ExternalDrag;
    protected override float GroundCheckDistance => 0f;
    protected override LayerMask GroundLayer => 0;

    protected override void Awake()
    {
        base.Awake();
        m_mover = GetComponentInChildren<MagneticMover>();

        if (m_health != null && m_statusData != null)
            m_health.SetMaxHealth(m_statusData.maxHp);

        CachePlayer();

        if (m_health != null)
            m_health.OnDie += Die;
    }

    private void OnDestroy()
    {
        if (m_health != null)
            m_health.OnDie -= Die;

        TriggerDisappearEffect();
    }

    private void Update()
    {
        CachePlayer();
        UpdateAir(Time.deltaTime);
        UpdateMagneticOrientation(Time.deltaTime);
        ApplyMovement(Time.deltaTime);
        CheckEnvironmentContact();
    }

    /// <summary>
    /// 派生クラスで空中AIを実装する。
    /// </summary>
    protected virtual void UpdateAir(float dt)
    {
    }

    /// <summary>指定方向へ3Dで飛行する。回転はY軸のみ。</summary>
    public void AccelerateToward(Vector3 worldDirection, float dt)
    {
        if (m_statusData == null) return;
        if (worldDirection.sqrMagnitude <= 0.0001f) return;

        Vector3 desiredDirection = worldDirection.normalized;
        Vector3 desiredVelocity = desiredDirection * Mathf.Max(0f, m_statusData.moveSpeed);

        if (velocity.sqrMagnitude > 0.001f)
        {
            float turnRadians = Mathf.Max(0f, m_statusData.turningDrag) * Mathf.Deg2Rad * dt;
            velocity = Vector3.RotateTowards(velocity, desiredVelocity, turnRadians, m_statusData.acceleration * dt);
        }
        else
        {
            velocity = Vector3.MoveTowards(velocity, desiredVelocity, m_statusData.acceleration * dt);
        }

        FaceTowardYaw(desiredDirection, dt);
    }

    /// <summary>3D移動を減速する。</summary>
    public void SlowDown(float dt)
    {
        if (m_statusData == null) return;

        velocity = Vector3.MoveTowards(velocity, Vector3.zero, m_statusData.deceleration * dt);
    }

    /// <summary>指定方向を Y 軸だけで向く。</summary>
    public void FaceTowardYaw(Vector3 direction, float dt)
    {
        if (m_statusData == null) return;

        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
        if (flatDirection.sqrMagnitude <= 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            m_statusData.rotationSpeed * dt
        );
    }

    /// <summary>指定方向を向く。現在は空中敵用に Y 軸回転のみ。</summary>
    public void FaceToward(Vector3 direction, float dt)
    {
        FaceTowardYaw(direction, dt);
    }

    public int ImpactDamage => m_statusData != null ? m_statusData.impactDamage : 1;

    public void SetPlayer(Transform player)
    {
        m_player = player;
    }

    public void DestroyWithDisappearEffect()
    {
        TriggerDisappearEffect();
        Destroy(gameObject);
    }

    protected void CachePlayer()
    {
        if (m_player != null)
            return;

        GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
            m_player = playerObj.transform;
    }

    private void CheckEnvironmentContact()
    {
        int hitCount = OverlapEntity(m_environmentContactBuffer, 0.02f);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = m_environmentContactBuffer[i];
            if (col == null)
                continue;

            int layer = col.gameObject.layer;
            if (layer == PhysicsLayers.Ground || layer == PhysicsLayers.Wall)
            {
                EnvironmentContact?.Invoke(col);
                return;
            }
        }
    }

    private void TriggerDisappearEffect()
    {
        if (m_disappearEffectPlayed)
            return;

        m_disappearEffectPlayed = true;

        if (m_disappearEffectPrefab == null)
            return;

        GameObject effectObject = Instantiate(m_disappearEffectPrefab, transform.position, Quaternion.identity);

        if (m_disappearEffectLifetime > 0f)
            Destroy(effectObject, m_disappearEffectLifetime);
    }

    protected virtual void Die()
    {
        DestroyWithDisappearEffect();
    }
}

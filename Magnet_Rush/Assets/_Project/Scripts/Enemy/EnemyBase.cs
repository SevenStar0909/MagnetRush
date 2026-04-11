using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 敵の基底クラス。Entityを継承しHealth・IMagnetTargetを共有する。
/// 移動はEntityStep() → EntityController経由。AIサブクラスがAccelerateToward等で速度を駆動する。
/// </summary>
public class EnemyBase : Entity
{
    [Header("Data")]
    [FormerlySerializedAs("statusData")]
    [SerializeField] private EnemySettings m_statusData;

    [Header("References")]
    [FormerlySerializedAs("player")]
    [SerializeField] private Transform m_player;
    [SerializeField] private Transform m_weaponHandSocket;

    [Header("Magnetic")]
    [FormerlySerializedAs("MagneticMover")]
    [SerializeField] private MagneticMover m_mover;

    private WeaponStateController m_equippedWeapon;

    public EnemySettings StatusData => m_statusData;
    public Transform Player => m_player;
    public Transform WeaponHandSocket => m_weaponHandSocket;
    public WeaponStateController EquippedWeapon => m_equippedWeapon;

    public bool HasWeapon
    {
        get
        {
            if (m_equippedWeapon == null) return false;

            if (m_equippedWeapon.State == WeaponStateController.WeaponOwnerState.Equipping)
                return true;

            return m_equippedWeapon.State == WeaponStateController.WeaponOwnerState.Owned
                && m_equippedWeapon.Owner == gameObject;
        }
    }

    /// <summary>MagneticMover が磁力移動モード中かどうか。AI はこの間スキップされる。</summary>
    public bool IsMagnetControlled => m_mover != null && m_mover.IsMagnetActive;

    protected override void Awake()
    {
        base.Awake();
        m_mover = GetComponentInChildren<MagneticMover>();

        // Playerタグ付きオブジェクトを常に取得
        GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
            m_player = playerObj.transform;

        // EnemySettings → Entity基底フィールド
        if (m_statusData != null)
        {
            m_gravity = m_statusData.gravity;
            m_snapForce = m_statusData.snapForce;
            m_groundCheckDistance = m_statusData.groundCheckDistance;
            if (m_statusData.groundLayer != 0)
                m_groundLayer = m_statusData.groundLayer;
            m_externalDrag = m_statusData.externalDrag;
        }

        if (m_health != null)
            m_health.OnDie += Die;

        if (m_controller == null)
            Debug.LogWarning($"[EnemyBase] {name}: EntityControllerがありません。衝突判定なしで動作します", this);

        // EntityControllerが元のCapsuleColliderを無効化するが、
        // 弾のOnTriggerEnterはtriggerコライダーを無視するため、非triggerコライダーを再有効化する。
        // EntityControllerは自分のRigidbodyのコライダーを自動除外するのでIgnoreCollider不要。
        if (m_capsuleCollider != null && !m_capsuleCollider.enabled)
            m_capsuleCollider.enabled = true;
    }

    void OnDestroy()
    {
        if (m_health != null)
            m_health.OnDie -= Die;
    }

    void Update()
    {
        ValidateWeaponState();
        EntityStep(Time.deltaTime);
    }

    /// <summary>指定方向（ワールド空間）に加速する。AIが計算した移動方向を渡す。</summary>
    public void AccelerateToward(Vector3 worldDirection, float dt)
    {
        if (worldDirection.sqrMagnitude > 0.01f)
        {
            // Accelerate()はlateralVelocity（ローカル空間）で計算するため変換が必要
            Vector3 localDir = Quaternion.FromToRotation(transform.up, Vector3.up) * worldDirection;
            localDir = localDir.normalized;

            Accelerate(localDir, m_statusData.turningDrag,
                       m_statusData.acceleration, m_statusData.moveSpeed, dt);
            FaceDirection(worldDirection, m_statusData.rotationSpeed, dt);
        }
    }

    /// <summary>横移動を減速する。</summary>
    public void SlowDown(float dt)
    {
        Decelerate(m_statusData.deceleration, dt);
    }

    /// <summary>指定方向を向く。</summary>
    public void FaceToward(Vector3 direction, float dt)
    {
        FaceDirection(direction, m_statusData.rotationSpeed, dt);
    }

    public bool TryEquipWeapon(WeaponStateController weapon)
    {
        if (weapon == null) return false;
        if (m_weaponHandSocket == null) return false;

        bool started = weapon.TryStartEquip(gameObject, m_weaponHandSocket);
        if (!started) return false;

        m_equippedWeapon = weapon;
        return true;
    }

    private void ValidateWeaponState()
    {
        if (m_equippedWeapon == null) return;

        if (m_equippedWeapon.State == WeaponStateController.WeaponOwnerState.Unowned)
        {
            m_equippedWeapon = null;
            return;
        }

        if (m_equippedWeapon.State == WeaponStateController.WeaponOwnerState.Owned
            && m_equippedWeapon.Owner != gameObject)
        {
            m_equippedWeapon = null;
        }
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}

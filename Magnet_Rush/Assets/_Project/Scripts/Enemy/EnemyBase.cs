using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// 敵の基底クラス。Entityを継承しHealth・IMagnetTargetを共有する。
/// 移動はNavMeshAgent経由、磁力はexternalVelocityで適用。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBase : Entity
{
    [Header("Data")]
    [FormerlySerializedAs("statusData")]
    [SerializeField] private EnemySettings m_statusData;

    [Header("References")]
    [FormerlySerializedAs("player")]
    [SerializeField] private Transform m_player;
    [SerializeField] private Transform m_weaponHandSocket;

    protected NavMeshAgent m_agent;

    [Header("Magnetic")]
    [FormerlySerializedAs("MagneticMover")]
    [SerializeField] private MagneticMover m_mover;

    private WeaponStateController m_equippedWeapon;

    public EnemySettings StatusData => m_statusData;
    public Transform Player => m_player;
    public NavMeshAgent Agent => m_agent;
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
        m_agent = GetComponent<NavMeshAgent>();

        m_mover = GetComponentInChildren<MagneticMover>();

        if (m_player == null)
        {
            GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
            if (playerObj != null)
                m_player = playerObj.transform;
        }

        // Health.OnDie → Die()
        if (m_health != null)
            m_health.OnDie += Die;
    }

    void OnDestroy()
    {
        if (m_health != null)
            m_health.OnDie -= Die;
    }

    protected virtual void Start()
    {
        // Additiveシーンの読み込み完了後にNavMeshが有効になるため、Startで確認してagentを有効化する
        if (m_agent != null && !m_agent.enabled)
        {
            bool hasNavMesh = NavMesh.SamplePosition(transform.position, out _, 10f, NavMesh.AllAreas);
            if (hasNavMesh)
                m_agent.enabled = true;
        }

        if (m_agent != null && m_agent.enabled && m_statusData != null)
        {
            m_agent.speed = m_statusData.moveSpeed;
            m_agent.stoppingDistance = m_statusData.stopDistance;
        }
    }

    void Update()
    {
        ValidateWeaponState();

        // MagneticMover がアクティブなら Rigidbody で物理移動中 → externalVelocity パスは不要
        if (IsMagnetControlled) return;

        // MagneticMover なしの敵は従来通り externalVelocity で磁力適用
        if (externalVelocity.sqrMagnitude > 0.01f && m_agent != null && m_agent.enabled)
        {
            m_agent.Move(externalVelocity * Time.deltaTime);
            externalVelocity = Vector3.zero;
        }
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

using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// �G�̊��N���X�BEntity���p����Health�EIMagnetTarget�����L����B
/// �ړ���UpdateEntity() �� EntityController�o�R�BAI�T�u�N���X��AccelerateToward���ő��x���쓮����B
/// </summary>
public class EnemyBossBase : Entity
{
    [Header("Data")]
    [FormerlySerializedAs("statusData")]
    [SerializeField] private EnemyBossSettings m_statusData;

    [Header("References")]
    [FormerlySerializedAs("player")]
    [SerializeField] private Transform m_player;

    [Header("Magnetic")]
    [Tooltip("no use for now")]
    //[FormerlySerializedAs("MagneticMover")]
    //[SerializeField] private MagneticMover m_mover;

    public EnemyBossSettings StatusData => m_statusData;
    public Transform Player => m_player;

    /// <summary>MagneticMover �����͈ړ����[�h�����ǂ����BAI �͂��̊ԃX�L�b�v�����B</summary>
    //public bool IsMagnetControlled => m_mover != null && m_mover.IsMagnetActive;

    protected override float Gravity => m_statusData != null ? m_statusData.gravity : base.Gravity;
    protected override float SnapForce => m_statusData != null ? m_statusData.snapForce : base.SnapForce;
    protected override float ExternalDrag => m_statusData != null ? m_statusData.externalDrag : base.ExternalDrag;
    protected override float GroundCheckDistance => m_statusData != null ? m_statusData.groundCheckDistance : base.GroundCheckDistance;
    protected override LayerMask GroundLayer => (m_statusData != null && m_statusData.groundLayer != 0) ? m_statusData.groundLayer : base.GroundLayer;

    protected override void Awake()
    {
        base.Awake();
        //m_mover = GetComponentInChildren<MagneticMover>();

        var magnetizable = GetComponent<Magnetizable>();
        if (magnetizable != null)
            magnetizable.mass = float.PositiveInfinity;

        // Player�^�O�t���I�u�W�F�N�g����Ɏ擾
        GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
            m_player = playerObj.transform;

        if (m_health != null)
            m_health.OnDie += Die;

        if (m_controller == null)
            Debug.LogWarning($"[EnemyBase] {name}: EntityController������܂���B�Փ˔���Ȃ��œ��삵�܂�", this);
    }

    void OnDestroy()
    {
        if (m_health != null)
            m_health.OnDie -= Die;
    }

    void Update()
    {
        UpdateEntity(Time.deltaTime);
    }

    /// <summary>�w������i���[���h��ԁj�ɉ�������BAI���v�Z�����ړ�������n���B</summary>
    public void AccelerateToward(Vector3 worldDirection, float dt)
    {
        if (worldDirection.sqrMagnitude > 0.01f)
        {
            // Accelerate()��lateralVelocity�i���[�J����ԁj�Ōv�Z���邽�ߕϊ����K�v
            Vector3 localDir = Quaternion.FromToRotation(transform.up, Vector3.up) * worldDirection;
            localDir = localDir.normalized;

            Accelerate(localDir, m_statusData.turningDrag,
                       m_statusData.acceleration, m_statusData.moveSpeed, dt);
            FaceDirection(worldDirection, m_statusData.rotationSpeed, dt);
        }
    }

    /// <summary>���ړ�����������B</summary>
    public void SlowDown(float dt)
    {
        Decelerate(m_statusData.deceleration, dt);
    }

    /// <summary>�w������������B</summary>
    public void FaceToward(Vector3 direction, float dt)
    {
        FaceDirection(direction, m_statusData.rotationSpeed, dt);
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}

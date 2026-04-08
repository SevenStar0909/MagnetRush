using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyBase))]
[RequireComponent(typeof(EnemyMeleeAttack))]
public class EnemyMeleeAI : MonoBehaviour
{
    private EnemyBase m_enemyBase;
    private EnemyMeleeAttack m_meleeAttack;
    private NavMeshAgent m_agent;
    private Transform m_player;
    private EnemySettings m_data;

    private WeaponStateController m_targetWeapon;

    private void Awake()
    {
        m_enemyBase = GetComponent<EnemyBase>();
        m_meleeAttack = GetComponent<EnemyMeleeAttack>();
    }

    private void Start()
    {
        m_agent = m_enemyBase.Agent;
        m_player = m_enemyBase.Player;
        m_data = m_enemyBase.StatusData;
    }

    private void Update()
    {
        // 磁力移動モード中は AI 処理をスキップ
        if (m_enemyBase.IsMagnetControlled)
        {
            return;
        }

        if (m_player == null || m_agent == null || m_data == null)
        {
            return;
        }

        // 武器が無い間は武器探索・取得を優先
        if (!m_enemyBase.HasWeapon)
        {
            HandleWeaponSearchAndPickup();
            return;
        }

        float distance = Vector3.Distance(transform.position, m_player.position);

        // プレイヤーが追跡範囲外の場合、移動を停止
        if (distance > m_data.chaseRange)
        {
            m_agent.isStopped = true;
            m_agent.ResetPath();
            return;
        }

        // プレイヤーが攻撃範囲内の場合、攻撃を試みる
        if (distance <= m_data.attackRange)
        {
            m_agent.isStopped = true;
            m_agent.ResetPath();

            if (!m_meleeAttack.IsAttacking) // 攻撃中でなければプレイヤーの方を向く
            {
                FacePlayer();
            }

            m_meleeAttack.TryAttack();
        }
        else
        {
            m_agent.isStopped = false;
            m_agent.SetDestination(m_player.position);
        }
    }

    private void HandleWeaponSearchAndPickup()
    {
        if (m_targetWeapon == null || !m_targetWeapon.CanBePickedUp)
        {
            if (WeaponManager.Instance == null)
            {
                m_agent.isStopped = true;
                m_agent.ResetPath();
                return;
            }

            m_targetWeapon = WeaponManager.Instance.FindNearestPickableWeapon(transform.position, m_data.chaseRange);
        }

        if (m_targetWeapon == null)
        {
            m_agent.isStopped = true;
            m_agent.ResetPath();
            return;
        }

        float distanceToWeapon = Vector3.Distance(transform.position, m_targetWeapon.transform.position);

        if (distanceToWeapon <= m_data.stopDistance + 0.1f)
        {
            m_agent.isStopped = true;
            m_agent.ResetPath();

            bool equipped = m_enemyBase.TryEquipWeapon(m_targetWeapon);
            if (equipped)
            {
                m_targetWeapon = null;
            }

            return;
        }

        m_agent.isStopped = false;
        m_agent.SetDestination(m_targetWeapon.transform.position);
    }

    private void FacePlayer()
    {
        Vector3 lookTarget = m_player.position - transform.position;
        lookTarget.y = 0f;

        if (lookTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(lookTarget);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * m_data.rotationSpeed
        );
    }
}

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

        if (m_player == null || m_agent == null || !m_agent.enabled || m_data == null)
        {
            return;
        }

        // NavMesh外ではAI処理をスキップ（磁力で飛ばされた直後など）
        if (!m_agent.isOnNavMesh)
        {
            return;
        }

        // 武器が無い場合は、まず武器取得を試みる。
        // 取れない場合は空手でプレイヤーを追跡・攻撃する。
        if (!m_enemyBase.HasWeapon)
        {
            bool isHandlingWeapon = HandleWeaponSearchAndPickup();
            if (isHandlingWeapon)
            {
                return;
            }
        }

        float distance = Vector3.Distance(transform.position, m_player.position);

        if (distance > m_data.chaseRange)
        {
            m_agent.isStopped = true;
            m_agent.ResetPath();
            return;
        }

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

    private bool HandleWeaponSearchAndPickup()
    {
        // 現在ターゲットが無効なら再探索
        if (m_targetWeapon == null || !m_targetWeapon.CanBePickedUp || m_targetWeapon.IsMagnetAffected)
        {
            if (WeaponManager.Instance == null)
            {
                m_targetWeapon = null;
                return false; // 武器探索不可 → 空手行動へフォールバック
            }

            m_targetWeapon = WeaponManager.Instance.FindNearestPickableWeapon(
                transform.position,
                m_data.chaseRange,
                true);
        }

        // 武器が見つからない → 空手行動へフォールバック
        if (m_targetWeapon == null)
        {
            return false;
        }

        float distanceToWeapon = Vector3.Distance(transform.position, m_targetWeapon.transform.position);

        if (distanceToWeapon <= m_data.stopDistance + 0.1f)
        {
            if (!m_targetWeapon.CanBePickedUp || m_targetWeapon.IsMagnetAffected)
            {
                m_targetWeapon = null;
                return false;
            }

            m_agent.isStopped = true;
            m_agent.ResetPath();

            bool equipped = m_enemyBase.TryEquipWeapon(m_targetWeapon);
            if (equipped)
            {
                m_targetWeapon = null;
            }

            return true;
        }

        m_agent.isStopped = false;
        m_agent.SetDestination(m_targetWeapon.transform.position);
        return true;
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

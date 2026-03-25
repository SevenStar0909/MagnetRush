using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyBase))]
[RequireComponent(typeof(EnemyMeleeAttack))]
public class EnemyMeleeAI : MonoBehaviour
{
    private EnemyBase enemyBase;
    private EnemyMeleeAttack meleeAttack;
    private NavMeshAgent agent;
    private Transform player;
    private EnemySettings data;

    private void Awake()
    {
        enemyBase = GetComponent<EnemyBase>();
        meleeAttack = GetComponent<EnemyMeleeAttack>();
    }

    private void Start()
    {
        agent = enemyBase.Agent;
        player = enemyBase.Player;
        data = enemyBase.StatusData;
    }

    private void Update()
    {
        if (player == null || agent == null || data == null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);

        // プレイヤーが追跡範囲外の場合、移動を停止
        if (distance > data.chaseRange)
        {
            agent.isStopped = true;
            agent.ResetPath();
            return;
        }

        // プレイヤーが攻撃範囲内の場合、攻撃を試みる
        if (distance <= data.attackRange)
        {
            agent.isStopped = true;
            agent.ResetPath();

            if (!meleeAttack.IsAttacking) // 攻撃中でなければプレイヤーの方を向く
            {
                FacePlayer();
            }
            meleeAttack.TryAttack();
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
    }

    private void FacePlayer()
    {
        Vector3 lookTarget = player.position - transform.position;
        lookTarget.y = 0f;

        if (lookTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }


        Quaternion targetRotation = Quaternion.LookRotation(lookTarget);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * data.rotationSpeed
        );
    }
}

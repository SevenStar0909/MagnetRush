using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敵の基底クラス。Entityを継承しHealth・IMagnetTargetを共有する。
/// 移動はNavMeshAgent経由、磁力はexternalVelocityで適用。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBase : Entity
{
    [Header("Data")]
    [SerializeField] private EnemySettings statusData;

    [Header("References")]
    [SerializeField] private Transform player;

    protected NavMeshAgent agent;

    public EnemySettings StatusData => statusData;
    public Transform Player => player;
    public NavMeshAgent Agent => agent;

    protected override void Awake()
    {
        base.Awake();
        agent = GetComponent<NavMeshAgent>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
            if (playerObj != null)
                player = playerObj.transform;
        }

        // Health.OnDie → Die()
        if (health != null)
            health.OnDie += Die;
    }

    void OnDestroy()
    {
        if (health != null)
            health.OnDie -= Die;
    }

    protected virtual void Start()
    {
        if (agent != null && statusData != null)
        {
            agent.speed = statusData.moveSpeed;
            agent.stoppingDistance = statusData.stopDistance;
        }
    }

    void Update()
    {
        // NavMeshAgentが移動を管理するのでApplyMovement/ApplyGravityは呼ばない
        // 磁力等の外部力だけ適用
        if (externalVelocity.sqrMagnitude > 0.01f && agent != null)
        {
            agent.Move(externalVelocity * Time.deltaTime);
            externalVelocity = Vector3.zero;
        }
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}

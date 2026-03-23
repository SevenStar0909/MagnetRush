using UnityEngine;
using UnityEngine.AI;

public class EnemyBase : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] protected EnemySettings statusData;

    [Header("References")]
    [SerializeField] protected Transform player;

    protected NavMeshAgent agent;
    protected int currentHp;

    public EnemySettings StatusData => statusData;
    public Transform Player => player;
    public NavMeshAgent Agent => agent;
    public int CurrentHp => currentHp;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }
    protected virtual void Start()
    {
        currentHp = statusData.maxHp;

        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }
        else
        {
            Debug.LogError("Player not found. Check Tag or spawn timing.");
        }

        if (agent != null && statusData != null)
        {
            agent.speed = statusData.moveSpeed;
            agent.stoppingDistance = statusData.stopDistance;
        }
    }

    /// <summary>
    /// “G‚ªƒ_ƒ[ƒW‚ğó‚¯‚éˆ—
    /// </summary>
    /// <param name="damage"></param>
    public virtual void TakeDamage(int damage)
    {
        currentHp -= damage;

        if (currentHp <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }

    // Update is called once per frame
    //void Update()
    //{
    //    
    //}
}

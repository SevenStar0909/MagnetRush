using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyBase))]
public class EnemyMeleeAttack : MonoBehaviour
{
    [Header("Attack Hitbox")]
    [SerializeField] private Collider attackHitbox;

    private EnemyBase enemyBase;
    private EnemySettings data;

    private float attackTimer;
    private bool isAttacking;

    private void Awake()
    {
        enemyBase = GetComponent<EnemyBase>();

        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
        }
    }

    private void Start()
    {
        data = enemyBase.StatusData;
        attackTimer = data.attackInterval;
    }

    private void Update()
    {
        attackTimer += Time.deltaTime;
    }

    public void TryAttack()
    {
        if (data == null) return;
        if (isAttacking) return;
        if (attackTimer < data.attackInterval) return;

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        attackTimer = 0f;

        if (attackHitbox != null)
        {
            attackHitbox.enabled = true;
        }

        yield return new WaitForSeconds(data.attackHitboxDuration);

        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
        }

        isAttacking = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (attackHitbox == null) return;
        if (!attackHitbox.enabled) return;

        if (other.CompareTag("Player"))
        {
            Debug.Log($"{gameObject.name} attacked Player. Damage = {data.attackDamage}");

            // example:
            // other.GetComponent<PlayerHealth>()?.TakeDamage(data.attackDamage);
        }
    }
}

using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem; // for testing

[RequireComponent(typeof(EnemyBase))]
public class EnemyMeleeAttack : MonoBehaviour
{
    [Header("Attack Hitbox")]
    [SerializeField] private CapsuleCollider attackHitbox;
    [SerializeField] private MeshRenderer attackHitboxMeshRenderer;

    private EnemyBase enemyBase;
    private EnemySettings data;

    //private CapsuleCollider attackCapsule; // for dynamic hitbox adjustment

    private float attackTimer;
    private bool isAttacking;
    public bool IsAttacking => isAttacking;

    private void Awake()
    {
        enemyBase = GetComponent<EnemyBase>();

        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
        }

        if (attackHitboxMeshRenderer != null)
        {
            attackHitboxMeshRenderer.enabled = false;
        }
    }

    private void Start()
    {
        data = enemyBase.StatusData;
        attackTimer = data.attackInterval;


        float atkR = data.attackRange;

        Debug.Log($"atkR = {atkR}");
        attackHitbox.height = atkR;
        attackHitbox.center = new Vector3(0f, atkR / 2f, 0f);
        attackHitboxMeshRenderer.transform.localPosition = new Vector3(0f, 0f, atkR / 2f);
        attackHitboxMeshRenderer.transform.localScale = new Vector3(1f, atkR / 2f, 1f);

    }

    private void Update()
    {
        attackTimer += Time.deltaTime;
        TestAtk();
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
            attackHitboxMeshRenderer.enabled = true;
        }

        yield return new WaitForSeconds(data.attackHitboxDuration);

        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
            attackHitboxMeshRenderer.enabled = false;
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

    private void TestAtk()
    {
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
        {
            TryAttack();
        }
    }
}

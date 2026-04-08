using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(EnemyBase))]
public class EnemyMeleeAttack : MonoBehaviour
{
    [Header("Attack Hitbox")]
    [FormerlySerializedAs("attackHitbox")]
    [SerializeField] private CapsuleCollider m_attackHitbox;
    [FormerlySerializedAs("attackHitboxMeshRenderer")]
    [SerializeField] private MeshRenderer m_attackHitboxMeshRenderer;

    private EnemyBase m_enemyBase;
    private EnemySettings m_data;

    private float m_attackTimer;
    private bool m_isAttacking;
    public bool IsAttacking => m_isAttacking;

    private void Awake()
    {
        m_enemyBase = GetComponent<EnemyBase>();

        if (m_attackHitbox != null)
        {
            m_attackHitbox.enabled = false;
        }

        if (m_attackHitboxMeshRenderer != null)
        {
            m_attackHitboxMeshRenderer.enabled = false;
        }
    }

    private void Start()
    {
        m_data = m_enemyBase.StatusData;
        m_attackTimer = m_data.attackInterval;

        if (m_attackHitbox != null)
        {
            float atkR = m_data.attackRange;
            m_attackHitbox.height = atkR;
            m_attackHitbox.center = new Vector3(0f, atkR / 2f, 0f);
        }

        if (m_attackHitboxMeshRenderer != null)
        {
            float atkR = m_data.attackRange;
            m_attackHitboxMeshRenderer.transform.localPosition = new Vector3(0f, 0f, atkR / 2f);
            m_attackHitboxMeshRenderer.transform.localScale = new Vector3(1f, atkR / 2f, 1f);
        }
    }

    private void Update()
    {
        m_attackTimer += Time.deltaTime;
    }

    public void TryAttack()
    {
        if (m_data == null) return;
        if (!m_enemyBase.HasWeapon) return;
        if (m_isAttacking) return;
        if (m_attackTimer < m_data.attackInterval) return;

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        m_isAttacking = true;
        m_attackTimer = 0f;

        WeaponStateController weapon = m_enemyBase.EquippedWeapon;
        if (weapon != null)
        {
            weapon.BeginAttackWindow();
        }

        if (m_attackHitbox != null)
        {
            m_attackHitbox.enabled = true;
        }

        if (m_attackHitboxMeshRenderer != null)
        {
            m_attackHitboxMeshRenderer.enabled = true;
        }

        yield return new WaitForSeconds(m_data.attackHitboxDuration);

        if (m_attackHitbox != null)
        {
            m_attackHitbox.enabled = false;
        }

        if (m_attackHitboxMeshRenderer != null)
        {
            m_attackHitboxMeshRenderer.enabled = false;
        }

        if (weapon != null)
        {
            weapon.EndAttackWindow();
        }

        m_isAttacking = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_attackHitbox == null) return;
        if (!m_attackHitbox.enabled) return;
        if (!m_enemyBase.HasWeapon) return;

        if (other.CompareTag(GameTags.Player))
        {
            var health = other.GetComponent<Health>();
            if (health != null) health.Damage(m_data.attackDamage);
        }
    }
}

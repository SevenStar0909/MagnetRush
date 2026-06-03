using UnityEngine;

/// <summary>
/// 空中敵のAI。EnemyAirBase を前提に、プレイヤー追跡と攻撃判定を実装する。
/// 追跡は3D移動、回転はY軸のみ。攻撃判定はAttackBox Colliderで行う。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyAirBase))]
public class EnemyAirAi : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Collider m_attackBox;

    private EnemyAirBase m_enemyBase;
    private Magnetizable m_magnetizable;
    private bool m_hasHit;

    private void Awake()
    {
        if (!TryGetComponent(out m_enemyBase))
        {
            Debug.LogError($"[EnemyAirAi] {name}: EnemyAirBase が見つかりません。", this);
            enabled = false;
            return;
        }

        m_magnetizable = GetComponent<Magnetizable>();

        if (m_attackBox == null)
            m_attackBox = FindAttackBoxCollider();

        if (m_attackBox != null)
        {
            m_attackBox.isTrigger = true;
        }
        else
        {
            Debug.LogWarning($"[EnemyAirAi] {name}: attackbox 用 Collider が見つかりません。", this);
        }
    }

    private void OnEnable()
    {
        if (m_enemyBase != null)
            m_enemyBase.EnvironmentContact += HandleEnvironmentContact;

        if (m_magnetizable != null)
            m_magnetizable.OnMagnetContact += HandleMagnetContact;
    }

    private void OnDisable()
    {
        if (m_enemyBase != null)
            m_enemyBase.EnvironmentContact -= HandleEnvironmentContact;

        if (m_magnetizable != null)
            m_magnetizable.OnMagnetContact -= HandleMagnetContact;
    }

    private void Update()
    {
        if (m_enemyBase == null)
            return;

        if (m_enemyBase.IsMagnetControlled)
            return;

        if (m_enemyBase.Player == null)
            return;

        Vector3 toPlayer = m_enemyBase.Player.position - transform.position;
        if (toPlayer.sqrMagnitude <= 0.0001f)
            return;

        EnemyAirSettings data = m_enemyBase.StatusData;
        if (data != null)
        {
            if (data.chaseRange > 0f)
            {
                float chaseRangeSqr = data.chaseRange * data.chaseRange;
                if (toPlayer.sqrMagnitude > chaseRangeSqr)
                {
                    m_enemyBase.SlowDown(Time.deltaTime);
                    return;
                }
            }

            if (data.stopDistance > 0f)
            {
                float stopDistanceSqr = data.stopDistance * data.stopDistance;
                if (toPlayer.sqrMagnitude <= stopDistanceSqr)
                {
                    m_enemyBase.FaceTowardYaw(toPlayer, Time.deltaTime);
                    m_enemyBase.SlowDown(Time.deltaTime);
                    return;
                }
            }
        }

        m_enemyBase.AccelerateToward(toPlayer, Time.deltaTime);
    }

    private Collider FindAttackBoxCollider()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        Collider fallback = null;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null)
                continue;

            if (col.transform == transform)
                continue;

            if (fallback == null)
                fallback = col;

            string lowerName = col.name.ToLowerInvariant();
            if (lowerName.Contains("attack"))
                return col;

            if (col.isTrigger)
                return col;
        }

        return fallback;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_hasHit)
            return;

        if (m_attackBox == null || !m_attackBox.enabled)
            return;

        if (other == null)
            return;

        if (other.transform.root == transform.root)
            return;

        var hittable = other.GetComponentInParent<IHittable>();
        if (hittable == null)
            return;

        m_hasHit = true;

        hittable.OnHit(new HitData
        {
            damage = m_enemyBase != null ? m_enemyBase.ImpactDamage : 1,
            hitPoint = other.ClosestPoint(transform.position),
            knockbackDir = (other.transform.position - transform.position).normalized,
            source = gameObject
        });

        DestroySelf();
    }

    private void HandleEnvironmentContact(Collider other)
    {
        if (m_hasHit)
            return;

        m_hasHit = true;
        DestroySelf();
    }

    private void HandleMagnetContact(Magnetizable other)
    {
        if (m_hasHit)
            return;

        if (other == null || other.transform.root == transform.root)
            return;

        var hittable = other.GetComponentInParent<IHittable>();
        if (hittable != null)
        {
            hittable.OnHit(new HitData
            {
                damage = m_enemyBase != null ? m_enemyBase.ImpactDamage : 1,
                hitPoint = other.Position,
                knockbackDir = (other.Position - transform.position).normalized,
                source = gameObject
            });
        }

        m_hasHit = true;
        DestroySelf();
    }

    private void DestroySelf()
    {
        if (m_enemyBase != null)
        {
            m_enemyBase.DestroyWithDisappearEffect();
            return;
        }

        Destroy(gameObject);
    }
}

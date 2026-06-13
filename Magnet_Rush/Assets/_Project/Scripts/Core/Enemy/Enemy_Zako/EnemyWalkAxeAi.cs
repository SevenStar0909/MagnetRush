using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyWalkBase))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyWalkAxeAi : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private CapsuleCollider m_attackBox;
    [SerializeField] private MeshRenderer m_attackBoxMeshRenderer;
    [SerializeField] private EnemyWalkAxeAnimator m_animator;

    [Header("Weapon")]
    [SerializeField] private EnemyWeaponHolder m_weaponHolder;

    [Tooltip("落ちた武器をこの距離まで近づいたら拾う")]
    [SerializeField] private float m_weaponPickupRange = 1.5f;

    private EnemyWalkBase m_enemyBase;
    private NavMeshAgent m_agent;
    private EnemySettings m_data;
    private Vector3 m_lastDirection;
    private float m_attackTimer;
    private bool m_isAttacking;

    private readonly HashSet<Health> m_hitTargets = new();
    private readonly Collider[] m_overlapResults = new Collider[16];

    private void Awake()
    {
        m_enemyBase = GetComponent<EnemyWalkBase>();
        m_agent = GetComponent<NavMeshAgent>();

        if (m_animator == null)
            m_animator = GetComponent<EnemyWalkAxeAnimator>();

        if (m_animator == null)
            m_animator = gameObject.AddComponent<EnemyWalkAxeAnimator>();

        if (m_weaponHolder == null)
            m_weaponHolder = GetComponent<EnemyWeaponHolder>();

        if (m_agent != null)
        {
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
        }

        if (m_attackBox == null)
            m_attackBox = FindAttackBox();

        if (m_attackBox != null)
        {
            m_attackBox.gameObject.layer = PhysicsLayers.MeleeHitbox;
            m_attackBox.isTrigger = true;
            m_attackBox.enabled = false;
        }

        if (m_attackBoxMeshRenderer == null && m_attackBox != null)
            m_attackBoxMeshRenderer = m_attackBox.GetComponent<MeshRenderer>();

        if (m_attackBoxMeshRenderer != null)
            m_attackBoxMeshRenderer.enabled = false;
    }

    private void Start()
    {
        m_data = m_enemyBase.StatusData;
        m_attackTimer = m_data != null ? m_data.attackInterval : 0f;

        if (m_agent != null && m_data != null)
        {
            m_agent.speed = m_data.moveSpeed;
            m_agent.acceleration = m_data.acceleration;
            m_agent.stoppingDistance = m_data.stopDistance;
        }
    }

    private void Update()
    {
        if (m_enemyBase == null || m_data == null)
            return;

        m_attackTimer += Time.deltaTime;

        if (m_enemyBase.IsMagnetControlled)
        {
            SetMoving(false);
            SetAttackBoxActive(false);
            return;
        }

        // 磁力で武器を剝がされていたら、落ちた武器を拾いに行く（プレイヤー追跡より優先）。
        if (m_weaponHolder != null && !m_weaponHolder.IsArmed)
        {
            UpdateWeaponPickup(Time.deltaTime);
            return;
        }

        Transform player = m_enemyBase.Player;
        if (player == null)
        {
            SetMoving(false);
            m_enemyBase.SlowDown(Time.deltaTime);
            return;
        }

        float dt = Time.deltaTime;
        TryRecoverAgent();

        if (!m_agent.enabled || !m_agent.isOnNavMesh)
        {
            TickDirectMove(player, dt);
            return;
        }

        m_agent.nextPosition = transform.position;
        m_agent.velocity = Vector3.zero;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > m_data.chaseRange)
        {
            SetMoving(false);
            m_agent.ResetPath();
            m_enemyBase.SlowDown(dt);
            return;
        }

        if (distance <= m_data.attackRange)
        {
            SetMoving(false);
            m_agent.ResetPath();
            m_enemyBase.SlowDown(dt);
            FacePlayer(player, dt);
            TryAttack();
            return;
        }

        m_agent.SetDestination(player.position);
        SetMoving(true);
        m_enemyBase.AccelerateToward(GetNavMeshDirection(player), dt);
    }

    // 丸腰のとき、落ちた自分の武器まで移動し、拾える状態になったら手元へ戻して再装備する。
    private void UpdateWeaponPickup(float dt)
    {
        m_isAttacking = false;
        SetAttackBoxActive(false);

        Vector3 weaponPos = m_weaponHolder.DroppedWeaponPosition;
        TryRecoverAgent();

        bool agentReady = m_agent.enabled && m_agent.isOnNavMesh;
        if (agentReady)
        {
            m_agent.nextPosition = transform.position;
            m_agent.velocity = Vector3.zero;
        }

        float distance = Vector3.Distance(transform.position, weaponPos);
        if (distance <= m_weaponPickupRange)
        {
            SetMoving(false);
            if (agentReady)
                m_agent.ResetPath();
            m_enemyBase.SlowDown(dt);
            m_enemyBase.FaceToward(weaponPos - transform.position, dt);

            // 磁力が切れて拾える状態になったら手元へ戻す。磁化中は CanReEquip が false なので待つ。
            if (m_weaponHolder.CanReEquip)
                m_weaponHolder.ReEquip();
            return;
        }

        Vector3 direction;
        if (agentReady)
        {
            m_agent.SetDestination(weaponPos);
            direction = m_agent.steeringTarget - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = weaponPos - transform.position;
        }
        else
        {
            direction = weaponPos - transform.position;
        }

        SetMoving(true);
        m_enemyBase.AccelerateToward(direction, dt);
    }

    private void TickDirectMove(Transform player, float dt)
    {
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > m_data.chaseRange)
        {
            SetMoving(false);
            m_enemyBase.SlowDown(dt);
            return;
        }

        if (distance <= m_data.attackRange)
        {
            SetMoving(false);
            m_enemyBase.SlowDown(dt);
            FacePlayer(player, dt);
            TryAttack();
            return;
        }

        SetMoving(true);
        m_enemyBase.AccelerateToward(GetDirectionToPlayer(player), dt);
    }

    private void TryAttack()
    {
        if (m_attackBox == null)
            return;

        // 磁力で武器を剝がされたら丸腰なので近接攻撃しない。
        if (m_weaponHolder != null && !m_weaponHolder.IsArmed)
            return;

        if (m_isAttacking)
            return;

        if (m_attackTimer < m_data.attackInterval)
            return;

        BeginAttack();
    }

    private void BeginAttack()
    {
        m_isAttacking = true;
        m_attackTimer = 0f;
        m_hitTargets.Clear();
        m_animator?.TriggerAttack();
        SetAttackBoxActive(false);
    }

    public void OnAttackHitStartEvent()
    {
        if (!m_isAttacking)
            return;

        SetAttackBoxActive(true);
        CheckAttackBoxOverlapAndDamage();
    }

    public void OnAttackHitEndEvent()
    {
        SetAttackBoxActive(false);
    }

    public void OnAttackFinishedEvent()
    {
        SetAttackBoxActive(false);
        m_isAttacking = false;
    }

    private void SetMoving(bool isMoving)
    {
        m_animator?.SetMoving(isMoving && !m_isAttacking);
    }

    private void OnDisable()
    {
        SetAttackBoxActive(false);
        m_isAttacking = false;
        SetMoving(false);
    }

    private void SetAttackBoxActive(bool active)
    {
        if (m_attackBox != null)
            m_attackBox.enabled = active;

        if (m_attackBoxMeshRenderer != null)
            m_attackBoxMeshRenderer.enabled = active;
    }

    private void CheckAttackBoxOverlapAndDamage()
    {
        // 攻撃判定を有効にした時点で、すでにPlayerが範囲内にいる場合、
        // OnTriggerEnterだけでは検知できないことがあるため、手動で重なりを確認する
        if (m_attackBox == null || !m_attackBox.enabled)
            return;

        // CapsuleColliderの現在の範囲をOverlapCapsule用のワールド座標に変換する
        GetCapsuleWorldPoints(m_attackBox, out Vector3 p0, out Vector3 p1, out float radius);

        // 攻撃範囲内にいるPlayerを直接検索する
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            p0,
            p1,
            radius,
            m_overlapResults,
            1 << PhysicsLayers.Player,
            QueryTriggerInteraction.Collide
        );

        // 範囲内にいたPlayerへダメージ処理を行う
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = m_overlapResults[i];
            if (col == null)
                continue;

            TryApplyDamage(col);
        }
    }

    private static void GetCapsuleWorldPoints(CapsuleCollider capsule, out Vector3 p0, out Vector3 p1, out float radius)
    {
        Transform t = capsule.transform;

        // OverlapCapsuleはワールド座標の2点と半径が必要なため、
        // CapsuleColliderの現在の位置・向き・スケールからそれらを計算する
        Vector3 center = t.TransformPoint(capsule.center);
        Vector3 scale = t.lossyScale;
        float sx = Mathf.Abs(scale.x);
        float sy = Mathf.Abs(scale.y);
        float sz = Mathf.Abs(scale.z);

        Vector3 axis;
        float axisScale;
        float radiusScale;

        switch (capsule.direction)
        {
            case 0:
                axis = t.right;
                axisScale = sx;
                radiusScale = Mathf.Max(sy, sz);
                break;

            case 2:
                axis = t.forward;
                axisScale = sz;
                radiusScale = Mathf.Max(sx, sy);
                break;

            default:
                axis = t.up;
                axisScale = sy;
                radiusScale = Mathf.Max(sx, sz);
                break;
        }

        radius = capsule.radius * radiusScale;
        float height = Mathf.Max(capsule.height * axisScale, radius * 2f);
        float halfLine = Mathf.Max(0f, height * 0.5f - radius);

        p0 = center + axis * halfLine;
        p1 = center - axis * halfLine;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_attackBox == null || !m_attackBox.enabled)
            return;

        TryApplyDamage(other);
    }

    private void TryApplyDamage(Collider other)
    {
        if (other == null)
            return;

        IHittable hittable = other.GetComponentInParent<IHittable>();
        if (hittable == null)
            return;

        Health health = other.GetComponentInParent<Health>();
        if (health != null && !m_hitTargets.Add(health))
            return;

        hittable.OnHit(new HitData
        {
            damage = m_data.attackDamage,
            hitPoint = other.ClosestPoint(transform.position),
            knockbackDir = (other.transform.position - transform.position).normalized,
            source = gameObject
        });
    }

    private Vector3 GetNavMeshDirection(Transform player)
    {
        if (m_agent == null || !m_agent.hasPath && !m_agent.pathPending)
            return GetDirectionToPlayer(player);

        if (m_agent.pathPending)
            return m_lastDirection;

        Vector3 direction = m_agent.steeringTarget - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            m_lastDirection = direction.normalized;
            return m_lastDirection;
        }

        return m_lastDirection;
    }

    private Vector3 GetDirectionToPlayer(Transform player)
    {
        if (player == null)
            return Vector3.zero;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private void FacePlayer(Transform player, float dt)
    {
        if (m_isAttacking)
            return;

        m_enemyBase.FaceToward(GetDirectionToPlayer(player), dt);
    }

    private CapsuleCollider FindAttackBox()
    {
        CapsuleCollider[] colliders = GetComponentsInChildren<CapsuleCollider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            CapsuleCollider col = colliders[i];
            if (col != null && col.name.Contains("AttackBox"))
                return col;
        }

        return null;
    }

    private void TryRecoverAgent()
    {
        if (m_agent == null)
            return;

        if (m_agent.enabled && m_agent.isOnNavMesh)
            return;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            return;

        if (m_agent.enabled)
            m_agent.enabled = false;

        transform.position = hit.position;
        m_agent.enabled = true;

        if (m_agent.isOnNavMesh)
        {
            m_agent.updatePosition = false;
            m_agent.updateRotation = false;
            m_agent.velocity = Vector3.zero;
        }
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyWalkBase))]
public class EnemyWalkAxeAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Driven Animator. If unset, the first child Animator is used.")]
    [SerializeField] private Animator m_animator;

    [Header("Animator Parameter Names")]
    [SerializeField] private string m_isMovingParameterName = "IsMoving";
    [SerializeField] private string m_attackTriggerName = "Attack";

    private int m_isMovingParameterHash;
    private int m_attackTriggerHash;
    private bool m_isMoving;

    private void Awake()
    {
        if (m_animator == null)
            m_animator = GetComponentInChildren<Animator>(true);

        m_isMovingParameterHash = Animator.StringToHash(m_isMovingParameterName);
        m_attackTriggerHash = Animator.StringToHash(m_attackTriggerName);

        if (m_animator == null)
        {
            ChannelLogger.LogGuardReturn("Enemy", $"[{nameof(EnemyWalkAxeAnimator)}] {name}: Animator was not found.");
            enabled = false;
            return;
        }

        EnemyWalkAxeAnimationEventRelay relay =
            m_animator.GetComponent<EnemyWalkAxeAnimationEventRelay>();
        if (relay == null)
            relay = m_animator.gameObject.AddComponent<EnemyWalkAxeAnimationEventRelay>();

        relay.Initialize(GetComponent<EnemyWalkAxeAi>());
        m_animator.SetBool(m_isMovingParameterHash, false);
    }

    public void SetMoving(bool isMoving)
    {
        if (m_animator == null || m_isMoving == isMoving)
            return;

        m_isMoving = isMoving;
        m_animator.SetBool(m_isMovingParameterHash, isMoving);
    }

    public void TriggerAttack()
    {
        if (m_animator == null)
            return;

        SetMoving(false);
        m_animator.SetTrigger(m_attackTriggerHash);
    }

    public bool IsMoving => m_isMoving;
}

[DisallowMultipleComponent]
public class EnemyWalkAxeAnimationEventRelay : MonoBehaviour
{
    private EnemyWalkAxeAi m_target;

    public void Initialize(EnemyWalkAxeAi target)
    {
        m_target = target;
    }

    private void Awake()
    {
        if (m_target == null)
            m_target = GetComponentInParent<EnemyWalkAxeAi>();
    }

    public void OnAttackHitStartEvent()
    {
        m_target?.OnAttackHitStartEvent();
    }

    public void OnAttackHitEndEvent()
    {
        m_target?.OnAttackHitEndEvent();
    }

    public void OnAttackFinishedEvent()
    {
        m_target?.OnAttackFinishedEvent();
    }
}

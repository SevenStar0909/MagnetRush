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

    [Header("Ally Avoidance")]
    [Tooltip("AxeEnemy 同士がこの距離以内に近づいたら離れる")]
    [SerializeField] private float m_allyAvoidanceRadius = 1.8f;

    [Tooltip("AxeEnemy 同士の重なり回避の強さ")]
    [SerializeField] private float m_allyAvoidanceWeight = 1.5f;

    [Header("Audio")]
    [SerializeField, Range(0f, 1f)] private float m_enemyWheelVolume = 1f;

    private EnemyWalkBase m_enemyBase;
    private NavMeshAgent m_agent;
    private EnemySettings m_data;
    private Collider m_activeAttackCollider;
    private Vector3 m_lastDirection;
    private float m_attackTimer;
    private bool m_isAttacking;
    private bool m_wasMoving;
    private bool m_enemyWheelPlaying;
    private Health m_ownHealth;
    private SoundManager.Playback m_enemyWheelPlayback;

    private readonly HashSet<Health> m_hitTargets = new();
    private readonly Collider[] m_overlapResults = new Collider[32];
    private readonly Collider[] m_allyAvoidanceResults = new Collider[12];

    private void Awake()
    {
        m_enemyBase = GetComponent<EnemyWalkBase>();
        m_agent = GetComponent<NavMeshAgent>();
        m_ownHealth = GetComponent<Health>();

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
            if (!TryMoveAwayFromOverlappingAlly(dt))
                m_enemyBase.SlowDown(dt);
            FacePlayer(player, dt);
            TryAttack();
            return;
        }

        m_agent.SetDestination(player.position);
        SetMoving(true);
        m_enemyBase.AccelerateToward(AddAllyAvoidance(GetNavMeshDirection(player)), dt);


    }

    // 丸腰のとき、落ちた自分の武器まで移動し、拾える状態になったら手元へ戻して再装備する。
    private void UpdateWeaponPickup(float dt)
    {
        m_isAttacking = false;
        SetAttackBoxActive(false);

        if (m_weaponHolder.IsReEquipping)
        {
            SetMoving(false);
            TryRecoverAgent();
            if (m_agent != null && m_agent.enabled && m_agent.isOnNavMesh)
            {
                m_agent.nextPosition = transform.position;
                m_agent.velocity = Vector3.zero;
                m_agent.ResetPath();
            }

            m_enemyBase.SlowDown(dt);
            FaceWeapon(m_weaponHolder.DroppedWeaponPosition, dt);
            return;
        }

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
            FaceWeapon(weaponPos, dt);

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
        m_enemyBase.AccelerateToward(AddAllyAvoidance(direction), dt);
    }

    private void FaceWeapon(Vector3 weaponPosition, float dt)
    {
        Vector3 direction = weaponPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        m_enemyBase.FaceToward(direction, dt);
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
            if (!TryMoveAwayFromOverlappingAlly(dt))
                m_enemyBase.SlowDown(dt);
            FacePlayer(player, dt);
            TryAttack();
            return;
        }

        SetMoving(true);
        m_enemyBase.AccelerateToward(AddAllyAvoidance(GetDirectionToPlayer(player)), dt);
    }

    private Vector3 AddAllyAvoidance(Vector3 desiredDirection)
    {
        Vector3 avoidance = GetAllyAvoidanceDirection();
        if (avoidance.sqrMagnitude <= 0.0001f)
            return desiredDirection;

        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude <= 0.0001f)
            return avoidance;

        return (desiredDirection.normalized + avoidance * m_allyAvoidanceWeight).normalized;
    }

    private bool TryMoveAwayFromOverlappingAlly(float dt)
    {
        Vector3 avoidance = GetAllyAvoidanceDirection();
        if (avoidance.sqrMagnitude <= 0.0001f)
            return false;

        m_enemyBase.AccelerateToward(avoidance, dt);
        return true;
    }

    private Vector3 GetAllyAvoidanceDirection()
    {
        if (m_allyAvoidanceRadius <= 0f)
            return Vector3.zero;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            m_allyAvoidanceRadius,
            m_allyAvoidanceResults,
            PhysicsLayers.Bit(PhysicsLayers.EntityBody),
            QueryTriggerInteraction.Ignore
        );

        Vector3 avoidance = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider col = m_allyAvoidanceResults[i];
            m_allyAvoidanceResults[i] = null;
            if (col == null)
                continue;

            if (col.transform == transform || col.transform.IsChildOf(transform))
                continue;

            EnemyWalkAxeAi other = col.GetComponentInParent<EnemyWalkAxeAi>();
            if (other == null || other == this)
                continue;

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude <= 0.0001f)
                away = transform.right;

            float distance = Mathf.Max(away.magnitude, 0.001f);
            float strength = Mathf.Clamp01((m_allyAvoidanceRadius - distance) / m_allyAvoidanceRadius);
            avoidance += away.normalized * strength;
        }

        return avoidance.sqrMagnitude > 0.0001f ? avoidance.normalized : Vector3.zero;
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
        bool shouldMove = isMoving && !m_isAttacking;
        m_animator?.SetMoving(shouldMove);

        bool isAnimatorMoving = m_animator != null && m_animator.IsMoving;
        if (isAnimatorMoving && !m_wasMoving)
            PlayEnemyWheel();
        else if (!isAnimatorMoving && m_wasMoving)
            StopEnemyWheel();

        m_wasMoving = isAnimatorMoving;
    }

    private void PlayEnemyWheel()
    {
        SoundManager soundManager = SoundManager.Instance;
        if (soundManager == null)
            return;

        StopEnemyWheel();
        m_enemyWheelPlayback = soundManager.PlayWithHandle(SoundData.CueSheet.SE, SoundData.SE.EnemyWheel);
        m_enemyWheelPlayback.SetVolumeAndPitch(m_enemyWheelVolume, 0f);
        m_enemyWheelPlaying = true;
    }

    private void StopEnemyWheel()
    {
        if (!m_enemyWheelPlaying)
            return;

        m_enemyWheelPlayback.Stop();
        m_enemyWheelPlaying = false;
    }

    private void OnDisable()
    {
        SetAttackBoxActive(false);
        m_isAttacking = false;
        SetMoving(false);
        StopEnemyWheel();
    }

    private void LateUpdate()
    {
        if (m_activeAttackCollider != null && m_activeAttackCollider.enabled)
            CheckAttackBoxOverlapAndDamage();
    }

    private void SetAttackBoxActive(bool active)
    {
        Collider weaponAttackCollider = GetWeaponAttackCollider();
        bool useWeaponAttackCollider = active && weaponAttackCollider != null;

        if (weaponAttackCollider != null)
        {
            weaponAttackCollider.gameObject.layer = PhysicsLayers.MeleeHitbox;
            weaponAttackCollider.isTrigger = true;

            if (useWeaponAttackCollider)
                m_weaponHolder.BeginAttackWindow();
            else
                m_weaponHolder.EndAttackWindow();
        }

        if (m_attackBox != null)
            m_attackBox.enabled = active && !useWeaponAttackCollider;

        if (m_attackBoxMeshRenderer != null)
            m_attackBoxMeshRenderer.enabled = active && !useWeaponAttackCollider;

        m_activeAttackCollider = active
            ? (useWeaponAttackCollider ? weaponAttackCollider : m_attackBox)
            : null;
    }

    private void CheckAttackBoxOverlapAndDamage()
    {
        // 武器側Rigidbody配下のTriggerは本体へOnTriggerEnterが届かないことがあるため、
        // 攻撃ウィンドウ中は手動Overlapでも当たりを拾う。
        Collider attackCollider = m_activeAttackCollider != null ? m_activeAttackCollider : GetCurrentAttackCollider();
        if (attackCollider == null || !attackCollider.enabled)
            return;

        int hitCount = OverlapAttackCollider(attackCollider);

        // 範囲内にいた対象へダメージ処理を行う
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = m_overlapResults[i];
            m_overlapResults[i] = null;
            if (col == null)
                continue;

            TryApplyDamage(col);
        }
    }

    private Collider GetCurrentAttackCollider()
    {
        Collider weaponAttackCollider = GetWeaponAttackCollider();
        return weaponAttackCollider != null ? weaponAttackCollider : m_attackBox;
    }

    private Collider GetWeaponAttackCollider()
    {
        if (m_weaponHolder == null || !m_weaponHolder.IsArmed)
            return null;

        return m_weaponHolder.AttackTrigger;
    }

    private int OverlapAttackCollider(Collider attackCollider)
    {
        int targetMask = PhysicsLayers.Bit(PhysicsLayers.Player) | PhysicsLayers.Bit(PhysicsLayers.Enemy);

        if (attackCollider is CapsuleCollider capsule)
        {
            GetCapsuleWorldPoints(capsule, out Vector3 p0, out Vector3 p1, out float radius);
            return Physics.OverlapCapsuleNonAlloc(
                p0,
                p1,
                radius,
                m_overlapResults,
                targetMask,
                QueryTriggerInteraction.Collide
            );
        }

        if (attackCollider is BoxCollider box)
        {
            GetBoxWorldShape(box, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);
            return Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                m_overlapResults,
                rotation,
                targetMask,
                QueryTriggerInteraction.Collide
            );
        }

        if (attackCollider is SphereCollider sphere)
        {
            GetSphereWorldShape(sphere, out Vector3 center, out float radius);
            return Physics.OverlapSphereNonAlloc(
                center,
                radius,
                m_overlapResults,
                targetMask,
                QueryTriggerInteraction.Collide
            );
        }

        Bounds bounds = attackCollider.bounds;
        return Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            m_overlapResults,
            Quaternion.identity,
            targetMask,
            QueryTriggerInteraction.Collide
        );
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

    private static void GetBoxWorldShape(BoxCollider box, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
    {
        Transform t = box.transform;
        Vector3 scale = t.lossyScale;
        center = t.TransformPoint(box.center);
        halfExtents = new Vector3(
            box.size.x * Mathf.Abs(scale.x) * 0.5f,
            box.size.y * Mathf.Abs(scale.y) * 0.5f,
            box.size.z * Mathf.Abs(scale.z) * 0.5f
        );
        rotation = t.rotation;
    }

    private static void GetSphereWorldShape(SphereCollider sphere, out Vector3 center, out float radius)
    {
        Transform t = sphere.transform;
        Vector3 scale = t.lossyScale;
        center = t.TransformPoint(sphere.center);
        radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_activeAttackCollider != m_attackBox || m_attackBox == null || !m_attackBox.enabled)
            return;

        TryApplyDamage(other);
    }

    private void TryApplyDamage(Collider other)
    {
        if (other == null)
            return;

        if (other.transform == transform || other.transform.IsChildOf(transform))
            return;

        IHittable hittable = other.GetComponentInParent<IHittable>();
        if (hittable == null)
            return;

        Health health = other.GetComponentInParent<Health>();
        if (health != null && health == m_ownHealth)
            return;

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

        if (m_enemyBase == null || !m_enemyBase.IsGrounded)
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

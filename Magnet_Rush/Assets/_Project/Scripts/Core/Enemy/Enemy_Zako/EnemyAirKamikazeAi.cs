using UnityEngine;

/// <summary>
/// カミカゼ空中敵のAI。EnemyAirBase を前提に、プレイヤーへ突撃して接触時に自壊する。
/// 追跡は3D移動、回転はY軸のみ。攻撃判定はAttackBox Colliderで行う。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyAirBase))]
public class EnemyAirKamikazeAi : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Collider m_attackBox;
    [SerializeField] private EnemyAirKamikazeAnimator m_animator;

    [Header("Obstacle Avoidance")]
    [Tooltip("通常飛行時に避ける障害物レイヤー。未設定なら Default/Ground/Wall/PhysicsObject を使う")]
    [SerializeField] private LayerMask m_obstacleMask;

    [Tooltip("前方の障害物を確認する距離")]
    [SerializeField] private float m_obstacleLookAhead = 4f;

    [Tooltip("障害物確認に使う球の半径")]
    [SerializeField] private float m_obstacleProbeRadius = 0.7f;

    [Tooltip("障害物回避の曲がりやすさ")]
    [SerializeField] private float m_obstacleAvoidanceWeight = 2.5f;

    [Header("Audio")]
    [Tooltip("この距離まではフル音量で鳴る（メートル）。近づいてもこれ以上は大きくならない")]
    [SerializeField] private float m_droneEnemyMinDistance = 3f;

    [Tooltip("この距離で音が最小になる（メートル）。これより遠いとほぼ聞こえない")]
    [SerializeField] private float m_droneEnemyMaxDistance = 20f;

    private EnemyAirBase m_enemyBase;
    private Magnetizable m_magnetizable;
    private bool m_hasHit;
    private bool m_droneEnemyPlaying;
    private SoundManager.Playback3d m_droneEnemyPlayback;
    private readonly Collider[] m_overlapBuffer = new Collider[8];

    // 自分の所属グループ（敵=Enemy）。Awake で自身の Hitbox から取得し、OnTriggerEnter の同士討ち判定に使う。
    private HitGroup m_ownHitGroup = HitGroup.Enemy;

    // カミカゼの爆発は「誰にでも当たる中立ハザード」として扱う。
    // 当たり判定設計原則: Physics は Player でも Enemy でもないので両方にダメージが通る（同士討ちを弾かない）。
    private const HitGroup k_DetonationHitGroup = HitGroup.Physics;

    private void Awake()
    {
        if (!TryGetComponent(out m_enemyBase))
        {
            Debug.LogError($"[{nameof(EnemyAirKamikazeAi)}] {name}: EnemyAirBase が見つかりません。", this);
            enabled = false;
            return;
        }

        m_magnetizable = GetComponent<Magnetizable>();

        var ownHittable = GetComponentInChildren<IHittable>(true);
        if (ownHittable != null)
            m_ownHitGroup = ownHittable.HitGroup;

        if (m_animator == null)
            m_animator = GetComponentInChildren<EnemyAirKamikazeAnimator>(true);

        if (m_attackBox == null)
            m_attackBox = FindAttackBoxCollider();

        if (m_attackBox != null)
        {
            m_attackBox.isTrigger = true;
        }
        else
        {
            Debug.LogWarning($"[{nameof(EnemyAirKamikazeAi)}] {name}: attackbox 用 Collider が見つかりません。", this);
        }
    }

    private void OnEnable()
    {
        if (m_enemyBase != null)
        {
            m_enemyBase.EnvironmentContact += HandleEnvironmentContact;
            m_enemyBase.Respawned += HandleRespawned;
        }
    }

    private void OnDisable()
    {
        StopDroneEnemy();

        if (m_enemyBase != null)
        {
            m_enemyBase.EnvironmentContact -= HandleEnvironmentContact;
            m_enemyBase.Respawned -= HandleRespawned;
        }
    }

    private void Update()
    {
        if (m_enemyBase == null)
            return;

        if (m_enemyBase.IsDead)
        {
            StopDroneEnemy();
            return;
        }

        // AttackBox の Trigger は HitGroup で敵同士を弾くため、実体同士の接触は別経路で拾う。
        // QueryTriggerInteraction.Ignore の OverlapEntity なので、磁界や HurtBox の大きい Trigger では誤爆しない。
        if (!m_hasHit && CheckBodyContact())
            return;

        // 磁化中は「引き寄せられて他の磁化オブジェクトに実接触したら自爆」を最優先で判定する。
        // 磁力移動中こそ衝突が起きるため、IsMagnetControlled の早期returnより前に置く。
        if (!m_hasHit && m_magnetizable != null && m_magnetizable.IsActive && CheckMagnetizedContact())
            return;

        if (m_enemyBase.IsMagnetControlled)
        {
            StopDroneEnemy();
            return;
        }

        if (m_enemyBase.Player == null)
        {
            StopDroneEnemy();
            return;
        }

        Vector3 toPlayer = m_enemyBase.Player.position - transform.position;
        if (toPlayer.sqrMagnitude <= 0.0001f)
        {
            StopDroneEnemy();
            return;
        }

        EnemyAirSettings data = m_enemyBase.StatusData;
        if (data != null)
        {
            // もし追跡範囲が設定されているなら、プレイヤーが範囲外のときは追跡しない
            if (data.chaseRange > 0f)
            {
                float chaseRangeSqr = data.chaseRange * data.chaseRange;
                if (toPlayer.sqrMagnitude > chaseRangeSqr)
                {
                    if (m_animator != null) m_animator.SetAttacking(false);
                    m_enemyBase.SlowDown(Time.deltaTime);
                    StopDroneEnemy();
                    return;
                }
            }
        }

        // プレイヤーが追跡範囲内のときは、停止距離を見ずに突撃する。
        Vector3 moveDirection = GetObstacleAwareDirection(toPlayer);
        if (m_animator != null)
        {
            m_animator.SetAttackTargetDirection(moveDirection);
            m_animator.TriggerAttack();
        }

        PlayDroneEnemy();
        m_enemyBase.AccelerateToward(moveDirection, Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (m_droneEnemyPlaying)
            m_droneEnemyPlayback.SetPosition(transform.position);
    }

    private Vector3 GetObstacleAwareDirection(Vector3 toPlayer)
    {
        Vector3 desiredDirection = toPlayer.normalized;
        int obstacleMask = ObstacleMask;
        if (obstacleMask == 0 || m_obstacleLookAhead <= 0f || m_obstacleProbeRadius <= 0f)
            return desiredDirection;

        if (!ProbeObstacle(desiredDirection, m_obstacleLookAhead, obstacleMask, out RaycastHit directHit))
            return desiredDirection;

        Vector3 bestDirection = desiredDirection;
        float bestScore = float.NegativeInfinity;

        EvaluateAvoidanceCandidate(desiredDirection, desiredDirection, obstacleMask, ref bestDirection, ref bestScore);

        TryEvaluateYawCandidate(desiredDirection, -75f, obstacleMask, ref bestDirection, ref bestScore);
        TryEvaluateYawCandidate(desiredDirection, -45f, obstacleMask, ref bestDirection, ref bestScore);
        TryEvaluateYawCandidate(desiredDirection, -25f, obstacleMask, ref bestDirection, ref bestScore);
        TryEvaluateYawCandidate(desiredDirection, 25f, obstacleMask, ref bestDirection, ref bestScore);
        TryEvaluateYawCandidate(desiredDirection, 45f, obstacleMask, ref bestDirection, ref bestScore);
        TryEvaluateYawCandidate(desiredDirection, 75f, obstacleMask, ref bestDirection, ref bestScore);

        EvaluateAvoidanceCandidate(desiredDirection, (desiredDirection + Vector3.up * 0.75f).normalized, obstacleMask, ref bestDirection, ref bestScore);
        EvaluateAvoidanceCandidate(desiredDirection, (desiredDirection + Vector3.down * 0.5f).normalized, obstacleMask, ref bestDirection, ref bestScore);

        if (bestScore > 0f)
            return bestDirection;

        Vector3 slideDirection = Vector3.ProjectOnPlane(desiredDirection, directHit.normal);
        if (slideDirection.sqrMagnitude > 0.0001f)
            return slideDirection.normalized;

        return (desiredDirection + directHit.normal * m_obstacleAvoidanceWeight).normalized;
    }

    private int ObstacleMask
    {
        get
        {
            if (m_obstacleMask.value != 0)
                return m_obstacleMask.value;

            return PhysicsLayers.Bit(PhysicsLayers.Default)
                | PhysicsLayers.Bit(PhysicsLayers.Ground)
                | PhysicsLayers.Bit(PhysicsLayers.Wall)
                | PhysicsLayers.Bit(PhysicsLayers.PhysicsObject);
        }
    }

    private void TryEvaluateYawCandidate(
        Vector3 desiredDirection,
        float yawDegrees,
        int obstacleMask,
        ref Vector3 bestDirection,
        ref float bestScore)
    {
        Vector3 candidate = Quaternion.AngleAxis(yawDegrees, Vector3.up) * desiredDirection;
        EvaluateAvoidanceCandidate(desiredDirection, candidate, obstacleMask, ref bestDirection, ref bestScore);
    }

    private void EvaluateAvoidanceCandidate(
        Vector3 desiredDirection,
        Vector3 candidate,
        int obstacleMask,
        ref Vector3 bestDirection,
        ref float bestScore)
    {
        if (candidate.sqrMagnitude <= 0.0001f)
            return;

        candidate.Normalize();
        float score = Vector3.Dot(candidate, desiredDirection);
        if (ProbeObstacle(candidate, m_obstacleLookAhead * 0.85f, obstacleMask, out RaycastHit hit))
        {
            float blockedRate = 1f - Mathf.Clamp01(hit.distance / Mathf.Max(0.01f, m_obstacleLookAhead));
            score -= blockedRate * m_obstacleAvoidanceWeight;
        }
        else
        {
            score += m_obstacleAvoidanceWeight;
        }

        if (score <= bestScore)
            return;

        bestScore = score;
        bestDirection = candidate;
    }

    private bool ProbeObstacle(Vector3 direction, float distance, int obstacleMask, out RaycastHit hit)
    {
        return Physics.SphereCast(
            transform.position,
            m_obstacleProbeRadius,
            direction,
            out hit,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
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

    private readonly System.Collections.Generic.HashSet<Health> m_hitTargets = new System.Collections.Generic.HashSet<Health>();

    /// <summary>
    /// 実体コライダーに触れたら即自爆する。敵同士は HitGroup が同じため AttackBox 経路では弾かれる。
    /// </summary>
    private bool CheckBodyContact()
    {
        int count = m_enemyBase.OverlapEntity(m_overlapBuffer, 0.02f);
        for (int i = 0; i < count; i++)
        {
            Collider col = m_overlapBuffer[i];
            if (col == null)
                continue;

            if (col.transform.root == transform.root)
                continue;

            var hittable = col.GetComponentInParent<IHittable>();
            if (ShouldIgnoreAsNormalObstacle(hittable))
                continue;

            Vector3 point = col.ClosestPoint(transform.position);
            ChannelLogger.Log("Enemy", $"[Kamikaze診断] 実体接触で自爆 vs {col.name}");
            Detonate(hittable, point);
            return true;
        }

        return false;
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

        if (ShouldIgnoreAsNormalObstacle(hittable))
            return;

        // 同グループ（敵同士／磁界トリガー経由で自陣 Hitbox に到達したケース）は自爆させない。
        // OnTriggerEnter はルートのRigidbodyに集約され HurtBox(Enemy層) 等の接触も拾う。
        // 他敵の MagnetFieldTrigger(8m) に HurtBox が触れて誤発火するのを HitGroup で弾く。
        if (hittable.HitGroup == m_ownHitGroup)
            return;

        m_hasHit = true;
        ChannelLogger.Log("Enemy", $"[Kamikaze診断] AttackBox接触で自爆 vs {other.name}");

        PerformExplosion(hittable, other.ClosestPoint(transform.position));
    }

    private void HandleEnvironmentContact(Collider other)
    {
        if (m_hasHit)
            return;

        if (!IsMagnetized)
            return;

        m_hasHit = true;
        ChannelLogger.Log("Enemy", $"[Kamikaze診断] 環境接触で自爆 vs {other.name}");
        PerformExplosion(null, transform.position);
    }

    private bool IsMagnetized => m_magnetizable != null && m_magnetizable.IsActive;

    private bool ShouldIgnoreAsNormalObstacle(IHittable hittable)
    {
        if (IsMagnetized)
            return false;

        return hittable == null || hittable.HitGroup == HitGroup.Physics;
    }

    /// <summary>
    /// 磁化中、実際に接触した別の磁化オブジェクトを検出して自爆する。
    /// 当たり判定設計原則準拠: ①OverlapEntity の LayerMask で事前フィルタ → ②Magnetizable / IHittable を
    /// コンポーネント取得して相手を特定（タグ・陣営レイヤーで判定しない）→ ③HitGroup で加害可否を決める。
    /// </summary>
    /// <returns>自爆したら true。</returns>
    private bool CheckMagnetizedContact()
    {
        int count = m_enemyBase.OverlapEntity(m_overlapBuffer, 0.02f);
        for (int i = 0; i < count; i++)
        {
            Collider col = m_overlapBuffer[i];
            if (col == null)
                continue;

            if (col.transform.root == transform.root)
                continue;

            // 相手が「磁化された別オブジェクト」か。磁化状態というデータで判定する（陣営では判定しない）。
            var otherMagnet = col.GetComponentInParent<Magnetizable>();
            if (otherMagnet == null || otherMagnet == m_magnetizable || !otherMagnet.IsActive)
                continue;

            var hittable = col.GetComponentInParent<IHittable>();
            if (hittable == null)
                continue;

            Detonate(hittable, col.ClosestPoint(transform.position));
            return true;
        }

        return false;
    }

    /// <summary>磁力衝突による自爆。爆発（中立ハザード）を相手に通してから自分を消す。</summary>
    private void Detonate(IHittable target, Vector3 point)
    {
        m_hasHit = true;
        ChannelLogger.Log("Enemy", "[Kamikaze診断] 磁化衝突で自爆 (CheckMagnetizedContact)");

        PerformExplosion(target, point);
    }

    /// <summary>
    /// 設定に基づき、単体または範囲爆発を実行する。
    /// </summary>
    private void PerformExplosion(IHittable primaryTarget, Vector3 point)
    {
        EnemyAirSettings data = m_enemyBase != null ? m_enemyBase.StatusData : null;
        float radius = data != null ? data.explosionRadius : 0f;
        int damage = m_enemyBase != null ? m_enemyBase.ExplosionDamage : 1;

        if (radius > 0.01f)
        {
            // 範囲爆発
            m_hitTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, m_overlapBuffer, m_enemyBase != null ? m_enemyBase.CollisionMask : -1);

            ChannelLogger.Log("Enemy", $"[Kamikaze診断] 範囲爆発実行: radius={radius}, targets={count}");

            for (int i = 0; i < count; i++)
            {
                Collider col = m_overlapBuffer[i];
                if (col == null || col.transform.root == transform.root) continue;

                var hittable = col.GetComponentInParent<IHittable>();
                if (hittable == null || hittable.HitGroup == k_DetonationHitGroup) continue;

                var health = col.GetComponentInParent<Health>();
                if (health != null && !m_hitTargets.Add(health)) continue;

                hittable.OnHit(new HitData
                {
                    damage = damage,
                    hitPoint = col.ClosestPoint(transform.position),
                    knockbackDir = (col.transform.position - transform.position).normalized,
                    source = gameObject
                });
            }
        }
        else if (primaryTarget != null)
        {
            // 従来通りの単体ダメージ（爆発は Physics ハザード扱い）
            if (primaryTarget.HitGroup != k_DetonationHitGroup)
            {
                primaryTarget.OnHit(new HitData
                {
                    damage = damage,
                    hitPoint = point,
                    knockbackDir = (point - transform.position).normalized,
                    source = gameObject
                });
            }
        }

        DestroySelf();
    }

    /// <summary>リスポーンしたらヒット済みフラグを戻し、再び突撃・自爆できるようにする。</summary>
    private void HandleRespawned()
    {
        m_hasHit = false;
    }

    private void DestroySelf()
    {
        StopDroneEnemy();

        if (m_enemyBase != null)
        {
            m_enemyBase.DestroyWithDisappearEffect();
            return;
        }

        Destroy(gameObject);
    }

    private void PlayDroneEnemy()
    {
        if (m_droneEnemyPlaying)
            return;

        SoundManager soundManager = SoundManager.Instance;
        if (soundManager == null)
            return;
        //Sound.Play(SoundData.CueSheet.SE, SoundData.SE.DroneEnemy);
        // 音量はサウンド調整シート（SoundVolumeSettings）の「ドローン敵の飛行ループ音」で管理する
        m_droneEnemyPlayback = soundManager.PlayAtWithHandle(
            SoundData.CueSheet.SE, SoundData.SE.DroneEnemy,
            transform.position, m_droneEnemyMinDistance, m_droneEnemyMaxDistance);
        m_droneEnemyPlaying = true;
    }

    private void StopDroneEnemy()
    {
        if (!m_droneEnemyPlaying)
            return;

        m_droneEnemyPlayback.Stop();
        m_droneEnemyPlaying = false;
    }
}

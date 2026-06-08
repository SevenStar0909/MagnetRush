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

    private EnemyAirBase m_enemyBase;
    private Magnetizable m_magnetizable;
    private bool m_hasHit;
    private readonly Collider[] m_overlapBuffer = new Collider[8];

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
            return;

        // 磁化中は「引き寄せられて他の磁化オブジェクトに実接触したら自爆」を最優先で判定する。
        // 磁力移動中こそ衝突が起きるため、IsMagnetControlled の早期returnより前に置く。
        if (!m_hasHit && m_magnetizable != null && m_magnetizable.IsActive && CheckMagnetizedContact())
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
            // もし追跡範囲が設定されているなら、プレイヤーが範囲外のときは追跡しない
            if (data.chaseRange > 0f)
            {
                float chaseRangeSqr = data.chaseRange * data.chaseRange;
                if (toPlayer.sqrMagnitude > chaseRangeSqr)
                {
                    if (m_animator != null) m_animator.SetAttacking(false);
                    m_enemyBase.SlowDown(Time.deltaTime);
                    return;
                }
            }
        }

        // プレイヤーが追跡範囲内のときは、停止距離を見ずに突撃する。
        if (m_animator != null)
        {
            m_animator.SetAttackTargetDirection(toPlayer);
            m_animator.TriggerAttack();
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
        ChannelLogger.Log("Enemy", $"[Kamikaze診断] AttackBox接触で自爆 vs {other.name}");

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
        ChannelLogger.Log("Enemy", $"[Kamikaze診断] 環境接触で自爆 vs {other.name}");
        DestroySelf();
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

        // 爆発は Physics ハザード。相手の HitGroup が異なるときだけ通す
        // （Physics≠Player / Physics≠Enemy は通る、Physics同士は弾く）。
        if (target.HitGroup != k_DetonationHitGroup)
        {
            target.OnHit(new HitData
            {
                damage = m_enemyBase != null ? m_enemyBase.ExplosionDamage : 1,
                hitPoint = point,
                knockbackDir = (point - transform.position).normalized,
                source = gameObject
            });
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
        if (m_enemyBase != null)
        {
            m_enemyBase.DestroyWithDisappearEffect();
            return;
        }

        Destroy(gameObject);
    }
}

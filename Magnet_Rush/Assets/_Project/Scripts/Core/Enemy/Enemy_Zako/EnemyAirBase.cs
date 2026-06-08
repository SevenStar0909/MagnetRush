using UnityEngine;

using System;
using System.Collections;

/// <summary>
/// 空中敵の基底クラス。Entity を継承し、プレイヤー参照・HP・3D飛行移動を共通化する。
/// 位置移動は3D、回転はY軸のみで制御する。
/// </summary>
[DisallowMultipleComponent]
public class EnemyAirBase : Entity
{
    [Header("Data")]
    [SerializeField] private EnemyAirSettings m_statusData;

    [Header("References")]
    [SerializeField] private Transform m_player;

    [Header("Magnetic")]
    [SerializeField] private MagneticMover m_mover;

    [Header("Disappear Effect")]
    [SerializeField] private GameObject m_disappearEffectPrefab;
    [SerializeField] private float m_disappearEffectLifetime = 0.75f;

    private readonly Collider[] m_environmentContactBuffer = new Collider[8];
    private bool m_disappearEffectPlayed;

    private Magnetizable m_magnetizable;
    private Transform m_modelRoot;
    private Transform m_hitboxRoot;
    private Vector3 m_spawnPosition;
    private Quaternion m_spawnRotation;
    private bool m_spawnCaptured;
    private bool m_isDead;

    public EnemyAirSettings StatusData => m_statusData;
    public Transform Player => m_player;
    public bool IsMagnetControlled => m_mover != null && m_mover.IsMagnetActive;
    public event Action<Collider> EnvironmentContact;

    /// <summary>リスポーン待ち（ソフト死）中かどうか。AI・移動はこの間スキップされる。</summary>
    public bool IsDead => m_isDead;

    /// <summary>最初に配置された位置。死亡演出で敵をここへ戻すのに使う。未確定時は現在位置を返す。</summary>
    public Vector3 SpawnPosition => m_spawnCaptured ? m_spawnPosition : transform.position;

    /// <summary>最初に配置された向き。</summary>
    public Quaternion SpawnRotation => m_spawnCaptured ? m_spawnRotation : transform.rotation;

    /// <summary>リスポーン完了時に発火。AI がヒット済みフラグ等をリセットするために購読する。</summary>
    public event Action Respawned;

    private float RespawnDelay => m_statusData != null ? m_statusData.respawnDelay : 0f;

    protected override float Gravity => 0f;
    protected override float SnapForce => 0f;
    protected override float ExternalDrag => m_statusData != null ? m_statusData.externalDrag : base.ExternalDrag;
    protected override float GroundCheckDistance => 0f;
    protected override LayerMask GroundLayer => 0;

    protected override void Awake()
    {
        base.Awake();
        m_mover = GetComponentInChildren<MagneticMover>();
        m_magnetizable = GetComponent<Magnetizable>();
        m_modelRoot = transform.Find("Model");
        m_hitboxRoot = transform.Find("Hitbox");

        if (m_health != null && m_statusData != null)
            m_health.SetMaxHealth(m_statusData.maxHp);

        CachePlayer();

        if (m_health != null)
            m_health.OnDie += Die;
    }

    private void OnDestroy()
    {
        if (m_health != null)
            m_health.OnDie -= Die;
    }

    private void Start()
    {
        // スポーン地点をリスポーン用に保存する。スポナーが Instantiate 後に位置を設定する場合に備え、
        // Awake ではなく Start（全 Awake 完了後）で確定させる。
        m_spawnPosition = transform.position;
        m_spawnRotation = transform.rotation;
        m_spawnCaptured = true;
    }

    private void Update()
    {
        if (m_isDead) { ChannelLogger.LogGuardReturn("Enemy", "リスポーン待ちのため停止中"); return; }

        CachePlayer();
        UpdateAir(Time.deltaTime);
        UpdateMagneticOrientation(Time.deltaTime);
        ApplyMovement(Time.deltaTime);
        CheckEnvironmentContact();
    }

    /// <summary>
    /// 派生クラスで空中AIを実装する。
    /// </summary>
    protected virtual void UpdateAir(float dt)
    {
    }

    /// <summary>指定方向へ3Dで飛行する。回転はY軸のみ。</summary>
    public void AccelerateToward(Vector3 worldDirection, float dt)
    {
        if (m_statusData == null) return;
        if (worldDirection.sqrMagnitude <= 0.0001f) return;

        Vector3 desiredDirection = worldDirection.normalized;
        Vector3 desiredVelocity = desiredDirection * Mathf.Max(0f, m_statusData.moveSpeed);

        if (velocity.sqrMagnitude > 0.001f)
        {
            float turnRadians = Mathf.Max(0f, m_statusData.turningDrag) * Mathf.Deg2Rad * dt;
            velocity = Vector3.RotateTowards(velocity, desiredVelocity, turnRadians, m_statusData.acceleration * dt);
        }
        else
        {
            velocity = Vector3.MoveTowards(velocity, desiredVelocity, m_statusData.acceleration * dt);
        }

        FaceTowardYaw(desiredDirection, dt);
    }

    /// <summary>3D移動を減速する。</summary>
    public void SlowDown(float dt)
    {
        if (m_statusData == null) return;

        velocity = Vector3.MoveTowards(velocity, Vector3.zero, m_statusData.deceleration * dt);
    }

    /// <summary>指定方向を Y 軸だけで向く。</summary>
    public void FaceTowardYaw(Vector3 direction, float dt)
    {
        if (m_statusData == null) return;

        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
        if (flatDirection.sqrMagnitude <= 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            m_statusData.rotationSpeed * dt
        );
    }

    /// <summary>指定方向を向く。現在は空中敵用に Y 軸回転のみ。</summary>
    public void FaceToward(Vector3 direction, float dt)
    {
        FaceTowardYaw(direction, dt);
    }

    public int ImpactDamage => m_statusData != null ? m_statusData.impactDamage : 1;

    /// <summary>磁力衝突で自爆した時、接触相手に与えるダメージ。</summary>
    public int ExplosionDamage => m_statusData != null ? m_statusData.explosionDamage : 1;

    public void SetPlayer(Transform player)
    {
        m_player = player;
    }

    /// <summary>
    /// 消滅エフェクトを出して死亡処理に入る。respawnDelay&gt;0 ならリスポーン、0以下なら破棄する。
    /// </summary>
    public void DestroyWithDisappearEffect()
    {
        HandleDeath();
    }

    /// <summary>
    /// 全死因（HP0・カミカゼ自爆）の共通チョークポイント。
    /// respawnDelay&gt;0 ならソフト死してリスポーンを予約し、0以下なら従来どおり破棄する。
    /// </summary>
    private void HandleDeath()
    {
        if (m_isDead) { ChannelLogger.LogGuardReturn("Enemy", "既に死亡（リスポーン待ち）"); return; }

        TriggerDisappearEffect();

        float delay = RespawnDelay;
        if (delay <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        BeginRespawn(delay);
    }

    /// <summary>ソフト死: 見た目・当たり判定・磁力・移動を止めてリスポーンを待つ。root は active のまま保つ。</summary>
    private void BeginRespawn(float delay)
    {
        m_isDead = true;

        velocity = Vector3.zero;
        externalVelocity = Vector3.zero;
        holdVelocity = Vector3.zero;
        ClearMagnetism();

        SetBodyActive(false);
        StartCoroutine(RespawnAfterDelay(delay));
    }

    /// <summary>
    /// 弾着弾で付与された磁力を完全に解除する。MagnetField を ForceExpire すると
    /// OnFieldExpired 経由で極解除・ビジュアライザ・着弾エフェクトまで片付くため、
    /// リスポーン後に磁力（オーラ）が残らない。Magnetizable.Deactivate だけでは
    /// 子に AddComponent された MagnetField が hide/show で復活してしまう。
    /// </summary>
    private void ClearMagnetism()
    {
        var fields = GetComponentsInChildren<MagnetField>(true);
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i] != null) fields[i].ForceExpire();
        }

        if (m_magnetizable != null) m_magnetizable.Deactivate();
    }

    private IEnumerator RespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Respawn();
    }

    /// <summary>スポーン地点・HP・見た目を元に戻して復活する。</summary>
    public void Respawn()
    {
        if (m_spawnCaptured)
            transform.SetPositionAndRotation(m_spawnPosition, m_spawnRotation);

        velocity = Vector3.zero;
        externalVelocity = Vector3.zero;
        holdVelocity = Vector3.zero;

        SetBodyActive(true);

        if (m_health != null) m_health.ResetHealth();

        m_disappearEffectPlayed = false;
        m_isDead = false;

        Respawned?.Invoke();
    }

    /// <summary>見た目(Model)と当たり判定(Hitbox)の子オブジェクトをまとめて表示/非表示する。</summary>
    private void SetBodyActive(bool active)
    {
        if (m_modelRoot != null) m_modelRoot.gameObject.SetActive(active);
        if (m_hitboxRoot != null) m_hitboxRoot.gameObject.SetActive(active);
    }

    protected void CachePlayer()
    {
        if (m_player != null)
            return;

        GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
            m_player = playerObj.transform;
    }

    private void CheckEnvironmentContact()
    {
        int hitCount = OverlapEntity(m_environmentContactBuffer, 0.02f);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = m_environmentContactBuffer[i];
            if (col == null)
                continue;

            int layer = col.gameObject.layer;
            if (layer == PhysicsLayers.Ground || layer == PhysicsLayers.Wall)
            {
                EnvironmentContact?.Invoke(col);
                return;
            }
        }
    }

    private void TriggerDisappearEffect()
    {
        if (m_disappearEffectPlayed)
            return;

        m_disappearEffectPlayed = true;

        if (m_disappearEffectPrefab == null)
            return;

        GameObject effectObject = Instantiate(m_disappearEffectPrefab, transform.position, Quaternion.identity);

        if (m_disappearEffectLifetime > 0f)
            Destroy(effectObject, m_disappearEffectLifetime);
    }

    protected virtual void Die()
    {
        HandleDeath();
    }
}

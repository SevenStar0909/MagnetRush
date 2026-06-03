using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Dyson-type air enemy. It approaches the player, anchors itself during pull,
/// then repositions after the pull finishes or the player gets too close.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyAirBase))]
public class EnemyAirDysonAi : MonoBehaviour
{
    private enum DysonState
    {
        Approach,
        Search,
        Pull,
        Reposition
    }

    [Header("Positioning")]
    [SerializeField] private float m_keepDistance = 5f;
    [SerializeField] private float m_keepDistanceTolerance = 0.75f;
    [SerializeField] private bool m_useAirSettingsChaseRange = true;
    [SerializeField] private float m_approachHeightOffset = 5.0f;  // Y軸方向のオフセット。
    [SerializeField] private float m_approachTimeout = 3f;
    [SerializeField] private float m_searchDuration = 1f;

    [Header("Pull Timing")]
    [SerializeField] private float m_pullDuration = 2.5f;
    [SerializeField] private float m_pullCooldown = 1f;
    [SerializeField] private float m_repositionDuration = 1.5f;

    [Header("Pull Movement")]
    [SerializeField] private float m_pullRange = 10f;
    [FormerlySerializedAs("m_pullForce")]
    [SerializeField] private float m_pullAcceleration = 35f;
    [SerializeField] private float m_pullMaxSpeed = 8f;
    [SerializeField] private float m_pullMinDistance = 0.5f;
    [SerializeField] private bool m_useDistanceFalloff = true;

    private EnemyAirBase m_enemyBase;
    private Entity m_playerEntity;
    private EntityController m_playerController;
    private Transform m_cachedPlayer;
    private Transform m_pullTargetThisFrame;
    private DysonState m_state = DysonState.Approach;
    private float m_stateTimer;
    private float m_approachTimer;
    private float m_currentPullSpeed;

    private void Awake()
    {
        if (!TryGetComponent(out m_enemyBase))
        {
            Debug.LogError($"[EnemyAirDysonAi] {name}: EnemyAirBase was not found.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        m_pullTargetThisFrame = null;

        if (m_enemyBase == null)
            return;

        if (m_enemyBase.IsMagnetControlled)
        {
            m_currentPullSpeed = 0f;
            return;
        }

        Transform player = m_enemyBase.Player;
        if (player == null)
        {
            m_enemyBase.SlowDown(Time.deltaTime);
            m_currentPullSpeed = 0f;
            return;
        }

        CachePlayerEntity(player);

        Vector3 toPlayer = player.position - transform.position;
        Vector3 flatToPlayer = new Vector3(toPlayer.x, 0f, toPlayer.z);
        if (flatToPlayer.sqrMagnitude <= 0.0001f)
            return;

        float dt = Time.deltaTime;
        float distance = flatToPlayer.magnitude;

        switch (m_state)
        {
            // プレイヤーに近づくフェーズ。プレイヤーへの水平距離が一定以上ある場合は近づき、近すぎる場合は減速する。
            case DysonState.Approach:
                TickApproach(flatToPlayer, distance, dt);
                break;
            // プレイヤーを見失ったときのフェーズ。一定時間経過後に再びApproachフェーズに移行する。
            case DysonState.Search:
                TickSearch(flatToPlayer, distance, dt);
                break;
            // プレイヤーを引き寄せるフェーズ。一定時間経過後、またはプレイヤーが近すぎる場合にRepositionフェーズに移行する。
            case DysonState.Pull:
                TickPull(player, flatToPlayer, distance, dt);
                break;
            // プレイヤーから一定距離を保ちながら位置を調整するフェーズ。一定時間経過後に再びApproachフェーズに移行する。
            case DysonState.Reposition:
                TickReposition(flatToPlayer, distance, dt);
                break;
        }
    }

    private void LateUpdate()
    {
        if (m_pullTargetThisFrame == null)
            return;

        PullPlayer(m_pullTargetThisFrame, Time.deltaTime);
    }

    private void CachePlayerEntity(Transform player)
    {
        if (m_cachedPlayer == player && m_playerEntity != null)
            return;

        m_cachedPlayer = player;
        m_playerEntity = player.GetComponentInParent<Entity>();
        m_playerController = player.GetComponentInParent<EntityController>();
    }

    // flatToPlayer　の意味は、プレイヤーへの水平距離のベクトルです。Y成分は無視されているため、敵がプレイヤーに向かって水平に移動するために使用されます。
    private void TickApproach(Vector3 flatToPlayer, float distance, float dt)
    {
        // 自分のdataを参照して、追跡範囲内にいる場合は減速する
        EnemyAirSettings data = m_enemyBase.StatusData;
        // 追跡範囲は、プレイヤーへの水平距離がdata.chaseRange以下の場合とする
        if (m_useAirSettingsChaseRange && data != null && data.chaseRange > 0f && distance > data.chaseRange)
        {
            ChangeState(DysonState.Search);
            return;
        }

        // プレイヤーへの水平距離が、m_keepDistance + m_keepDistanceTolerance より大きい場合は、プレイヤーに近づく
        if (distance > m_keepDistance + m_keepDistanceTolerance)
        {
            m_approachTimer += dt;
            if (m_approachTimeout > 0f && m_approachTimer >= m_approachTimeout)
            {
                ChangeState(DysonState.Search);
                return;
            }

            // Y軸方向のオフセットを加えたプレイヤーへのベクトルを計算
            Vector3 approachTarget = m_enemyBase.Player.position + Vector3.up * m_approachHeightOffset;
            Vector3 toApproachTarget = approachTarget - transform.position;
            m_enemyBase.AccelerateToward(toApproachTarget, dt);
            return;
        }

        m_approachTimer = 0f;
        m_enemyBase.FaceTowardYaw(flatToPlayer, dt);
        m_enemyBase.SlowDown(dt);

        if (distance >= m_pullMinDistance * 2f && m_enemyBase.velocity.sqrMagnitude < 0.1f)
            ChangeState(DysonState.Pull);
    }

    // プレイヤーを見失ったときのフェーズ。一定時間経過後に再びApproachフェーズに移行する。
    private void TickSearch(Vector3 flatToPlayer, float distance, float dt)
    {
        m_enemyBase.FaceTowardYaw(flatToPlayer, dt);
        m_enemyBase.SlowDown(dt);

        m_stateTimer -= dt;
        if (m_stateTimer <= 0f)
            ChangeState(DysonState.Approach);
    }

    // プレイヤーを引き寄せるフェーズ。一定時間経過後、またはプレイヤーが近すぎる場合にRepositionフェーズに移行する。
    private void TickPull(Transform player, Vector3 flatToPlayer, float distance, float dt)
    {
        m_enemyBase.FaceTowardYaw(flatToPlayer, dt);
        m_enemyBase.SlowDown(dt);
        m_pullTargetThisFrame = player;

        m_stateTimer -= dt;
        if (m_stateTimer <= 0f || distance <= m_pullMinDistance)
            ChangeState(DysonState.Reposition);
    }

    // プレイヤーから一定距離を保ちながら位置を調整するフェーズ。一定時間経過後に再びApproachフェーズに移行する。
    private void TickReposition(Vector3 flatToPlayer, float distance, float dt)
    {
        m_stateTimer -= dt;

        float targetDistance = Mathf.Max(0.1f, m_keepDistance + m_keepDistanceTolerance);
        if (distance < targetDistance && m_stateTimer > 0f)
        {
            m_enemyBase.AccelerateToward(-flatToPlayer, dt);
            return;
        }

        m_enemyBase.FaceTowardYaw(flatToPlayer, dt);
        m_enemyBase.SlowDown(dt);

        if (m_stateTimer <= -m_pullCooldown)
            ChangeState(DysonState.Approach);
    }

    // プレイヤーを引き寄せる処理。プレイヤーが一定距離以上離れている場合に、加速度を計算してプレイヤーを引き寄せる。
    private void PullPlayer(Transform player, float dt)
    {
        if (m_playerEntity == null)
            return;

        Vector3 pull = transform.position - player.position;
        pull.y = 0f;

        float distance = pull.magnitude;
        if (distance < m_pullMinDistance)
            return;

        float pullRange = Mathf.Max(0f, m_pullRange);
        if (pullRange > 0f && distance > pullRange)
            return;

        float acceleration = Mathf.Max(0f, m_pullAcceleration);
        if (m_useDistanceFalloff && pullRange > 0f)
        {
            float t = Mathf.Clamp01(distance / pullRange);
            acceleration *= t * t;
        }

        if (acceleration <= 0f)
            return;

        float targetSpeed = Mathf.Max(0f, m_pullMaxSpeed);
        m_currentPullSpeed = Mathf.MoveTowards(m_currentPullSpeed, targetSpeed, acceleration * dt);

        Vector3 motion = pull.normalized * m_currentPullSpeed * dt;
        if (m_playerController != null)
        {
            player.position = m_playerController.Move(player.position, motion);
        }
        else
        {
            player.position += motion;
        }
    }

    // 状態を変更する処理。状態が変わるたびに、タイマーや引き寄せ速度などの変数をリセットする。
    private void ChangeState(DysonState next)
    {
        if (m_state == next)
            return;

        m_state = next;

        switch (m_state)
        {
            case DysonState.Search:
                m_stateTimer = Mathf.Max(0f, m_searchDuration);
                m_approachTimer = 0f;
                m_currentPullSpeed = 0f;
                m_enemyBase.velocity = Vector3.zero;
                break;
            case DysonState.Pull:
                m_stateTimer = Mathf.Max(0.1f, m_pullDuration);
                m_approachTimer = 0f;
                m_currentPullSpeed = 0f;
                break;
            case DysonState.Reposition:
                m_stateTimer = Mathf.Max(0f, m_repositionDuration);
                m_approachTimer = 0f;
                m_currentPullSpeed = 0f;
                break;
            default:
                m_stateTimer = 0f;
                m_approachTimer = 0f;
                m_currentPullSpeed = 0f;
                break;
        }
    }
}

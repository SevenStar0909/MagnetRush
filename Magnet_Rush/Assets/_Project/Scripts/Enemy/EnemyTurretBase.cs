using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 砲台敵人的基底クラス。移動せず、共通参照と生存管理のみを担当する。
/// </summary>
[DisallowMultipleComponent]
public class EnemyTurretBase : Entity
{
    [Header("Data")]
    [FormerlySerializedAs("statusData")]
    [SerializeField] private EnemyTurretSettings m_statusData;

    [Header("References")]
    [FormerlySerializedAs("player")]
    [SerializeField] private Transform m_player;
    [SerializeField] private Transform m_firePoint;

    private Magnetizable m_magnetizable;

    public EnemyTurretSettings StatusData => m_statusData;
    public Transform Player => m_player;
    public Transform FirePoint => m_firePoint;
    public bool IsMagnetized => m_magnetizable != null && m_magnetizable.IsActive;

    protected override void Awake()
    {
        base.Awake();

        m_magnetizable = GetComponent<Magnetizable>();

        if (m_player == null)
        {
            GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
            if (playerObj != null)
                m_player = playerObj.transform;
        }

        if (m_health != null)
            m_health.OnDie += Die;
    }

    private void OnDestroy()
    {
        if (m_health != null)
            m_health.OnDie -= Die;
    }

    private void Update()
    {
        // タレットは移動しないため、外力速度は毎フレーム破棄
        externalVelocity = Vector3.zero;
    }

    public void SetPlayer(Transform player)
    {
        m_player = player;
    }

    public void SetFirePoint(Transform firePoint)
    {
        m_firePoint = firePoint;
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}

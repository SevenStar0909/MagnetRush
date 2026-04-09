using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyTurretBase))]
[RequireComponent(typeof(Magnetizable))]
public class EnemyTurretMagneticAim : MonoBehaviour, IMagneticResponse
{
    [Header("Aim")]
    [Tooltip("回転させる砲塔のTransform。未指定なら自身を使用。")]
    [SerializeField] private Transform m_yawPivot;
    [Tooltip("磁性オブジェクト探索半径（m）。0以下ならMagnetSettings.magnetRangeを使用。")]
    [SerializeField] private float m_detectionMagRange = 0f;
    [Tooltip("未磁化時にプレイヤーを追従する最大距離（m）。0以下なら無制限。")]
    [SerializeField] private float m_aimToPlayerRange = 20f;
    [Tooltip("探索更新間隔（秒）")]
    [SerializeField] private float m_targetRefreshInterval = 0.1f;
    [Tooltip("回転速度（度/秒）")]
    [SerializeField] private float m_rotationSpeed = 120f;

    private EnemyTurretBase m_turretBase;
    private Magnetizable m_selfMagnetizable;
    private Magnetizable m_currentTarget;
    private float m_refreshTimer;
    private Vector3 m_initialLocalEuler;

    public bool IsResponseActive => m_selfMagnetizable != null && m_selfMagnetizable.IsActive;
    public bool HasAimTarget { get; private set; }
    public Vector3 CurrentAimDirection { get; private set; } = Vector3.forward;

    private void Awake()
    {
        m_turretBase = GetComponent<EnemyTurretBase>();
        m_selfMagnetizable = GetComponent<Magnetizable>();

        if (m_yawPivot == null)
            m_yawPivot = transform;

        m_initialLocalEuler = m_yawPivot.localEulerAngles;
    }

    private void Update()
    {
        HasAimTarget = false;

        if (!IsResponseActive)
        {
            FacePlayerWhenNotMagnetized();
            return;
        }

        m_refreshTimer -= Time.deltaTime;
        if (m_refreshTimer <= 0f)
        {
            m_refreshTimer = Mathf.Max(0.02f, m_targetRefreshInterval);
            m_currentTarget = FindNearestMagnetizable();
        }

        if (m_currentTarget == null)
            return;

        Vector3 toTarget = m_currentTarget.transform.position - m_yawPivot.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
            return;

        bool isOppositePole = IsOppositePole(m_currentTarget);
        Vector3 desiredWorldDir = isOppositePole ? toTarget.normalized : (-toTarget.normalized);

        HasAimTarget = true;
        CurrentAimDirection = desiredWorldDir;
        RotateYawOnly(desiredWorldDir);
    }

    private void FacePlayerWhenNotMagnetized()
    {
        if (m_turretBase == null || m_turretBase.Player == null)
            return;

        Vector3 toPlayer = m_turretBase.Player.position - m_yawPivot.position;
        toPlayer.y = 0f;

        float sqrDist = toPlayer.sqrMagnitude;
        if (sqrDist <= 0.0001f)
            return;

        if (m_aimToPlayerRange > 0f && sqrDist > m_aimToPlayerRange * m_aimToPlayerRange)
            return;

        Vector3 dir = toPlayer.normalized;
        HasAimTarget = true;
        CurrentAimDirection = dir;
        RotateYawOnly(dir);
    }

    private void RotateYawOnly(Vector3 desiredWorldDir)
    {
        Vector3 localDir = m_yawPivot.parent != null
            ? m_yawPivot.parent.InverseTransformDirection(desiredWorldDir)
            : desiredWorldDir;

        localDir.y = 0f;
        if (localDir.sqrMagnitude <= 0.0001f)
            return;

        float targetYaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        float currentYaw = m_yawPivot.localEulerAngles.y;
        float nextYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, Mathf.Max(1f, m_rotationSpeed) * Time.deltaTime);

        m_yawPivot.localRotation = Quaternion.Euler(
            m_initialLocalEuler.x,
            nextYaw,
            m_initialLocalEuler.z
        );
    }

    private bool IsOppositePole(Magnetizable other)
    {
        if (m_selfMagnetizable == null || other == null)
            return false;

        MagneticPole selfPole = m_selfMagnetizable.Pole;
        MagneticPole otherPole = other.Pole;
        if (selfPole == MagneticPole.None || otherPole == MagneticPole.None)
            return false;

        return selfPole != otherPole;
    }

    private Magnetizable FindNearestMagnetizable()
    {
        if (m_selfMagnetizable == null)
            return null;

        Magnetizable[] all = FindObjectsByType<Magnetizable>(FindObjectsSortMode.None);

        Magnetizable nearest = null;
        float detectionRange = ResolveDetectionRange();
        float nearestSqr = detectionRange * detectionRange;
        Vector3 origin = m_yawPivot.position;

        for (int i = 0; i < all.Length; i++)
        {
            Magnetizable candidate = all[i];
            if (candidate == null) continue;
            if (candidate == m_selfMagnetizable) continue;
            if (!candidate.IsActive) continue;
            if (candidate.Pole == MagneticPole.None) continue;

            Vector3 delta = candidate.transform.position - origin;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;

            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private float ResolveDetectionRange()
    {
        if (m_detectionMagRange > 0f)
            return m_detectionMagRange;

        if (MagnetManager.Instance != null && MagnetManager.Instance.Settings != null)
            return MagnetManager.Instance.Settings.magnetRange;

        return 10f;
    }

    public void OnMagnetForce(Vector3 force, Vector3 sourcePosition) { }
    public void OnMagnetContact(Magnetizable self, Magnetizable other) { }
}

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyTurretBase))]
[RequireComponent(typeof(Magnetizable))]
public class EnemyTurretMagneticAim : MonoBehaviour, IMagneticResponse
{
    [Header("Aim")]
    [Tooltip("回転させる砲塔のTransform。未指定なら自身を使用。")]
    [SerializeField] private Transform m_yawPivot;

    private EnemyTurretBase m_turretBase;
    private EnemyTurretSettings m_data;
    private Magnetizable m_selfMagnetizable;
    private Magnetizable m_currentTarget;
    private float m_refreshTimer;

    public bool IsResponseActive => m_selfMagnetizable != null && m_selfMagnetizable.IsActive;

    private void Awake()
    {
        m_turretBase = GetComponent<EnemyTurretBase>();
        m_selfMagnetizable = GetComponent<Magnetizable>();
        m_data = m_turretBase != null ? m_turretBase.StatusData : null;

        if (m_yawPivot == null)
            m_yawPivot = transform;
    }

    private void Update()
    {
        if (!IsResponseActive)
        {
            FacePlayer(checkRange: true);
            return;
        }

        m_refreshTimer -= Time.deltaTime;
        if (m_refreshTimer <= 0f)
        {
            float refresh = m_data != null ? m_data.targetRefreshInterval : 0.1f;
            m_refreshTimer = Mathf.Max(0.02f, refresh);
            m_currentTarget = FindNearestMagnetizable();
        }

        // 磁力ターゲットがなければプレイヤーを追従し続ける
        if (m_currentTarget == null)
        {
            ChannelLogger.LogGuardReturn("Enemy", "磁力ターゲットなし");
            FacePlayer(checkRange: false);
            return;
        }

        Vector3 toTarget = m_currentTarget.transform.position - m_yawPivot.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            ChannelLogger.LogGuardReturn("Enemy", "ターゲット距離ほぼゼロ");
            return;
        }

        bool isOppositePole = IsOppositePole(m_currentTarget);
        Vector3 desiredWorldDir = isOppositePole ? toTarget.normalized : (-toTarget.normalized);

        RotateToward(desiredWorldDir);
    }

    private void FacePlayer(bool checkRange)
    {
        if (m_turretBase == null || m_turretBase.Player == null)
        {
            ChannelLogger.LogGuardReturn("Enemy", "タレット基底/プレイヤー参照なし");
            return;
        }

        Vector3 toPlayer = m_turretBase.Player.position - m_yawPivot.position;

        float sqrDist = toPlayer.sqrMagnitude;
        if (sqrDist <= 0.0001f)
        {
            ChannelLogger.LogGuardReturn("Enemy", "プレイヤー距離ほぼゼロ");
            return;
        }

        float aimRange = m_data != null ? m_data.aimToPlayerRange : 20f;
        if (checkRange && aimRange > 0f && sqrDist > aimRange * aimRange)
        {
            ChannelLogger.LogGuardReturn("Enemy", "プレイヤー照準範囲外");
            return;
        }

        RotateToward(toPlayer.normalized);
    }

    /// <summary>
    /// 砲身をターゲット方向にyaw+pitchで回転させる（localRotation使用、ロールなし）。
    /// </summary>
    private void RotateToward(Vector3 desiredWorldDir)
    {
        if (desiredWorldDir.sqrMagnitude <= 0.0001f)
        {
            ChannelLogger.LogGuardReturn("Enemy", "目標方向ほぼゼロ");
            return;
        }

        // 親空間に変換（Model Y180°を吸収）
        Vector3 localDir = m_yawPivot.parent != null
            ? m_yawPivot.parent.InverseTransformDirection(desiredWorldDir)
            : desiredWorldDir;

        if (localDir.sqrMagnitude <= 0.0001f)
        {
            ChannelLogger.LogGuardReturn("Enemy", "ローカル変換後の方向ほぼゼロ");
            return;
        }

        // バレルメッシュがlocal -Z方向に伸びているため180°オフセット
        float targetYaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg + 180f;
        float horizontalDist = Mathf.Sqrt(localDir.x * localDir.x + localDir.z * localDir.z);
        float targetPitch = Mathf.Atan2(localDir.y, horizontalDist) * Mathf.Rad2Deg;

        Quaternion targetLocalRot = Quaternion.Euler(targetPitch, targetYaw, 0f);
        float rotSpeed = m_data != null ? m_data.rotationSpeed : 120f;
        float maxDelta = Mathf.Max(1f, rotSpeed) * Time.deltaTime;
        m_yawPivot.localRotation = Quaternion.RotateTowards(
            m_yawPivot.localRotation, targetLocalRot, maxDelta);
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

        // 全シーン走査(FindObjectsByType)は配列確保のGCゴミと全探索コストが重い。
        // MagnetManager がキャッシュ済みの登録一覧を読む(結果は同一・ゴミゼロ)。
        if (MagnetManager.Instance == null)
            return null;
        List<Magnetizable> all = MagnetManager.Instance.GetActiveMagnetizables();

        Magnetizable nearest = null;
        float detectionRange = ResolveDetectionRange();
        float nearestSqr = detectionRange * detectionRange;
        Vector3 origin = m_yawPivot.position;

        for (int i = 0; i < all.Count; i++)
        {
            Magnetizable candidate = all[i];
            if (candidate == null) continue;
            if (candidate == m_selfMagnetizable) continue;
            if (!candidate.IsActive) continue;
            if (candidate.Pole == MagneticPole.None) continue;

            Vector3 delta = candidate.transform.position - origin;
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
        float detRange = m_data != null ? m_data.detectionMagRange : 0f;
        if (detRange > 0f)
            return detRange;

        if (MagnetManager.Instance != null && MagnetManager.Instance.Settings != null)
            return MagnetManager.Instance.Settings.magnetRange;

        return 10f;
    }

    public void OnMagnetForce(Vector3 force, Vector3 sourcePosition) { }
    public void OnMagnetContact(Magnetizable self, Magnetizable other) { }
}

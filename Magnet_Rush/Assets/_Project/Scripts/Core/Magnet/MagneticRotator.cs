using UnityEngine;

/// <summary>
/// タレット砲塔など、移動せず回転だけで磁力に応答するコンポーネント。
/// 仕様書: 「タレット回転」で使用。砲塔にS弾 + 壁にN弾 → 砲塔が壁方向を向く。
/// </summary>
[RequireComponent(typeof(Magnetizable))]
public class MagneticRotator : MonoBehaviour, IMagneticResponse
{
    [SerializeField] private MagneticRotatorSettings m_settings;

    private Magnetizable m_magnetizable;

    public bool IsResponseActive => m_magnetizable != null && m_magnetizable.IsActive;

    void Awake()
    {
        m_magnetizable = GetComponent<Magnetizable>();
    }

    public void OnMagnetForce(Vector3 force, Vector3 sourcePosition)
    {
        if (m_settings == null) { ChannelLogger.LogGuardReturn("Magnet", "Rotator設定なし"); return; }

        Vector3 dirToSource = (sourcePosition - transform.position).normalized;
        if (dirToSource.sqrMagnitude < 0.001f) { ChannelLogger.LogGuardReturn("Magnet", "ソース方向がほぼゼロ"); return; }

        Vector3 projectedDir = Vector3.ProjectOnPlane(dirToSource, m_settings.rotationAxis).normalized;
        if (projectedDir.sqrMagnitude < 0.001f) { ChannelLogger.LogGuardReturn("Magnet", "回転軸への射影がほぼゼロ"); return; }

        Quaternion targetRotation = Quaternion.LookRotation(projectedDir, m_settings.rotationAxis);

        // 符号付き角度で制限チェック（親のローカル空間基準）
        Vector3 referenceForward = transform.parent != null ? transform.parent.forward : Vector3.forward;
        float signedAngle = Vector3.SignedAngle(referenceForward, projectedDir, m_settings.rotationAxis);
        if (signedAngle < m_settings.minAngle || signedAngle > m_settings.maxAngle) { ChannelLogger.LogGuardReturn("Magnet", "回転角制限外"); return; }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            m_settings.maxAngularSpeed * Time.deltaTime
        );
    }

    public void OnMagnetContact(Magnetizable self, Magnetizable other)
    {
        // タレットは接触固定しない
    }
}

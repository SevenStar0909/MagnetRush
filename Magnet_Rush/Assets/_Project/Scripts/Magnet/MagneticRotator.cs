using UnityEngine;

/// <summary>
/// タレット砲塔など、移動せず回転だけで磁力に応答するコンポーネント。
/// 仕様書: 「タレット回転」で使用。砲塔にS弾 + 壁にN弾 → 砲塔が壁方向を向く。
/// </summary>
[RequireComponent(typeof(Magnetizable))]
public class MagneticRotator : MonoBehaviour, IMagneticResponse
{
    [SerializeField] private Vector3 m_rotationAxis = Vector3.up;
    [SerializeField] private float m_maxAngularSpeed = 90f;
    [SerializeField] private float m_minAngle = -180f;
    [SerializeField] private float m_maxAngle = 180f;

    private Magnetizable m_magnetizable;

    public bool IsResponseActive => m_magnetizable != null && m_magnetizable.IsActive;

    void Awake()
    {
        m_magnetizable = GetComponent<Magnetizable>();
    }

    public void OnMagnetForce(Vector3 force, Vector3 sourcePosition)
    {
        Vector3 dirToSource = (sourcePosition - transform.position).normalized;
        if (dirToSource.sqrMagnitude < 0.001f) return;

        // 回転軸に投影して目標回転を計算
        Vector3 projectedDir = Vector3.ProjectOnPlane(dirToSource, m_rotationAxis).normalized;
        if (projectedDir.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(projectedDir, m_rotationAxis);

        // 角度制限チェック
        float angle = Quaternion.Angle(Quaternion.identity, Quaternion.Inverse(transform.parent != null ? transform.parent.rotation : Quaternion.identity) * targetRotation);
        if (angle < m_minAngle || angle > m_maxAngle) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            m_maxAngularSpeed * Time.deltaTime
        );
    }

    public void OnMagnetContact(Magnetizable self, Magnetizable other)
    {
        // タレットは接触固定しない
    }
}

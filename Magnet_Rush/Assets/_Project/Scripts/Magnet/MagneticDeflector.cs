using UnityEngine;

/// <summary>
/// 飛行中の弾が磁力の影響で軌道を曲げる応答コンポーネント。
/// 仕様書: 「敵の弾にS + 自分にS → 弾が反発して跳ね返る」「攻撃誘導」で使用。
/// 敵弾プレハブに Magnetizable(pole=None) + MagneticDeflector を事前アタッチ。
/// プレイヤーの弾が当たると Magnetizable.SetPole() で極性設定 → 偏向開始。
/// </summary>
[RequireComponent(typeof(Magnetizable))]
[RequireComponent(typeof(Rigidbody))]
public class MagneticDeflector : MonoBehaviour, IMagneticResponse
{
    [SerializeField] private float m_deflectionStrength = 0.5f;
    [SerializeField] private float m_maxDeflectionAngle = 45f;

    private Magnetizable m_magnetizable;
    private Rigidbody m_rb;

    public bool IsResponseActive => m_magnetizable != null && m_magnetizable.IsActive;

    void Awake()
    {
        m_magnetizable = GetComponent<Magnetizable>();
        m_rb = GetComponent<Rigidbody>();
    }

    public void OnMagnetForce(Vector3 force, Vector3 sourcePosition)
    {
        if (m_rb.linearVelocity.sqrMagnitude < 0.01f) return;

        // 速度方向を力の方向にブレンド（速さは維持）
        float speed = m_rb.linearVelocity.magnitude;
        Vector3 currentDir = m_rb.linearVelocity / speed;
        Vector3 forceDir = force.normalized;

        float maxAngleRad = m_maxDeflectionAngle * Mathf.Deg2Rad * m_deflectionStrength * Time.fixedDeltaTime;
        Vector3 newDir = Vector3.RotateTowards(currentDir, currentDir + forceDir, maxAngleRad, 0f);

        m_rb.linearVelocity = newDir.normalized * speed;
    }

    public void OnMagnetContact(Magnetizable self, Magnetizable other)
    {
        // 弾は接触固定しない（既存の OnTriggerEnter で着弾処理）
    }
}

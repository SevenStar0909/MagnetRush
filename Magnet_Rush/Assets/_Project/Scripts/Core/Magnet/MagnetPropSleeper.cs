using UnityEngine;

/// <summary>
/// 磁力で動かせる物理プロップ(クレート等)を、開始時は kinematic で静止させる。
/// 密集して互いにめり込んだ配置でも、開始時に物理のめり込み解消で弾け飛ばなくなる。
/// 磁力で磁化された時、または動いている剛体にぶつかられた時だけ動的化して物理に参加する。
/// 依存: Rigidbody, Magnetizable
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class MagnetPropSleeper : MonoBehaviour
{
    private Rigidbody m_rb;
    private Magnetizable m_magnetizable;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_magnetizable = GetComponent<Magnetizable>();
    }

    private void Start()
    {
        // 開始時は静止。密集配置でも物理で弾けない。磁化や接触で初めて起きる。
        if (m_rb != null)
            m_rb.isKinematic = true;
    }

    private void Update()
    {
        // 磁力で磁化されたら動的化し、磁力で動かせるようにする。
        if (m_rb != null && m_rb.isKinematic && m_magnetizable != null && m_magnetizable.IsActive)
            m_rb.isKinematic = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 動いている物体(動的剛体)にぶつかられたら起きる。山が崩れる連鎖のため。
        if (m_rb != null && m_rb.isKinematic && collision.rigidbody != null && !collision.rigidbody.isKinematic)
            m_rb.isKinematic = false;
    }
}

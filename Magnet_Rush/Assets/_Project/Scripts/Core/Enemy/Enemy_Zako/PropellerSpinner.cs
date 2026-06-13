using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プロペラを常時一定速度で回す。Animatorのステート（Idle/Attack行き止まり等）に依存させず、
/// LateUpdateでAnimatorが書き込んだプロペラ姿勢を上書きして回転を保証する。
/// カミカゼのプロペラがAttackクリップ終了で止まる問題への対処。
/// 依存: なし（子階層から名前に "Propeller" を含むTransformを自動収集）
/// </summary>
[DisallowMultipleComponent]
public class PropellerSpinner : MonoBehaviour
{
    [Header("[回転]")]
    [Tooltip("プロペラの回転速度（度/秒）。大きいほど速く回る")]
    [SerializeField] private float m_speed = 1440f;

    [Tooltip("プロペラのローカル回転軸。シャフトの向きに合わせる")]
    [SerializeField] private Vector3 m_localAxis = Vector3.right;

    [Header("[対象]")]
    [Tooltip("空なら子階層から名前に Propeller を含む Transform を自動収集する")]
    [SerializeField] private Transform[] m_propellers;

    private Quaternion[] m_baseRotations;
    private float m_angle;

    private void Awake()
    {
        if (m_propellers == null || m_propellers.Length == 0)
        {
            var found = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.IndexOf("Propeller", StringComparison.OrdinalIgnoreCase) >= 0)
                    found.Add(t);
            }
            m_propellers = found.ToArray();
        }

        m_baseRotations = new Quaternion[m_propellers.Length];
        for (int i = 0; i < m_propellers.Length; i++)
        {
            if (m_propellers[i] != null)
                m_baseRotations[i] = m_propellers[i].localRotation;
        }
    }

    // Animatorはプロペラの姿勢を毎フレーム書き込むため、その後のLateUpdateで上書きしないと回転が打ち消される。
    private void LateUpdate()
    {
        if (m_propellers == null || m_propellers.Length == 0) return;

        m_angle += m_speed * Time.deltaTime;
        if (m_angle >= 360f) m_angle -= 360f;

        Quaternion spin = Quaternion.AngleAxis(m_angle, m_localAxis.normalized);
        for (int i = 0; i < m_propellers.Length; i++)
        {
            if (m_propellers[i] != null)
                m_propellers[i].localRotation = m_baseRotations[i] * spin;
        }
    }
}

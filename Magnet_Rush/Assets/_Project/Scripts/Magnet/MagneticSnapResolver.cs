using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 異極オブジェクトの吸い込み→固定を管理する。
/// 臨界減衰スプリングでスムーズに接近し、FixedJointで物理固定する。
/// MagnetManagerのフィールドとして保持される（MonoBehaviourではない）。
/// </summary>
public class MagneticSnapResolver
{
    private readonly MagnetSettings m_settings;
    private readonly HashSet<long> m_attachedPairs = new();
    private readonly Dictionary<long, Vector3> m_velocities = new();
    private readonly Dictionary<long, FixedJoint> m_joints = new();

    public MagneticSnapResolver(MagnetSettings settings)
    {
        m_settings = settings;
    }

    /// <summary>snapDistance内の異極ペアを即座に固定する。</summary>
    public void Resolve(Magnetizable a, Magnetizable b, float dt)
    {
        if (a == null || b == null) return;

        long key = MakePairKey(a, b);
        if (m_attachedPairs.Contains(key)) return;

        // snapDistance 以内に入ったら即固定
        Snap(a, b);
    }

    /// <summary>FixedJointで物理固定する。mass=Infinity側にJointを生成。</summary>
    public void Snap(Magnetizable a, Magnetizable b)
    {
        if (a == null || b == null) return;

        long key = MakePairKey(a, b);
        if (m_attachedPairs.Contains(key)) return;

        Magnetizable anchor = FindAnchor(a, b);
        Magnetizable mover = anchor == a ? b : a;
        if (anchor == null || mover == null) return;

        var joint = mover.gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = anchor.GetComponent<Rigidbody>();
        joint.breakForce = m_settings.snapBreakForce;

        m_attachedPairs.Add(key);
        m_joints[key] = joint;
        m_velocities.Remove(key);
    }

    public void Release(long pairKey)
    {
        if (!m_attachedPairs.Remove(pairKey)) return;

        if (m_joints.TryGetValue(pairKey, out var joint))
        {
            if (joint != null)
                Object.Destroy(joint);
            m_joints.Remove(pairKey);
        }

        m_velocities.Remove(pairKey);
    }

    /// <summary>指定フィールドに関連する全Jointを破棄する。磁場消滅時に呼ぶ。</summary>
    public void ReleaseAllForField(MagnetField field)
    {
        if (field == null) return;

        var toRemove = new List<long>();
        foreach (var kvp in m_joints)
        {
            if (kvp.Value == null) { toRemove.Add(kvp.Key); continue; }

            bool related =
                (kvp.Value.gameObject == field.gameObject) ||
                (kvp.Value.connectedBody != null && kvp.Value.connectedBody.gameObject == field.gameObject);

            if (related) toRemove.Add(kvp.Key);
        }

        foreach (var key in toRemove)
            Release(key);
    }

    /// <summary>指定Magnetizableに関連する全Jointを破棄する。OnDisable時に呼ぶ。</summary>
    public void ReleaseAllFor(Magnetizable mag)
    {
        if (mag == null) return;

        var toRemove = new List<long>();
        foreach (var kvp in m_joints)
        {
            if (kvp.Value == null) { toRemove.Add(kvp.Key); continue; }

            bool related =
                (kvp.Value.gameObject == mag.gameObject) ||
                (kvp.Value.connectedBody != null && kvp.Value.connectedBody.gameObject == mag.gameObject);

            if (related) toRemove.Add(kvp.Key);
        }

        foreach (var key in toRemove)
            Release(key);
    }

    public bool IsAttached(long pairKey) => m_attachedPairs.Contains(pairKey);

    /// <summary>null化したJointを掃除する。</summary>
    public void CleanupDestroyedJoints()
    {
        var toRemove = new List<long>();
        foreach (var kvp in m_joints)
        {
            if (kvp.Value == null) toRemove.Add(kvp.Key);
        }

        foreach (var key in toRemove)
        {
            m_joints.Remove(key);
            m_attachedPairs.Remove(key);
        }
    }

    private Magnetizable FindMover(Magnetizable a, Magnetizable b)
    {
        if (float.IsInfinity(a.mass) && float.IsInfinity(b.mass)) return null;
        if (float.IsInfinity(a.mass)) return b;
        if (float.IsInfinity(b.mass)) return a;
        return a.mass <= b.mass ? a : b;
    }

    private Magnetizable FindAnchor(Magnetizable a, Magnetizable b)
    {
        if (float.IsInfinity(a.mass)) return a;
        if (float.IsInfinity(b.mass)) return b;
        return a.mass > b.mass ? a : b;
    }

    private static long MakePairKey(Magnetizable a, Magnetizable b)
    {
        int idA = a.GetInstanceID();
        int idB = b.GetInstanceID();
        if (idA > idB) (idA, idB) = (idB, idA);
        return ((long)idA << 32) | (uint)idB;
    }
}

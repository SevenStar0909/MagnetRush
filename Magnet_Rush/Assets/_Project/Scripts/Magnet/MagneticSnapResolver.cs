using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 異極オブジェクトの吸い込み→固定を管理する。
/// FixedJointで物理固定し、距離超過で解除する。
/// 解除されたペアはbrokenリストに入り、どちらかの磁化が解除されるまで再スナップ＋力適用を禁止する。
/// </summary>
public class MagneticSnapResolver
{
    private readonly MagnetSettings m_settings;
    private readonly HashSet<long> m_attachedPairs = new();
    private readonly Dictionary<long, Vector3> m_velocities = new();
    private readonly Dictionary<long, FixedJoint> m_joints = new();

    // 距離解除されたペア。磁化が解除されるまで再スナップ＋力適用を禁止
    private readonly HashSet<long> m_brokenPairs = new();

    public MagneticSnapResolver(MagnetSettings settings)
    {
        m_settings = settings;
    }

    /// <summary>ペアがbroken状態か。MagnetManagerがProcessPairで力をスキップするために使用。</summary>
    public bool IsBroken(Magnetizable a, Magnetizable b)
    {
        return m_brokenPairs.Contains(MakePairKey(a, b));
    }

    /// <summary>指定Magnetizableに関連するbrokenペアを解除する。磁化解除時にMagnetizable.OnDisableから呼ぶ。</summary>
    public void ClearBrokenFor(Magnetizable mag)
    {
        if (mag == null) return;
        m_brokenPairs.RemoveWhere(key =>
        {
            int id = mag.GetInstanceID();
            int high = (int)(key >> 32);
            int low = (int)(key & 0xFFFFFFFF);
            return high == id || low == id;
        });
    }

    /// <summary>snapDistance内の異極ペアを固定する。brokenペアは再スナップしない。</summary>
    public void Resolve(Magnetizable a, Magnetizable b, float dt)
    {
        if (a == null || b == null) return;

        long key = MakePairKey(a, b);
        if (m_attachedPairs.Contains(key)) return;
        if (m_brokenPairs.Contains(key)) return;

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

        ClearBrokenFor(mag);
    }

    public bool IsAttached(long pairKey) => m_attachedPairs.Contains(pairKey);

    /// <summary>null化したJointや、磁力範囲外に出たJointを掃除する。距離解除ペアはbrokenリストに追加。</summary>
    public void CleanupDestroyedJoints()
    {
        float maxRange = m_settings != null ? m_settings.magnetRange : 10f;
        float maxRangeSqr = maxRange * maxRange;

        var toRemove = new List<long>();
        foreach (var kvp in m_joints)
        {
            if (kvp.Value == null) { toRemove.Add(kvp.Key); continue; }

            Rigidbody connected = kvp.Value.connectedBody;
            if (connected == null) { toRemove.Add(kvp.Key); continue; }

            float sqrDist = (kvp.Value.transform.position - connected.transform.position).sqrMagnitude;
            if (sqrDist > maxRangeSqr)
                toRemove.Add(kvp.Key);
        }

        foreach (var key in toRemove)
        {
            if (m_joints.TryGetValue(key, out var joint) && joint != null)
                Object.Destroy(joint);
            m_joints.Remove(key);
            m_attachedPairs.Remove(key);
            // 距離解除 → brokenリストに追加（磁化解除まで再スナップ＋力を禁止）
            m_brokenPairs.Add(key);
        }
    }

    private Magnetizable FindAnchor(Magnetizable a, Magnetizable b)
    {
        if (float.IsInfinity(a.mass)) return a;
        if (float.IsInfinity(b.mass)) return b;
        return a.mass > b.mass ? a : b;
    }

    public static long MakePairKey(Magnetizable a, Magnetizable b)
    {
        int idA = a.GetInstanceID();
        int idB = b.GetInstanceID();
        if (idA > idB) (idA, idB) = (idB, idA);
        return ((long)idA << 32) | (uint)idB;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Object×Object 専用の異極吸い込み→FixedJoint 固定を管理する。
/// Entity 絡みペア（片方または両方が Entity）は PD ホルダー (MagnetManager.ProcessHold) に委譲するため、
/// Resolve 冒頭で CachedEntity ガードで弾く。
/// 距離超過で解除されたペアは brokenリストに入り、どちらかの磁化が解除されるまで再スナップ＋力適用を禁止する。
/// </summary>
public class MagneticSnapResolver
{
    private readonly MagnetSettings m_settings;
    private readonly HashSet<long> m_attachedPairs = new();
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
        if (mag == null) { ChannelLogger.LogGuardReturn("Magnet", "Magnetizableなし"); return; }
        m_brokenPairs.RemoveWhere(key =>
        {
            int id = mag.GetInstanceID();
            int high = (int)(key >> 32);
            int low = (int)(key & 0xFFFFFFFF);
            return high == id || low == id;
        });
    }

    /// <summary>snapDistance内の異極Object×Objectペアを固定する。Entity絡みペアはPDホルダーに委譲するため対象外。brokenペアは再スナップしない。</summary>
    public void Resolve(Magnetizable a, Magnetizable b, float dt)
    {
        if (a == null || b == null) { ChannelLogger.LogGuardReturn("Magnet", "Resolve対象のMagnetizableがnull"); return; }

        if (a.CachedEntity != null || b.CachedEntity != null) { ChannelLogger.LogGuardReturn("Magnet", "Entity絡みペアはPDホルダーに委譲"); return; }

        long key = MakePairKey(a, b);
        if (m_attachedPairs.Contains(key)) { ChannelLogger.LogGuardReturn("Magnet", "既に吸着済みペア"); return; }
        if (m_brokenPairs.Contains(key)) { ChannelLogger.LogGuardReturn("Magnet", "brokenペアは再スナップ禁止"); return; }

        Snap(a, b);
    }

    /// <summary>FixedJointで物理固定する。mass=Infinity側にJointを生成。</summary>
    private void Snap(Magnetizable a, Magnetizable b)
    {
        if (a == null || b == null) { ChannelLogger.LogGuardReturn("Magnet", "Snap対象のMagnetizableがnull"); return; }

        long key = MakePairKey(a, b);
        if (m_attachedPairs.Contains(key)) { ChannelLogger.LogGuardReturn("Magnet", "既に吸着済みペア"); return; }

        Magnetizable anchor = FindAnchor(a, b);
        Magnetizable mover = anchor == a ? b : a;
        if (anchor == null || mover == null) { ChannelLogger.LogGuardReturn("Magnet", "アンカー/ムーバー決定失敗"); return; }

        var joint = mover.gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = anchor.GetComponent<Rigidbody>();
        joint.breakForce = m_settings.snapBreakForce;

        m_attachedPairs.Add(key);
        m_joints[key] = joint;
    }

    private void Release(long pairKey)
    {
        if (!m_attachedPairs.Remove(pairKey)) { ChannelLogger.LogGuardReturn("Magnet", "解放対象のペアが未登録"); return; }

        if (m_joints.TryGetValue(pairKey, out var joint))
        {
            if (joint != null)
                Object.Destroy(joint);
            m_joints.Remove(pairKey);
        }
    }

    /// <summary>指定フィールドに関連する全Jointを破棄する。磁場消滅時に呼ぶ。</summary>
    public void ReleaseAllForField(MagnetField field)
    {
        if (field == null) { ChannelLogger.LogGuardReturn("Magnet", "解放対象のフィールドがnull"); return; }

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
        if (mag == null) { ChannelLogger.LogGuardReturn("Magnet", "解放対象のMagnetizableがnull"); return; }

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

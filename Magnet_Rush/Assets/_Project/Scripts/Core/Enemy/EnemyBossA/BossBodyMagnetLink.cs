using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ボス本体 (root) の Magnetizable を購読し、手以外のすべてのボーン outline を
/// 同じ磁極で同期させる。「ボス本体一括磁化」用のブリッジ。
/// 手 (L_Wrist / R_Wrist) 配下は除外する（あちらは BossHandMagnetLink が独立に管理）。
/// </summary>
[RequireComponent(typeof(Magnetizable))]
public class BossBodyMagnetLink : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer m_smr;
    [SerializeField] private BossPerBoneOutline m_outline;

    [Tooltip("除外したいボーン名 (これらの subtree 配下は本体磁化の影響を受けない)")]
    [SerializeField] private string[] m_excludeBoneSubtrees = { "L_Wrist", "R_Wrist" };

    private Magnetizable m_magnetizable;
    private List<string> m_targetBoneNames;

    void Awake()
    {
        m_magnetizable = GetComponent<Magnetizable>();
        if (m_outline == null) m_outline = GetComponent<BossPerBoneOutline>();
        if (m_outline == null) m_outline = GetComponentInChildren<BossPerBoneOutline>();
        if (m_smr == null && m_outline != null) m_smr = m_outline.GetComponentInChildren<SkinnedMeshRenderer>(true);

        ResolveBoneSet();
    }

    void OnEnable()
    {
        if (m_magnetizable != null) m_magnetizable.OnPoleChanged += HandlePoleChanged;
    }

    void OnDisable()
    {
        if (m_magnetizable != null) m_magnetizable.OnPoleChanged -= HandlePoleChanged;
    }

    private void ResolveBoneSet()
    {
        m_targetBoneNames = new List<string>();
        if (m_smr == null) { ChannelLogger.LogGuardReturn("EnemyBossA", "SMR未設定"); return; }

        var excludeRoots = new HashSet<Transform>();
        if (m_excludeBoneSubtrees != null)
        {
            foreach (var name in m_excludeBoneSubtrees)
            {
                if (string.IsNullOrEmpty(name)) continue;
                foreach (var b in m_smr.bones)
                {
                    if (b != null && b.name == name) { excludeRoots.Add(b); break; }
                }
            }
        }

        foreach (var bone in m_smr.bones)
        {
            if (bone == null) continue;
            bool excluded = false;
            foreach (var ex in excludeRoots)
            {
                if (bone == ex || bone.IsChildOf(ex)) { excluded = true; break; }
            }
            if (!excluded) m_targetBoneNames.Add(bone.name);
        }
    }

    private void HandlePoleChanged(MagneticPole pole)
    {
        if (m_outline == null || m_targetBoneNames == null) return;
        var state = MapPole(pole);
        for (int i = 0; i < m_targetBoneNames.Count; i++)
        {
            m_outline.SetBonePole(m_targetBoneNames[i], state);
        }
    }

    private static BossPerBoneOutline.BonePoleState MapPole(MagneticPole p)
    {
        return p switch
        {
            MagneticPole.N => BossPerBoneOutline.BonePoleState.N,
            MagneticPole.S => BossPerBoneOutline.BonePoleState.S,
            _ => BossPerBoneOutline.BonePoleState.None,
        };
    }
}

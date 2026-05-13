using UnityEngine;

/// <summary>
/// ボスの手に付与。同じ GameObject の Magnetizable.OnPoleChanged を購読し、
/// 親階層の BossPerBoneOutline にボーン名 + 磁極を通知してアウトライン色を更新する。
/// 弾の着弾で Magnetizable.SetPole が呼ばれ → このリンクが outline 同期する流れ。
/// </summary>
[RequireComponent(typeof(Magnetizable))]
public class BossHandMagnetLink : MonoBehaviour
{
    [Tooltip("空なら親階層を遡って最初に見つけた手のボーン名を採用する")]
    [SerializeField] private string m_boneNameOverride;

    [Tooltip("空なら親階層から自動取得")]
    [SerializeField] private BossPerBoneOutline m_outline;

    private Magnetizable m_magnetizable;
    private string m_boneName;

    void Awake()
    {
        m_magnetizable = GetComponent<Magnetizable>();
        if (m_outline == null) m_outline = GetComponentInParent<BossPerBoneOutline>();
        if (m_outline == null) { ChannelLogger.LogGuardReturn("EnemyBossA", "BossPerBoneOutlineが親階層に見つからない"); return; }

        m_boneName = ResolveBoneName();
        if (string.IsNullOrEmpty(m_boneName)) { ChannelLogger.LogGuardReturn("EnemyBossA", "対象ボーン名を解決できない"); return; }
    }

    void OnEnable()
    {
        if (m_magnetizable != null) m_magnetizable.OnPoleChanged += HandlePoleChanged;
    }

    void OnDisable()
    {
        if (m_magnetizable != null) m_magnetizable.OnPoleChanged -= HandlePoleChanged;
    }

    private void HandlePoleChanged(MagneticPole pole)
    {
        if (m_outline == null || string.IsNullOrEmpty(m_boneName)) return;
        m_outline.SetBonePole(m_boneName, MapPole(pole));
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

    private string ResolveBoneName()
    {
        if (!string.IsNullOrEmpty(m_boneNameOverride)) return m_boneNameOverride;

        // 配置想定: <Bone>/Hitbox/Hurtbox にこのコンポーネントが乗る前提。
        // 親階層を遡って "L_Wrist" / "R_Wrist" を最優先で拾う。
        for (var t = transform.parent; t != null; t = t.parent)
        {
            if (t.name == "L_Wrist" || t.name == "R_Wrist") return t.name;
        }
        // フォールバック: 親の親(= Bone)
        if (transform.parent != null && transform.parent.parent != null) return transform.parent.parent.name;
        return null;
    }
}

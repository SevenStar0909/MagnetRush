using UnityEngine;

/// <summary>
/// 骨側 Hurtbox に付与。PlayerBullet / MeleeHitbox が触れたら
/// 親ボーンを BossPerBoneOutline.FlashBonePole で一時点滅させる。
/// 配置想定: Bone/Hitbox/Hurtbox → このコンポーネントは Hurtbox GameObject に付ける。
/// 親ボーン名は Awake で transform.parent.parent (Hurtbox→Hitbox→Bone) から決定。
/// </summary>
[RequireComponent(typeof(Collider))]
public class BoneHurtboxFlash : MonoBehaviour
{
    [SerializeField] private BossPerBoneOutline.BonePoleState m_flashPole = BossPerBoneOutline.BonePoleState.N;
    [SerializeField] private float m_flashDuration = 0.3f;

    [Tooltip("空の場合は transform.parent.parent.name (Bone) を自動使用")]
    [SerializeField] private string m_boneNameOverride;

    private BossPerBoneOutline m_outline;
    private string m_boneName;

    void Awake()
    {
        m_outline = GetComponentInParent<BossPerBoneOutline>();
        if (m_outline == null) { ChannelLogger.LogGuardReturn("EnemyBossA", "BossPerBoneOutlineが親階層に見つからない"); return; }

        if (!string.IsNullOrEmpty(m_boneNameOverride))
        {
            m_boneName = m_boneNameOverride;
        }
        else if (transform.parent != null && transform.parent.parent != null)
        {
            m_boneName = transform.parent.parent.name;
        }
        else
        {
            ChannelLogger.LogGuardReturn("EnemyBossA", "骨を辿れない階層構造");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (m_outline == null || string.IsNullOrEmpty(m_boneName)) return;

        int layer = other.gameObject.layer;
        if (layer != PhysicsLayers.PlayerBullet && layer != PhysicsLayers.MeleeHitbox) return;

        m_outline.FlashBonePole(m_boneName, m_flashPole, m_flashDuration);
    }
}

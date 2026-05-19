using UnityEngine;

/// <summary>
/// 同一プレハブ階層の SkinnedMeshRenderer を ParticleSystem.ShapeModule に実行時バインドする。
/// FX プレハブをキャラクターの任意の階層に配置すれば Awake で自動的に骨追従の Emit Shape になる。
/// 依存: ParticleSystem
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class BindShapeToParentSkinnedMesh : MonoBehaviour
{
    [Tooltip("バインド対象の SkinnedMeshRenderer。未設定なら同一階層から自動取得")]
    [SerializeField] private SkinnedMeshRenderer m_target;

    private void Awake()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null) return;

        if (m_target == null)
            m_target = transform.root.GetComponentInChildren<SkinnedMeshRenderer>(true);

        if (m_target == null)
        {
            ChannelLogger.LogGuardReturn("Entity", $"[{name}] 階層内に SkinnedMeshRenderer が見つからない");
            return;
        }

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
        shape.skinnedMeshRenderer = m_target;
    }
}

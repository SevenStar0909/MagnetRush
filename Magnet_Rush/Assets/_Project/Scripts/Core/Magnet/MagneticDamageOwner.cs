using UnityEngine;

/// <summary>
/// 磁力接触ダメージを出す物体の所有者を保持する。
/// 斧など、落下後も元の持ち主だけ自傷を無効化したい物理オブジェクトで使用する。
/// </summary>
public class MagneticDamageOwner : MonoBehaviour
{
    [SerializeField] private GameObject m_owner;

    public GameObject Owner => m_owner;

    public void SetOwner(GameObject owner)
    {
        m_owner = owner;
    }

    public bool IsOwnedBy(GameObject target)
    {
        if (m_owner == null || target == null)
            return false;

        Transform ownerTransform = m_owner.transform;
        Transform targetTransform = target.transform;
        return targetTransform == ownerTransform || targetTransform.IsChildOf(ownerTransform);
    }
}

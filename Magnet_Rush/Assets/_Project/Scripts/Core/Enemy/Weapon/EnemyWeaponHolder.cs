using UnityEngine;

/// <summary>
/// 敵に近接武器(Weapon_Axe)を持たせ、磁力で剝がされたかどうかを外へ知らせる。
/// 武器の磁力ドロップ判定そのものは WeaponStateController が持ち、ここは装備の橋渡しと所持状態の公開だけを担う。
/// 依存: WeaponStateController, ChannelLogger
/// </summary>
[DisallowMultipleComponent]
public class EnemyWeaponHolder : MonoBehaviour
{
    [Header("References")]
    [Tooltip("手に持たせる武器インスタンス")]
    [SerializeField] private WeaponStateController m_weapon;

    [Tooltip("武器の握り位置を合わせる手のソケット")]
    [SerializeField] private Transform m_handSocket;

    [Tooltip("モデルに最初から付いている飾りの武器。装備時に隠す")]
    [SerializeField] private GameObject m_modelWeapon;

    /// <summary>武器を所持しているか。磁力で剝がされると false になる。</summary>
    public bool IsArmed => m_weapon != null && m_weapon.HasOwner;

    private void Start()
    {
        // モデル付属の飾り武器は、手に持つ本物の武器と二重に見えるので隠す。
        if (m_modelWeapon != null)
            m_modelWeapon.SetActive(false);

        if (m_weapon == null) { ChannelLogger.LogGuardReturn("Enemy", "武器インスタンス未設定"); return; }
        if (m_handSocket == null) { ChannelLogger.LogGuardReturn("Enemy", "手ソケット未設定"); return; }

        m_weapon.TryEquipTo(gameObject, m_handSocket);
    }
}

using UnityEngine;

/// <summary>
/// 敵に近接武器(Weapon_Axe)を持たせ、磁力で剝がされた/再装備の橋渡しをする。
/// 武器はモデル付属の飾り斧ノードに重ねて追従させる（同じメッシュなので位置・向き・振りが一致）。
/// 飾り斧は描画を消して二重表示を防ぐ。磁力ドロップ判定そのものは WeaponStateController が持つ。
/// 依存: WeaponStateController, ChannelLogger
/// </summary>
[DisallowMultipleComponent]
public class EnemyWeaponHolder : MonoBehaviour
{
    [Header("References")]
    [Tooltip("手に持たせる武器インスタンス")]
    [SerializeField] private WeaponStateController m_weapon;

    [Tooltip("モデル付属の飾り斧。描画を消し、この Transform に武器を重ねて追従させる")]
    [SerializeField] private GameObject m_modelWeapon;

    // 装備時の握り姿勢。プレハブで調整したローカル値を Awake で控え、再装備で復元する。
    private Vector3 m_heldLocalPosition;
    private Quaternion m_heldLocalRotation;
    private Vector3 m_heldLocalScale = Vector3.one;

    /// <summary>武器を所持しているか。磁力で剝がされると false になる。</summary>
    public bool IsArmed => m_weapon != null && m_weapon.HasOwner;

    /// <summary>落ちた武器の現在位置。拾いに行くAIが目標にする。武器がなければ自分の位置。</summary>
    public Vector3 DroppedWeaponPosition => m_weapon != null ? m_weapon.transform.position : transform.position;

    /// <summary>再装備できるか。磁化中・クールダウン中・所持中は false。</summary>
    public bool CanReEquip => m_weapon != null && m_weapon.CanBePickedUp;

    private void Awake()
    {
        // プレハブで調整済みの握り姿勢を控える（この時点では武器は飾り斧ノードの子で装備姿勢のまま）。
        if (m_weapon != null)
        {
            Transform weaponTransform = m_weapon.transform;
            m_heldLocalPosition = weaponTransform.localPosition;
            m_heldLocalRotation = weaponTransform.localRotation;
            m_heldLocalScale = weaponTransform.localScale;
        }
    }

    private void Start()
    {
        if (m_weapon == null) { ChannelLogger.LogGuardReturn("Enemy", "武器インスタンス未設定"); return; }
        if (m_modelWeapon == null) { ChannelLogger.LogGuardReturn("Enemy", "飾り斧ノード未設定"); return; }

        // 飾り斧の描画があれば消す（メッシュ削除済みなら Renderer は無い）。Transform はアニメで動き続ける。
        Renderer modelRenderer = m_modelWeapon.GetComponent<Renderer>();
        if (modelRenderer != null)
            modelRenderer.enabled = false;

        // 武器の位置・向き・スケールはプレハブで調整した値をそのまま使う（上書きしない）。
        m_weapon.EquipInPlace(gameObject);
    }

    /// <summary>
    /// 落ちた武器を手元（飾り斧ノード）の装備姿勢へ戻して再装備する。拾いに行くAIが接近後に呼ぶ。
    /// </summary>
    public void ReEquip()
    {
        if (m_weapon == null) { ChannelLogger.LogGuardReturn("Enemy", "武器インスタンス未設定"); return; }
        if (m_modelWeapon == null) { ChannelLogger.LogGuardReturn("Enemy", "飾り斧ノード未設定"); return; }

        Transform weaponTransform = m_weapon.transform;
        weaponTransform.SetParent(m_modelWeapon.transform, false);
        weaponTransform.localPosition = m_heldLocalPosition;
        weaponTransform.localRotation = m_heldLocalRotation;
        weaponTransform.localScale = m_heldLocalScale;

        m_weapon.EquipInPlace(gameObject);
    }
}

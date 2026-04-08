using UnityEngine;

// 近接武器の所持状態を管理するコンポーネント。
// 無主状態と有主状態を切り替え、磁力条件に応じて装備可否や強制ドロップを制御する。

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Magnetizable))]
public class WeaponStateController : MonoBehaviour
{
    public enum WeaponOwnerState
    {
        Unowned,    // 誰にも所持されていない地面状態。
        Equipping,  // 装備中の遷移状態。手元へ移動している途中。
        Owned       // 敵に所持されている装備状態。
    }

    [Header("Settings")]
    [SerializeField] private WeaponMeleeSettings m_settings;

    [Header("References")]
    [SerializeField] private Transform m_gripPoint;
    [SerializeField] private Collider[] m_physicsColliders;
    //[SerializeField] private Collider m_pickupTrigger;
    [SerializeField] private Collider m_attackTrigger;

    [Header("Owned Pose")]
    [SerializeField] private Vector3 m_ownedLocalEulerOffset = new Vector3(0f, -90f, 0f);

    [Header("State")]
    [SerializeField] private WeaponOwnerState m_state = WeaponOwnerState.Unowned;

    [Header("Equip Move")]
    [SerializeField] private float m_equipMoveSpeed = 12f;
    [SerializeField] private float m_equipArriveDistance = 0.05f;

    private Rigidbody m_rb;
    private Magnetizable m_magnetizable;

    private GameObject m_owner;
    private Transform m_ownerHand;
    private float m_nextPickupAllowedTime;

    private GameObject m_pendingOwner;
    private Transform m_pendingHand;

    public WeaponOwnerState State => m_state;           // 現在の所持状態。
    public GameObject Owner => m_owner;                 // 所持者。無主状態ではnull。
    public Collider AttackTrigger => m_attackTrigger;   // 攻撃判定トリガー。攻撃ウィンドウ中のみ有効化される。
    public Transform GripPoint => m_gripPoint;          // 手の位置合わせに使うポイント。装備時にここを手ソケットに合わせる。
    public bool HasOwner => m_state == WeaponOwnerState.Owned || m_state == WeaponOwnerState.Equipping; // 装備中も含む所持状態。
    public bool IsMagnetAffected => m_magnetizable != null && m_magnetizable.IsActive;    // 磁力影響を受けているか。


    // 拾える条件：無主状態、クールダウン完了、（設定があれば）磁力影響なし。
    public bool CanBePickedUp
    {
        get
        {
            if (m_state != WeaponOwnerState.Unowned) return false;
            if (Time.time < m_nextPickupAllowedTime) return false;
            if (m_settings == null) return true;

            if (m_settings.disablePickupWhileMagnetized && m_magnetizable.IsActive)
                return false;

            return true;
        }
    }

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_magnetizable = GetComponent<Magnetizable>();

        if (m_attackTrigger != null)
            m_attackTrigger.enabled = false;
    }

    private void Update()
    {
        if (m_state == WeaponOwnerState.Equipping)
        {
            UpdateEquipping();
            return;
        }

        if (m_state == WeaponOwnerState.Owned)
            CheckForcedDropByMagnet();
    }

    public bool TryStartEquip(GameObject newOwner, Transform handSocket)
    {
        if (!CanBePickedUp) return false;
        if (newOwner == null || handSocket == null) return false;

        m_pendingOwner = newOwner;
        m_pendingHand = handSocket;
        m_state = WeaponOwnerState.Equipping;

        if (m_rb != null)
        {
            m_rb.isKinematic = true;
            m_rb.linearVelocity = Vector3.zero;
            m_rb.angularVelocity = Vector3.zero;
        }

        SetPhysicsCollidersEnabled(false);

        if (m_attackTrigger != null)
            m_attackTrigger.enabled = false;

        transform.SetParent(null, true);

        return true;
    }

    private void UpdateEquipping()
    {
        if (m_pendingOwner == null || m_pendingHand == null)
        {
            CancelEquip();
            return;
        }

        Vector3 toTarget = m_pendingHand.position - m_gripPoint.position;
        transform.position += toTarget.normalized * m_equipMoveSpeed * Time.deltaTime;

        Quaternion targetRotation = m_pendingHand.rotation * Quaternion.Inverse(m_gripPoint.localRotation);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_equipMoveSpeed * Time.deltaTime);

        float remain = Vector3.Distance(m_gripPoint.position, m_pendingHand.position);
        if (remain <= m_equipArriveDistance)
        {
            CompleteEquip();
        }
    }

    private void CompleteEquip()
    {
        m_owner = m_pendingOwner;
        m_ownerHand = m_pendingHand;

        m_pendingOwner = null;
        m_pendingHand = null;

        m_state = WeaponOwnerState.Owned;

        AlignToHand(m_ownerHand);
        transform.SetParent(m_ownerHand, true);

        // 装備時の向き補正（斧刃を前に向ける）
        transform.localRotation = transform.localRotation * Quaternion.Euler(m_ownedLocalEulerOffset);

        if (m_rb != null)
        {
            m_rb.isKinematic = true;
            m_rb.linearVelocity = Vector3.zero;
            m_rb.angularVelocity = Vector3.zero;
        }

        if (m_attackTrigger != null)
            m_attackTrigger.enabled = false;
    }

    public void CancelEquip()
    {
        m_pendingOwner = null;
        m_pendingHand = null;
        Drop();
    }

    public bool TryEquipTo(GameObject newOwner, Transform handSocket)
    {
        return TryStartEquip(newOwner, handSocket);
    }

    public void Drop()
    {
        m_owner = null;
        m_ownerHand = null;
        m_pendingOwner = null;
        m_pendingHand = null;

        m_state = WeaponOwnerState.Unowned;

        transform.SetParent(null, true);

        if (m_rb != null)
        {
            m_rb.isKinematic = false;

            if (m_settings != null)
            {
                m_rb.mass = m_settings.groundMass;
                m_rb.linearDamping = m_settings.drag;
                m_rb.angularDamping = m_settings.angularDrag;
            }
        }

        SetPhysicsCollidersEnabled(true);

        if (m_attackTrigger != null)
            m_attackTrigger.enabled = false;

        if (m_settings != null)
            m_nextPickupAllowedTime = Time.time + m_settings.repickupCooldown;
    }

    public void BeginAttackWindow()
    {
        if (m_state != WeaponOwnerState.Owned) return;
        if (m_attackTrigger != null)
            m_attackTrigger.enabled = true;
    }

    public void EndAttackWindow()
    {
        if (m_attackTrigger != null)
            m_attackTrigger.enabled = false;
    }

    private void CheckForcedDropByMagnet()
    {
        if (m_settings == null) return;
        if (!m_settings.dropWhenMagnetAffectedWhileOwned) return;
        if (m_magnetizable == null) return;
        if (!m_magnetizable.IsActive) return;

        float influence = m_magnetizable.GetInfluence(1f);
        if (influence >= m_settings.forcedDropInfluenceThreshold)
            Drop();
    }

    private void AlignToHand(Transform handSocket)
    {
        if (m_gripPoint == null || handSocket == null) return;

        Quaternion deltaRot = handSocket.rotation * Quaternion.Inverse(m_gripPoint.rotation);
        transform.rotation = deltaRot * transform.rotation;

        Vector3 deltaPos = handSocket.position - m_gripPoint.position;
        transform.position += deltaPos;
    }

    private void SetPhysicsCollidersEnabled(bool enabled)
    {
        if (m_physicsColliders == null) return;

        for (int i = 0; i < m_physicsColliders.Length; i++)
        {
            if (m_physicsColliders[i] != null)
                m_physicsColliders[i].enabled = enabled;
        }
    }

    // 武器マネージャーへの登録/解除。PickableWeaponが呼び出す。
    private void OnEnable()
    {
        if (WeaponManager.Instance != null)
            WeaponManager.Instance.Register(this);
    }
    private void OnDisable()
    {
        if (WeaponManager.Instance != null)
            WeaponManager.Instance.Unregister(this);
    }
}